using System.Diagnostics;
using System.Text;

namespace DawishContentStudio.Core;

public sealed class ClipboardService
{
    public async Task<bool> TrySetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c clip",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
