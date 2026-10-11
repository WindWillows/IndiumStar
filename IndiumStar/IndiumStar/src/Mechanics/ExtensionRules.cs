using System.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace IndiumStar.Mechanics;

/// <summary>
/// 延拓获得的拦截器（回忆-磷语）：每回合第一次获得延拓时，把延拓换成等量能量
/// （或同时获得）。实现挂在玩家能力上；返回 true 表示默认的延拓获得被替换。
/// </summary>
public interface IExtensionGainInterceptor
{
    Task<bool> TryConvertExtensionToEnergy(PlayerChoiceContext choiceContext, Player player, int amount);
}

/// <summary>
/// 延拓操作的唯一入口。当前保留原有语义：与原版下回合能量共享层数，
/// 在能量重置后一次性兑现并移除。若以后改为独立资源，只需更换此处的实现。
/// </summary>
public static class ExtensionRules
{
    private sealed class ExtensionPlayTag { }
    private static readonly ConditionalWeakTable<CardPlay, ExtensionPlayTag> AddedExtensionPlays = new();

    // 对本次出牌拍快照：后来给同一张晶化加文字，不会追溯改变此前的出牌历史。
    public static void RecordAddedExtension(CardPlay play, bool hasExtension)
    {
        if (hasExtension) AddedExtensionPlays.GetValue(play, _ => new ExtensionPlayTag());
    }

    public static bool IsExtensionPlay(CardPlay play) =>
        play.Card is IExtensionCard || AddedExtensionPlays.TryGetValue(play, out _);

    public static int GetAmount(Player player) => player.Creature.GetPowerAmount<EnergyNextTurnPower>();

    public static async Task Gain(
        PlayerChoiceContext choiceContext,
        Player player,
        int amount,
        CardModel source)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0)
        {
            return;
        }

        // 回忆-磷语之类的拦截：每回合第一次获得延拓时可被换成等量能量。
        var interceptor = player.Creature.Powers
            .OfType<IExtensionGainInterceptor>()
            .FirstOrDefault();
        if (interceptor != null
            && await interceptor.TryConvertExtensionToEnergy(choiceContext, player, amount))
        {
            return;
        }

        await PowerCmd.Apply<EnergyNextTurnPower>(
            choiceContext, player.Creature, amount, source.Owner.Creature, source);
    }

    public static async Task RemoveAll(Player player)
    {
        EnergyNextTurnPower? power = player.Creature.GetPower<EnergyNextTurnPower>();
        if (power != null)
        {
            await PowerCmd.Remove(power);
        }
    }

    /// <summary>层数达到正数阈值时清空全部延拓；不足时不改变状态。</summary>
    public static async Task<bool> TryConsumeAll(Player player, int minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum);
        if (GetAmount(player) < minimum)
        {
            return false;
        }

        await RemoveAll(player);
        return true;
    }
}
