using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class SemanticAuditTests {
    private static void Check(bool condition) { if (!condition) throw new Exception("assertion failed"); }
    public static void Register(List<(string Name, Func<Task> Run)> tests) {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (int energy in new[] { 0, 1 }) {
            int e = energy;
            Test($"中子重复施加只使用原版状态，无隐藏清除器 energy={e}", async () => {
                var r = new Rig(energy: e); var card = r.Crystal<ApplicationNeutron>();
                var target = new Creature { CombatState = r.State };
                await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, 3, null, null);
                await PowerCmd.Apply<VulnerablePower>(r.Choice, r.Player.Creature, 3, null, null);
                await card.Play(r.Choice, new() { Card = card, Target = target });
                await card.Play(r.Choice, new() { Card = card, Target = target });
                Check(r.Player.Creature.GetPowerAmount<WeakPower>() == (e == 1 ? 5 : 3));
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == (e == 0 ? 5 : 3));
                Check(!r.Player.Creature.Powers.Any(p => p.GetType().Name.StartsWith("Lose")));
            });
        }
        Test("反演借用随机攻击不推进原敌人的RNG", async () => {
            var r = new Rig(); var target = new Creature { CombatState = r.State };
            var monster = new ProbeEnemy { Creature = target, Intent = ProbeEnemy.Mode.RandomAttack };
            target.Monster = monster; monster.SetUpForCombat();
            var before = monster.Rng.ToSerializable();
            var card = r.Crystal<Inversion>();
            await card.Play(r.Choice, new() { Card = card, Target = target });
            Check(monster.Rng.ToSerializable() == before && monster.ExecutionCount == 0 && DamageCmd.Attacks.Count == 2);
        });
        Test("Co标记复制继承且回合结束清除原件与复制", async () => {
            var r = new Rig(); var source = r.Crystal<MarkedApplication>();
            ApplicationCoPower.MarkForCrystallize(source, 2);
            var copy = source.CreateClone(); r.Player.PlayerCombatState!.DrawPile.Cards.Add(copy);
            r.Player.PlayerCombatState.Hand.Cards.Add(source);
            var power = new ApplicationCoPower { Owner = r.Player.Creature };
            await power.BeforeCardPlayed(new() { Card = copy });
            Check(r.Player.PlayerCombatState.Hand.Cards.OfType<Crystallization>().Count() == 2);
            await power.BeforeSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            await power.BeforeCardPlayed(new() { Card = source }); await power.BeforeCardPlayed(new() { Card = copy });
            Check(r.Player.PlayerCombatState.Hand.Cards.OfType<Crystallization>().Count() == 2);
        });
        Test("U标记复制生成晶化U且不会修改未标记牌", async () => {
            var r = new Rig(); var source = r.Crystal<MarkedApplication>();
            ApplicationUCrystallizePower.MarkForCrystallizeU(source, 1);
            var copy = source.CreateClone(); var plain = r.Crystal<MarkedApplication>().CreateClone();
            var power = new ApplicationUCrystallizePower { Owner = r.Player.Creature };
            await power.BeforeCardPlayed(new() { Card = copy }); await power.BeforeCardPlayed(new() { Card = plain });
            Check(r.Player.PlayerCombatState!.Hand.Cards.Single() is CrystallizationU);
        });
        Test("免费运用两份额度不会被一张牌的系列重放耗尽", async () => {
            var r = new Rig(); var power = await PowerCmd.Apply<FreeApplicationPower>(r.Choice, r.Player.Creature, 2, null, null);
            var card = r.Crystal<MarkedApplication>(); card.Pile = new() { Type = PileType.Play };
            Check(power.TryModifyEnergyCostInCombatLate(card, 3, out decimal cost) && cost == 0);
            await power.BeforeCardPlayed(new() { Card = card });
            await power.BeforeCardPlayed(new() { Card = card, PlayIndex = 1 });
            Check(power.Amount == 1);
            await power.BeforeCardPlayed(new() { Card = card }); Check(r.Player.Creature.GetPower<FreeApplicationPower>() == null);
        });
        Test("Ca两份翻倍额度分别作用于分影连气的前两次格挡", async () => {
            var r = new Rig(); await PowerCmd.Apply<ApplicationCaBlockPower>(r.Choice, r.Player.Creature, 2, null, null);
            var card = r.Crystal<ShadowChainBreath>();
            for (int i = 0; i < 3; i++) r.Player.PlayerCombatState!.Hand.Cards.Add(r.Crystal<ShadowChainBreath>());
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 6, 6, 3 }));
            Check(r.Player.Creature.GetPower<ApplicationCaBlockPower>() == null && new ApplicationCa().GainsBlock);
        });
        foreach (PileType pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust }) {
            var p = pile;
            Test($"锡疑在{p}也获得延拓牌费用减免且不重复计算重放", async () => {
                var r = new Rig(); var card = r.Crystal<ReminiscenceTinDoubt>(); card.Upgrade(); card.Pile = p.GetPile(r.Player);
                var extension = r.Crystal<MarkedExtension>();
                await card.AfterCardPlayed(r.Choice, new() { Card = extension });
                await card.AfterCardPlayed(r.Choice, new() { Card = extension, PlayIndex = 1 });
                Check(card.EnergyCost.Value == 6);
            });
        }
        foreach (bool upgraded in new[] { false, true }) {
            bool up = upgraded;
            Test($"碎晶雨晶逆保留主伤害的多段模式 upgraded={up}", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<ShatterCrystalRain>(); if (up) card.Upgrade();
                var first = new Creature { CombatState = r.State }; var other = new Creature { CombatState = r.State };
                r.State.CreatureList.AddRange([first, other]);
                await card.Play(r.Choice, new() { Card = card, Target = first });
                Check(DamageCmd.Attacks.Count == 2 && DamageCmd.Attacks.All(a => a.Damage == 3 && a.Hits == (up ? 4 : 3)));
            });
        }
        Test("崩碎之歌不把失败消耗计入伤害段数", async () => {
            var r = new Rig(); CardCmd.ExhaustBlocked = true;
            r.Player.PlayerCombatState!.Hand.Cards.Add(r.Crystal<CrystalA>());
            var song = r.Crystal<ShatterSong>(); await song.Play(r.Choice, new() { Card = song, Target = new() });
            Check(DamageCmd.Attacks.Count == 0);
        });
        Test("晶化U只回收已升级中子，不额外升级普通中子", async () => {
            var r = new Rig(); var plain = r.Crystal<ApplicationNeutron>(); plain.Pile = PileType.Draw.GetPile(r.Player); plain.Pile.Cards.Add(plain);
            var upgraded = r.Crystal<ApplicationNeutron>(); upgraded.Upgrade(); upgraded.Pile = PileType.Discard.GetPile(r.Player); upgraded.Pile.Cards.Add(upgraded);
            var crystal = r.Crystal<CrystallizationU>(); await crystal.Play(r.Choice, new() { Card = crystal });
            Check(plain.Pile.Type == PileType.Draw && !plain.IsUpgraded && upgraded.Pile.Type == PileType.Hand);
        });
        Test("Hs光环不扣回未发放给不能接收能力单位的贡献", async () => {
            var r = new Rig(); var enemy = new Creature { CombatState = r.State, CanReceivePowers = false }; r.State.CreatureList.Add(enemy);
            await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
            Check(enemy.GetPowerAmount<StrengthPower>() == 0);
            r.Player.PlayerCombatState!.Hand.Cards.Clear(); enemy.CanReceivePowers = true;
            await r.Player.Creature.GetPower<CrystalHandAuraPower>()!.Sync(); Check(enemy.GetPowerAmount<StrengthPower>() == 0);
        });
        Test("Hs光环能在单位恢复可接收能力后补发与正确收回", async () => {
            var r = new Rig(); var enemy = new Creature { CombatState = r.State, CanReceivePowers = false }; r.State.CreatureList.Add(enemy);
            await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
            enemy.CanReceivePowers = true; var aura = r.Player.Creature.GetPower<CrystalHandAuraPower>()!; await aura.Sync();
            Check(enemy.GetPowerAmount<StrengthPower>() == 1);
            r.Player.PlayerCombatState!.Hand.Cards.Clear(); await aura.Sync(); Check(enemy.GetPowerAmount<StrengthPower>() == 0);
        });
    }
}
