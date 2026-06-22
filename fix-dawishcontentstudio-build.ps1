# Auto-fix for DawishContentStudio.Manager build errors
# Run from the repository root (the folder that contains DawishContentStudio.sln)

$ErrorActionPreference = "Stop"

$target = "src/DawishContentStudio.Manager/MainWindow.xaml.cs"

if (!(Test-Path $target)) {
    Write-Host "ERROR: Cannot find $target" -ForegroundColor Red
    Write-Host "Run this script from the repository root, next to DawishContentStudio.sln." -ForegroundColor Yellow
    exit 1
}

$content = Get-Content $target -Raw
$original = $content

# Make System.IO types available if the file uses Path/File.
if ($content -notmatch '(?m)^\s*using\s+System\.IO\s*;') {
    $lastUsing = [regex]::Matches($content, '(?m)^using\s+[^;]+;') | Select-Object -Last 1
    if ($lastUsing) {
        $insertAt = $lastUsing.Index + $lastUsing.Length
        $content = $content.Insert($insertAt, "`r`nusing System.IO;")
    } else {
        $content = "using System.IO;`r`n" + $content
    }
}

# Resolve WPF/WinForms ambiguity explicitly.
# WPF file picker should use Microsoft.Win32.OpenFileDialog.
$content = [regex]::Replace($content, '(?<![\w.])new\s+OpenFileDialog\b', 'new Microsoft.Win32.OpenFileDialog')

# WPF MessageBox should use System.Windows.MessageBox.
$content = [regex]::Replace($content, '(?<![\w.])MessageBox\.Show\s*\(', 'System.Windows.MessageBox.Show(')

# Resolve missing/ambiguous IO references safely.
$content = [regex]::Replace($content, '(?<![\w.])Path\.', 'System.IO.Path.')
$content = [regex]::Replace($content, '(?<![\w.])File\.', 'System.IO.File.')

if ($content -ne $original) {
    Set-Content -Path $target -Value $content -Encoding UTF8
    Write-Host "Fixed: $target" -ForegroundColor Green
} else {
    Write-Host "No source changes were needed in $target" -ForegroundColor Yellow
}

Write-Host "Restoring..." -ForegroundColor Cyan
dotnet restore DawishContentStudio.sln

Write-Host "Building Release..." -ForegroundColor Cyan
dotnet build DawishContentStudio.sln --configuration Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build still failed. Send the new log." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Build passed." -ForegroundColor Green

# Optional commit when running inside a git repo.
if (Test-Path ".git") {
    git status --short
    git add $target
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "Fix Manager WPF dialog build errors"
        Write-Host "Committed fix." -ForegroundColor Green
    } else {
        Write-Host "Nothing to commit." -ForegroundColor Yellow
    }
}
