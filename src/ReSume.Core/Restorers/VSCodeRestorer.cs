using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class VSCodeRestorer : IAppRestorer {
    public string ProcessName => "code";
    public bool CanRestore(ApplicationState app) =>
        app.ProcessName.StartsWith("code", System.StringComparison.OrdinalIgnoreCase) ||
        (app.CommandLine?.Contains("code") ?? false);
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            string? codeExe = @"C:\Program Files\Microsoft VS Code\Code.exe";
            if (app.ExecutablePath != null && app.ExecutablePath.EndsWith("Code.exe", System.StringComparison.OrdinalIgnoreCase))
                codeExe = app.ExecutablePath;

            string args = "";
            if (app.DocumentPaths.Count > 0) {
                string path = app.DocumentPaths[0];
                if (System.IO.Directory.Exists(path))
                    args = $"\"{path}\"";
                else
                    args = $"--file-uri \"file:///{path.Replace("\\", "/")}\"";
            }
            Process proc = Process.Start(codeExe, args);
            return new RestoreResult { Success = true, Process = proc };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}