using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Mechanics;

/// <summary>一次CardPlay对应一个预约，避免实例字段被嵌套出牌、克隆或下次出牌复用。</summary>
public static class CrystalReplayRules
{
    private sealed record PendingReplay(CardModel Card, int AdditionalPlays);
    private static readonly ConditionalWeakTable<CardPlay, PendingReplay> Pending = new();
    // 原版init setter可以创建托管委托；不改写只读backing field。
    private static readonly Action<CardPlay, int> SetPlayCount = typeof(CardPlay)
        .GetProperty(nameof(CardPlay.PlayCount))!.SetMethod!
        .CreateDelegate<Action<CardPlay, int>>();

    public static void Request(CardModel card, CardPlay play, int additionalPlays)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(play);
        if (additionalPlays <= 0) throw new ArgumentOutOfRangeException(nameof(additionalPlays));
        if (!ReferenceEquals(card, play.Card) || !play.IsFirstInSeries)
            throw new InvalidOperationException("Crystal replay must belong to the first play of this card series.");
        // 提前验证溢出；重复预约是调用错误，而非允许叠加同一段的晶逆。
        _ = checked(play.PlayCount + additionalPlays);
        Pending.Add(play, new PendingReplay(card, additionalPlays));
    }

    /// <summary>OnPlay成功完成后，修改正在运行的本体系列循环；不重新执行OnPlay。</summary>
    public static void Apply(CardModel card, CardPlay play, ref int playCount)
    {
        if (!Pending.TryGetValue(play, out PendingReplay? pending)) return;
        if (!ReferenceEquals(card, pending.Card) || !ReferenceEquals(card, play.Card) || !play.IsFirstInSeries)
            throw new InvalidOperationException("Crystal replay series identity changed before completion.");
        if (playCount != play.PlayCount || playCount <= 0)
            throw new InvalidOperationException("Crystal replay requires matching native series metadata.");
        int extended = checked(playCount + pending.AdditionalPlays);
        if (!Pending.Remove(play)) return;
        SetPlayCount(play, extended);
        playCount = extended;
    }
}
