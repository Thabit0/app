using System.Runtime.InteropServices;

namespace DawishContentStudio.Core;

public sealed class PowerAwakeService : IDisposable
{
    private bool _active;

    public void KeepAwake(string reason = "Dawish Publisher Agent")
    {
        if (!OperatingSystem.IsWindows()) return;
        SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired | ExecutionState.EsDisplayRequired);
        _active = true;
    }

    public void Dispose()
    {
        if (_active && OperatingSystem.IsWindows())
            SetThreadExecutionState(ExecutionState.EsContinuous);
        _active = false;
    }

    [DllImport("kernel32.dll")]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [Flags]
    private enum ExecutionState : uint
    {
        EsContinuous = 0x80000000,
        EsSystemRequired = 0x00000001,
        EsDisplayRequired = 0x00000002
    }
}
