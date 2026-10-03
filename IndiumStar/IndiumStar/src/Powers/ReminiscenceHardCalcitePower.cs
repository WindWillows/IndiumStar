using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-硬钙石」的能力：回合结束时，若你本回合消耗过牌，
/// 对所有敌人造成 N × 手牌中晶化数量的伤害。
/// </summary>
/// <remarks>
/// <para>
/// 「本回合消耗过牌」查本体出牌历史里的 <see cref="CardExhaustedEntry" />
/// （按本回合、本玩家过滤），不自己维护标记。
/// </para>
/// <para>
/// 「晶化数」按<strong>手牌</strong>统计（与「碎晶雨」「构象打击」的口径一致；
/// 规格文档此处原本待确认，已按手牌落地）。
/// </para>
/// <para>
/// 全体伤害参照原版「炸弹」（<c>TheBombPower</c>）：
/// <c>CreatureCmd.Damage</c> 直接打 <c>CombatState.HittableEnemies</c>，
/// 伤害不吃力量（<c>ValueProp.Unpowered</c>）。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscenceHardCalcitePower : ModPowerTemplate
{
    /// <summary>每张手牌晶化造成的伤害。</summary>
    private int _damagePerCrystal = 3;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是配置而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>由卡牌在施加时设置每张晶化的伤害（升级会提高）。</summary>
    public void Configure(int damagePerCrystal)
    {
        _damagePerCrystal = Math.Max(0, damagePerCrystal);
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

        ICombatState? combatState = base.Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        bool exhaustedThisTurn = CombatManager.Instance.History.Entries
            .OfType<CardExhaustedEntry>()
            .Any(entry => entry.HappenedThisTurn(combatState) && entry.Actor == base.Owner);
        if (!exhaustedThisTurn)
        {
            return;
        }

        int crystals = PileType.Hand
            .GetPile(base.Owner.Player)
            .Cards.Count(CrystallizationRules.IsCrystallizationCard);
        decimal totalDamage = (decimal)_damagePerCrystal * crystals;
        if (totalDamage <= 0m)
        {
            return;
        }

        await CreatureCmd.Damage(
            choiceContext,
            combatState.HittableEnemies,
            new MegaCrit.Sts2.Core.Localization.DynamicVars.DamageVar(
                totalDamage, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered),
            base.Owner);
    }
}
