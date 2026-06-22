DawishContentStudio build fix

This fixes the WPF Manager build errors caused by ambiguous references:
- OpenFileDialog between System.Windows.Forms and Microsoft.Win32
- MessageBox between System.Windows.Forms and System.Windows
- Missing Path/File references from System.IO

How to use:
1) Put fix-dawishcontentstudio-build.ps1 in the repository root, next to DawishContentStudio.sln
2) Run PowerShell:
   powershell -ExecutionPolicy Bypass -File .\fix-dawishcontentstudio-build.ps1

The script edits:
src/DawishContentStudio.Manager/MainWindow.xaml.cs

Then it runs:
dotnet restore DawishContentStudio.sln
dotnet build DawishContentStudio.sln --configuration Release --no-restore

If the folder is a git repository, it also commits the fix.
