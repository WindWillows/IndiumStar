param([string]$ProjectDir = (Join-Path $PSScriptRoot '..\..\IndiumStar\IndiumStar'),
    [string]$GameDir = $env:STS2_DIR, [string]$Focus = '')
$ErrorActionPreference = 'Stop'
if (!$GameDir) { throw '请用 -GameDir 或 STS2_DIR 指定游戏根目录。' }
$ProjectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$dotnetRoot = 'C:\Program Files\dotnet'
$sdkDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'sdk') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refDir = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory |
    Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (!$sdkDir -or !$refDir) { throw '需要 .NET 9 SDK 和引用程序集。' }
$outputDir = Join-Path $PSScriptRoot 'output\tests'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$usingsPath = Join-Path $outputDir 'TestUsings.cs'
[System.IO.File]::WriteAllText($usingsPath, "global using System;`nglobal using System.Collections.Generic;`nglobal using System.Linq;`nglobal using System.Threading.Tasks;`n", [System.Text.UTF8Encoding]::new($false))
$sourcePaths = @('Cards\Analyze.cs', 'Cards\BondingEster.cs', 'Cards\BondingImidogen.cs', 'Cards\BondingNitro.cs', 'Cards\Characterization.cs', 'Cards\Collapse.cs', 'Cards\ConformationBlow.cs', 'Cards\CrystalClusterGun.cs', 'Cards\DefendIndiumStar.cs', 'Cards\ExploitAdvantage.cs', 'Cards\ExtensionSpread.cs', 'Cards\GroupMigration.cs', 'Cards\OrigionStrike.cs', 'Cards\Primed.cs', 'Cards\PrimordialPierce.cs', 'Cards\QuantumDot.cs', 'Cards\ReminiscenceHardCalcite.cs', 'Cards\ReminiscencePhosphorus.cs', 'Cards\ReminiscencePollutionO.cs', 'Cards\RustEdge.cs', 'Cards\Segregation.cs', 'Cards\Slash.cs', 'Cards\SpinKick.cs', 'Cards\StrikeIndiumStar.cs', 'Cards\ThermobaricField.cs', 'Cards\TilingForm.cs', 'Cards\Transmutation.cs', 'Powers\BondingImidogenDexterityPower.cs', 'Powers\ExtensionSpreadPower.cs', 'Powers\ReminiscenceHardCalcitePower.cs', 'Powers\ReminiscencePhosphorusPower.cs', 'Powers\ReminiscencePollutionOPower.cs', 'Powers\ThermobaricFieldStrengthPower.cs', 'Powers\TransmutationPower.cs', 'Cards\ApplicationAs.cs', 'Cards\ApplicationCo.cs', 'Cards\ApplicationCu.cs', 'Cards\ApplicationF.cs', 'Cards\ApplicationH.cs', 'Cards\ApplicationHs.cs', 'Cards\ApplicationIn.cs', 'Cards\ApplicationMg.cs', 'Cards\ApplicationNb.cs', 'Cards\ApplicationPb.cs', 'Cards\ApplicationU.cs', 'Cards\ApplicationZn.cs', 'Cards\CrystallizationMg.cs', 'Powers\ApplicationCuPower.cs', 'Powers\ApplicationHsStrengthDownPower.cs', 'Cards\ApplicationCard.cs', 'Cards\ReminiscenceVanadiumEternal.cs', 'Powers\ReminiscenceDrawPower.cs', 'Cards\GasPhaseSurge.cs', 'Cards\CrystalFlow.cs', 'Cards\ApplicationTh.cs', 'Mechanics\CrystalReplayRules.cs', 'Patches\CrystalReplaySeriesPatch.cs', 'Cards\ApplicationRules.cs', 'Cards\CrystalCoin.cs', 'Cards\CrystallizationCard.cs',
    'Cards\Crystallization.cs', 'Cards\CrystallizationRules.cs', 'Cards\ICardFamilies.cs',
    'Mechanics\CrystalBranch.cs', 'Mechanics\CrystalBranchRules.cs', 'Mechanics\ExtensionRules.cs',
    'Mechanics\IndiumStarKeywords.cs',
    'Powers\CharacterizationStrengthPower.cs', 'Powers\QuantumDotPower.cs',
    'Cards\ReminiscencePollutionH.cs', 'Powers\ReminiscencePollutionHPower.cs',
    'Powers\ApplicationNbPower.cs', 'Patches\VigorRetentionPatch.cs',
    'Cards\ShatterSong.cs', 'Cards\CrystallizationNb.cs', 'Cards\CrystallizationHs.cs', 'Cards\CrystallizationPb.cs',
    'Powers\OnlyAttacksThisTurnPower.cs', 'Powers\TilingFormPower.cs', 'Powers\CrystalHandAuraPower.cs',
    'Cards\MacroDendrite.cs', 'Cards\Cyclization.cs', 'Cards\ShadowChainBreath.cs', 'Cards\Reiteration.cs',
    'Cards\NeighboringGroupParticipation.cs', 'Cards\Inversion.cs', 'Cards\BondingTripleBond.cs', 'Cards\DeferredRelease.cs', 'Powers\DeferredReleasePower.cs', 'Powers\ReiterationPower.cs',
    'Powers\NeighboringGroupStrengthPower.cs',
    'Patches\CrystallizeMarkClonePatch.cs', 'Powers\ApplicationCoPower.cs', 'Powers\ApplicationUCrystallizePower.cs',
    'Powers\FreeApplicationPower.cs', 'Powers\ApplicationCaBlockPower.cs', 'Cards\ApplicationCa.cs',
    'Cards\ReminiscenceTinDoubt.cs', 'Powers\ReminiscenceTinDoubtPower.cs', 'Powers\TinDoubtTemporaryPowers.cs',
    'Cards\ShatterCrystalRain.cs', 'Cards\CrystallizationU.cs', 'Cards\ApplicationNeutron.cs', 'Cards\DerivedApplicationNeutron.cs', 'Cards\CrystalPunishment.cs',
    'Powers\LoseWeakAtTurnEndPower.cs', 'Powers\LoseVulnerableAtTurnStartPower.cs') |
    ForEach-Object { Join-Path (Join-Path $ProjectDir 'src') $_ }
$sourcePaths += @($usingsPath, (Join-Path $PSScriptRoot 'GameStubs.cs'), (Join-Path $PSScriptRoot 'NewCardStubs.cs'),
    (Join-Path $PSScriptRoot 'NewCardTests.cs'), (Join-Path $PSScriptRoot 'SemanticAuditTests.cs'),
    (Join-Path $PSScriptRoot 'MechanismTests.cs'), (Join-Path $PSScriptRoot 'UpdatedSpecificationTests.cs'), (Join-Path $PSScriptRoot 'DeferredReleaseTests.cs'), (Join-Path $PSScriptRoot 'CrystalReplayTests.cs'), (Join-Path $PSScriptRoot 'TurnSchedulingTests.cs'), (Join-Path $PSScriptRoot 'ApplicationEffectTests.cs'), (Join-Path $PSScriptRoot 'RemainingCardStubs.cs'), (Join-Path $PSScriptRoot 'RemainingCardEffectTests.cs'))
$sourcePaths += (Join-Path $PSScriptRoot 'RuleDecisionTests.cs')
$sourcePaths += (Join-Path $PSScriptRoot 'DerivedNeutronTests.cs')
$sourcePaths += (Join-Path $PSScriptRoot 'CrystalPunishmentTests.cs')
$sourcePaths += (Join-Path $PSScriptRoot 'CrystalPunishmentStubs.cs')
$sourcePaths += @('Cards\Tunneling.cs', 'Cards\CrystalMarrow.cs', 'Powers\TunnelingPower.cs', 'Powers\CrystalMarrowPower.cs') |
    ForEach-Object { Join-Path (Join-Path $ProjectDir 'src') $_ }
$sourcePaths += (Join-Path $PSScriptRoot 'RemainingXCardTests.cs')
$arguments = @('/nostdlib+', '/target:exe', '/optimize+', '/langversion:13.0', '/nullable:enable', '/utf8output',
    ('/out:"' + (Join-Path $outputDir 'MechanismTests.dll') + '"'))
$arguments += Get-ChildItem -LiteralPath (Join-Path $refDir.FullName 'ref\net9.0') -Filter '*.dll' |
    ForEach-Object { '/reference:"' + $_.FullName + '"' }
$harmonyPath = Join-Path $GameDir 'data_sts2_windows_x86_64\0Harmony.dll'
if (!(Test-Path -LiteralPath $harmonyPath)) { throw "缺少本体 Harmony 引用：$harmonyPath" }
$arguments += '/reference:"' + $harmonyPath + '"'
Copy-Item -LiteralPath $harmonyPath -Destination $outputDir -Force
$arguments += $sourcePaths | ForEach-Object { '"' + $_ + '"' }
$responseFile = Join-Path $outputDir 'tests.rsp'
[System.IO.File]::WriteAllLines($responseFile, $arguments, [System.Text.UTF8Encoding]::new($false))
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
& $dotnetExe (Join-Path $sdkDir.FullName 'Roslyn\bincore\csc.dll') '/noconfig' ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw "测试源码编译失败：$LASTEXITCODE" }
[System.IO.File]::WriteAllText((Join-Path $outputDir 'MechanismTests.runtimeconfig.json'),
    '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}', [System.Text.UTF8Encoding]::new($false))
& $dotnetExe (Join-Path $outputDir 'MechanismTests.dll') $GameDir $ProjectDir $Focus | Tee-Object -FilePath (Join-Path $outputDir 'test-results.txt')
if ($LASTEXITCODE -ne 0) { throw "机制测试失败：$LASTEXITCODE" }

# 迁移完整性检查：已有牌的声明不能漏掉实际分支，也不能漏掉运用牌族标记。
$branchCards = 0
$applicationCards = 0
Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'src\Cards') -Filter '*.cs' | ForEach-Object {
    $source = [System.IO.File]::ReadAllText($_.FullName)
    if ($source -match 'public sealed class (Application\w+|DerivedApplicationNeutron) : (ApplicationCard|ModCardTemplate)') {
        if ($source -notmatch 'public sealed class (Application\w+|DerivedApplicationNeutron) : [^\r\n]+, IApplicationCard') { throw "运用牌缺少标记：$($_.Name)" }
        $applicationCards++
    }
    $hasResistance = $source -match 'protected override (async )?Task CrystalResistance\('
    $hasReversal = $source -match 'protected override (async )?Task CrystalReversal\('
    if ($hasResistance -or $hasReversal) {
        $expected = if ($hasResistance -and $hasReversal) { 'Both' } elseif ($hasResistance) { 'Resistance' } else { 'Reversal' }
        if ($source -notmatch ('AvailableCrystalBranches => CrystalBranch\.' + $expected + ';')) { throw "分支声明不完整：$($_.Name)" }
        $branchCards++
    }
}
Write-Output "Metadata audit passed: $applicationCards application cards, $branchCards cards with branches."
