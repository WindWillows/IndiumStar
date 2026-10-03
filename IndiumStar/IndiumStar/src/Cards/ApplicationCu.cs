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
/// 运用-Cu：造成 13 点伤害。本回合，这名敌人每受到 7 点攻击伤害，获得 2 层中毒。
/// <para>升级后：伤害不变，触发阈值 7 → 6。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「每受到 N 点攻击伤害就中毒」不是原版已有的机制，靠 <see cref="ApplicationCuPower" />
/// 实现：它挂在目标身上，用 <c>AfterDamageGiven</c> 累计<strong>实际掉血</strong>，
/// 每够一整份就施加一次中毒，余数保留。
/// </para>
/// <para>
/// 伤害先打、再挂能力：这样这次的 13 点伤害本身<strong>不会</strong>被自己触发的中毒计入
/// （能力是在伤害结算之后才挂上去的）。如果你想让它连自己这次的伤害也算进去，
/// 把 <c>PowerCmd.Apply</c> 挪到伤害前面即可。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationCu : ApplicationCard, IApplicationCard
{
    /// <summary>每次触发给几层中毒。</summary>
    private const int PoisonPerTrigger = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(13m, ValueProp.Move),
        // 触发阈值：每受到这么多点攻击伤害就触发一次。
        new DynamicVar("DamageThreshold", 7m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationCu()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 先造成伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 再挂上「每累计 N 点伤害就中毒」的能力。
        // 阈值要传进去：卡牌升级会改它，能力自己不知道。
        // 同时传入施加方，能力在它的回合结束时清除（而不是挂载对象的回合）。
        // 持续时长由能力自己清理（自定义能力不会自动减层）。
        ApplicationCuPower? power = await PowerCmd.Apply<ApplicationCuPower>(
            choiceContext,
            cardPlay.Target,
            1m,
            Owner.Creature,
            this);

        power?.Configure(Owner.Creature, DynamicVars["DamageThreshold"].IntValue, PoisonPerTrigger);

        return null;
    }

    protected override void OnUpgrade()
    {
        // 升级只降阈值，不加伤害。
        DynamicVars["DamageThreshold"].UpgradeValueBy(-1m);
    }
}
