using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>键合-三键：基础格挡结算后选择分支，分别按手牌晶化数或当前延拓层数获得格挡。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class BondingTripleBond : ApplicationCard
{
    public BondingTripleBond() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(12m, ValueProp.Move), new DynamicVar("CrystalBlock", 4m),
         new DynamicVar("ExtensionBlock", 6m)];

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        return null;
    }

    protected override async Task CrystalResistance(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        // 沿用现有晶化数量的手牌口径；文档未指明牌堆范围，另记规则问题单。
        int count = CrystallizationRules.CountCrystallizationInHand(this);
        if (count > 0)
            await CreatureCmd.GainBlock(Owner.Creature, count * DynamicVars["CrystalBlock"].BaseValue,
                ValueProp.Move, play);
    }

    protected override async Task CrystalReversal(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        int amount = ExtensionRules.GetAmount(Owner);
        if (amount > 0)
            await CreatureCmd.GainBlock(Owner.Creature, amount * DynamicVars["ExtensionBlock"].BaseValue,
                ValueProp.Move, play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["CrystalBlock"].UpgradeValueBy(2m);
        DynamicVars["ExtensionBlock"].UpgradeValueBy(2m);
    }
}
