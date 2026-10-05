param(
    [string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'),
    [string]$PackageCache = (Join-Path $env:USERPROFILE '.nuget\packages')
)
$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$sdkPath = Join-Path $PackageCache 'godot.net.sdk\4.5.1\Sdk'
$generatorProps = Join-Path $PackageCache 'godot.sourcegenerators\4.5.1\build\Godot.SourceGenerators.props'
foreach ($path in @((Join-Path $sdkPath 'Sdk.props'), (Join-Path $sdkPath 'Sdk.targets'), $generatorProps)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "缺少已缓存的SDK：$path。请先在普通开发环境恢复依赖。" }
}
# 不读取受限制的用户NuGet.Config，不修改正式csproj；使用已恢复的assets与同一SDK/生成器。
$validationDir = Join-Path $ProjectDir '.godot\validation'
New-Item -ItemType Directory -Path $validationDir -Force | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $ProjectDir 'IndiumStar.csproj'))
$header = '<Project>' + [Environment]::NewLine +
    '<PropertyGroup><GodotProjectDir>' + [Security.SecurityElement]::Escape($ProjectDir) +
    '\</GodotProjectDir><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>' +
    [Environment]::NewLine + '<Import Project="' + [Security.SecurityElement]::Escape((Join-Path $sdkPath 'Sdk.props')) + '" />'
$source = $source.Replace('<Project Sdk="Godot.NET.Sdk/4.5.1">', $header).
    Replace('$(MSBuildProjectDirectory)', [Security.SecurityElement]::Escape($ProjectDir)).
    Replace('$(ProjectDir)', [Security.SecurityElement]::Escape($ProjectDir + '\'))
$tail = '<ItemGroup><Compile Include="' + [Security.SecurityElement]::Escape($ProjectDir) +
    '\src\**\*.cs" /></ItemGroup>' + [Environment]::NewLine +
    '<Import Project="' + [Security.SecurityElement]::Escape((Join-Path $sdkPath 'Sdk.targets')) + '" />' +
    '<Import Project="' + [Security.SecurityElement]::Escape($generatorProps) + '" />' + [Environment]::NewLine + '</Project>'
$source = $source.Replace('</Project>', $tail)
$validationProject = Join-Path $validationDir 'IndiumStar.Validation.csproj'
[IO.File]::WriteAllText($validationProject, $source, [Text.UTF8Encoding]::new($false))
$dependencyOutput = Join-Path $PSScriptRoot 'output\ritsu'
New-Item -ItemType Directory -Path $dependencyOutput -Force | Out-Null
dotnet build $validationProject --no-restore -c Release -p:ExportPck=false -p:DeployMod=false ("-p:RitsuLibDeployDir=" + $dependencyOutput) |
    Tee-Object (Join-Path $validationDir 'build-log.txt')
if ($LASTEXITCODE) { throw "缓存SDK构建失败：$LASTEXITCODE" }
