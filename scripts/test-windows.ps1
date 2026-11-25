$ErrorActionPreference = 'Stop'
if (Get-Process CornerGrab -ErrorAction SilentlyContinue) { throw 'Quit CornerGrab in the tray before the interactive test.' }
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path $env:TEMP ('CornerGrab-check-' + [guid]::NewGuid().ToString('N'))
$shots = Join-Path $temp 'screenshots'
$key = 'HKCU:\Software\CornerGrab'
$hadKey = Test-Path $key
$old = Get-ItemProperty $key -ErrorAction SilentlyContinue
New-Item -ItemType Directory $shots -Force | Out-Null
try {
    New-Item $key -Force | Out-Null
    Set-ItemProperty $key Folder $shots
    $app = Join-Path $temp 'CornerGrab.exe'
    Copy-Item (Join-Path $root 'dist\CornerGrab.exe') $app
    $runner = Join-Path $temp 'WindowsSmoke.exe'
    & "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe "/out:$runner" "/win32manifest:$root\windows\CornerGrab.manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $root 'tests\WindowsSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Smoke test did not compile.' }
    & $runner $app $shots
    if ($LASTEXITCODE -ne 0) { throw 'Interactive smoke test failed.' }
} finally {
    if ($hadKey) {
        foreach ($name in 'Folder','Latest') {
            if ($null -ne $old.$name) { Set-ItemProperty $key $name $old.$name }
            else { Remove-ItemProperty $key $name -ErrorAction SilentlyContinue }
        }
    } else { Remove-Item $key -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
