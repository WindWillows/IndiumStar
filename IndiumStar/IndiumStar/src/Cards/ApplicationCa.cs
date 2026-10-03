using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Ca：获得 3 点格挡（升级 5）。
/// <para>晶抗：失去 1 点能量；下一次从卡牌中获得的格挡翻倍。</para>
/// </summary>
/// <remarks>
/// <para>
/// 这张牌只有晶抗，没有晶逆——按规格它没写晶逆。
/// </para>
/// <para>
/// 「下一次从卡牌中获得的格挡翻倍」是挂在自己身上的一个隐藏能力
/// （<see cref="ApplicationCaBlockPower" />），它会在第一次真正生效后自己移除。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationCa : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Resistance;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationCa()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        return null;
    }

    /// <summary>晶抗：失去 1 点能量，并让下一次从卡牌中获得的格挡翻倍。</summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PlayerCmd.LoseEnergy(1m, Owner);

        await PowerCmd.Apply<ApplicationCaBlockPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
