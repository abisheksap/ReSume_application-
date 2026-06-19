using Xunit;
using ReSume.Core.Services;
using ReSume.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

public class SessionManagerTests {
    [Fact]
    public async Task SaveAndLoadSession() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var mgr = new SessionManager(path);
        var session = new Session { Label = "Test" };
        await mgr.SaveSessionAsync(session);
        var loaded = await mgr.LoadSessionAsync(session.SessionId);
        Assert.NotNull(loaded);
        Assert.Equal("Test", loaded!.Label);
        Directory.Delete(path, true);
    }
}