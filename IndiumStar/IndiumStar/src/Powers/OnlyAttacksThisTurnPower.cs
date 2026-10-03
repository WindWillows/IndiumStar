using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 隐藏效果：<strong>本回合只能打出攻击牌</strong>。
/// </summary>
/// <remarks>
/// <para>
/// 「待发」打出后挂上，到你回合结束时移除。
/// </para>
/// <para>
/// 拦截靠覆写 <see cref="ShouldPlay" />：这是原版用来禁止出牌的通用钩子
/// （原版 <c>SlothPower</c>「不能打出攻击牌」、<c>RingingPower</c>「本回合只能打出 1 张牌」、
/// <c>VelvetChoker</c> 都用它）。它同时管住玩家手动出牌
/// （<c>CardModel.CanPlay</c> → <c>Hook.ShouldPlay</c>）和自动打出
/// （<c>CardCmd.AutoPlay</c> → <c>Hook.ShouldPlay</c>）。
/// </para>
/// <para>
/// 用 <see cref="PowerStackType.Single" /> 让层数恒为 1、不显示数字，
/// 并覆写 <see cref="IsVisibleInternal" /> 让它<strong>完全不出现在能力栏</strong>里。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class OnlyAttacksThisTurnPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>隐藏：不显示在能力栏里。</summary>
    protected override bool IsVisibleInternal => false;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        // 只约束这张能力的主人，别的角色/敌人不受影响。
        if (card.Owner.Creature != base.Owner)
        {
            return true;
        }

        // 自己的回合里只允许攻击牌。
        return card.Type == CardType.Attack;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
