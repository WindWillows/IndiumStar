using System.Reflection;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IndiumStar.Mechanics;

/// <summary>
/// 借用敌人的动作脚本，以独立模型和玩家行动者执行；不推进原敌人的意图状态机。
/// 原版没有公开的怪物动作转移接口，因此仅私有_creature绑定需要版本契约检查。
/// </summary>
public static class IntentExecutionRules
{
    private static readonly FieldInfo CreatureField = AccessTools.Field(typeof(MonsterModel), "_creature")
        ?? throw new MissingFieldException(typeof(MonsterModel).FullName, "_creature");
    private static readonly AsyncLocal<ExecutionContext?> ActiveContext = new();

    public sealed record ExecutionContext(MonsterModel BorrowedMonster, CardModel Source,
        CardPlay Play, Creature Target, PlayerChoiceContext Choice)
    {
        internal bool IsActive { get; set; } = true;
        public Creature Actor => Source.Owner.Creature;
    }
    public static ExecutionContext? Current => ActiveContext.Value is { IsActive: true } value ? value : null;

    public static async Task Execute(PlayerChoiceContext choice, CardModel source, CardPlay play)
    {
        Creature? target = play.Target;
        if (target?.Monster == null || !target.IsAlive || !source.Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }
        MonsterModel original = target.Monster;
        if (!BorrowedIntentCompatibility.CanExecute(original.NextMove, out string? reason))
        {
            Log.Warn($"IndiumStar Inversion: unsupported intent {original.Id.Entry}/{original.NextMove.Id}: {reason}");
            return;
        }
        string moveId = original.NextMove.Id;
        // MutableClone保留当前怪物的战斗数值；仅修改克隆的行动者和状态机。
        var borrowed = (MonsterModel)original.MutableClone();
        CreatureField.SetValue(borrowed, source.Owner.Creature);
        borrowed.ResetStateMachine();
        borrowed.SetUpForCombat();
        if (borrowed.MoveStateMachine?.States.GetValueOrDefault(moveId) is not MoveState move)
        {
            Log.Warn($"IndiumStar Inversion: cannot recreate {original.Id.Entry} move '{moveId}'.");
            return;
        }
        borrowed.SetMoveImmediate(move, forceTransition: true);
        var previous = ActiveContext.Value;
        var context = new ExecutionContext(borrowed, source, play, target, choice);
        ActiveContext.Value = context;
        try
        {
            // 对方的单体攻击/负面效果以所选敌人为目标，自身收益由克隆的Creature指向玩家。
            await move.PerformMove([target]);
        }
        finally
        {
            context.IsActive = false;
            ActiveContext.Value = previous;
        }
    }
}
