using System.Threading.Tasks;
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
/// 回忆-锡疑：本回合，你每打出一张牌，就交替获得 1 点临时力量/临时敏捷。
/// 每当你打出延拓牌时，此牌花费 -2。
/// <para>升级后：花费降低量 2 → 3。</para>
/// </summary>
/// <remarks>
/// <para>
/// 交替收益由 <see cref="ReminiscenceTinDoubtPower" /> 承担（本回合有效）。
/// </para>
/// <para>
/// 「此牌花费 -N」由卡牌自己监听 <c>AfterCardPlayed</c> 实现：牌堆里的每张牌
/// 都是钩子监听者，所以在手牌中也能收到出牌通知。判定条件是：在手牌中、
/// 自己玩家打出的是延拓牌（<see cref="IExtensionCard" /> 稳定标记）、
/// 系列第一段（重放段不重复降费）。降费是本场战斗永久的（<c>AddThisCombat</c>）。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscenceTinDoubt : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每打出一张延拓牌，此牌花费降低的数量。
        new DynamicVar("CostReduction", 2m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscenceTinDoubt()
        : base(9, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ReminiscenceTinDoubtPower? power = await PowerCmd.Apply<ReminiscenceTinDoubtPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        // 锡疑自己这次出牌不计入交替计数。
        power?.Configure(cardPlay);
    }

    /// <summary>在手牌中时，自己玩家每打出一张延拓牌，此牌本场战斗花费 -N。</summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Pile?.IsCombatPile != true
            || cardPlay.Card.Owner != Owner
            || !cardPlay.IsFirstInSeries
            || !IndiumStar.Mechanics.ExtensionRules.IsExtensionPlay(cardPlay))
        {
            return Task.CompletedTask;
        }

        EnergyCost.AddThisCombat(-DynamicVars["CostReduction"].IntValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CostReduction"].UpgradeValueBy(1m);
    }
}
