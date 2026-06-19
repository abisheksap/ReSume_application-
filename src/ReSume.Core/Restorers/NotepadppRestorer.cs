using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class NotepadppRestorer : IAppRestorer {
    public string ProcessName => "notepad++";
    public bool CanRestore(ApplicationState app) => app.ProcessName.IndexOf("notepad++", System.StringComparison.OrdinalIgnoreCase) >= 0;
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            string? exe = app.ExecutablePath ?? @"C:\Program Files\Notepad++\notepad++.exe";
            string args = app.DocumentPaths.Count > 0 ? $"\"{app.DocumentPaths[0]}\"" : "";
            Process proc = Process.Start(exe, args);
            return new RestoreResult { Success = true, Process = proc };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}