using System.Threading.Tasks;
using System.Diagnostics;
using ReSume.Core.Models;

namespace ReSume.Core.Restorers;

public interface IAppRestorer {
    string ProcessName { get; }
    bool CanRestore(ApplicationState app);
    Task<RestoreResult> RestoreAsync(ApplicationState app);
}

public sealed class RestoreResult {
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Process? Process { get; set; }
}