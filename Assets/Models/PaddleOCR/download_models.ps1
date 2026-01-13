# PaddleOCR 模型下载脚本
# 从 BetterGI.Assets.Model NuGet 包下载模型文件

param(
    [string]$OutputDir = ".",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "=== PaddleOCR 模型下载脚本 ===" -ForegroundColor Cyan
Write-Host ""

# 检查 nuget 是否可用
$nugetPath = Get-Command nuget -ErrorAction SilentlyContinue
if (-not $nugetPath) {
    Write-Host "正在下载 nuget.exe..." -ForegroundColor Yellow
    $nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    $nugetExe = Join-Path $env:TEMP "nuget.exe"
    Invoke-WebRequest -Uri $nugetUrl -OutFile $nugetExe
    $nugetPath = $nugetExe
} else {
    $nugetPath = $nugetPath.Source
}

# 创建临时目录
$tempDir = Join-Path $env:TEMP "BetterGI_Models_$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Write-Host "正在下载 BetterGI.Assets.Model NuGet 包..." -ForegroundColor Yellow
    & $nugetPath install BetterGI.Assets.Model -OutputDirectory $tempDir -NonInteractive
    
    # 查找下载的包
    $packageDir = Get-ChildItem $tempDir -Directory -Filter "BetterGI.Assets.Model*" | Select-Object -First 1
    if (-not $packageDir) {
        throw "未找到下载的 NuGet 包"
    }
    
    $modelSourceDir = Join-Path $packageDir.FullName "contentFiles\any\any\Assets\Model\PaddleOCR"
    if (-not (Test-Path $modelSourceDir)) {
        # 尝试其他可能的路径
        $modelSourceDir = Join-Path $packageDir.FullName "content\Assets\Model\PaddleOCR"
    }
    
    if (-not (Test-Path $modelSourceDir)) {
        Write-Host "NuGet 包结构:" -ForegroundColor Yellow
        Get-ChildItem $packageDir.FullName -Recurse -Directory | ForEach-Object { Write-Host $_.FullName }
        throw "未找到模型文件目录"
    }
    
    Write-Host "正在复制模型文件..." -ForegroundColor Yellow
    
    # 复制所有文件
    $files = Get-ChildItem $modelSourceDir -Recurse -File
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($modelSourceDir.Length + 1)
        $destPath = Join-Path $OutputDir $relativePath
        $destDir = Split-Path $destPath -Parent
        
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        if ((Test-Path $destPath) -and -not $Force) {
            Write-Host "  跳过 (已存在): $relativePath" -ForegroundColor Gray
        } else {
            Copy-Item $file.FullName $destPath -Force
            Write-Host "  复制: $relativePath" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "模型下载完成！" -ForegroundColor Green
    
} finally {
    # 清理临时目录
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
