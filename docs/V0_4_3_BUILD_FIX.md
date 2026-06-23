# v0.4.3 Build Fix

Fixed WPF build errors in `MainWindow.xaml.cs`:

- Added `using System.IO;` so `Path` and `File` resolve correctly.
- Changed `OpenFileDialog` calls to `Microsoft.Win32.OpenFileDialog`.
- Changed `MessageBox.Show` calls to `System.Windows.MessageBox.Show`.

This avoids ambiguity between WPF and Windows Forms types in GitHub Actions.
