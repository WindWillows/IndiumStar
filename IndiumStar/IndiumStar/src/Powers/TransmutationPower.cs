using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「嬗变」的能力：回合开始时，将一张晶化变化为抽牌堆顶的牌。
/// </summary>
/// <remarks>
/// <para>
/// 时点是 <c>BeforeSideTurnStart</c>（回合开始、抽牌之前），与隐藏能力
/// 「晶逆·易伤消退」的回合开始处理同一个钩子。被变化的晶化从手牌里随机取
/// （游戏 RNG，同晶化-Pb 的随机保留）；抽牌堆顶是 <c>Cards[0]</c>
/// （本体 <c>MoveToTopInternal</c> 插到 0 号位）。
/// </para>
/// <para>
/// 变化产物是抽牌堆顶牌的<strong>克隆</strong>（<c>CloneCard</c> 保留升级状态），
/// 原顶牌留在抽牌堆不动。手牌没有晶化或抽牌堆空了就不触发。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class TransmutationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || base.Owner.Player == null)
        {
            return;
        }

        CardModel? crystal = PileType.Hand.GetPile(base.Owner.Player).Cards
            .Where(CrystallizationRules.IsCrystallizationCard)
            .TakeRandom(1, base.Owner.Player.RunState.Rng.CombatCardSelection)
            .FirstOrDefault();
        if (crystal == null)
        {
            return;
        }

        CardModel? topOfDraw = PileType.Draw.GetPile(base.Owner.Player).Cards.FirstOrDefault();
        if (topOfDraw == null)
        {
            return;
        }

        CardModel replacement = combatState.CloneCard(topOfDraw);
        await CardCmd.Transform(crystal, replacement);
    }
}
