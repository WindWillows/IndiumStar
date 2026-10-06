using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 回忆-钒永：先抽1张牌（升级2），再统计后续抽牌以预约下回合等量抽牌。
/// </summary>
/// <remarks>
/// <para>
/// <b>「等量」在回合结束时才确定</b>，因为要统计整个回合抽了多少张。
/// 所以用自己写的 <see cref="ReminiscenceDrawPower" /> 而不是原版
/// <c>DrawCardsNextTurnPower</c>（后者靠 <c>AmountOnTurnStart</c> 判断，
/// 只能"本回合施加、下回合生效"，做不到回合结束才结算）。
/// </para>
/// <para>
/// 先抽牌再挂能力：首次施加不统计自身抽牌；已有统计器时照常统计，且不重置。
/// </para>
/// <para>
/// 这张牌没有晶抗/晶逆——按规格它没写。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscenceVanadiumEternal : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 立即抽几张。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscenceVanadiumEternal()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        // 自身抽牌结束后才开始统计，重复施加保留已有累计。
        // 初始层数必须是 1（不能是 0）：PowerCmd.Apply 在 amount == 0 时直接 return
        // （PowerCmd.cs:107），能力根本不会被创建。
        ReminiscenceDrawPower? power = await PowerCmd.Apply<ReminiscenceDrawPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        power?.Configure(cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
