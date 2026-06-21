using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

/// <summary>
/// Manages one Named Pipe connection per Chrome profile / NativeHost instance.
/// 
/// BUG FIX: The previous version expected Chrome to push data on its own.
/// The correct flow is:
///   1. ReSume.exe → pipe → NativeHost → Chrome: { "action": "capture" }
///   2. Chrome gathers tabs, sends them → NativeHost → pipe → ReSume.exe
/// This class now actively sends the capture command and awaits the response.
/// </summary>
public class ProfileConnectionManager
{
    private readonly ConcurrentDictionary<string, PipeConnection> _connections = new();
    public event Action<BrowserProfile, string>? MessageReceived;

    private sealed class PipeConnection
    {
        public required BrowserProfile Profile { get; set; }
        public required NamedPipeServerStream Pipe { get; set; }
        public TaskCompletionSource<string>? PendingCapture { get; set; }
    }

    // ── Server loop ────────────────────────────────────────────────────────

    public async Task StartAsync(string pipeName)
    {
        while (true)
        {
            var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync();
            _ = HandleConnectionAsync(server);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
    {
        PipeConnection? conn = null;
        try
        {
            // ── 1. Read handshake ──────────────────────────────────────────
            byte[]? handshakeBytes = await ReadFramedMessageAsync(pipe);
            if (handshakeBytes == null) return;

            var hs = JsonSerializer.Deserialize<HandshakeMessage>(handshakeBytes);
            if (hs == null) return;

            var profile = new BrowserProfile
            {
                ProfileName      = string.IsNullOrWhiteSpace(hs.ProfileName) ? hs.ProfileId : hs.ProfileName,
                ProfileDirectory = hs.ProfileDirectory,
                ProfileId        = hs.ProfileId,
                IsConnected      = true,
                LastSeen         = DateTimeOffset.UtcNow
            };

            conn = new PipeConnection { Profile = profile, Pipe = pipe };
            _connections[profile.ProfileId] = conn;

            // ── 2. Send ACK ────────────────────────────────────────────────
            await WriteFramedMessageAsync(pipe, Encoding.UTF8.GetBytes("OK"));

            // ── 3. Message loop ────────────────────────────────────────────
            while (true)
            {
                byte[]? msgBytes = await ReadFramedMessageAsync(pipe);
                if (msgBytes == null) break;

                string msg = Encoding.UTF8.GetString(msgBytes);
                profile.LastSeen = DateTimeOffset.UtcNow;

                // Try to resolve a pending capture request
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    if (doc.RootElement.TryGetProperty("action", out var actionEl))
                    {
                        string action = actionEl.GetString() ?? "";

                        if (action == "capture" && conn.PendingCapture != null)
                        {
                            // Update profile windows from the data
                            if (doc.RootElement.TryGetProperty("data", out var dataEl))
                            {
                                var windows = JsonSerializer.Deserialize<List<WindowData>>(dataEl.GetRawText());
                                if (windows != null)
                                {
                                    profile.Windows = windows.Select(w => new BrowserWindow
                                    {
                                        WindowId       = w.id,
                                        Position       = new WindowPosition { X = w.left, Y = w.top, Width = w.width, Height = w.height },
                                        WindowState    = w.state ?? "Normal",
                                        IsIncognito    = w.incognito,
                                        ActiveTabIndex = w.tabs?.FindIndex(t => t.active) ?? 0,
                                        Tabs           = w.tabs?.Select(t => new TabInfo
                                        {
                                            Index      = t.index,
                                            Url        = t.url ?? "",
                                            Title      = t.title ?? "",
                                            IsPinned   = t.pinned,
                                            IsMuted    = t.muted,
                                            GroupId    = t.groupId,
                                            GroupTitle = t.groupTitle,
                                            GroupColor = t.groupColor
                                        }).ToList() ?? []
                                    }).ToList();

                                    // Update our stored connection profile
                                    _connections[profile.ProfileId] = conn;
                                }
                            }
                            conn.PendingCapture.TrySetResult(msg);
                            conn.PendingCapture = null;
                            continue;
                        }
                    }
                }
                catch { /* malformed JSON – still surface via event */ }

                MessageReceived?.Invoke(profile, msg);
            }
        }
        catch { }
        finally
        {
            if (conn != null)
            {
                conn.PendingCapture?.TrySetCanceled();
                _connections.TryRemove(conn.Profile.ProfileId, out _);
                conn.Profile.IsConnected = false;
            }
            try { pipe.Dispose(); } catch { }
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public List<BrowserProfile> GetConnectedProfiles()
        => _connections.Values.Select(c => c.Profile).ToList();

    /// <summary>
    /// Send a capture command to one profile's Chrome extension and wait for the tab data.
    /// Timeout: 15 seconds.
    /// </summary>
    public async Task<bool> CaptureProfileAsync(string profileId)
    {
        if (!_connections.TryGetValue(profileId, out var conn)) return false;

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.PendingCapture = tcs;

        var command = JsonSerializer.SerializeToUtf8Bytes(new { action = "capture" });
        await WriteFramedMessageAsync(conn.Pipe, command);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        cts.Token.Register(() => tcs.TrySetCanceled());

        try   { await tcs.Task; return true; }
        catch { return false; }
    }

    /// <summary>
    /// Send a restore command to one profile.
    /// </summary>
    public async Task SendRestoreAsync(string profileId, List<BrowserWindow> windows)
    {
        if (!_connections.TryGetValue(profileId, out var conn)) return;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { action = "restore", data = windows });
        await WriteFramedMessageAsync(conn.Pipe, payload);
    }

    // ── Framing helpers ────────────────────────────────────────────────────
    // Protocol: 4-byte little-endian length prefix + UTF-8 JSON payload.
    // This matches Chrome Native Messaging framing exactly.

    private static async Task<byte[]?> ReadFramedMessageAsync(NamedPipeServerStream pipe)
    {
        byte[] lenBuf = new byte[4];
        int total = 0;
        while (total < 4)
        {
            int n = await pipe.ReadAsync(lenBuf, total, 4 - total);
            if (n == 0) return null;
            total += n;
        }
        int length = BitConverter.ToInt32(lenBuf, 0);
        if (length <= 0 || length > 1_048_576) return null;

        byte[] data = new byte[length];
        total = 0;
        while (total < length)
        {
            int n = await pipe.ReadAsync(data, total, length - total);
            if (n == 0) return null;
            total += n;
        }
        return data;
    }

    private static async Task WriteFramedMessageAsync(PipeStream pipe, byte[] payload)
    {
        byte[] lenBytes = BitConverter.GetBytes(payload.Length);
        await pipe.WriteAsync(lenBytes, 0, 4);
        await pipe.WriteAsync(payload, 0, payload.Length);
        await pipe.FlushAsync();
    }

    // ── DTOs ───────────────────────────────────────────────────────────────

    private class HandshakeMessage
    {
        public string ProfileDirectory { get; set; } = "";
        public string ProfileName      { get; set; } = "";
        public string ProfileId        { get; set; } = "";
    }

    private class WindowData
    {
        public int id       { get; set; }
        public string? state { get; set; }
        public int left     { get; set; }
        public int top      { get; set; }
        public int width    { get; set; }
        public int height   { get; set; }
        public bool incognito { get; set; }
        public List<TabData>? tabs { get; set; }
    }

    private class TabData
    {
        public string? url       { get; set; }
        public string? title     { get; set; }
        public bool   pinned     { get; set; }
        public bool   muted      { get; set; }
        public int    groupId    { get; set; }
        public int    index      { get; set; }
        public bool   active     { get; set; }
        public string? groupTitle { get; set; }
        public string? groupColor { get; set; }
    }
}
