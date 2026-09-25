$ErrorActionPreference = "Stop"

Write-Host "===== 1. 清理 =====" -ForegroundColor Cyan
dotnet clean -c Release

Write-Host "`n===== 2. 发布 =====" -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish\win-x64

Write-Host "`n===== 3. 验证 VLC 依赖 =====" -ForegroundColor Cyan
$required = @(
    ".\publish\win-x64\libvlc.dll",
    ".\publish\win-x64\libvlccore.dll",
    ".\publish\win-x64\plugins"
)

$missing = @()
foreach ($item in $required) {
    if (-not (Test-Path $item)) {
        $missing += $item
        Write-Host "✗ 缺失: $item" -ForegroundColor Red
    } else {
        Write-Host "✓ 存在: $item" -ForegroundColor Green
    }
}

# 如果缺失，从 NuGet 缓存复制
if ($missing.Count -gt 0) {
    Write-Host "`n尝试从 NuGet 缓存复制 VLC..." -ForegroundColor Yellow
    $nugetVlc = "$env:USERPROFILE\.nuget\packages\videolan.libvlc.windows\3.0.21\build\x64"
    if (Test-Path $nugetVlc) {
        Copy-Item "$nugetVlc\*" -Destination ".\publish\win-x64\" -Recurse -Force
        Write-Host "✓ 已从 $nugetVlc 复制" -ForegroundColor Green
    } else {
        Write-Host "✗ 找不到 NuGet 缓存: $nugetVlc" -ForegroundColor Red
        Write-Host "请执行: dotnet nuget locals all --clear && dotnet restore" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "`n===== 4. Inno Setup 打包 =====" -ForegroundColor Cyan
$inno = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $inno) {
    & $inno .\installer.iss
    Write-Host "`n✓ 安装包：publish\installer\XiYue_Setup_v1.2.0.exe" -ForegroundColor Green
} else {
    Write-Host "✗ 未找到 Inno Setup" -ForegroundColor Yellow
}

Write-Host "`n===== 完成 =====" -ForegroundColor Cyan