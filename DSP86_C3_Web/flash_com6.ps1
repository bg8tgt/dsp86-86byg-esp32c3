# 一键编译 + 烧录 + 串口（固定 COM6）
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==> Building ..." -ForegroundColor Cyan
pio run
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Write-Host "==> Uploading to COM6 ..." -ForegroundColor Cyan
pio run -t upload --upload-port COM6
if ($LASTEXITCODE -ne 0) { throw "upload failed (check COM6 / driver / board)" }

Write-Host "==> Serial monitor COM6 @115200 (Ctrl+C to exit)" -ForegroundColor Cyan
pio device monitor -p COM6 -b 115200
