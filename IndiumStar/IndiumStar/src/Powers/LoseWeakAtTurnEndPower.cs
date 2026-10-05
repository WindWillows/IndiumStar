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
/// 旧版本隐藏清除器：本回合结束时失去1层虚弱。
/// 当前运用-电子已不再施加；类型保留用于旧数据兼容。
/// </summary>
/// <remarks>
/// <para>
/// 虚弱会立即降低攻击伤害；原版在敌人回合结束时递减不代表它只在敌人回合生效。
/// 旧版额外清除规则没有卡牌文档依据，且不能区分新施加与既有层数，故不再使用。
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
