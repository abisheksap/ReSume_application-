using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class ExplorerRestorer : IAppRestorer {
    public string ProcessName => "explorer";
    public bool CanRestore(ApplicationState app) => app.ProcessName.Equals("explorer", System.StringComparison.OrdinalIgnoreCase);
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            if (app.DocumentPaths.Count > 0) {
                Process proc = Process.Start("explorer.exe", $"\"{app.DocumentPaths[0]}\"");
                return new RestoreResult { Success = true, Process = proc };
            }
            Process procDef = Process.Start("explorer.exe");
            return new RestoreResult { Success = true, Process = procDef };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}