using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class WordRestorer : IAppRestorer {
    public string ProcessName => "winword";
    public bool CanRestore(ApplicationState app) => app.ProcessName.Equals("winword", System.StringComparison.OrdinalIgnoreCase);
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            string? exe = app.ExecutablePath ?? @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE";
            string args = app.DocumentPaths.Count > 0 ? $"\"{app.DocumentPaths[0]}\"" : "";
            Process proc = Process.Start(exe, args);
            return new RestoreResult { Success = true, Process = proc };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}