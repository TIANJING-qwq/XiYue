<#
.SYNOPSIS
    XiYue one-click build & GitHub release
.EXAMPLE
    .\publish.ps1 -Version 0.3.1
    .\publish.ps1 -Version 0.3.1 -SkipPublish
#>

[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipPublish,
    [string]$Notes = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# Config
# ============================================================
$RepoOwner    = "TIANJING-qwq"
$RepoName     = "XiYue"
$CsprojPath   = "SBtools.csproj"
$InstallerIss = "installer.iss"
$PublishDir   = ".\publish\win-x64"
$ZipOutputDir = ".\publish"
$InnoSetupExe = "C:\Program Files\Inno Setup 7\ISCC.exe"
$Runtime      = "win-x64"

# ============================================================
# Helpers
# ============================================================
function Write-Step($msg) { Write-Host "`n===== $msg =====" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  [!!] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "  [XX] $msg" -ForegroundColor Red }

# ============================================================
# 0. Version
# ============================================================
Write-Step "0. Version"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $content = Get-Content $CsprojPath -Raw
    if ($content -match '<Version>([\d\.]+)</Version>') {
        $Version = $Matches[1]
        Write-Ok "Using csproj version: $Version"
    } else {
        Write-Err "Cannot read version from $CsprojPath"
        exit 1
    }
} else {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Err "Version format must be x.y.z"
        exit 1
    }
    Write-Ok "Target version: $Version"

    $content = Get-Content $CsprojPath -Raw
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
    $content = $content -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$Version</FileVersion>"
    $content = $content -replace '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>Dev$Version</InformationalVersion>"
    $content | Set-Content $CsprojPath -NoNewline -Encoding UTF8
    Write-Ok "csproj updated"

    if (Test-Path $InstallerIss) {
        $issContent = Get-Content $InstallerIss -Raw
        $issContent = $issContent -replace 'AppVersion=.*', "AppVersion=$Version"
        $issContent = $issContent -replace 'OutputBaseFilename=.*', "OutputBaseFilename=XiYue_Setup_v$Version"
        $issContent | Set-Content $InstallerIss -NoNewline -Encoding UTF8
        Write-Ok "installer.iss updated"
    }
}

$Tag     = "v$Version"
$ZipName = "XiYue_win-x64_v$Version.zip"
$ZipPath = Join-Path $ZipOutputDir $ZipName
$InstallerPath = ".\publish\installer\XiYue_Setup_v$Version.exe"

Write-Host "`n  Version: $Version" -ForegroundColor White
Write-Host "  Git Tag: $Tag" -ForegroundColor White

# ============================================================
# 1. Clean
# ============================================================
Write-Step "1. Clean"
dotnet clean -c Release --nologo
if (Test-Path ".\publish") { Remove-Item ".\publish" -Recurse -Force }
Write-Ok "Clean done"

# ============================================================
# 2. Publish
# ============================================================
Write-Step "2. Publish ($Runtime)"
dotnet publish -c Release -r $Runtime --self-contained false -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { Write-Err "dotnet publish failed"; exit 1 }
Write-Ok "Publish done"

# ============================================================
# 3. Verify VLC deps
# ============================================================
Write-Step "3. Verify VLC dependencies"
$required = @(
    "$PublishDir\libvlc.dll",
    "$PublishDir\libvlccore.dll",
    "$PublishDir\plugins"
)

$missing = @()
foreach ($item in $required) {
    if (-not (Test-Path $item)) {
        $missing += $item
        Write-Err "Missing: $item"
    } else {
        Write-Ok "Found: $item"
    }
}

if ($missing.Count -gt 0) {
    Write-Warn "Try copying from NuGet cache..."
    $nugetVlc = "$env:USERPROFILE\.nuget\packages\videolan.libvlc.windows\3.0.21\build\x64"
    if (Test-Path $nugetVlc) {
        Copy-Item "$nugetVlc\*" -Destination $PublishDir -Recurse -Force
        Write-Ok "Copied from $nugetVlc"
    } else {
        Write-Err "NuGet cache not found. Run: dotnet nuget locals all --clear ; dotnet restore"
        exit 1
    }
}

# ============================================================
# 4. Build ZIP
# ============================================================
Write-Step "4. Build portable ZIP"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force
$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Ok "ZIP: $ZipName ($zipSize MB)"

# ============================================================
# 5. Inno Setup
# ============================================================
Write-Step "5. Inno Setup installer"

if (Test-Path $InnoSetupExe) {
    Write-Host "  Compiling installer.iss ..." -ForegroundColor Gray
    & $InnoSetupExe $InstallerIss
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Inno Setup compile failed (exit $LASTEXITCODE)"
    } elseif (Test-Path $InstallerPath) {
        $instSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 1)
        Write-Ok "Installer: XiYue_Setup_v$Version.exe ($instSize MB)"
    } else {
        Write-Warn "Inno Setup ran but output not found at $InstallerPath"
    }
} else {
    Write-Warn "Inno Setup compiler not found: $InnoSetupExe"
    Write-Warn "Skipping installer, only ZIP will be published"
}

# ============================================================
# 6. Publish to GitHub Releases
# ============================================================
if ($SkipPublish) {
    Write-Step "6. Skip GitHub publish"
    Write-Host "`nLocal artifacts:" -ForegroundColor Yellow
    Write-Host "  $ZipPath" -ForegroundColor White
    if (Test-Path $InstallerPath) {
        Write-Host "  $InstallerPath" -ForegroundColor White
    }
    exit 0
}

Write-Step "6. Publish to GitHub Releases"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Err "GitHub CLI (gh) not found. Install: https://cli.github.com/"
    exit 1
}

# 清除可能存在的 GITHUB_TOKEN 环境变量，避免覆盖登录凭据
if ($env:GITHUB_TOKEN) {
    Write-Warn "GITHUB_TOKEN env var detected, ignoring it for this run"
    Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
}

gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Err "Not logged in. Run: gh auth login"
    exit 1
}
Write-Ok "GitHub CLI authenticated"

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $Notes = "## XiYue v$Version`n`n### Downloads`n- Installer: XiYue_Setup_v$Version.exe`n- Portable: XiYue_win-x64_v$Version.zip"
}

$assets = @()
if (Test-Path $InstallerPath) { $assets += $InstallerPath }
if (Test-Path $ZipPath)       { $assets += $ZipPath }

if ($assets.Count -eq 0) {
    Write-Err "No build artifacts found"
    exit 1
}

Write-Host "`n  Assets to upload:" -ForegroundColor White
foreach ($a in $assets) {
    $size = [math]::Round((Get-Item $a).Length / 1MB, 1)
    Write-Host "    - $(Split-Path $a -Leaf) ($size MB)" -ForegroundColor Gray
}

$tagExists = $false
try {
    gh release view $Tag --repo "$RepoOwner/$RepoName" 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $tagExists = $true }
} catch { }

if ($tagExists) {
    Write-Warn "Release $Tag already exists, uploading assets (clobber)"
    foreach ($a in $assets) {
        gh release upload $Tag $a --repo "$RepoOwner/$RepoName" --clobber
        Write-Ok "Uploaded: $(Split-Path $a -Leaf)"
    }
} else {
    Write-Host "`n  Creating Release $Tag ..." -ForegroundColor Gray
    $notesFile = [System.IO.Path]::GetTempFileName()
    $Notes | Set-Content $notesFile -Encoding UTF8

    gh release create $Tag `
        --repo "$RepoOwner/$RepoName" `
        --title "XiYue $Tag" `
        --notes-file $notesFile `
        --latest `
        $assets

    Remove-Item $notesFile -Force -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Release $Tag created"
    } else {
        Write-Err "Release creation failed"
        exit 1
    }
}

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "  Publish complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  https://github.com/$RepoOwner/$RepoName/releases/tag/$Tag" -ForegroundColor White
Write-Host ""