using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 同步晶化的手牌光环到真正的力量/敏捷。单独记录本光环的贡献，
/// 离手时只移除贡献，不覆盖战斗中从其他来源获得或失去的能力。
/// 控制器在整个战斗中存在，因此最后一张Hs变化或被移除后仍能回收光环。
/// </summary>
[RegisterPower]
public sealed class CrystalHandAuraPower : ModPowerTemplate
{
    private Dictionary<Creature, (int Strength, int Dexterity)> _contributions = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _contributions = [];
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public static async Task EnsureAndSync(Creature owner)
    {
        var controller = owner.GetPower<CrystalHandAuraPower>();
        if (controller == null)
        {
            controller = await PowerCmd.Apply<CrystalHandAuraPower>(
                new ThrowingPlayerChoiceContext(), owner, 1, owner, null, silent: true);
        }
        if (controller != null)
        {
            await controller.Sync();
        }
    }

    public async Task Sync()
    {
        ICombatState? state = Owner.CombatState;
        if (state == null || Owner.Player == null ||
            (CombatManager.Instance.IsOverOrEnding && !CombatManager.Instance.IsStarting))
        {
            return;
        }

        var hand = PileType.Hand.GetPile(Owner.Player).Cards;
        int hsStrength = hand.OfType<CrystallizationHs>().Count();
        int tiling = Owner.GetPower<TilingFormPower>() != null
            ? 2 * hand.Count(CrystallizationRules.IsCrystallizationCard) : 0;

        foreach (Creature recipient in state.Creatures.Concat(_contributions.Keys).Distinct().ToList())
        {
            var previous = _contributions.GetValueOrDefault(recipient);
            int strength = recipient.IsAlive ? hsStrength + (recipient == Owner ? tiling : 0) : 0;
            int dexterity = recipient.IsAlive && recipient == Owner ? tiling : 0;
            int strengthChange = await Adjust<StrengthPower>(recipient, strength - previous.Strength);
            int dexterityChange = await Adjust<DexterityPower>(recipient, dexterity - previous.Dexterity);
            // 未发放的贡献不能在离手时扣回；恢复可接收能力后仍允许补发。
            _contributions[recipient] = (previous.Strength + strengthChange, previous.Dexterity + dexterityChange);
            if (recipient.IsMonster && recipient.IsAlive && strengthChange != 0)
            {
                var node = NCombatRoom.Instance?.GetCreatureNode(recipient);
                if (node != null)
                {
                    await node.UpdateIntent(state.Allies);
                }
            }
        }
    }

    private static async Task<int> Adjust<TPower>(Creature recipient, int difference) where TPower : PowerModel
    {
        if (difference == 0 || !recipient.CanReceivePowers)
        {
            return 0;
        }
        TPower? power = recipient.GetPower<TPower>();
        if (power == null)
        {
            // “在手中具有”是精确的临时层数，不是一次可被增益倍率修改的获得指令。
            // 使用本体的可变能力与能力通知，能力栏、伤害以及查询层数共用同一份状态。
            power = (TPower)ModelDb.Power<TPower>().ToMutable();
            power.ApplyInternal(recipient, difference, silent: true);
        }
        else
        {
            power.SetAmount(power.Amount + difference, silent: true);
            if (power.Amount == 0)
            {
                await PowerCmd.Remove(power);
            }
        }
        return difference;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        card.Owner.Creature == Owner ? Sync() : Task.CompletedTask;

    public override Task AfterCreatureAddedToCombat(Creature creature) => Sync();

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource) =>
        power is TilingFormPower && power.Owner == Owner ? Sync() : Task.CompletedTask;
}
