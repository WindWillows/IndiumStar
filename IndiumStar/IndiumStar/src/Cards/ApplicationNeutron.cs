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
using IndiumStar.Characters;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-电子：造成11点伤害（升级15）。内部ApplicationNeutron模型ID保留用于已有数据兼容。
/// <para>晶抗（结算后能量不为 0）：<strong>自己</strong>获得 1 层虚弱。</para>
/// <para>晶逆（结算后能量为 0）：<strong>自己</strong>获得 1 层易伤。</para>
/// </summary>
/// <remarks>
/// <para>
/// 晶抗/晶逆的负面效果都是上给<strong>铟星自己</strong>的（打出这张牌的代价），
/// 不是给被打的敌人。所以两边都用 <c>Owner.Creature</c>。
/// </para>
/// <para>
/// 按最新用户定稿改为<strong>攻击</strong>牌：升级后费用维持1，伤害11→15。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
[RegisterCharacterStarterCard(typeof(IndiumStarCharacter), 1, Order = 3)]
public sealed class ApplicationNeutron : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(11m, ValueProp.Move),
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationNeutron()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        return null;
    }

    /// <summary>
    /// 晶抗：自己获得原版1层虚弱，持续与衰减均遵循原版规则。
    /// </summary>
    /// <remarks>
    /// 文档没有额外的提前清除规则；不创建隐藏清除器，避免抵扣既有状态或重复衰减。
    /// </remarks>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);

    }

    /// <summary>
    /// 晶逆：自己获得原版1层易伤，持续与衰减均遵循原版规则。
    /// </summary>
    /// <remarks>
    /// 原版易伤会自行衰减；不在下一回合额外扣减既有易伤。
    /// </remarks>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Vulnerable.BaseValue,
            Owner.Creature,
            this);

    }

    /// <summary>
    /// 升级：费用维持1，伤害11→15。
    /// 晶抗/晶逆的层数保持 1 层不变。
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
