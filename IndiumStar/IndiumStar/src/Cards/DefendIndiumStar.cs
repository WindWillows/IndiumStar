using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Characters;

namespace IndiumStar.Cards;

/// <summary>
/// 防御：获得 5 点格挡；火堆锻造（升级）后改为 8 点。
/// </summary>
/// <remarks>
/// 卡图暂时直接指向原版铁甲战士的「防御」卡图，等你有自己的画再替换
/// <see cref="AssetProfile" /> 里的 <c>PortraitPath</c>。
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
[RegisterCharacterStarterCard(typeof(IndiumStarCharacter), 4, Order = 1)]
public sealed class DefendIndiumStar : ModCardTemplate
{
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public DefendIndiumStar()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
