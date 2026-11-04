param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = if ($OutputDirectory) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'bin' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /win32icon:"$PSScriptRoot\assets\ChargeTrail.ico" /out:"$output\ChargeTrail.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Core.dll "$PSScriptRoot\src\PilTakip.cs" "$PSScriptRoot\src\ChargeTrail.cs" "$PSScriptRoot\src\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
Write-Output "Hazır: $output\ChargeTrail.exe"
