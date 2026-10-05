using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Mechanics;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>每份磷语增加每回合一次转换额度；各份保留其升级模式，按施加顺序使用。</summary>
[RegisterPower]
public sealed class ReminiscencePhosphorusPower : ModPowerTemplate, IExtensionGainInterceptor
{
    private List<bool> _grantBothBySlot = [];
    private bool _pendingGrantBoth;
    private int _usedThisTurn;
    private int _resetTurn;
    private static readonly AsyncLocal<bool?> ApplyingMode = new();

    /// <summary>模式跟随施加动作的异步调用域，嵌套施加返回后恢复外层模式。</summary>
    public static async Task ApplyFromCard(PlayerChoiceContext context, ReminiscencePhosphorus card)
    {
        card.Owner.Creature.GetPower<ReminiscencePhosphorusPower>()?.SyncAcceptedSlots();
        bool? previous = ApplyingMode.Value;
        ApplyingMode.Value = card.IsUpgraded;
        try
        {
            await PowerCmd.Apply<ReminiscencePhosphorusPower>(context, card.Owner.Creature,
                1m, card.Owner.Creature, card);
        }
        finally { ApplyingMode.Value = previous; }
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _grantBothBySlot = new(_grantBothBySlot);
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        _pendingGrantBoth = cardSource is ReminiscencePhosphorus { IsUpgraded: true };
        _resetTurn = target.Player?.PlayerCombatState?.TurnNumber ?? 0;
        // 新能力尚未设置Owner/Amount，这里的amount已经过本体修改器。
        for (int i = 0; i < (int)amount; i++) _grantBothBySlot.Add(_pendingGrantBoth);
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount,
        Creature target, Creature? applier, CardModel? cardSource)
    {
        if (power == this && target == Owner)
        {
            // 外层施加已经修改Amount时，先保存外层新增额度，再接受内层模式。
            SyncAcceptedSlots();
            if (cardSource is ReminiscencePhosphorus card) _pendingGrantBoth = card.IsUpgraded;
        }
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this) SyncAcceptedSlots();
        return Task.CompletedTask;
    }

    private void SyncAcceptedSlots()
    {
        // 用真正接受的层数对齐；受阻或减为零的施加不能产生隐藏额度。
        int accepted = System.Math.Max(0, Amount);
        while (_grantBothBySlot.Count < accepted)
            _grantBothBySlot.Add(ApplyingMode.Value ?? _pendingGrantBoth);
        if (_grantBothBySlot.Count > accepted)
            _grantBothBySlot.RemoveRange(accepted, _grantBothBySlot.Count - accepted);
    }

    public async Task<bool> TryConvertExtensionToEnergy(PlayerChoiceContext context, Player player, int amount)
    {
        if (amount <= 0 || player.Creature != Owner || Owner.CombatState == null) return false;
        SyncAcceptedSlots();
        if (_usedThisTurn >= _grantBothBySlot.Count) return false;
        // 能量钩子可能等待、再施加或再次获得延拓；先消费次数，并固定本次模式。
        bool grantBoth = _grantBothBySlot[_usedThisTurn++];
        await PlayerCmd.GainEnergy(amount, player);
        return !grantBoth;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (combatState == Owner.CombatState && participants.Contains(Owner)
            && Owner.Player?.PlayerCombatState is { } state && state.TurnNumber != _resetTurn)
        {
            _resetTurn = state.TurnNumber;
            _usedThisTurn = 0;
        }
        return Task.CompletedTask;
    }
}
