using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;
using IndiumStar.Mechanics;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-污染H」的能力：你每触发 N 次晶抗，延拓 1 并抽 1 张牌。
/// </summary>
/// <remarks>
/// <para>
/// 计数走公共机制 <see cref="ICrystalBranchListener" />：只有已声明并实际完成的
/// 晶抗分支才会通知，没有晶抗的牌不会产生空计数。进度独立于能力施加的层数，
/// 达到阈值就扣掉阈值并结算，余数保留。
/// </para>
/// <para>
/// 只统计<strong>自己玩家的牌</strong>触发的晶抗；延拓的来源卡记为触发它的那张牌。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscencePollutionHPower : ModPowerTemplate, ICrystalBranchListener
{
    /// <summary>每触发多少次晶抗结算一次。</summary>
    private int _threshold = 4;

    public int Progress { get; private set; }

    public override int DisplayAmount => Progress;

    public override PowerType Type => PowerType.Buff;

    /// <summary>Counter的显示数值由独立进度提供。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>保留最低阈值；重复施加不改变进度。</summary>
    public void Configure(int threshold)
    {
        _threshold = Math.Min(_threshold, Math.Max(1, threshold));
    }

    public async Task AfterCrystalBranchTriggered(
        PlayerChoiceContext choiceContext,
        CrystalBranchContext context,
        CrystalBranch branch)
    {
        if (branch != CrystalBranch.Resistance || context.Card.Owner.Creature != base.Owner)
        {
            return;
        }

        Progress++;
        InvokeDisplayAmountChanged();

        while (Progress >= _threshold)
        {
            Progress -= _threshold;
            InvokeDisplayAmountChanged();
            await ExtensionRules.Gain(choiceContext, base.Owner.Player!, 1, context.Card);
            await CardPileCmd.Draw(choiceContext, 1, base.Owner.Player!);
        }
    }
}
