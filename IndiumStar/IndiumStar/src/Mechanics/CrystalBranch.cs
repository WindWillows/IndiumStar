using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Mechanics;

[Flags]
public enum CrystalBranch
{
    None = 0,
    Resistance = 1,
    Reversal = 2,
    Both = Resistance | Reversal,
}

/// <summary>
/// 一次基础效果完成后的快照；分支及监听者改变能量不会改变本次快照。
/// </summary>
/// <remarks>
/// 出牌次数预测（<c>ModifyCardPlayCount</c>）发生在主效果结算之前，
/// 此时 <paramref name="CardPlay" /> 与 <paramref name="MainEffectResult" /> 为 <c>null</c>。
/// 修改规则与监听者必须容忍这两个字段为空。
/// </remarks>
public sealed record CrystalBranchContext(
    ApplicationCard Card,
    CardPlay? CardPlay,
    CardModel? MainEffectResult,
    int EnergyAfterMainEffect,
    CrystalBranch AvailableBranches);

/// <summary>
/// 战斗中的能力、卡牌等模型可实现此接口修改分支，例如量子点返回 Both。
/// 按本体监听顺序调用；方法应只决定分支，不改变游戏状态。
/// 实现者须自行检查玩家、牌堆和有效回合等适用范围。
/// </summary>
public interface ICrystalBranchModifier
{
    CrystalBranch ModifyCrystalBranches(CrystalBranchContext context, CrystalBranch selectedBranches);
}

/// <summary>
/// 已声明的分支完成后通知一次，供污染H等能力计数。
/// 无对应分支的卡牌不通知；分支已执行但数值结果为0仍算一次触发。
/// branch 只会是 Resistance 或 Reversal。实现者须检查适用玩家。
/// </summary>
public interface ICrystalBranchListener
{
    Task AfterCrystalBranchTriggered(
        PlayerChoiceContext choiceContext,
        CrystalBranchContext context,
        CrystalBranch branch);
}
