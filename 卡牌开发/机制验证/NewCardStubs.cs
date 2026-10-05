// 这些替身只模拟新卡使用的指令边界，不替代真实游戏中的怪物、UI或存档验证。
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using Models = MegaCrit.Sts2.Core.Models;

namespace MegaCrit.Sts2.Core.CardSelection {
    public record CardSelectorPrefs(object Prompt, int Count) {
        public static object ExhaustSelectionPrompt { get; } = new();
    }
}
namespace MegaCrit.Sts2.Core.Commands {
    public static class CardSelectCmd {
        public static Task<IEnumerable<CardModel>> FromHand(PlayerChoiceContext context, Player player,
            MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs prefs, object? filter, CardModel source) =>
            Task.FromResult<IEnumerable<CardModel>>(player.PlayerCombatState!.Hand.Cards.Where(c => c != source).Take(prefs.Count).ToArray());
    }
    public static class CreatureCmd {
        public static List<int> BlockGains { get; } = [];
        public static Func<Creature, decimal, CardPlay?, Task>? AfterBlockGained { get; set; }
        public static Task<decimal> GainBlock(Creature creature, BlockVar block, CardPlay? play, bool fast = false) =>
            GainBlock(creature, block.BaseValue, block.Props, play, fast);
        public static async Task<decimal> GainBlock(Creature creature, decimal baseAmount,
            MegaCrit.Sts2.Core.ValueProps.ValueProp props, CardPlay? play, bool fast = false) {
            if (!creature.IsAlive || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding) return 0;
            int amount = Math.Max(0, (int)baseAmount + creature.GetPowerAmount<Models.Powers.DexterityPower>());
            var modifiers = new List<PowerModel>();
            foreach (var power in creature.Powers.ToArray()) {
                decimal multiplier = power.ModifyBlockMultiplicative(creature, amount, props, play?.Card, play);
                if (multiplier != 1m) { amount = (int)(amount * multiplier); modifiers.Add(power); }
            }
            foreach (var power in modifiers) await power.AfterModifyingBlockAmount(amount, play?.Card, play);
            if (amount > 0) { BlockGains.Add(amount); creature.Block += amount; }
            if (AfterBlockGained != null) await AfterBlockGained(creature, amount, play);
            return amount;
        }
        public static string? LastAnimation { get; set; }
        public static Task TriggerAnim(Creature creature, string triggerName, float waitTime) {
            LastAnimation = triggerName; return Task.CompletedTask;
        }
    }
}
namespace MegaCrit.Sts2.Core.Models {
    public record TestModelId(string Entry);
    public abstract class MonsterModel : AbstractModel {
        private Creature _creature = null!;
        public MegaCrit.Sts2.Core.Random.Rng Rng { get; set; } = new(42);
        public Creature Creature { get => _creature; set => _creature = value; }
        public TestModelId Id { get; } = new("PROBE_MONSTER");
        public MonsterMoveStateMachine? MoveStateMachine { get; private set; }
        public MoveState NextMove { get; private set; } = null!;
        public void ResetStateMachine() => MoveStateMachine = null;
        protected abstract MonsterMoveStateMachine GenerateMoveStateMachine();
        public void SetUpForCombat() {
            MoveStateMachine = GenerateMoveStateMachine(); NextMove = MoveStateMachine.States.Values.OfType<MoveState>().First();
        }
        public void SetMoveImmediate(MoveState move, bool forceTransition = false) => NextMove = move;
    }
}
namespace MegaCrit.Sts2.Core.Random {
    public sealed record SerializableRng(ulong State, int Counter);
    public sealed class Rng {
        private ulong _state;
        public int Counter { get; private set; }
        public Rng(ulong seed) { _state = seed; }
        public Rng(SerializableRng snapshot) { _state = snapshot.State; Counter = snapshot.Counter; }
        public int NextInt() { Counter++; _state = unchecked(_state * 6364136223846793005UL + 1); return (int)(_state >> 33); }
        public SerializableRng ToSerializable() => new(_state, Counter);
    }
}
namespace MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine {
    public class MoveState {
        private readonly Func<IReadOnlyList<Creature>, Task> _onPerform;
        public MoveState(string id, Func<IReadOnlyList<Creature>, Task> action) { Id = id; _onPerform = action; }
        public string Id { get; }
        public bool Performed { get; private set; }
        public Task PerformMove(IReadOnlyList<Creature> targets) { Performed = true; return _onPerform(targets); }
    }
    public class MonsterMoveStateMachine(params MoveState[] moves) {
        public Dictionary<string, object> States { get; } = moves.ToDictionary(m => m.Id, m => (object)m);
    }
}
namespace MegaCrit.Sts2.Core.Logging {
    public static class Log { public static void Warn(string message) { } }
}

public class ProbeEnemy : MonsterModel {
    public enum Mode { Attack, Buff, Debuff, Animation, RandomAttack, Fail }
    public Mode Intent { get; set; }
    public int ExecutionCount { get; private set; }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine() => new(
        new MoveState("MOVE", async targets => {
            ExecutionCount++;
            switch (Intent) {
                case Mode.Attack:
                    await MegaCrit.Sts2.Core.Commands.DamageCmd.Attack(7m).FromMonster(this).WithHitCount(2).Execute(null); break;
                case Mode.Buff:
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<Models.Powers.StrengthPower>(new(), Creature, 2, Creature, null); break;
                case Mode.Debuff:
                    await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<Models.Powers.DexterityPower>(new(), targets[0], -2, Creature, null); break;
                case Mode.Animation:
                    await MegaCrit.Sts2.Core.Commands.CreatureCmd.TriggerAnim(Creature, "MonsterSpecial", 1f); break;
                case Mode.RandomAttack:
                    await MegaCrit.Sts2.Core.Commands.DamageCmd.Attack(Rng.NextInt() % 5 + 1).FromMonster(this).Execute(null); break;
                case Mode.Fail: throw new InvalidOperationException("probe failure");
            }
        }));
}
