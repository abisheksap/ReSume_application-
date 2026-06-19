using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ReSume.NativeHost;

class Program {
    static async Task Main(string[] args) {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        string profileDir = Directory.GetCurrentDirectory();
        var handshake = new { ProfileDirectory = profileDir, ProfileName = Path.GetFileName(profileDir), ProfileId = Path.GetFileName(profileDir) };
        string handshakeJson = JsonSerializer.Serialize(handshake);

        using var pipe = new NamedPipeClientStream(".", "resume_pipe", PipeDirection.InOut);
        await pipe.ConnectAsync(5000);
        byte[] handshakeData = Encoding.UTF8.GetBytes(handshakeJson);
        await pipe.WriteAsync(handshakeData, 0, handshakeData.Length);
        byte[] ackBuf = new byte[2];
        await pipe.ReadAsync(ackBuf, 0, 2);

        while (true) {
            byte[] msg = await ReadMessageAsync(stdin);
            if (msg == null) break;
            await pipe.WriteAsync(msg, 0, msg.Length);
            pipe.WaitForPipeDrain();
            byte[] respBuf = new byte[4096];
            int read = await pipe.ReadAsync(respBuf, 0, respBuf.Length);
            byte[] respMsg = new byte[read];
            Array.Copy(respBuf, respMsg, read);
            await SendMessageAsync(stdout, respMsg);
        }
    }

    static async Task<byte[]> ReadMessageAsync(Stream stream) {
        byte[] lengthBytes = new byte[4];
        int read = await stream.ReadAsync(lengthBytes, 0, 4);
        if (read < 4) return null!;
        int length = BitConverter.ToInt32(lengthBytes, 0);
        byte[] buffer = new byte[length];
        await stream.ReadAsync(buffer, 0, length);
        return buffer;
    }

    static async Task SendMessageAsync(Stream stream, byte[] data) {
        byte[] length = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(length, 0, length.Length);
        await stream.WriteAsync(data, 0, data.Length);
        await stream.FlushAsync();
    }
}