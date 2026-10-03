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
/// 锈蚀之刃：造成 10 点伤害，将目标的虚弱层数翻倍。消耗。
/// <para>升级后：伤害 10 → 14，其余不变。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>「翻倍」怎么实现：</b>虚弱是 <c>Counter</c> 叠加的能力，所以再施加
/// <strong>等于当前层数</strong>的量就等价于翻倍（3 层 → 3+3 = 6 层）。
/// </para>
/// <para>
/// 目标没有虚弱时（层数 0）跳过，避免无意义地施加 0 层。
/// </para>
/// <para>
/// 顺序是<strong>先造成伤害、再翻倍</strong>——伤害本身不吃这次翻倍，
/// 翻倍是为了后续的攻击更疼。
/// </para>
/// <para>
/// 「消耗」写在 <see cref="CanonicalKeywords" /> 里（不是升级才加），
/// 所以升级前后都会消耗；关键字文字由游戏自动渲染到卡面。
/// </para>
/// <para>
/// 它没有晶抗/晶逆，所以是普通 <see cref="ModCardTemplate" />，不继承
/// <see cref="ApplicationCard" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class RustEdge : ModCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public RustEdge()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 造成 10 点伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 将目标的虚弱层数翻倍：再施加「当前层数」的量。
        int currentWeak = cardPlay.Target.GetPowerAmount<WeakPower>();
        if (currentWeak <= 0)
        {
            return;
        }

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            currentWeak,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
