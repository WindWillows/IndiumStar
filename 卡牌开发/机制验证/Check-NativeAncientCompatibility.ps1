param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$BaseLocalizationDir,
    [string]$ModDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar\build\staging')
)
$ErrorActionPreference = 'Stop'
$taskGameData = Join-Path $GameDir 'data_sts2_windows_x86_64'
$taskOutput = Join-Path $PSScriptRoot 'output\ancient-fix'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskSdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskRefs = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskModDir = (Resolve-Path -LiteralPath $ModDir).Path
$taskRitsuDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar\.godot\mono\temp\bin\Release')).Path
$taskUsings = Join-Path $taskOutput 'Usings.cs'
[IO.File]::WriteAllText($taskUsings, "global using System;`nglobal using System.Collections.Generic;`nglobal using System.Linq;`nglobal using System.IO;`n")
$taskArguments = @('/nostdlib+', '/target:exe', '/langversion:13', '/nullable:enable', '/utf8output', ('/out:"' + (Join-Path $taskOutput 'NativeAncientCompatibility.dll') + '"'))
$taskArguments += Get-ChildItem (Join-Path $taskRefs.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
foreach ($taskName in @('sts2.dll', 'GodotSharp.dll', '0Harmony.dll', 'SmartFormat.dll')) { $taskArguments += '/reference:"' + (Join-Path $taskGameData $taskName) + '"' }
$taskArguments += '/reference:"' + (Join-Path $taskModDir 'IndiumStar.dll') + '"'
$taskArguments += Get-ChildItem $taskRitsuDir -Filter 'STS2-RitsuLib*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$taskArguments += @((Join-Path $PSScriptRoot 'NativeAncientCompatibility.cs'), $taskUsings) | ForEach-Object { '"' + $_ + '"' }
$taskResponse = Join-Path $taskOutput 'ancient.rsp'
[IO.File]::WriteAllLines($taskResponse, $taskArguments, [Text.UTF8Encoding]::new($false))
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $taskSdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $taskResponse)
if ($LASTEXITCODE) { throw 'Native ancient check compilation failed' }
Get-ChildItem $taskGameData -Filter '*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $taskOutput -Force }
Get-ChildItem $taskRitsuDir -Filter 'STS2-RitsuLib*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $taskOutput -Force }
Copy-Item -LiteralPath (Join-Path $taskModDir 'IndiumStar.dll') -Destination $taskOutput -Force
[IO.File]::WriteAllText((Join-Path $taskOutput 'NativeAncientCompatibility.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}')
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $taskOutput 'NativeAncientCompatibility.dll') $taskModDir $BaseLocalizationDir (Join-Path $taskOutput 'verification.json') | Tee-Object -LiteralPath (Join-Path $taskOutput 'native-check.log')
if ($LASTEXITCODE) { throw 'Native ancient compatibility checks failed' }
