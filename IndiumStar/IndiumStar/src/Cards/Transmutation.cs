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
/// 嬗变：能力牌。回合开始时，将一张晶化变化为抽牌堆顶的牌。
/// <para>升级后：获得固有。</para>
/// </summary>
/// <remarks>
/// <para>
/// 结算由 <see cref="TransmutationPower" /> 承担。这张牌自身没有晶抗/晶逆，
/// 所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 升级的「固有」用 <c>AddKeyword</c>（实例持久关键字，随克隆保留）。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Transmutation : ModCardTemplate
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public Transmutation()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TransmutationPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
