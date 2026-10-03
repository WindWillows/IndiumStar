$ErrorActionPreference = 'Stop'
$dotnetRoot = 'C:\Program Files\dotnet'
$sdkDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'sdk') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$outPath = Join-Path $PSScriptRoot 'ApiDump.dll'
$arguments = @('/nostdlib+', '/target:exe', '/langversion:13.0', '/nullable:enable', '/utf8output',
    ('/out:"' + $outPath + '"'))
$arguments += Get-ChildItem -LiteralPath (Join-Path $refDir.FullName 'ref\net9.0') -Filter '*.dll' |
    ForEach-Object { '/reference:"' + $_.FullName + '"' }
$sourcePath = Join-Path $PSScriptRoot 'ApiDump.cs'
$arguments += ('"' + $sourcePath + '"')
$responseFile = Join-Path $PSScriptRoot 'dump.rsp'
[System.IO.File]::WriteAllLines($responseFile, $arguments, [System.Text.UTF8Encoding]::new($false))
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
& $dotnetExe (Join-Path $sdkDir.FullName 'Roslyn\bincore\csc.dll') '/noconfig' ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw "compile failed: $LASTEXITCODE" }
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ApiDump.runtimeconfig.json'),
    '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}', [System.Text.UTF8Encoding]::new($false))
& $dotnetExe (Join-Path $PSScriptRoot 'ApiDump.dll') @args
if ($LASTEXITCODE -ne 0) { throw "dump failed: $LASTEXITCODE" }
