using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 隐藏效果：<strong>在本回合结束时，失去 1 层虚弱</strong>。
/// </summary>
/// <remarks>
/// <para>
/// 「运用-中子」晶抗给自己上虚弱时，会额外挂上这个能力，用来把虚弱限制在<strong>本回合</strong>内。
/// 虚弱是「减少自己造成的伤害」，而它是在<strong>敌人回合</strong>才生效的
/// （<see cref="WeakPower.AfterSideTurnEnd" /> 只在 <c>CombatSide.Enemy</c> 时递减），
/// 所以本回合结束时主动摘掉，就能让这次虚弱只影响「敌人下一次行动前你自己打出的伤害」。
/// </para>
/// <para>
/// 用 <see cref="PowerStackType.Single" /> 让层数恒为 1、不显示数字，
/// 并覆写 <see cref="IsVisibleInternal" /> 让它<strong>完全不出现在能力栏</strong>里
/// （<c>PowerStackType.Single</c> 只管数字显不显示，不管能力本身显不显示）。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class LoseWeakAtTurnEndPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>隐藏：不显示在能力栏里。</summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>图标复用原版「虚弱」的图标。</summary>
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("weak_power");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            // 减 1 层虚弱；层数归零时 PowerCmd 会自动移除那个能力，所以这里不用再管它。
            WeakPower? weak = base.Owner.GetPower<WeakPower>();
            if (weak != null)
            {
                await PowerCmd.Decrement(weak);
            }

            await PowerCmd.Remove(this);
        }
    }
}
