param([Parameter(Mandatory=$true)][string]$GameDir,
    [string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'))
$ErrorActionPreference = 'Stop'
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$testsDir = Join-Path $PSScriptRoot 'output\tests'
$response = Join-Path $testsDir 'tests.rsp'
if (!(Test-Path -LiteralPath $response)) { throw '先运行Run-Tests.ps1生成当前源码测试装配。' }
$sdk = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -like '9.*' |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$utf8 = [Text.UTF8Encoding]::new($false)
$rows = @()
foreach ($mutation in @('early-energy', 'th-order', 'lost-series-extension')) {
    $name = if ($mutation -eq 'early-energy') { 'ApplicationCard.cs' } elseif ($mutation -eq 'th-order') { 'ApplicationTh.cs' } else { 'CrystalReplaySeriesPatch.cs' }
    $sourceFolder = if ($mutation -eq 'lost-series-extension') { 'src\Patches\' } else { 'src\Cards\' }
    $original = Join-Path $ProjectDir ($sourceFolder + $name)
    $source = [IO.File]::ReadAllText($original).Replace("`r`n", "`n")
    if ($mutation -eq 'early-energy') {
        $main = 'CardModel? playResult = await PlayEffect(choiceContext, cardPlay);'
        $energy = 'int remainingEnergy = Owner.PlayerCombatState?.Energy ?? 0;'
        if (!$source.Contains($main) -or !$source.Contains($energy)) { throw '基础效果时序突变锚点不匹配' }
        $source = $source.Replace($main, "int incorrectlyEarlyEnergy = Owner.PlayerCombatState?.Energy ?? 0;`n        " + $main)
        $source = $source.Replace($energy, 'int remainingEnergy = incorrectlyEarlyEnergy;')
        $expectedFailure = 'FAIL 晶流等待格挡异步钩子后选择'
    } elseif ($mutation -eq 'lost-series-extension') {
        $anchor = 'code.InsertRange(anchor, new[] {'
        if (!$source.Contains($anchor)) { throw '系列补丁突变锚点不匹配' }
        $source = $source.Replace($anchor, 'if (false) code.InsertRange(anchor, new[] {')
        $expectedFailure = 'FAIL 晶流按基础效果后能量 0'
    } else {
        $old = @'
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        // 使一名敌人失去生命（无视格挡、不吃力量）。
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, this, cardPlay);
'@
        $old = $old.Replace("`r`n", "`n")
        if (!$source.Contains($old)) { throw 'Th顺序突变锚点不匹配' }
        $wrong = @'
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, this, cardPlay);
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);
'@
        $source = $source.Replace($old, $wrong.Replace("`r`n", "`n"))
        $expectedFailure = 'FAIL 运用Th先生成晶化后造成生命损失'
    }
    $mutationDir = Join-Path $testsDir ('mutations\' + $mutation)
    New-Item -ItemType Directory -Path $mutationDir -Force | Out-Null
    $mutantPath = Join-Path $mutationDir $name
    [IO.File]::WriteAllText($mutantPath, $source, $utf8)
    $dll = Join-Path $testsDir ('Mutation-' + $mutation + '.dll')
    $lines = [IO.File]::ReadAllLines($response) | ForEach-Object {
        if ($_.StartsWith('/out:')) { '/out:"' + $dll + '"' }
        else { $_.Replace('"' + $original + '"', '"' + $mutantPath + '"') }
    }
    if (($lines -join "`n").Contains('"' + $original + '"') -or !($lines -join "`n").Contains($mutantPath)) {
        throw '突变源码未正确替换到测试装配'
    }
    $rsp = Join-Path $mutationDir 'mutation.rsp'
    [IO.File]::WriteAllLines($rsp, $lines, $utf8)
    $compile = & $dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig ('@' + $rsp)
    if ($LASTEXITCODE) { $compile | Write-Output; throw '突变版本编译失败，不能冒充已检出语义错误' }
    Copy-Item -LiteralPath (Join-Path $testsDir 'MechanismTests.runtimeconfig.json') -Destination ($dll.Replace('.dll', '.runtimeconfig.json')) -Force
    $result = & $dotnet $dll $GameDir $ProjectDir
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $mutationDir 'result.txt'), [string[]]$result, $utf8)
    $failures = @($result | Where-Object { $_.StartsWith('FAIL ') })
    if ($code -ne 1 -or !$failures.Count -or !($failures | Where-Object { $_.StartsWith($expectedFailure) })) {
        throw "测试未检出指定语义突变：$mutation，退出码$code"
    }
    $rows += [pscustomobject]@{ mutation = $mutation; result = 'detected'; exit_code = $code; failures = $failures }
    Write-Output "PASS mutation detection: $mutation ($($failures.Count) failed regression cases)"
}
[IO.File]::WriteAllText((Join-Path $testsDir 'replay-mutations.json'),
    ([pscustomobject]@{ scope = 'Mutant sources only in ignored output; production sources and normal test assembly untouched';
        generated_at_utc = [DateTime]::UtcNow; mutations = $rows } | ConvertTo-Json -Depth 8), $utf8)
