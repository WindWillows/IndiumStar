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
    @{ name='baseline-phosphorus-configuration'; source='Powers/ReminiscencePhosphorusPower.cs'; additional=@('Cards/ReminiscencePhosphorus.cs'); expect='FAIL 回忆-磷语施加链内首次延拓读取当前卡牌配置 repeat=False upgraded=True' },
    @{ name='baseline-calcite-configuration'; source='Powers/ReminiscenceHardCalcitePower.cs'; additional=@('Cards/ReminiscenceHardCalcite.cs'); expect='FAIL 回忆-硬钙石施加链内回合末读取当前伤害配置 repeat=False upgraded=True' },
    @{ name='baseline-characterization-turn'; source='Powers/CharacterizationStrengthPower.cs'; expect='FAIL 表征在本人开始阶段自动打出不能提前兑现当前回合 upgraded=False' }
)
$rows = @()
foreach ($case in $cases) {
    $mutationDir = Join-Path $testsDir ('mutations\' + $case.name)
    New-Item -ItemType Directory -Path $mutationDir -Force | Out-Null
    $dll = Join-Path $testsDir ('Mutation-' + $case.name + '.dll')
    $lines = [IO.File]::ReadAllLines($response) | ForEach-Object {
        if ($_.StartsWith('/out:')) { '/out:"' + $dll + '"' }
        else { $_ }
    }
    $snapshots = @()
    foreach ($relativeSource in (@($case.source) + @($case.additional) | Where-Object { $_ })) {
        $sourcePath = Join-Path (Join-Path $ProjectDir 'src') $relativeSource
        $snapshot = & git -C $repoRoot show ('5ae63f0:IndiumStar/IndiumStar/src/' + $relativeSource)
        if ($LASTEXITCODE) { throw '无法读取既有提交的真实缺陷源码' }
        $mutantPath = Join-Path $mutationDir ([IO.Path]::GetFileName($sourcePath))
        [IO.File]::WriteAllText($mutantPath, ($snapshot -join "`n") + "`n", $utf8)
        $lines = $lines | ForEach-Object { $_.Replace('"' + $sourcePath + '"', '"' + $mutantPath + '"') }
        if (!($lines -join "`n").Contains($mutantPath) -or ($lines -join "`n").Contains('"' + $sourcePath + '"')) {
            throw '原实现源码未正确替换到隔离测试装配'
        }
        $snapshots += [pscustomobject]@{ source=$relativeSource; sha256=(Get-FileHash -LiteralPath $mutantPath -Algorithm SHA256).Hash }
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
    $rows += [pscustomobject]@{ variant=$case.name; source=$case.source; baseline='5ae63f0';
        source_snapshots=$snapshots;
        result='detected'; exit_code=$code; failures=$failures }
    Write-Output "PASS original defect detection: $($case.name) ($($failures.Count) failed regression cases)"
}
[IO.File]::WriteAllText((Join-Path $testsDir 'remaining-card-mutations.json'),
    ([pscustomobject]@{ scope='Compile exact original committed sources in ignored output only; phosphorus/calcite cards and powers restored together; production and normal test assembly untouched';
        generated_at_utc=[DateTime]::UtcNow; variants=$rows } | ConvertTo-Json -Depth 8), $utf8)
