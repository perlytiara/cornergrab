$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$files = (Get-ChildItem (Join-Path $root 'windows\*.cs')).FullName
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /codepage:65001 /warnaserror+ "/out:$out\CornerGrab.exe" "/win32manifest:$root\windows\CornerGrab.manifest" "/win32icon:$root\windows\CornerGrab.ico" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $files
if ($LASTEXITCODE -ne 0) { throw "The Windows compiler exited with $LASTEXITCODE" }
Write-Output (Join-Path $out 'CornerGrab.exe')
