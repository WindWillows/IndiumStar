using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化U：继承晶化的公共属性和额度；打出时从任意位置将一张「运用-中子+」放回手牌，
/// 并使其获得消耗。
/// </summary>
/// <remarks>
/// <para>
/// 「任意位置」指抽牌堆、弃牌堆、消耗堆（不含手牌——在手牌里就不需要放回了）。
/// 多张候选时用游戏 RNG 随机取一张（和「晶化-Pb」的随机保留同一口径）。
/// </para>
/// <para>
/// 仅选择已经升级的运用-中子；这个效果不会额外升级普通中子。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystallizationU : CrystallizationCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    protected override async Task<CardModel?> PlayCrystallizationEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = new[] { PileType.Draw, PileType.Discard, PileType.Exhaust }
            .SelectMany(pile => pile.GetPile(Owner).Cards)
            .Where(card => card is DerivedApplicationNeutron && card.IsUpgraded)
            .ToList();

        CardModel? picked = candidates
            .TakeRandom(1, Owner.RunState.Rng.CombatCardSelection)
            .FirstOrDefault();
        if (picked == null)
        {
            return null;
        }

        picked.AddKeyword(CardKeyword.Exhaust);
        await CardPileCmd.Add(picked, PileType.Hand);
        return picked;
    }
}
