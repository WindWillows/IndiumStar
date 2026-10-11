using HarmonyLib;
using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IndiumStar.Patches;

/// <summary>兼容旧版本污染的卡牌快照：原版牌的零值残留移除，真正晶币计数迁移到专属属性。</summary>
public static class CrystalCoinSaveCompatibility
{
    public const string LegacyProperty = "CardModel_crystal_coin_plays_this_run";

    public static void Normalize(SerializableCard card)
    {
        if (card.Props?.ints is not { } ints) return;
        var legacy = ints.Where(p => p.name == LegacyProperty).ToArray();
        if (legacy.Length == 0) return;
        if (card.Id == ModelDb.GetId(typeof(CrystalCoin)))
        {
            // 新字段已存在时以新字段为准；旧晶币不能因升级版本而恢复到完整收益。
            if (!ints.Any(p => p.name == nameof(CrystalCoin.CrystalCoinPlaysThisRun)))
                ints.Add(new(nameof(CrystalCoin.CrystalCoinPlaysThisRun), Math.Max(0, legacy.Max(p => p.value))));
            ints.RemoveAll(p => p.name == LegacyProperty);
        }
        else
        {
            // 非晶币只能自动清理已确认无意义的零值；保留异常非零数据供人工诊断。
            ints.RemoveAll(p => p.name == LegacyProperty && p.value == 0);
        }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
public static class CrystalCoinRestoreCompatibilityPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(SerializableCard __0) => CrystalCoinSaveCompatibility.Normalize(__0);
}

// 地图历史和奖励候选中的卡牌快照可能不经过FromSerializable，也必须在写回放前规范化。
[HarmonyPatch(typeof(SerializableCard), nameof(SerializableCard.Serialize))]
public static class CrystalCoinReplayCompatibilityPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(SerializableCard __instance) => CrystalCoinSaveCompatibility.Normalize(__instance);
}
