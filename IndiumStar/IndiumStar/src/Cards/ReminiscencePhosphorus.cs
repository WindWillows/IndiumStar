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
/// 回忆-磷语：能力牌。每回合第一次获得延拓时，改为获得等量能量。
/// <para>升级后：改为「同时」获得等量能量和延拓。</para>
/// </summary>
/// <remarks>
/// <para>
/// 拦截由 <see cref="ReminiscencePhosphorusPower" /> 在延拓公共入口
/// <c>ExtensionRules.Gain</c> 里完成。这张牌自身没有晶抗/晶逆，
/// 所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscencePhosphorus : ModCardTemplate
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscencePhosphorus()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ReminiscencePhosphorusPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }
}
