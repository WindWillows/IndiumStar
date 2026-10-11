param([string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'))
$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$outputDir = Join-Path $PSScriptRoot 'output\export-failure'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$fakeGodot = Join-Path $outputDir 'fake-godot.cmd'
# 真实观察到的Godot行为：.NET导出失败时打印ERROR，但进程返回0。
# 此夹具只打印错误，不写资源包、不操作任何目录。
[IO.File]::WriteAllText($fakeGodot, "@echo off`r`necho ERROR: Export .NET Project: test fixture failure`r`nexit /b 0`r`n", [Text.Encoding]::ASCII)
$validationProject = Join-Path $ProjectDir '.godot\validation\IndiumStar.Validation.csproj'
if (!(Test-Path -LiteralPath $validationProject -PathType Leaf)) { throw '先运行Build-CachedSdk.ps1生成验证工程。' }
$lines = @(dotnet msbuild $validationProject '-t:Export Pck' '-p:ExportPck=true' '-p:DeployMod=false' ("-p:GodotExe=" + $fakeGodot) ("-p:RitsuLibDeployDir=" + (Join-Path $PSScriptRoot 'output\ritsu')) 2>&1)
$exitCode = $LASTEXITCODE
$lines | Set-Content -LiteralPath (Join-Path $outputDir 'export-failure-log.txt') -Encoding utf8
if ($exitCode -eq 0 -or !($lines -match '导出 .pck 失败')) {
    throw '导出失败检测未拦截：进程0退出但存在ERROR: Export。'
}
Write-Output 'PASS MSBuild export failure guard: ERROR: Export rejected despite exporter exit 0; deployment disabled.'
