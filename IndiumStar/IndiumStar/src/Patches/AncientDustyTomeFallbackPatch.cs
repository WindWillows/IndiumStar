using HarmonyLib;
using IndiumStar.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace IndiumStar.Patches;

/// <summary>
/// 达弗的尘封魔典要求角色有先古牌；铟星尚无此类牌时，使用原版腐化作为占位奖励。
/// 不将腐化加入铟星常规卡池；将来加入可获得的先古牌后自动沿用原版处理。
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class AncientDustyTomeFallbackPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(DustyTome __instance, Player player)
    {
        if (player.Character is not IndiumStarCharacter) return true;

        var transcendenceCards = ArchaicTooth.TranscendenceCards;
        bool hasTomeCard = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Any(card => card.Rarity == CardRarity.Ancient && !transcendenceCards.Contains(card));
        if (hasTomeCard) return true;

        // 使用玩家的奖励随机数流，与原版选择单个先古牌时的流程一致。
        var fallbackCard = ModelDb.Card<Corruption>();
        __instance.AncientCard = (player.PlayerRng.Rewards.NextItem<CardModel>([fallbackCard]) ?? fallbackCard).Id;
        return false;
    }
}
