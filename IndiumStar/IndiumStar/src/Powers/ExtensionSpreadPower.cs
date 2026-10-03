using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Mechanics;

namespace IndiumStar.Powers;

/// <summary>
/// 「延拓展延」的能力：回合结束时，你每拥有 1 层延拓，获得 3 点格挡。
/// </summary>
/// <remarks>
/// <para>
/// 延拓层数通过公共入口 <see cref="ExtensionRules.GetAmount" /> 查询，
/// 不直接碰底层能力，以便以后替换延拓的表示方式。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ExtensionSpreadPower : ModPowerTemplate
{
    /// <summary>每层延拓转化的格挡。</summary>
    private int _blockPerExtension = 3;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是配置而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>由卡牌在施加时设置每层延拓的格挡（当前固定为 3）。</summary>
    public void Configure(int blockPerExtension)
    {
        _blockPerExtension = Math.Max(0, blockPerExtension);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner) || base.Owner.Player == null)
        {
            return;
        }

        int extension = ExtensionRules.GetAmount(base.Owner.Player);
        if (extension <= 0)
        {
            return;
        }

        await CreatureCmd.GainBlock(
            base.Owner,
            (decimal)_blockPerExtension * extension,
            ValueProp.Move,
            null);
    }
}
