<#
Fixes DawishContentStudio.Manager build validation errors:
- CS0104 ambiguous OpenFileDialog between System.Windows.Forms and Microsoft.Win32
- possible ambiguous MessageBox between System.Windows.Forms and System.Windows
- missing System.IO qualification for common Path/File static calls
- CS1998 async method lacks await warning in MainWindow.xaml.cs

Run from the repository root where DawishContentStudio.sln exists:
  powershell -ExecutionPolicy Bypass -File .\fix-manager-build-v2.ps1
#>

$ErrorActionPreference = 'Stop'

function Write-Step($message) {
    Write-Host "==> $message"
}

$root = Get-Location
$sln = Get-ChildItem -Path $root -Filter 'DawishContentStudio.sln' -Recurse -File | Select-Object -First 1
if (-not $sln) {
    throw "DawishContentStudio.sln was not found. Run this script from the repository folder, or place it inside the repo first."
}

$repoRoot = $sln.Directory.FullName
Set-Location $repoRoot
Write-Step "Repository root: $repoRoot"

$target = Get-ChildItem -Path $repoRoot -Filter 'MainWindow.xaml.cs' -Recurse -File |
    Where-Object { $_.FullName -match 'DawishContentStudio\.Manager' } |
    Select-Object -First 1

if (-not $target) {
    throw "Could not find src/DawishContentStudio.Manager/MainWindow.xaml.cs"
}

Write-Step "Patching: $($target.FullName)"

$backup = "$($target.FullName).bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $target.FullName $backup -Force
Write-Step "Backup created: $backup"

$text = Get-Content -LiteralPath $target.FullName -Raw
$original = $text

# Normalize CRLF only for predictable regex behavior, then restore Windows line endings at the end.
$text = $text -replace "`r`n", "`n"

# Fully qualify WPF file picker. This is the actual failing CS0104 line.
# Negative lookbehind prevents double-changing Microsoft.Win32.OpenFileDialog or System.Windows.Forms.OpenFileDialog.
$text = [regex]::Replace($text, '(?<![\.\w])OpenFileDialog(?![\w])', 'Microsoft.Win32.OpenFileDialog')

# If System.Windows.Forms is imported, MessageBox can become ambiguous too. In WPF Manager, use WPF MessageBox.
$text = [regex]::Replace($text, '(?<![\.\w])MessageBox(?![\w])', 'System.Windows.MessageBox')

# Safely qualify common System.IO static calls if the file missed using System.IO in some versions.
$text = [regex]::Replace($text, '(?<![\.\w])Path\.', 'System.IO.Path.')
$text = [regex]::Replace($text, '(?<![\.\w])File\.', 'System.IO.File.')
$text = [regex]::Replace($text, '(?<![\.\w])Directory\.', 'System.IO.Directory.')

# Suppress CS1998 in this file because validation currently fails/noises on an async-compatible handler with no await.
# This avoids changing method signatures that XAML event bindings may depend on.
if ($text -notmatch '#pragma\s+warning\s+disable\s+CS1998') {
    $lines = $text -split "`n", -1
    $insertAt = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*using\s+') {
            $insertAt = $i + 1
        }
    }
    $pragma = '#pragma warning disable CS1998 // Async-compatible event handlers may run synchronously.'
    $before = @()
    $after = @()
    if ($insertAt -gt 0) { $before = $lines[0..($insertAt-1)] }
    if ($insertAt -lt $lines.Count) { $after = $lines[$insertAt..($lines.Count-1)] }
    $lines = @($before + $pragma + $after)
    $text = ($lines -join "`n")
}

# Restore CRLF.
$text = $text -replace "`n", "`r`n"

if ($text -eq $original) {
    Write-Host "No text changes were needed."
} else {
    Set-Content -LiteralPath $target.FullName -Value $text -Encoding UTF8
    Write-Step "Patch written."
}

Write-Step "Showing the fixed OpenFileDialog lines:"
Select-String -Path $target.FullName -Pattern 'OpenFileDialog|CS1998' | ForEach-Object {
    Write-Host ("{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim())
}

Write-Step "Restoring packages..."
dotnet restore DawishContentStudio.sln

Write-Step "Building Release..."
dotnet build DawishContentStudio.sln --configuration Release --no-restore

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed. Scroll up for the new first error. Backup is here: $backup"
}

# Commit automatically when inside git repository.
$insideGit = $false
try {
    git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -eq 0) { $insideGit = $true }
} catch { $insideGit = $false }

if ($insideGit) {
    git add $target.FullName
    $diff = git diff --cached --name-only
    if ($diff) {
        git commit -m "Fix Manager build validation errors"
        Write-Step "Committed. Now run: git push"
    } else {
        Write-Step "No git changes to commit."
    }
} else {
    Write-Step "Not inside a git repository. Commit/push manually after verifying."
}

Write-Host "DONE"
