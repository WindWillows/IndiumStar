using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;
using IndiumStar.Mechanics;

namespace IndiumStar.Powers;

/// <summary>
/// 「量子点」的能力：你每回合打出的前 2 张牌同时触发晶抗和晶逆。
/// </summary>
/// <remarks>
/// <para>
/// 实现公共机制的 <see cref="ICrystalBranchModifier" />：对适用的牌返回
/// <see cref="CrystalBranch.Both" />。能不能享受由本体出牌历史决定——
/// 统计本回合、本玩家、系列第一段（<c>IsFirstInSeries</c>）的出牌数，
/// <strong>没有分支的牌也占「前两张」的位置</strong>，所以不能按修改接口被调用的次数统计。
/// </para>
/// <para>
/// 出牌次数预测阶段（<c>ModifyCardPlayCount</c>）当前这张牌还没进历史，
/// 实际结算阶段已经进了，两种时点用同一个「不超过 2 张」判断，结论一致。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class QuantumDotPower : ModPowerTemplate, ICrystalBranchModifier
{
    /// <summary>每回合前几张牌享受双分支。</summary>
    private const int AffectedCardsPerTurn = 2;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public CrystalBranch ModifyCrystalBranches(CrystalBranchContext context, CrystalBranch selectedBranches)
    {
        // 只作用于自己玩家的牌。
        if (context.Card.Owner.Creature != base.Owner)
        {
            return selectedBranches;
        }

        ICombatState? combatState = context.Card.CombatState;
        if (combatState == null)
        {
            return selectedBranches;
        }

        // 本回合本玩家已经打出的牌（系列第一段算一张）。实际结算时当前这张牌
        // 已经在历史里，预测时还没进历史——「不超过 2」在两种时点等价。
        int playedThisTurn = CombatManager.Instance.History.CardPlaysStarted
            .Where(entry => entry.HappenedThisTurn(combatState)
                && entry.CardPlay.Player == context.Card.Owner
                && entry.CardPlay.IsFirstInSeries)
            .Take(AffectedCardsPerTurn)
            .Count();

        return playedThisTurn < AffectedCardsPerTurn
            ? CrystalBranch.Both
            : selectedBranches;
    }
}
