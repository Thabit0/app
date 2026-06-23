# v0.8.3 Build Fix

Fixes GitHub Actions build error:

- `OpenFileDialog` ambiguous between `System.Windows.Forms.OpenFileDialog` and `Microsoft.Win32.OpenFileDialog`.
- Manager now uses `Microsoft.Win32.OpenFileDialog` explicitly.
- Removed unused `<UseWindowsForms>true</UseWindowsForms>` from Manager csproj.
- Removed unnecessary async from `StartShopStation_Click`.
