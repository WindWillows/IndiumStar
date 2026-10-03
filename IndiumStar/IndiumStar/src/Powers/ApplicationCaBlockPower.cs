using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 运用-Ca 的「下一次从卡牌中获得的格挡翻倍」。
/// <para>一次性：生效后立刻移除自己。</para>
/// </summary>
/// <remarks>
/// <para>
/// 用 <see cref="ModifyBlockMultiplicative" /> 返回 2 倍，和原版「影融」
/// （<c>ShadowmeldPower</c>）的做法一致。
/// </para>
/// <para>
/// <b>和「影融」的关键区别是"只翻倍一次"：</b>「影融」是整个回合都翻倍、回合结束才移除；
/// 这里是"下一次"，所以在 <see cref="AfterModifyingBlockAmount" /> 里把自己减掉一层
/// ——那个钩子<strong>只会对真正修改了数值的能力触发</strong>
/// （<c>Hook.cs:1332-1350</c> 把改动过数值的能力收集进 <c>modifiers</c>，
/// 然后 <c>Hook.cs:661-671</c> 只回调这些），所以不会误伤无关的格挡。
/// </para>
/// <para>
/// <b>判定"从卡牌中获得"</b>用 <c>cardSource != null</c>：
/// 卡牌给的格挡一定带 <c>cardSource</c>，怪物动作 / 遗物等不带。
/// </para>
/// <para>
/// 图标暂时借用原版「敏捷」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationCaBlockPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("dexterity_power");

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!Applies(target, props, cardSource))
        {
            return 1m;
        }

        // 每层翻一倍。
        // Math.Pow 返回 double，必须显式转成 decimal（原版 ShadowmeldPower 也是这么转的）。
        return (decimal)Math.Pow(2.0, base.Amount);
    }

    /// <summary>用掉一次：这个钩子只对真正改了数值的能力触发。</summary>
    public override async Task AfterModifyingBlockAmount(
        decimal modifiedAmount,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        await PowerCmd.Decrement(this);
    }

    /// <summary>是不是该由这个能力来翻倍。</summary>
    private bool Applies(Creature target, ValueProp props, CardModel? cardSource)
        => base.Owner == target
            && props.IsPoweredCardOrMonsterMoveBlock()
            && cardSource != null;
}
