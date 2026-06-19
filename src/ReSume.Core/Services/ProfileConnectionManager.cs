using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class ProfileConnectionManager
{
    private readonly ConcurrentDictionary<string, NamedPipeServerStream> _connections = new();
    private readonly ConcurrentDictionary<string, BrowserProfile> _profiles = new();
    public event Action<BrowserProfile, string>? MessageReceived;

    public async Task StartAsync(string pipeName)
    {
        while (true)
        {
            var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            _ = HandleConnectionAsync(server);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
    {
        BrowserProfile? profile = null;
        try
        {
            byte[] buffer = new byte[4096];
            int read = await pipe.ReadAsync(buffer, 0, buffer.Length);
            string handshakeJson = Encoding.UTF8.GetString(buffer, 0, read);

            // DTO that matches what NativeHost actually sends
            var handshake = JsonSerializer.Deserialize<HandshakeMessage>(handshakeJson);
            if (handshake == null) return;

            profile = new BrowserProfile
            {
                ProfileName = handshake.ProfileName ?? handshake.ProfileId,
                ProfileDirectory = handshake.ProfileDirectory,
                ProfileId = handshake.ProfileId,
                IsConnected = true,
                LastSeen = DateTimeOffset.UtcNow
            };

            _connections[profile.ProfileId] = pipe;
            _profiles[profile.ProfileId] = profile;

            // Send ACK
            byte[] ack = Encoding.UTF8.GetBytes("OK");
            await pipe.WriteAsync(ack, 0, ack.Length);

            // Main message loop
            while (true)
            {
                byte[] msgBuffer = new byte[65536];
                int msgRead = await pipe.ReadAsync(msgBuffer, 0, msgBuffer.Length);
                if (msgRead == 0) break;
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, msgRead);
                MessageReceived?.Invoke(profile, msg);

                // If it's a capture response, update the profile with window/tab data
                try
                {
                    var captureData = JsonSerializer.Deserialize<CaptureResponse>(msg);
                    if (captureData?.action == "capture" && captureData.data != null)
                    {
                        profile.Windows = captureData.data.Select(w => new BrowserWindow
                        {
                            WindowId = w.id,
                            Position = new WindowPosition { X = w.left, Y = w.top, Width = w.width, Height = w.height },
                            WindowState = w.state ?? "Normal",
                            IsIncognito = w.incognito,
                            ActiveTabIndex = w.tabs?.FindIndex(t => t.active) ?? 0,
                            Tabs = w.tabs?.Select(t => new TabInfo
                            {
                                Index = t.index,
                                Url = t.url ?? "",
                                Title = t.title ?? "",
                                IsPinned = t.pinned,
                                IsMuted = t.muted,
                                GroupId = t.groupId,
                                GroupTitle = t.groupTitle,
                                GroupColor = t.groupColor
                            }).ToList() ?? new List<TabInfo>()
                        }).ToList();
                        _profiles[profile.ProfileId] = profile;
                    }
                }
                catch { /* ignore malformed JSON */ }
            }
        }
        catch { }
        finally
        {
            if (profile != null)
            {
                _profiles.TryRemove(profile.ProfileId, out _);
                _connections.TryRemove(profile.ProfileId, out _);
            }
        }
    }

    public List<BrowserProfile> GetConnectedProfiles()
    {
        return _profiles.Values.Where(p => p.IsConnected).ToList();
    }

    public async Task SendToProfileAsync(string profileId, string message)
    {
        if (_connections.TryGetValue(profileId, out var pipe) && pipe.IsConnected)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await pipe.WriteAsync(data, 0, data.Length);
        }
    }

    // --- DTOs ---
    private class HandshakeMessage
    {
        public string ProfileDirectory { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
    }

    private class CaptureResponse
    {
        public string action { get; set; } = string.Empty;
        public List<WindowData>? data { get; set; }
    }

    private class WindowData
    {
        public int id { get; set; }
        public bool focused { get; set; }
        public string? state { get; set; }
        public int left { get; set; }
        public int top { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public bool incognito { get; set; }
        public List<TabData>? tabs { get; set; }
    }

    private class TabData
    {
        public string? url { get; set; }
        public string? title { get; set; }
        public bool pinned { get; set; }
        public bool muted { get; set; }
        public int groupId { get; set; }
        public int index { get; set; }
        public bool active { get; set; }
        public string? groupTitle { get; set; }
        public string? groupColor { get; set; }
    }
}