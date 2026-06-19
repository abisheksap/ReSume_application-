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
        try
        {
            byte[] buffer = new byte[4096];
            int read = await pipe.ReadAsync(buffer, 0, buffer.Length);
            string handshakeJson = Encoding.UTF8.GetString(buffer, 0, read);
            var profile = JsonSerializer.Deserialize<BrowserProfile>(handshakeJson);
            if (profile == null) return;

            profile.IsConnected = true;
            profile.LastSeen = DateTimeOffset.UtcNow;
            _connections[profile.ProfileId] = pipe;
            _profiles[profile.ProfileId] = profile;

            byte[] ack = Encoding.UTF8.GetBytes("OK");
            await pipe.WriteAsync(ack, 0, ack.Length);

            while (true)
            {
                byte[] msgBuffer = new byte[65536];
                int msgRead = await pipe.ReadAsync(msgBuffer, 0, msgBuffer.Length);
                if (msgRead == 0) break;
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, msgRead);
                MessageReceived?.Invoke(profile, msg);

                // If it's a capture response, update the profile with window data
                try
                {
                    var captureData = JsonSerializer.Deserialize<CaptureResponse>(msg);
                    if (captureData?.action == "capture")
                    {
                        profile.Windows = captureData.data?.Select(w => new BrowserWindow
                        {
                            WindowId = w.id,
                            Position = new WindowPosition { X = w.left, Y = w.top, Width = w.width, Height = w.height },
                            Monitor = null,
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
                            }).ToList() ?? new()
                        }).ToList();
                        _profiles[profile.ProfileId] = profile;
                    }
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            // Remove disconnected profile
            if (_profiles.TryRemove(profile?.ProfileId ?? "", out var _))
                _connections.TryRemove(profile?.ProfileId ?? "", out var _);
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

    // Helper model for deserializing capture messages
    private class CaptureResponse
    {
        public string action { get; set; }
        public List<WindowData> data { get; set; }
    }

    private class WindowData
    {
        public int id { get; set; }
        public bool focused { get; set; }
        public string state { get; set; }
        public int left { get; set; }
        public int top { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public bool incognito { get; set; }
        public List<TabData> tabs { get; set; }
    }

    private class TabData
    {
        public string url { get; set; }
        public string title { get; set; }
        public bool pinned { get; set; }
        public bool muted { get; set; }
        public int groupId { get; set; }
        public int index { get; set; }
        public bool active { get; set; }
        public string groupTitle { get; set; }
        public string groupColor { get; set; }
    }
    public List<BrowserProfile> GetConnectedProfiles()
{
    return _profiles.Values.Where(p => p.IsConnected).ToList();
}
    public async Task RequestCaptureAllAsync()
{
    var request = JsonSerializer.Serialize(new { action = "capture" });
    foreach (var profileId in _profiles.Keys)
    {
        await SendToProfileAsync(profileId, request);
        // Wait a bit for response? For simplicity we just fire and hope it arrives before we save.
        // In production you'd await a response.
    }
    // Give extensions a moment to respond
    await Task.Delay(500);
}
}