# v0.4.2 Build Fix

This version fixes GitHub Actions build errors in the WPF Manager project:

- `SelectionChangedEventArgs` is now referenced as `WpfControls.SelectionChangedEventArgs`.
- This avoids ambiguity after adding Windows Forms aliases for folder dialogs.
- Node 20/24 warnings from GitHub Actions are not project build errors; the project builds with .NET.
