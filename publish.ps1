# ShineProCS 一键打包脚本
# 生成单文件可执行程序

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ShineProCS 打包脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 清理旧的发布目录
$publishDir = ".\publish"
if (Test-Path $publishDir) {
    Write-Host "清理旧的发布目录..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $publishDir
}

# 发布单文件（自包含模式，无需安装.NET运行时）
Write-Host "正在编译发布..." -ForegroundColor Green
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败!" -ForegroundColor Red
    exit 1
}

# 复制必要的运行时文件
Write-Host "复制运行时依赖..." -ForegroundColor Green

# 复制 libs 目录 (WGC.dll)
$libsDir = "$publishDir\libs"
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir | Out-Null
}
Copy-Item ".\libs\*" -Destination $libsDir -Recurse -Force

# 复制默认配置到 config_default (会被嵌入或首次释放)
$configDefaultDir = "$publishDir\config_default"
if (-not (Test-Path $configDefaultDir)) {
    New-Item -ItemType Directory -Path $configDefaultDir | Out-Null
}
Copy-Item ".\config\appsettings.json" -Destination $configDefaultDir -Force
Copy-Item ".\config\skills.json" -Destination $configDefaultDir -Force

# 复制运行时配置目录（程序启动时需要）
$configDir = "$publishDir\config"
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir | Out-Null
}
Copy-Item ".\config\appsettings.json" -Destination $configDir -Force
Copy-Item ".\config\skills.json" -Destination $configDir -Force

# 复制使用说明文档
Write-Host "复制使用说明文档..." -ForegroundColor Green
$docFile = Join-Path $PSScriptRoot "使用说明.md"
if (Test-Path $docFile) {
    Copy-Item $docFile -Destination $publishDir -Force
    Write-Host "  已复制: 使用说明.md" -ForegroundColor Gray
}

# 获取版本号
$version = (Get-Date -Format "yyyy.MM.dd")
$exeName = "ShineProCS_$version.exe"

# 重命名
$originalExe = "$publishDir\ShineProCS.exe"
$newExe = "$publishDir\$exeName"
if (Test-Path $originalExe) {
    Rename-Item -Path $originalExe -NewName $exeName
}

# 统计文件大小
$fileSize = [math]::Round((Get-Item $newExe).Length / 1MB, 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  打包完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "输出目录: $publishDir" -ForegroundColor White
Write-Host "可执行文件: $exeName" -ForegroundColor White
Write-Host "文件大小: $fileSize MB" -ForegroundColor White
Write-Host ""
Write-Host "发布目录结构:" -ForegroundColor Yellow
Get-ChildItem $publishDir -Recurse | ForEach-Object {
    $indent = "  " * ($_.FullName.Split('\').Count - $publishDir.Split('\').Count)
    Write-Host "$indent$($_.Name)"
}
