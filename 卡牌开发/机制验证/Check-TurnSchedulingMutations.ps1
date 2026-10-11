param([Parameter(Mandatory=$true)][string]$GameDir,
    [string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'))
$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$repoRoot = (Resolve-Path (Join-Path $ProjectDir '..\..')).Path
$testsDir = Join-Path $PSScriptRoot 'output\tests'
$response = Join-Path $testsDir 'tests.rsp'
if (!(Test-Path -LiteralPath $response)) { throw '先运行当前Run-Tests.ps1。' }
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$utf8 = [Text.UTF8Encoding]::new($false)
$cases = @(
    @{ name='baseline-draw-scheduler'; source='Powers/ReminiscenceDrawPower.cs'; expect='FAIL 钒永与预约自动打出及抽牌选择暂停组合' },
    @{ name='baseline-gas-turn-filter'; source='Cards/GasPhaseSurge.cs'; expect='FAIL 气相涌流他人同阵营开始不重置成长次数' }
)
$rows = @()
foreach ($case in $cases) {
    $sourcePath = Join-Path (Join-Path $ProjectDir 'src') $case.source
    $snapshot = & git -C $repoRoot show ('d3b34cf:IndiumStar/IndiumStar/src/' + $case.source)
    if ($LASTEXITCODE) { throw '无法读取既有提交的真实缺陷源码' }
    $source = ($snapshot -join "`n") + "`n"
    $mutationDir = Join-Path $testsDir ('mutations\' + $case.name)
    New-Item -ItemType Directory -Path $mutationDir -Force | Out-Null
    $mutantPath = Join-Path $mutationDir ([IO.Path]::GetFileName($sourcePath))
    [IO.File]::WriteAllText($mutantPath, $source, $utf8)
    $dll = Join-Path $testsDir ('Mutation-' + $case.name + '.dll')
    $lines = [IO.File]::ReadAllLines($response) | ForEach-Object {
        if ($_.StartsWith('/out:')) { '/out:"' + $dll + '"' }
        else { $_.Replace('"' + $sourcePath + '"', '"' + $mutantPath + '"') }
    }
    if (!($lines -join "`n").Contains($mutantPath) -or ($lines -join "`n").Contains('"' + $sourcePath + '"')) {
        throw '原实现源码未正确替换到隔离测试装配'
    }
    $rsp = Join-Path $mutationDir 'mutation.rsp'
    [IO.File]::WriteAllLines($rsp, $lines, $utf8)
    $compiled = & $dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $rsp)
    if ($LASTEXITCODE) { $compiled | Write-Output; throw '原实现编译失败，不能作为语义回归的检出证据' }
    Copy-Item -LiteralPath (Join-Path $testsDir 'MechanismTests.runtimeconfig.json') -Destination ($dll.Replace('.dll', '.runtimeconfig.json')) -Force
    $result = & $dotnet $dll $GameDir $ProjectDir
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $mutationDir 'result.txt'), [string[]]$result, $utf8)
    $failures = @($result | Where-Object { $_.StartsWith('FAIL ') })
    if ($code -ne 1 -or !($failures | Where-Object { $_.StartsWith($case.expect) })) {
        throw "未检出指定原始缺陷：$($case.name)"
    }
    $rows += [pscustomobject]@{ variant=$case.name; source=$case.source; baseline='d3b34cf';
        source_sha256=(Get-FileHash -LiteralPath $mutantPath -Algorithm SHA256).Hash;
        result='detected'; exit_code=$code; failures=$failures }
    Write-Output "PASS original defect detection: $($case.name) ($($failures.Count) failed regression cases)"
}
[IO.File]::WriteAllText((Join-Path $testsDir 'turn-scheduling-mutations.json'),
    ([pscustomobject]@{ scope='Compile exact original committed source in ignored output only; production and normal test assembly untouched';
        generated_at_utc=[DateTime]::UtcNow; variants=$rows } | ConvertTo-Json -Depth 8), $utf8)
