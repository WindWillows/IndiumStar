param([Parameter(Mandatory=$true)][string]$GameDir,
    [string]$ModDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar\build\staging'))
$ErrorActionPreference = 'Stop'
$gameData = Join-Path $GameDir 'data_sts2_windows_x86_64'
$outputDir = Join-Path $PSScriptRoot 'output\native-models'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refs = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$ModDir = (Resolve-Path -LiteralPath $ModDir).Path
$ritsuDir = Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar\.godot\mono\temp\bin\Release'
$usings = Join-Path $outputDir 'Usings.cs'
[IO.File]::WriteAllText($usings, "global using System;`nglobal using System.Collections.Generic;`nglobal using System.Linq;`nglobal using System.IO;`n")
$arguments = @('/nostdlib+', '/target:exe', '/langversion:13', '/nullable:enable', '/utf8output', ('/out:"' + (Join-Path $outputDir 'NativeModelSmoke.dll') + '"'))
$arguments += Get-ChildItem (Join-Path $refs.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
foreach ($name in @('sts2.dll', 'GodotSharp.dll', '0Harmony.dll')) { $arguments += '/reference:"' + (Join-Path $gameData $name) + '"' }
$arguments += '/reference:"' + (Join-Path $modDir 'IndiumStar.dll') + '"'
$arguments += Get-ChildItem $ritsuDir -Filter 'STS2-RitsuLib*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$arguments += @((Join-Path $PSScriptRoot 'NativeModelSmoke.cs'), $usings) | ForEach-Object { '"' + $_ + '"' }
$rsp = Join-Path $outputDir 'smoke.rsp'
[IO.File]::WriteAllLines($rsp, $arguments, [Text.UTF8Encoding]::new($false))
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $rsp)
if ($LASTEXITCODE) { throw 'Native model smoke compilation failed' }
Get-ChildItem $gameData -Filter '*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $outputDir -Force }
Get-ChildItem $ritsuDir -Filter 'STS2-RitsuLib*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $outputDir -Force }
Copy-Item -LiteralPath (Join-Path $modDir 'IndiumStar.dll') -Destination $outputDir -Force
[IO.File]::WriteAllText((Join-Path $outputDir 'NativeModelSmoke.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}')
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $outputDir 'NativeModelSmoke.dll') (Join-Path $outputDir 'native-model-smoke.json') | Tee-Object (Join-Path $outputDir 'smoke-log.txt')
if ($LASTEXITCODE) { throw 'Native model smoke failed' }
