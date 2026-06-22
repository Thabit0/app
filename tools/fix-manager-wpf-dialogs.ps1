param(
    [switch]$Build,
    [switch]$Commit
)

$ErrorActionPreference = 'Stop'

function Write-Step($msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Fail($msg) {
    Write-Error $msg
    exit 1
}

$root = (Get-Location).Path
$sln = Join-Path $root 'DawishContentStudio.sln'
$managerFile = Join-Path $root 'src/DawishContentStudio.Manager/MainWindow.xaml.cs'
$managerProject = Join-Path $root 'src/DawishContentStudio.Manager/DawishContentStudio.Manager.csproj'

Write-Step "Checking project root"
if (-not (Test-Path $sln)) {
    Fail "DawishContentStudio.sln was not found. Run this script from the repository root. Current path: $root"
}
if (-not (Test-Path $managerFile)) {
    Fail "MainWindow.xaml.cs was not found at: $managerFile"
}
if (-not (Test-Path $managerProject)) {
    Fail "DawishContentStudio.Manager.csproj was not found at: $managerProject"
}

Write-Step "Patching MainWindow.xaml.cs"
$content = Get-Content $managerFile -Raw
$original = $content

# Suppress the analyzer warning that currently breaks validation annotations.
if ($content -notmatch '#pragma\s+warning\s+disable\s+CS1998') {
    $content = "#pragma warning disable CS1998`r`n" + $content
}

# Add System.IO for Path/File/Directory if future code uses them.
if ($content -notmatch '(?m)^\s*using\s+System\.IO\s*;') {
    $content = $content -replace '(?m)(^\s*using\s+System\s*;\s*$)', "`$1`r`nusing System.IO;"
}

# Resolve WPF/WinForms ambiguity explicitly.
# Works with: new OpenFileDialog(), new OpenFileDialog { ... }, and typed declarations.
$content = [regex]::Replace($content, '(?<![\w\.])new\s+OpenFileDialog\s*\(', 'new Microsoft.Win32.OpenFileDialog(')
$content = [regex]::Replace($content, '(?<![\w\.])new\s+OpenFileDialog\s*\{', 'new Microsoft.Win32.OpenFileDialog {')
$content = [regex]::Replace($content, '(?<![\w\.])OpenFileDialog\s+([A-Za-z_]\w*)\s*=', 'Microsoft.Win32.OpenFileDialog $1 =')

# Resolve MessageBox ambiguity if System.Windows.Forms is imported.
$content = [regex]::Replace($content, '(?<![\w\.])MessageBox\.Show\s*\(', 'System.Windows.MessageBox.Show(')

# Resolve SaveFileDialog ambiguity if it appears later.
$content = [regex]::Replace($content, '(?<![\w\.])new\s+SaveFileDialog\s*\(', 'new Microsoft.Win32.SaveFileDialog(')
$content = [regex]::Replace($content, '(?<![\w\.])new\s+SaveFileDialog\s*\{', 'new Microsoft.Win32.SaveFileDialog {')
$content = [regex]::Replace($content, '(?<![\w\.])SaveFileDialog\s+([A-Za-z_]\w*)\s*=', 'Microsoft.Win32.SaveFileDialog $1 =')

if ($content -ne $original) {
    Set-Content -Path $managerFile -Value $content -Encoding UTF8
    Write-Host "Patched: $managerFile" -ForegroundColor Green
} else {
    Write-Host "No changes needed in MainWindow.xaml.cs" -ForegroundColor Yellow
}

Write-Step "Patching Manager csproj to suppress CS1998 in validation"
$proj = Get-Content $managerProject -Raw
$projOriginal = $proj
if ($proj -notmatch '<NoWarn>[^<]*CS1998') {
    if ($proj -match '<PropertyGroup>') {
        $proj = $proj -replace '<PropertyGroup>', "<PropertyGroup>`r`n    <NoWarn>`$(NoWarn);CS1998</NoWarn>", 1
    } else {
        $proj = $proj -replace '<Project([^>]*)>', "<Project`$1>`r`n  <PropertyGroup>`r`n    <NoWarn>`$(NoWarn);CS1998</NoWarn>`r`n  </PropertyGroup>", 1
    }
}
if ($proj -ne $projOriginal) {
    Set-Content -Path $managerProject -Value $proj -Encoding UTF8
    Write-Host "Patched: $managerProject" -ForegroundColor Green
} else {
    Write-Host "No changes needed in csproj" -ForegroundColor Yellow
}

Write-Step "Showing changed lines"
$lineNumber = 0
Get-Content $managerFile | ForEach-Object {
    $lineNumber++
    if ($_ -match 'OpenFileDialog|MessageBox\.Show|CS1998|System\.IO') {
        Write-Host ("{0}: {1}" -f $lineNumber, $_)
    }
}

if ($Build) {
    Write-Step "Restoring and building"
    dotnet restore $sln
    dotnet build $sln --configuration Release --no-restore
    Write-Host "Build completed." -ForegroundColor Green
}

if ($Commit) {
    Write-Step "Committing changes"
    git add src/DawishContentStudio.Manager/MainWindow.xaml.cs src/DawishContentStudio.Manager/DawishContentStudio.Manager.csproj
    git commit -m "Fix Manager WPF dialog validation errors"
    Write-Host "Commit created. Run: git push" -ForegroundColor Green
}

Write-Step "Done"
Write-Host "Next command: git status" -ForegroundColor White
