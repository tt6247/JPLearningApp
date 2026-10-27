# 在 Windows 上编译《假名小步 · 独立桌面版》
# 使用系统自带的 .NET Framework 4 编译器（C# 5），无需安装 Visual Studio 或 .NET SDK。
# 用法：双击 build.cmd；或在 PowerShell 中运行 .\build.ps1 [-Test]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 .NET Framework 4 编译器 csc.exe' }

$out = Join-Path $PSScriptRoot 'dist\假名小步-独立桌面版.exe'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

$cscArgs = @(
  '/nologo', '/langversion:5', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
  '/win32manifest:src\app.manifest', '/win32icon:src\app.ico',
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
  '/r:System.Xml.dll', '/r:System.Xml.Linq.dll',
  "/out:$out"   # 旧版 csc 要求 /out、/target 写在源文件之前
)
Get-ChildItem 'src\Resources\KanaAudio' -File | ForEach-Object { $cscArgs += "/resource:$($_.FullName),$($_.Name)" }
Get-ChildItem 'src\Resources\Fonts' -File | ForEach-Object { $cscArgs += "/resource:$($_.FullName),Fonts.$($_.Name)" }
Get-ChildItem 'src\Resources\Strokes' -File | ForEach-Object { $cscArgs += "/resource:$($_.FullName),Strokes.$($_.Name)" }
Get-ChildItem 'src\Resources\Words' -File | ForEach-Object { $cscArgs += "/resource:$($_.FullName),Words.$($_.Name)" }
Get-ChildItem 'src' -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $cscArgs += $_.FullName }

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Host "已生成：$out" -ForegroundColor Green

if ($Test) {
  $dir = Join-Path $PSScriptRoot 'dist\selftest'
  $p = Start-Process -FilePath $out -ArgumentList @('--self-test', "`"$dir`"") -Wait -PassThru
  Get-Content (Join-Path $dir 'result.txt') -Encoding UTF8
  if ($p.ExitCode -ne 0) { throw '自检未通过' }
  Write-Host '自检全部通过' -ForegroundColor Green
}
