using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Mechanics;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-磷语」的能力：每回合第一次获得延拓时，改为获得等量能量（升级版为同时获得）。
/// </summary>
/// <remarks>
/// <para>
/// 拦截点在延拓公共入口 <c>ExtensionRules.Gain</c>（通过
/// <see cref="IExtensionGainInterceptor" /> 解耦，公共入口不认识具体能力）。
/// 每回合的第一次有效：返回 true 表示延拓被替换成能量（基础版）；
/// 升级版返回 false，能量照发、延拓也照常获得。
/// </para>
/// <para>
/// 「每回合第一次」在拥有者的回合开始时重置。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscencePhosphorusPower : ModPowerTemplate, IExtensionGainInterceptor
{
    /// <summary>本回合是否已经拦截过一次。</summary>
    private bool _usedThisTurn;

    /// <summary>升级版：同时获得能量和延拓，而不是替换。</summary>
    private bool _grantBoth;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>由卡牌在施加时设置：升级版为同时获得。</summary>
    public void Configure(bool grantBoth)
    {
        _grantBoth = grantBoth;
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        ConfigureFromCard(cardSource);
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount,
        Creature target, Creature? applier, CardModel? cardSource)
    {
        if (power == this && target == Owner) ConfigureFromCard(cardSource);
        return Task.CompletedTask;
    }

    private void ConfigureFromCard(CardModel? source)
    {
        if (source is ReminiscencePhosphorus card) Configure(card.IsUpgraded);
    }

    public async Task<bool> TryConvertExtensionToEnergy(
        PlayerChoiceContext choiceContext,
        Player player,
        int amount)
    {
        if (_usedThisTurn)
        {
            return false;
        }

        // 能量修改钩子可以等待或嵌套重施磷语；本次按进入拦截时的模式结算。
        bool grantBoth = _grantBoth;
        _usedThisTurn = true;
        await PlayerCmd.GainEnergy(amount, player);

        // 同时模式：能量照发，延拓也照常获得（不替换）。
        return !grantBoth;
    }

    /// <summary>拥有者的回合开始时重置「每回合第一次」。</summary>
    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(base.Owner))
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }
}
