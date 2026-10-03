using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 回身踢：造成 2 点伤害 2 次，将弃牌堆中的 1 张牌加入手牌，消耗。
/// <para>晶逆（结算后能量为 0）：使加入手牌的那张牌本回合具有保留。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级：攻击段数 2 次 → 3 次，且不再消耗（设计文档：（不）消耗）。
/// 用 <c>RemoveKeyword</c> 移除——它改的是实例的持久关键字状态，会随克隆保留。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class SpinKick : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Reversal;

    /// <summary>消耗：打出后进入消耗堆。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        // 攻击段数。
        new RepeatVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public SpinKick()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 多段攻击：同原版「回旋镖」的 WithHitCount 写法。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 从弃牌堆选 1 张加入手牌（同原版「全息影像」的写法）。
        CardModel? picked = (await CardSelectCmd.FromCombatPile(
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
            context: choiceContext,
            pile: PileType.Discard.GetPile(Owner),
            player: Owner)).FirstOrDefault();

        if (picked == null)
        {
            return null;
        }

        await CardPileCmd.Add(picked, PileType.Hand);

        // 把选中的牌交给晶逆用（晶逆的判定在基类里、发生在主效果之后）。
        return picked;
    }

    /// <summary>晶逆：使加入手牌的那张牌本回合具有保留。</summary>
    protected override Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        if (playResult != null)
        {
            CardCmd.ApplySingleTurnRetain(playResult);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
