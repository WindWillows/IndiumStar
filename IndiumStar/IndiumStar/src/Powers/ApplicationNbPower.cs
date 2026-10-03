using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 活力在攻击牌结算完成后减少2层。本体攻击后的全量消耗由 VigorRetentionPatch 抑制。
/// 读取当前活力，保留卡牌效果、自动晶化和嵌套出牌新增的层数。
/// </summary>
[RegisterPower]
public sealed class ApplicationNbPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != Owner || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }
        VigorPower? vigor = Owner.GetPower<VigorPower>();
        if (vigor != null && vigor.Amount > 0)
        {
            await PowerCmd.ModifyAmount(choiceContext, vigor, -Math.Min(2, vigor.Amount), null, cardPlay.Card);
        }
    }
}
