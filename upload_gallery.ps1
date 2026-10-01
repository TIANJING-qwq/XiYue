<#
.SYNOPSIS
    上传 bili.zip 到 GitHub Releases 的 gallery 标签
.EXAMPLE
    .\upload_gallery.ps1
#>

$ErrorActionPreference = "Stop"

$RepoOwner = "TIANJING-qwq"
$RepoName  = "XiYue"
$Tag       = "gallery"
$File      = "bili.zip"

if (-not (Test-Path $File)) {
    Write-Host "找不到 $File，请把 bili.zip 放到项目根目录" -ForegroundColor Red
    exit 1
}

$size = [math]::Round((Get-Item $File).Length / 1MB, 1)
Write-Host "文件: $File ($size MB)" -ForegroundColor White

# 检查 tag 是否存在
Write-Host "`n检查 Release $Tag ..." -ForegroundColor Cyan
$exists = $false
try {
    gh release view $Tag --repo "$RepoOwner/$RepoName" 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $exists = $true }
} catch { }

if (-not $exists) {
    Write-Host "Release $Tag 不存在，创建中..." -ForegroundColor Yellow
    gh release create $Tag `
        --repo "$RepoOwner/$RepoName" `
        --title "图库资源" `
        --notes "汐月图库资源（不随程序打包，按需下载）"
}

Write-Host "`n上传 $File ..." -ForegroundColor Cyan
gh release upload $Tag $File --repo "$RepoOwner/$RepoName" --clobber

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ 上传成功" -ForegroundColor Green
    Write-Host "  下载地址: https://github.com/$RepoOwner/$RepoName/releases/download/$Tag/$File" -ForegroundColor White
} else {
    Write-Host "`n✗ 上传失败" -ForegroundColor Red
    exit 1
}