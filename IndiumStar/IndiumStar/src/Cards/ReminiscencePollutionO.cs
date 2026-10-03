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
using IndiumStar.Mechanics;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 回忆-污染O：能力牌。
/// <para>晶抗（结算后能量不为 0）：本战斗中，打出「晶化」时获得 4 点格挡。</para>
/// <para>晶逆（结算后能量为 0）：本战斗中，打出「晶化」时获得 3 层活力。</para>
/// <para>升级后：获得固有。</para>
/// </summary>
/// <remarks>
/// <para>
/// 主效果只负责挂上 <see cref="ReminiscencePollutionOPower" />；两个分支各自
/// 往能力上配置对应的触发器。双分支（如量子点）会同时配置两种收益。
/// </para>
/// <para>
/// 升级的「固有」用 <c>AddKeyword</c>：它改的是实例的持久关键字状态，会随克隆保留。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscencePollutionO : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 晶抗触发器：每打出一张晶化获得的格挡。
        new BlockVar(4m, ValueProp.Move),
        // 晶逆触发器：每打出一张晶化获得的活力。
        new PowerVar<VigorPower>(3m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscencePollutionO()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ReminiscencePollutionOPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        return null;
    }

    /// <summary>晶抗：打出「晶化」时获得格挡。</summary>
    protected override Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        Owner.Creature.GetPower<ReminiscencePollutionOPower>()
            ?.Configure(DynamicVars.Block.IntValue, 0);
        return Task.CompletedTask;
    }

    /// <summary>晶逆：打出「晶化」时获得活力。</summary>
    protected override Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        Owner.Creature.GetPower<ReminiscencePollutionOPower>()
            ?.Configure(0, DynamicVars["VigorPower"].IntValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
