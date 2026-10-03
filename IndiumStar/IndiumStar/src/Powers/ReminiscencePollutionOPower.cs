using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-污染O」的能力：每当你打出「晶化」时，按出牌时选中的分支获得格挡或活力。
/// </summary>
/// <remarks>
/// <para>
/// 这张能力牌的效果由晶抗/晶逆在<strong>打出时</strong>决定：晶抗挂上格挡触发器，
/// 晶逆挂上活力触发器；双分支（如量子点）则两个都挂。
/// 两个收益是独立字段，同一个能力实例可以同时具备。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscencePollutionOPower : ModPowerTemplate
{
    /// <summary>每打出一张晶化获得的格挡；0 表示晶抗没有选中。</summary>
    private int _blockPerCrystal;

    /// <summary>每打出一张晶化获得的活力；0 表示晶逆没有选中。</summary>
    private int _vigorPerCrystal;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是配置而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>由卡牌的晶抗/晶逆在打出时调用；重复施加累加配置而不是层数。</summary>
    public void Configure(int blockPerCrystal, int vigorPerCrystal)
    {
        _blockPerCrystal = Math.Max(_blockPerCrystal, blockPerCrystal);
        _vigorPerCrystal = Math.Max(_vigorPerCrystal, vigorPerCrystal);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is not ICrystallizationCard || cardPlay.Card.Owner.Creature != base.Owner)
        {
            return;
        }

        if (_blockPerCrystal > 0)
        {
            await CreatureCmd.GainBlock(base.Owner, _blockPerCrystal, ValueProp.Move, cardPlay);
        }

        if (_vigorPerCrystal > 0)
        {
            await PowerCmd.Apply<VigorPower>(
                choiceContext,
                base.Owner,
                _vigorPerCrystal,
                base.Owner,
                null);
        }
    }
}
