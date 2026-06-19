using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class GenericRestorer : IAppRestorer {
    public string ProcessName => "*";
    public bool CanRestore(ApplicationState app) => true;
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            if (!string.IsNullOrEmpty(app.ExecutablePath)) {
                Process proc = Process.Start(new ProcessStartInfo(app.ExecutablePath) { UseShellExecute = true });
                return new RestoreResult { Success = true, Process = proc };
            }
            return new RestoreResult { Success = false, ErrorMessage = "No executable path" };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}