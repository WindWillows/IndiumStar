using HarmonyLib;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Patches;

/// <summary>只改借用模型发出的攻击，避免原版FromMonster把目标强制设置为玩家阵营。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.FromMonster))]
public static class BorrowedIntentAttackPatch
{
    [HarmonyPrefix]
    public static bool Prefix(AttackCommand __instance, MonsterModel monster, ref AttackCommand __result)
    {
        var context = IntentExecutionRules.Current;
        if (context == null || monster != context.BorrowedMonster) return true;
        __result = __instance.FromCard(context.Source, context.Play).Targeting(context.Target);
        return false;
    }
}

/// <summary>原怪物攻击常传null上下文；借用后必须保留玩家选择上下文。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class BorrowedIntentChoicePatch
{
    [HarmonyPrefix]
    public static void Prefix(AttackCommand __instance, ref PlayerChoiceContext? choiceContext)
    {
        var context = IntentExecutionRules.Current;
        if (context != null && __instance.ModelSource == context.Source)
        {
            choiceContext = context.Choice;
        }
    }
}

/// <summary>玩家不具有所有怪物动画；仅在借用动作的玩家行动者上回退到原生攻击/施法。</summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
public static class BorrowedIntentAnimationPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref string triggerName, ref float waitTime)
    {
        var context = IntentExecutionRules.Current;
        if (context == null || creature != context.Actor) return;
        triggerName = triggerName == "Attack" ? "Attack" : "Cast";
        waitTime = 0f;
    }
}
