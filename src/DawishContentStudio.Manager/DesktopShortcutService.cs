using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DawishContentStudio.Manager;

internal static class DesktopShortcutService
{
    public static void TryCreate()
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var executable = Environment.ProcessPath ?? "";
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) return;

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop)) return;

            var shortcutPath = Path.Combine(desktop, "Dawish Content Studio.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;

            shell = Activator.CreateInstance(shellType);
            if (shell is null) return;

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);
            if (shortcut is null) return;

            var shortcutType = shortcut.GetType();
            Set(shortcutType, shortcut, "TargetPath", executable);
            Set(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(executable) ?? "");
            Set(shortcutType, shortcut, "IconLocation", executable + ",0");
            Set(shortcutType, shortcut, "Description", "Dawish Content Studio");
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch
        {
            // The shortcut is a convenience feature and must never prevent startup.
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void Set(Type type, object target, string property, string value) =>
        type.InvokeMember(property, BindingFlags.SetProperty, null, target, [value]);
}
