using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystalMarrow : ApplicationCard, IExtensionCard
{
    protected override bool HasEnergyCostX => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ExtraDraw", 0m)];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public CrystalMarrow() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();
        int draw = checked(x + DynamicVars["ExtraDraw"].IntValue);
        int dueTurn = checked(Owner.PlayerCombatState!.TurnNumber + 1);
        // 升级同时增加延拓和下回合抽牌各1。
        await ExtensionRules.Gain(choiceContext, Owner, draw, this);
        if (draw > 0)
            await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, draw, Owner.Creature, this);
        CrystalMarrowPower? power = await PowerCmd.Apply<CrystalMarrowPower>(
            choiceContext, Owner.Creature, 1, Owner.Creature, this);
        if (power != null && Owner.Creature.GetPower<CrystalMarrowPower>() == power
            && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            power.Schedule(dueTurn);
        return null;
    }

    protected override void OnUpgrade() => DynamicVars["ExtraDraw"].UpgradeValueBy(1m);
}
