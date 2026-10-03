using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Mechanics;

/// <summary>统一分支选择和通知；通过当前战斗的模型发现监听者，不使用全局事件订阅。</summary>
public static class CrystalBranchRules
{
    public static CrystalBranch SelectBranches(CrystalBranchContext context)
    {
        CrystalBranch selected = context.EnergyAfterMainEffect != 0
            ? CrystalBranch.Resistance
            : CrystalBranch.Reversal;

        if (context.AvailableBranches == CrystalBranch.None)
        {
            return CrystalBranch.None;
        }

        if (context.Card.CombatState is { } combatState && CanDispatch())
        {
            foreach (AbstractModel model in combatState.IterateHookListeners())
            {
                if (model is ICrystalBranchModifier modifier)
                {
                    selected = modifier.ModifyCrystalBranches(context, selected) & CrystalBranch.Both;
                }
            }
        }

        // 修改规则可以选择双分支，但不能给没有对应效果的卡凭空制造触发。
        return selected & context.AvailableBranches;
    }

    public static async Task NotifyTriggered(
        PlayerChoiceContext choiceContext,
        CrystalBranchContext context,
        CrystalBranch branch)
    {
        if (branch is not (CrystalBranch.Resistance or CrystalBranch.Reversal))
        {
            throw new ArgumentOutOfRangeException(nameof(branch));
        }

        if (context.Card.CombatState is not { } combatState || !CanDispatch())
        {
            return;
        }

        // 使用本体当前战斗的监听顺序及存活筛选；不保留跨战斗的模型引用。
        foreach (AbstractModel model in combatState.IterateHookListeners())
        {
            if (model is ICrystalBranchListener listener)
            {
                await listener.AfterCrystalBranchTriggered(choiceContext, context, branch);
                model.InvokeExecutionFinished();
            }
        }
    }

    private static bool CanDispatch() =>
        !CombatManager.Instance.IsOverOrEnding || CombatManager.Instance.IsStarting;
}
