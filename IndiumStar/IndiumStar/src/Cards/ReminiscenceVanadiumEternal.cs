using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 回忆-钒永：本回合每当你抽牌时，下回合开始时抽等量牌。抽 1 张牌（升级 2）。
/// </summary>
/// <remarks>
/// <para>
/// <b>「等量」在回合结束时才确定</b>，因为要统计整个回合抽了多少张。
/// 所以用自己写的 <see cref="ReminiscenceDrawPower" /> 而不是原版
/// <c>DrawCardsNextTurnPower</c>（后者靠 <c>AmountOnTurnStart</c> 判断，
/// 只能"本回合施加、下回合生效"，做不到回合结束才结算）。
/// </para>
/// <para>
/// 顺序很关键：<strong>先挂能力，再抽牌</strong>——否则这张牌自己抽的那几张
/// 不会被统计进去，而规格说的正是"本回合每当你抽牌时"。
/// </para>
/// <para>
/// 这张牌没有晶抗/晶逆——按规格它没写。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscenceVanadiumEternal : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 立即抽几张。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscenceVanadiumEternal()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先挂统计能力。
        // 初始层数必须是 1（不能是 0）：PowerCmd.Apply 在 amount == 0 时直接 return
        // （PowerCmd.cs:107），能力根本不会被创建。
        ReminiscenceDrawPower? power = await PowerCmd.Apply<ReminiscenceDrawPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        // 统计起点放在抽牌**之前**：它自己抽的那张也要计入。
        //   打出 1 张   → 下回合多抽 1（自己抽的那张）
        //   打出 2 张   → 下回合多抽 2
        //   钒永 + 抽2张 → 下回合多抽 3
        // 所以能力必须在 Draw 之前就开始数。
        power?.Configure(cardPlay);

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
