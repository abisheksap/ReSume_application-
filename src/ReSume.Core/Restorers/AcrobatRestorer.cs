using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public class AcrobatRestorer : IAppRestorer {
    public string ProcessName => "acrord32";
    public bool CanRestore(ApplicationState app) =>
        app.ProcessName.IndexOf("acrord32", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
        app.ProcessName.IndexOf("acrobat", System.StringComparison.OrdinalIgnoreCase) >= 0;
    public async Task<RestoreResult> RestoreAsync(ApplicationState app) {
        try {
            string? exe = app.ExecutablePath ?? @"C:\Program Files (x86)\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe";
            string args = app.DocumentPaths.Count > 0 ? $"\"{app.DocumentPaths[0]}\"" : "";
            Process proc = Process.Start(exe, args);
            return new RestoreResult { Success = true, Process = proc };
        } catch (System.Exception ex) {
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}