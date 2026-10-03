param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'),
    [string]$RitsuLibDir = '',
    [string]$OutputDir = (Join-Path $PSScriptRoot 'output\mod')
)

$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
if (!$RitsuLibDir) { $RitsuLibDir = Join-Path $ProjectDir '.godot\mono\temp\bin\Release' }
$gameDataDir = Join-Path $GameDir 'data_sts2_windows_x86_64'
$dotnetRoot = 'C:\Program Files\dotnet'
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
$sdkDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'sdk') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (!$sdkDir -or !$refDir) { throw '需要本机 .NET 9 SDK 和引用程序集。' }
$cscPath = Join-Path $sdkDir.FullName 'Roslyn\bincore\csc.dll'
$references = @(Get-ChildItem -LiteralPath (Join-Path $refDir.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object FullName)
foreach ($name in @('sts2.dll', '0Harmony.dll', 'SmartFormat.dll', 'GodotSharp.dll')) {
    $referencePath = Join-Path $gameDataDir $name
    if (!(Test-Path -LiteralPath $referencePath -PathType Leaf)) { throw "缺少游戏引用：$referencePath" }
    $references += $referencePath
}
$ritsuReferences = @(Get-ChildItem -LiteralPath $RitsuLibDir -Filter 'STS2-RitsuLib*.dll' | ForEach-Object FullName)
if (!$ritsuReferences) { throw "缺少 RitsuLib 编译引用：$RitsuLibDir" }
$references += $ritsuReferences

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
$globalUsings = Join-Path $OutputDir 'CompilerUsings.cs'
[System.IO.File]::WriteAllText($globalUsings, @'
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.0.0")]
'@, [System.Text.UTF8Encoding]::new($false))
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'src') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$responseFile = Join-Path $OutputDir 'compile.rsp'
$arguments = @('/nostdlib+', '/target:library', '/langversion:13.0', '/nullable:enable',
    '/deterministic+', '/optimize+', '/debug:portable', '/utf8output',
    ('/out:"' + (Join-Path $OutputDir 'IndiumStar.dll') + '"'),
    ('/pdb:"' + (Join-Path $OutputDir 'IndiumStar.pdb') + '"'))
$arguments += $references | ForEach-Object { '/reference:"' + $_ + '"' }
$arguments += @($globalUsings) + $sourceFiles | ForEach-Object { '"' + $_ + '"' }
[System.IO.File]::WriteAllLines($responseFile, $arguments, [System.Text.UTF8Encoding]::new($false))
# 直接调用 C# 编译器，只写 OutputDir，不调用项目的复制/部署/PCK 导出目标。
& $dotnetExe $cscPath '/noconfig' ('@' + $responseFile) | Tee-Object -FilePath (Join-Path $OutputDir 'compile-log.txt')
if ($LASTEXITCODE -ne 0) { throw "C# 编译失败，退出码 $LASTEXITCODE" }
$metadata = [ordered]@{
    scope = 'C# source compilation only; no MSBuild, Godot generators, PCK export, or game deployment'
    gameDir = $GameDir
    ritsuLibDir = $RitsuLibDir
    sourceCount = $sourceFiles.Count
    gameReleaseInfo = (Get-Content -LiteralPath (Join-Path $GameDir 'release_info.json') -Raw | ConvertFrom-Json)
    sources = @($sourceFiles | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    references = @($references | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    artifact = [ordered]@{
        path = (Join-Path $OutputDir 'IndiumStar.dll')
        sha256 = (Get-FileHash -LiteralPath (Join-Path $OutputDir 'IndiumStar.dll') -Algorithm SHA256).Hash
    }
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDir 'compile-metadata.json') -Encoding utf8
Write-Output "C# compilation passed: $($sourceFiles.Count) source files; $OutputDir\IndiumStar.dll"
