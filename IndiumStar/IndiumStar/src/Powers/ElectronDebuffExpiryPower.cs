using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>只回收电子实际施加的临时层数；标记本身不属于负面效果、不消耗人工制品。</summary>
public abstract class ElectronDebuffExpiryPower<TPower> : ModPowerTemplate where TPower : PowerModel, new()
{
    [SavedProperty]
    public int TemporaryAmount { get; set; }

    [SavedProperty]
    public int AppliedTurn { get; set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount,
        Creature target, Creature? applier, CardModel? cardSource)
    {
        // 旧负面效果被完全移除后，新实例不能继承旧回收额度。
        if (power is TPower && target == Owner && Owner.GetPower<TPower>() != power)
            TemporaryAmount = 0;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is not TPower || power.Owner != Owner) return Task.CompletedTask;
        int accepted = (int)amount;
        if (accepted > 0 && applier == Owner && cardSource is ApplicationNeutron)
        {
            TemporaryAmount = (int)Math.Min(int.MaxValue, (long)TemporaryAmount + accepted);
            AppliedTurn = Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
        }
        else if (accepted < 0)
            TemporaryAmount = (int)Math.Max(0L, (long)TemporaryAmount + accepted);
        if (power.Amount <= 0) TemporaryAmount = 0;
        return Task.CompletedTask;
    }

    protected async Task Expire(PlayerChoiceContext context)
    {
        TPower? power = Owner.GetPower<TPower>();
        int amount = Math.Min(TemporaryAmount, Math.Max(0, power?.Amount ?? 0));
        TemporaryAmount = 0;
        // 先注销本次标记；移除钩子中产生的新电子效果使用独立的新标记。
        await PowerCmd.Remove(this);
        if (amount > 0 && power != null && Owner.GetPower<TPower>() == power)
            await PowerCmd.ModifyAmount(context, power, -amount, null, null);
    }

    public static async Task ApplyFromCard<TExpiry>(PlayerChoiceContext context,
        ApplicationNeutron card, decimal amount) where TExpiry : ElectronDebuffExpiryPower<TPower>, new()
    {
        if (CombatManager.Instance.IsOverOrEnding || !card.Owner.Creature.IsAlive || amount <= 0) return;
        TExpiry? expiry = card.Owner.Creature.GetPower<TExpiry>();
        expiry ??= await PowerCmd.Apply<TExpiry>(context, card.Owner.Creature, 1, null, null, silent: true);
        if (expiry == null) return;
        await PowerCmd.Apply<TPower>(context, card.Owner.Creature, amount, card.Owner.Creature, card);
        if (expiry.TemporaryAmount == 0 && card.Owner.Creature.GetPower<TExpiry>() == expiry)
            await PowerCmd.Remove(expiry);
    }
}
