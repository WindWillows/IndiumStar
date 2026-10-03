using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 量子点：能力牌。你每回合打出的前 2 张牌会同时触发晶抗和晶逆效果。
/// <para>升级后：费用 2 → 1。</para>
/// </summary>
/// <remarks>
/// <para>
/// 双分支由 <see cref="QuantumDotPower" /> 通过公共机制的
/// <c>ICrystalBranchModifier</c> 实现；牌本身没有晶抗/晶逆，也不提供分支。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class QuantumDot : ModCardTemplate
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public QuantumDot()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<QuantumDotPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
