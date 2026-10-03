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

namespace IndiumStar.Cards;

/// <summary>
/// 键合-硝基：造成 6 点伤害，延拓 1，抽 1 张牌。
/// <para>升级后：获得<strong>保留</strong>。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「键合」系列（键合-亚胺基、键合-硝基）都是普通 <c>ModCardTemplate</c>，
/// <strong>不</strong>继承 <c>ApplicationCard</c>——它们没有晶抗/晶逆。
/// </para>
/// <para>
/// 延拓用原版的 <c>EnergyNextTurnPower</c>（同「运用-In」「键合-亚胺基」）：
/// 它是 <c>Counter</c> 叠加，所以多次延拓会累加，并且在回合开始的 <c>AfterEnergyReset</c>
/// 阶段发放能量后把自己移除，只影响下一个回合。
/// </para>
/// <para>
/// 升级只加「保留」，伤害、延拓、抽牌数都不变（同「运用-In」的升级方式）。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class BondingNitro : ModCardTemplate, IExtensionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        // 延拓：下回合开始时获得的能量。
        new EnergyVar(1),
        // 抽牌数。
        new CardsVar(1),
    ];

    /// <summary>延拓牌带「延拓」卡牌关键字（悬浮提示由 RitsuLib 补丁提供）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [IndiumStarKeywords.ExtensionKeyword];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public BondingNitro()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 造成 6 点伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 延拓 1：下回合开始时额外获得 1 点能量。
        await ExtensionRules.Gain(choiceContext, Owner, DynamicVars.Energy.IntValue, this);

        // 抽 1 张牌。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    /// <summary>
    /// 升级：获得「保留」（回合结束时不被弃掉）。
    /// 这是原版给卡牌追加关键字的做法（同「运用-In」），关键字文字由游戏自动渲染到卡面上。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
