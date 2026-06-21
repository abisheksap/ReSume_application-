using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReSume.NativeHost;

/// <summary>
/// Chrome Native Messaging Host relay.
/// Chrome launches this process per-profile via stdin/stdout (4-byte LE length prefix + JSON).
/// This process connects to the ReSume.exe Named Pipe server, performs a handshake,
/// then relays messages bidirectionally.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Chrome provides profile directory as the "origin" in the manifest.
        // We detect it from our own location or from Chrome's --parent-window arg.
        string profileId   = DetectProfileId(args);
        string profileName = profileId; // ReSume.exe will resolve a friendly name

        var handshake = new
        {
            ProfileDirectory = profileId,
            ProfileName      = profileName,
            ProfileId        = profileId
        };

        // ── Connect to ReSume Named Pipe ──────────────────────────────────
        using var pipe = new NamedPipeClientStream(".", "resume_pipe",
            PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(10_000); // 10 s timeout
        }
        catch (TimeoutException)
        {
            // ReSume.exe not running – tell Chrome and exit
            await SendNativeMessageAsync(Console.OpenStandardOutput(),
                JsonSerializer.SerializeToUtf8Bytes(new { connected = false, error = "ReSume not running" }));
            return;
        }

        // Send handshake
        byte[] handshakeBytes = JsonSerializer.SerializeToUtf8Bytes(handshake);
        await pipe.WriteAsync(handshakeBytes);
        await pipe.FlushAsync();

        // Wait for ACK ("OK")
        byte[] ackBuf = new byte[2];
        int ackRead = await pipe.ReadAsync(ackBuf, 0, 2);
        if (ackRead < 2)
            return; // pipe closed before ACK

        // ── Bidirectional relay loop ──────────────────────────────────────
        var stdin  = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        using var cts = new CancellationTokenSource();

        // Pipe → stdout (unsolicited messages from ReSume, e.g. restore commands)
        var pipeToChrome = Task.Run(async () =>
        {
            byte[] buf = new byte[65536];
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    int n = await pipe.ReadAsync(buf, 0, buf.Length, cts.Token);
                    if (n == 0) break;
                    byte[] msg = new byte[n];
                    Array.Copy(buf, msg, n);
                    await SendNativeMessageAsync(stdout, msg);
                }
            }
            catch { }
            finally { cts.Cancel(); }
        }, cts.Token);

        // stdin → pipe (messages from Chrome extension)
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                byte[]? msg = await ReadNativeMessageAsync(stdin, cts.Token);
                if (msg == null) break;
                await pipe.WriteAsync(msg, 0, msg.Length, cts.Token);
                await pipe.FlushAsync(cts.Token);
            }
        }
        catch { }
        finally
        {
            cts.Cancel();
        }

        await pipeToChrome.ConfigureAwait(false);
    }

    /// <summary>
    /// Read one Chrome native message: 4-byte little-endian length + payload.
    /// Returns null on EOF.
    /// </summary>
    static async Task<byte[]?> ReadNativeMessageAsync(Stream stream, CancellationToken ct)
    {
        byte[] lengthBuf = new byte[4];
        int totalRead = 0;
        while (totalRead < 4)
        {
            int n = await stream.ReadAsync(lengthBuf, totalRead, 4 - totalRead, ct);
            if (n == 0) return null;
            totalRead += n;
        }
        int length = BitConverter.ToInt32(lengthBuf, 0);
        if (length <= 0 || length > 1_048_576) return null; // sanity check (max 1 MB)

        byte[] payload = new byte[length];
        totalRead = 0;
        while (totalRead < length)
        {
            int n = await stream.ReadAsync(payload, totalRead, length - totalRead, ct);
            if (n == 0) return null;
            totalRead += n;
        }
        return payload;
    }

    /// <summary>
    /// Write one Chrome native message: 4-byte LE length + payload.
    /// </summary>
    static async Task SendNativeMessageAsync(Stream stream, byte[] payload)
    {
        byte[] length = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(length, 0, 4);
        await stream.WriteAsync(payload, 0, payload.Length);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Attempt to figure out which Chrome profile launched this host.
    /// Chrome passes "--parent-window=HWND" but not the profile dir directly.
    /// We use the process working directory or fallback to "Default".
    /// </summary>
    static string DetectProfileId(string[] args)
    {
        // If launched from a profile directory, the CWD is often the profile folder.
        string cwd = Directory.GetCurrentDirectory();
        string last = Path.GetFileName(cwd);
        if (!string.IsNullOrEmpty(last) && last != ".")
            return last;
        return "Default";
    }
}
