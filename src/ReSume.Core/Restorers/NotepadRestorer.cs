using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class NotepadRestorer : IAppRestorer {
    public string ProcessName => "notepad";
    public bool CanRestore(ApplicationState app) => app.ProcessName.Equals("notepad", System.StringComparison.OrdinalIgnoreCase);
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            if (app.DocumentPaths.Count > 0) {
                Process proc = Process.Start("notepad.exe", $"\"{app.DocumentPaths[0]}\"");
                return new RestoreResult { Success = true, Process = proc };
            }
            Process procDef = Process.Start("notepad.exe");
            return new RestoreResult { Success = true, Process = procDef };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}