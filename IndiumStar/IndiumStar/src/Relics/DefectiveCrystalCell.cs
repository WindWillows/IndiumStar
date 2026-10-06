using IndiumStar.CardPools;
using IndiumStar.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Relics;

/// <summary>缺陷晶胞：每场战斗第一次实际获得延拓时，获得1能量。</summary>
[RegisterRelic(typeof(IndiumStarRelicPool))]
[RegisterCharacterStarterRelic(typeof(IndiumStarCharacter))]
public sealed class DefectiveCrystalCell : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];
    protected override bool IncludeEnergyHoverTip => true;
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("lantern");

    // 随原生遗物属性保存；战斗中读档不会再次取得首次奖励。
    [SavedProperty]
    public bool TriggeredThisCombat { get; set; }

    public override Task BeforeCombatStart()
    {
        TriggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (TriggeredThisCombat || power is not EnergyNextTurnPower || amount <= 0 || power.Amount <= 0
            || power.Owner != Owner.Creature || Owner.Creature.CombatState == null
            || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        // 先消费资格，防止GainEnergy及其钩子嵌套再次获得延拓。
        TriggeredThisCombat = true;
        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}
