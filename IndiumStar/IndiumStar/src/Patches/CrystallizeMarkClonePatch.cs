using HarmonyLib;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Patches;

/// <summary>
/// 本体克隆会复制侵蚀和动态数值，RitsuLib运行时AttachedState不会自动复制。
/// 在共同的克隆入口同步Co/U结算标记，覆盖复沓、嬗变与本体复制牌效果。
/// </summary>
[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
public static class CrystallizeMarkClonePatch
{
    [HarmonyPostfix]
    public static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (__instance is not CardModel source || __result is not CardModel copy) return;
        ApplicationCoPower.CopyMark(source, copy);
        ApplicationUCrystallizePower.CopyMark(source, copy);
    }
}
