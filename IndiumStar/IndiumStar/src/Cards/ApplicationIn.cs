using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Characters;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-In：向手牌加入 1 张「晶化」，并获得延拓 2（下回合开始时额外获得 2 点能量）。
/// </summary>
/// <remarks>
/// <para>
/// 它属于「运用」系列，但<strong>没有晶抗/晶逆</strong>——所以不重写
/// <see cref="ApplicationCard.CrystalResistance" /> 与 <see cref="ApplicationCard.CrystalReversal" />，
/// 基类默认什么都不做。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
[RegisterCharacterStarterCard(typeof(IndiumStarCharacter), 1, Order = 2)]
public sealed class ApplicationIn : ApplicationCard, IApplicationCard, IExtensionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 延拓：下回合开始时获得的能量。
        new EnergyVar(2),
        // 晶化：向手牌加入的「晶化」张数。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationIn()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 晶化1：向手牌加入 1 张「晶化」状态卡。
        // CombatState 的 `!`：出牌时卡牌一定在战斗牌堆里，它必然非空。
        // 原版所有群攻牌（AstralPulse、Shatter 等 29 处）也是这么直接传 base.CombatState 的，
        // 这里的警告只来自游戏源码带的可空注解。
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        // 延拓2：用原版的「下回合能量」能力，它是 Counter 叠加，所以多次延拓会累加。
        await ExtensionRules.Gain(choiceContext, Owner, DynamicVars.Energy.IntValue, this);

        return null;
    }

    /// <summary>
    /// 升级后获得「保留」：回合结束时不被弃掉，可以留着下回合再打。
    /// 这是原版给卡牌追加关键字的做法（同 GoldAxe、Dredge、Scrawl），
    /// 关键字文字会由游戏自动渲染到卡面上，不需要手写。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
