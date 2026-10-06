using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>电子的易伤覆盖敌方行动；在原生回合末衰减之前回收，避免重复扣除其他来源。</summary>
[RegisterPower]
public sealed class LoseVulnerableAtTurnStartPower : ElectronDebuffExpiryPower<VulnerablePower>
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("vulnerable_power");

    public override Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) => side == CombatSide.Enemy && Owner.Side == CombatSide.Player
            ? Expire(context) : Task.CompletedTask;

    // 跳过敌方行动等特殊流程也不能带入下一本人回合；不清除开始阶段新打出的电子。
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) => side == Owner.Side
            && participants.Contains(Owner) && combatState == Owner.CombatState
            && Owner.Player?.PlayerCombatState?.TurnNumber > AppliedTurn
            ? Expire(context) : Task.CompletedTask;
}
