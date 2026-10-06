using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>电子的虚弱仅持续至本人本回合结束；保留原模型ID。</summary>
[RegisterPower]
public sealed class LoseWeakAtTurnEndPower : ElectronDebuffExpiryPower<WeakPower>
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("weak_power");

    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) => side == Owner.Side && participants.Contains(Owner)
            ? Expire(context) : Task.CompletedTask;

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) => side == Owner.Side
            && participants.Contains(Owner) && combatState == Owner.CombatState
            && Owner.Player?.PlayerCombatState?.TurnNumber > AppliedTurn
            ? Expire(context) : Task.CompletedTask;
}
