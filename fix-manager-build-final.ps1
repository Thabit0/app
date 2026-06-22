$ErrorActionPreference = 'Stop'

Write-Host "== DawishContentStudio Manager final build fix =="

$solution = "DawishContentStudio.sln"
$target = "src/DawishContentStudio.Manager/MainWindow.xaml.cs"

if (-not (Test-Path $solution)) {
    throw "Run this from the repository root, the same folder that contains DawishContentStudio.sln"
}

if (-not (Test-Path $target)) {
    throw "Cannot find $target"
}

$full = Resolve-Path $target
$backup = "$target.bak"
Copy-Item $target $backup -Force
Write-Host "Backup created: $backup"

$content = Get-Content $target -Raw
$original = $content

# Normalize line endings for reliable replacements, restore CRLF at the end.
$content = $content -replace "`r`n", "`n"

# Stop validate/build from failing on async methods that intentionally do not await yet.
if ($content -notmatch '#pragma\s+warning\s+disable\s+CS1998') {
    $content = "#pragma warning disable CS1998`n" + $content
}

# Fully qualify WPF OpenFileDialog everywhere it appears without namespace.
# Handles:
#   new OpenFileDialog()
#   new OpenFileDialog { ... }
#   OpenFileDialog dialog = ...
#   OpenFileDialog? dialog = ...
$content = [regex]::Replace($content, '(?<![\w.])new\s+OpenFileDialog\s*(?=[({])', 'new Microsoft.Win32.OpenFileDialog')
$content = [regex]::Replace($content, '(?<![\w.])OpenFileDialog(\s*\??\s+[A-Za-z_][A-Za-z0-9_]*\s*=)', 'Microsoft.Win32.OpenFileDialog$1')

# Fully qualify common WPF MessageBox calls in case WinForms is also imported.
$content = [regex]::Replace($content, '(?<![\w.])MessageBox\.Show\s*\(', 'System.Windows.MessageBox.Show(')

# Fully qualify System.IO helpers in case System.Windows.Forms also introduced ambiguity/missing using.
$content = [regex]::Replace($content, '(?<![\w.])Path\.', 'System.IO.Path.')
$content = [regex]::Replace($content, '(?<![\w.])File\.', 'System.IO.File.')
$content = [regex]::Replace($content, '(?<![\w.])Directory\.', 'System.IO.Directory.')

# Avoid double-qualifying if the script is run more than once.
$content = $content -replace 'Microsoft\.Win32\.Microsoft\.Win32\.OpenFileDialog', 'Microsoft.Win32.OpenFileDialog'
$content = $content -replace 'System\.Windows\.System\.Windows\.MessageBox', 'System.Windows.MessageBox'
$content = $content -replace 'System\.IO\.System\.IO\.Path\.', 'System.IO.Path.'
$content = $content -replace 'System\.IO\.System\.IO\.File\.', 'System.IO.File.'
$content = $content -replace 'System\.IO\.System\.IO\.Directory\.', 'System.IO.Directory.'

# Restore Windows line endings.
$content = $content -replace "`n", "`r`n"
Set-Content -Path $target -Value $content -NoNewline -Encoding UTF8

Write-Host "Applied patch to $target"
Write-Host "Checking remaining ambiguous OpenFileDialog usages..."
$remaining = Select-String -Path $target -Pattern '(?<![\w.])OpenFileDialog' -AllMatches | Where-Object { $_.Line -notmatch 'Microsoft\.Win32\.OpenFileDialog' }
if ($remaining) {
    Write-Host "Remaining unqualified OpenFileDialog references:" -ForegroundColor Red
    $remaining | ForEach-Object { Write-Host ("Line {0}: {1}" -f $_.LineNumber, $_.Line) -ForegroundColor Red }
    throw "Patch did not remove all unqualified OpenFileDialog references."
}

Write-Host "Restoring and building..."
dotnet restore $solution
dotnet build $solution --configuration Release --no-restore

if (Test-Path .git) {
    git status --short
    git add $target
    git commit -m "Fix Manager WPF dialog build error" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Commit created. Now run: git push" -ForegroundColor Green
    } else {
        Write-Host "No commit created, maybe there were no changes or git identity is missing." -ForegroundColor Yellow
        Write-Host "If build passed, run: git add $target; git commit -m 'Fix Manager WPF dialog build error'; git push"
    }
}

Write-Host "Done." -ForegroundColor Green
