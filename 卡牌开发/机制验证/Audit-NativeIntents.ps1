param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$gameData = Join-Path $GameDir 'data_sts2_windows_x86_64'
$auditOut = Join-Path $PSScriptRoot 'output\native-intents'
New-Item -ItemType Directory -Path $auditOut -Force | Out-Null
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refs = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object Name -like '9.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$usings = Join-Path $auditOut 'Usings.cs'
[IO.File]::WriteAllText($usings, "global using System;`nglobal using System.Collections.Generic;`nglobal using System.Linq;`nglobal using System.Threading.Tasks;`nglobal using System.IO;`n")
$arguments = @('/nostdlib+', '/target:exe', '/langversion:13', '/nullable:enable', '/utf8output', ('/out:"' + (Join-Path $auditOut 'NativeIntentAudit.dll') + '"'))
$arguments += Get-ChildItem (Join-Path $refs.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
foreach ($name in @('sts2.dll', 'GodotSharp.dll')) { $arguments += '/reference:"' + (Join-Path $gameData $name) + '"' }
$projectRoot = Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'
$arguments += @((Join-Path $projectRoot 'src\Mechanics\BorrowedIntentCompatibility.cs'), (Join-Path $PSScriptRoot 'NativeIntentAudit.cs'), $usings) | ForEach-Object { '"' + $_ + '"' }
$rsp = Join-Path $auditOut 'audit.rsp'
[IO.File]::WriteAllLines($rsp, $arguments, [Text.UTF8Encoding]::new($false))
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $rsp)
if ($LASTEXITCODE) { throw 'Native intent audit compilation failed' }
Get-ChildItem $gameData -Filter '*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $auditOut -Force }
[IO.File]::WriteAllText((Join-Path $auditOut 'NativeIntentAudit.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}')
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $auditOut 'NativeIntentAudit.dll') (Join-Path $auditOut 'native-intent-audit.json') | Tee-Object (Join-Path $auditOut 'audit-log.txt')
if ($LASTEXITCODE) { throw 'Native intent audit failed' }
