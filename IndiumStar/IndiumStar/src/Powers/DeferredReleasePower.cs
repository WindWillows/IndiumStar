using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>在本人下回合能量结算后、正常抽牌前，用本体自动出牌流程兑现预约。</summary>
[RegisterPower]
public sealed class DeferredReleasePower : ModPowerTemplate
{
    private sealed record Pending(CardModel Card, int DueTurn, ICombatState Combat);
    private List<Pending> _pending = [];
    private bool _settling;
    public override int DisplayAmount => _pending.Count;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("duplication_power");

    public void Schedule(CardModel card, int dueTurn, ICombatState combat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dueTurn);
        _pending.Add(new(card, dueTurn, combat));
        SetAmount(_pending.Count);
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pending = new(_pending);
        _settling = false;
    }
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combat)
    {
        if (player.Creature != Owner || player.PlayerCombatState is not { } state || _settling
            || Owner.CombatState != combat || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;
        var due = _pending.Where(entry => entry.DueTurn <= state.TurnNumber).ToArray();
        if (due.Length == 0) return;
        _pending.RemoveAll(entry => entry.DueTurn <= state.TurnNumber);
        SetAmount(_pending.Count);
        _settling = true;
        int index = 0;
        try
        {
            for (; index < due.Length; index++)
            {
                if (CombatManager.Instance.IsOverOrEnding || !Owner.IsAlive) break;
                var entry = due[index];
                CardModel card = entry.Card;
                // 预约跟随被消耗的原牌身份；后来移到其他战斗牌堆也仍可打出。
                // 已移出战斗、转交他人或正在打出的牌，不会被重新拉入当前行动。
                if (entry.Combat != combat || card.CombatState != combat || card.Owner != player
                    || card.Pile?.IsCombatPile != true || card.Pile.Type == PileType.Play)
                    continue;
                await CardCmd.AutoPlay(context, card, null);
            }
        }
        catch
        {
            // 当前尝试可能已有部分效果，不能重试；保留尚未尝试的预约并传递异常。
            _pending.InsertRange(0, due.Skip(index + 1));
            throw;
        }
        finally
        {
            _settling = false;
            SetAmount(_pending.Count);
            if (_pending.Count == 0 && Owner.GetPower<DeferredReleasePower>() == this)
                await PowerCmd.Remove(this);
        }
    }
}
