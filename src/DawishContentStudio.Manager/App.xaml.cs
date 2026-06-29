using System;
using System.IO;
using System.Windows;

namespace DawishContentStudio.Manager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrash(args.Exception);
            MessageBox.Show("صار خطأ أثناء تشغيل البرنامج. تم حفظ التفاصيل في ملف DawishContentStudio-error.txt على سطح المكتب.", "Dawish Content Studio");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) WriteCrash(ex);
        };
    }

    private static void WriteCrash(Exception ex)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = Path.Combine(desktop, "DawishContentStudio-error.txt");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch
        {
            // ignore logging failures
        }
    }
}
