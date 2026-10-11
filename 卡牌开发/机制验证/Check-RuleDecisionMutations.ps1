param([Parameter(Mandatory=$true)][string]$GameDir,
    [string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'))
$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$testsDir = Join-Path $PSScriptRoot 'output\tests'
$response = Join-Path $testsDir 'tests.rsp'
if (!(Test-Path -LiteralPath $response)) { throw '先运行当前Run-Tests.ps1。' }
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$utf8 = [Text.UTF8Encoding]::new($false)
$cases = @(
    @{ name='coin-copy-resets-decay'; source='Patches/CrystallizeMarkClonePatch.cs';
       from='CrystalCoin.CopyDecay(source, copy);'; to=''; expect='FAIL 晶币复制继承已衰减收益' },
    @{ name='analyze-counts-instances'; source='Cards/Analyze.cs';
       from='.DistinctBy(power => power.GetType())'; to=''; expect='FAIL 分析同类型多个实例只计一种' },
    @{ name='pierce-counts-instances'; source='Cards/PrimordialPierce.cs';
       from='.DistinctBy(power => power.GetType())'; to=''; expect='FAIL 原波穿刺同类型负面状态只计一种' },
    @{ name='phosphorus-loses-outer-async-mode'; source='Powers/ReminiscencePhosphorusPower.cs';
       from='_grantBothBySlot.Add(ApplyingMode.Value ?? _pendingGrantBoth);'; to='_grantBothBySlot.Add(_pendingGrantBoth);';
       expect='FAIL 磷语Before施加钩子嵌套也保留内外模式' },
    @{ name='phosphorus-shares-clone-list'; source='Powers/ReminiscencePhosphorusPower.cs';
       from='_grantBothBySlot = new(_grantBothBySlot);'; to=''; expect='FAIL 磷语能力克隆后的模式列表独立' },
    @{ name='inversion-counts-replay-segments'; source='Cards/Inversion.cs';
       from='&& entry.CardPlay.IsFirstInSeries'; to=''; expect='FAIL 反演只计本人完成系列' }
)
$rows = @()
foreach ($case in $cases) {
    $sourcePath = Join-Path (Join-Path $ProjectDir 'src') $case.source
    $originalHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $original = [IO.File]::ReadAllText($sourcePath)
    if (!$original.Contains($case.from)) { throw "缺少突变锚点：$($case.name)" }
    $mutationDir = Join-Path $testsDir ('mutations\' + $case.name)
    New-Item -ItemType Directory -Path $mutationDir -Force | Out-Null
    $mutantPath = Join-Path $mutationDir ([IO.Path]::GetFileName($sourcePath))
    [IO.File]::WriteAllText($mutantPath, $original.Replace($case.from, $case.to), $utf8)
    $dll = Join-Path $testsDir ('Mutation-' + $case.name + '.dll')
    $lines = [IO.File]::ReadAllLines($response) | ForEach-Object {
        if ($_.StartsWith('/out:')) { '/out:"' + $dll + '"' }
        else { $_.Replace('"' + $sourcePath + '"', '"' + $mutantPath + '"') }
    }
    if (!($lines -join "`n").Contains($mutantPath)) { throw '突变源码没有被链接' }
    $rsp = Join-Path $mutationDir 'mutation.rsp'
    [IO.File]::WriteAllLines($rsp, $lines, $utf8)
    $compiled = & $dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $rsp)
    if ($LASTEXITCODE) { $compiled | Write-Output; throw '突变编译失败，不能作为语义检出证据' }
    Copy-Item -LiteralPath (Join-Path $testsDir 'MechanismTests.runtimeconfig.json') -Destination ($dll.Replace('.dll', '.runtimeconfig.json')) -Force
    $result = & $dotnet $dll $GameDir $ProjectDir
    $code = $LASTEXITCODE
    $failures = @($result | Where-Object { $_.StartsWith('FAIL ') })
    [IO.File]::WriteAllLines((Join-Path $mutationDir 'result.txt'), [string[]]$result, $utf8)
    if ($code -ne 1 -or !($failures | Where-Object { $_.StartsWith($case.expect) })) {
        throw "没有检出指定语义错误：$($case.name)"
    }
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $originalHash) { throw '生产源码被改变' }
    $rows += [pscustomobject]@{ variant=$case.name; source=$case.source; result='detected'; exit_code=$code;
        production_source_sha256=$originalHash; mutant_sha256=(Get-FileHash -LiteralPath $mutantPath -Algorithm SHA256).Hash;
        failures=$failures }
    Write-Output "PASS rule mutation detection: $($case.name) ($($failures.Count) failed cases)"
}
[IO.File]::WriteAllText((Join-Path $testsDir 'rule-decision-mutations.json'),
    ([pscustomobject]@{ scope='Deliberate semantic variants compiled in ignored output only; production and normal regression assembly unchanged';
        generated_at_utc=[DateTime]::UtcNow; variants=$rows } | ConvertTo-Json -Depth 8), $utf8)
