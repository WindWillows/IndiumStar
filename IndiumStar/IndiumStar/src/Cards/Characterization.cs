using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 表征：获得 7 点格挡。
/// <para>晶抗（结算后能量不为 0）：<strong>下回合</strong>获得 3 点临时力量。</para>
/// <para>晶逆（结算后能量为 0）：下回合抽 2 张牌。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：格挡 7 → 9，临时力量 3 → 4，抽牌 2 → 3。
/// </para>
/// <para>
/// 「下回合抽牌」直接用原版的 <see cref="DrawCardsNextTurnPower" />（Counter 叠加）。
/// </para>
/// <para>
/// 「下回合获得临时力量」原版没有现成能力，用 <see cref="CharacterizationStrengthPower" />：
/// 它是 <c>TemporaryStrengthPower</c> 的子类，在基类之上加了一层「武装」延迟，
/// 让你的<strong>下个回合</strong>才拿到力量，并在那个回合结束时消失。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Characterization : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new PowerVar<StrengthPower>(3m),
        // 下回合抽几张。
        new CardsVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public Characterization()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        return null;
    }

    /// <summary>晶抗：下回合获得 3 点临时力量。</summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<CharacterizationStrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Strength.BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>晶逆：下回合抽 2 张牌。</summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<DrawCardsNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Cards.IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Strength.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
