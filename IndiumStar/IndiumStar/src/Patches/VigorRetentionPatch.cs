using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models.Powers;
using IndiumStar.Powers;

namespace IndiumStar.Patches;

/// <summary>只在运用-Nb生效时，将活力消耗移到攻击牌结算后。</summary>
[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.AfterAttack))]
public static class VigorRetentionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(VigorPower __instance, AttackCommand command,
        ref object? ____internalData, ref Task __result)
    {
        if (__instance.Owner.GetPower<ApplicationNbPower>() == null)
        {
            return true;
        }
        if (command.Attacker == __instance.Owner && ____internalData != null)
        {
            // 原版通过移除活力清理攻击缓存；保留时要重置，否则下张攻击被旧 ModelSource 过滤。
            ____internalData = Activator.CreateInstance(____internalData.GetType(), nonPublic: true);
        }
        __result = Task.CompletedTask;
        return false;
    }
}
