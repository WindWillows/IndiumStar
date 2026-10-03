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
/// 隐藏效果：<strong>下个回合开始时，失去 1 层易伤</strong>。
/// </summary>
/// <remarks>
/// <para>
/// 「运用-中子」晶逆给自己上易伤时，会额外挂上这个能力，用来把易伤限制在<strong>一个敌人回合</strong>内。
/// 易伤是「受到的伤害增加」，所以留到敌人行动时才真正产生影响；
/// 等到你的下个回合开始，就把它摘掉。
/// </para>
/// <para>
/// 用 <see cref="PowerStackType.Single" /> 让层数恒为 1、不显示数字，
/// 并覆写 <see cref="IsVisibleInternal" /> 让它<strong>完全不出现在能力栏</strong>里
/// （<c>PowerStackType.Single</c> 只管数字显不显示，不管能力本身显不显示）。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class LoseVulnerableAtTurnStartPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>隐藏：不显示在能力栏里。</summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>图标复用原版「易伤」的图标。</summary>
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("vulnerable_power");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(base.Owner))
        {
            // 减 1 层易伤；层数归零时 PowerCmd 会自动移除那个能力，所以这里不用再管它。
            VulnerablePower? vulnerable = base.Owner.GetPower<VulnerablePower>();
            if (vulnerable != null)
            {
                await PowerCmd.Decrement(vulnerable);
            }

            await PowerCmd.Remove(this);
        }
    }
}
