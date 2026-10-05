// 指令替身仅用于源码回归；原生战斗、选择UI、存档及同步另行验证。
namespace MegaCrit.Sts2.Core.Entities.Powers { public interface ITemporaryPower { } }
namespace MegaCrit.Sts2.Core.Combat.History.Entries {
    public record CardExhaustedEntry(MegaCrit.Sts2.Core.Combat.ICombatState State, int Turn,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature Actor) {
        public bool HappenedThisTurn(MegaCrit.Sts2.Core.Combat.ICombatState state) => ReferenceEquals(State, state) && Turn == state.Turn;
    }
}
