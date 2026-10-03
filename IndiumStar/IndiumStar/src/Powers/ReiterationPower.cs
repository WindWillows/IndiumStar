using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>下一次本玩家出牌开始时复制，系列重放不重复消费预约；回合结束清除。</summary>
[RegisterPower]
public sealed class ReiterationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("duplication_power");
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player.Creature != Owner || !play.IsFirstInSeries || Amount <= 0)
        {
            return;
        }
        int copies = Amount;
        // 在生成复制前消费预约，生成触发的嵌套钩子不能再用同一份预约。
        await PowerCmd.Remove(this);
        for (int i = 0; i < copies; i++)
        {
            await CardPileCmd.AddGeneratedCardToCombat(play.Card.CreateClone(), PileType.Draw,
                Owner.Player, CardPilePosition.Top);
        }
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) => participants.Contains(Owner)
            ? PowerCmd.Remove(this) : Task.CompletedTask;
}
