using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class CrystalPunishmentTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Crystal Punishment assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile = PileType.Hand) {
        card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card);
    }
    private static Creature Enemy(Rig r) { var e = new Creature { CombatState = r.State }; r.State.CreatureList.Add(e); return e; }
    private static CrystallizationCard Crystal(Rig r, Type type) {
        var c = (CrystallizationCard)Activator.CreateInstance(type)!; c.Owner = r.Player; c.CombatState = r.State; return c;
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        var variants = new[] { typeof(Crystallization), typeof(CrystallizationMg), typeof(CrystallizationNb),
            typeof(CrystallizationHs), typeof(CrystallizationPb), typeof(CrystallizationU) };
        foreach (Type variant in variants) foreach (bool upgraded in new[] { false, true }) {
            Type type = variant; bool up = upgraded;
            Test($"晶罚覆盖晶化变体并保留原有效果 type={type.Name} upgraded={up}", async () => {
                var r = new Rig(energy: 2); var target = Enemy(r); var c = r.Crystal<CrystalPunishment>(); if (up) c.Upgrade();
                var crystal = Crystal(r, type); Put(r, crystal);
                var affliction = new IndiumStar.Afflictions.CrystallizeUAffliction(); crystal.Affliction = affliction;
                await c.OnPlayWrapper(r.Choice, target);
                Check(DamageCmd.Attacks.Single().Damage == 9 && c.EnergyCost.Value == 1 && c.Type == CardType.Attack
                    && !c.Keywords.Contains(CardKeyword.Exhaust));
                Check(crystal.CrystalDoomAmount == (up ? 4 : 3) && crystal.TargetType == TargetType.AnyEnemy
                    && ReferenceEquals(crystal.Affliction, affliction));
                Check(crystal.Keywords.Contains(CardKeyword.Retain) && crystal.Keywords.Contains(CardKeyword.Exhaust)
                    && (type != typeof(CrystallizationPb) || crystal.Keywords.Contains(CardKeyword.Ethereal)));
                DerivedApplicationNeutron? neutron = null;
                if (type == typeof(CrystallizationU)) {
                    neutron = r.Crystal<DerivedApplicationNeutron>(); neutron.Upgrade(); Put(r, neutron, PileType.Exhaust);
                }
                await crystal.OnPlayWrapper(r.Choice, target);
                Check(target.GetPowerAmount<DoomPower>() == (up ? 4 : 3) && r.Player.Creature.GetPowerAmount<DoomPower>() == 0);
                Check(PowerCmd.LastSource == crystal && PowerCmd.LastApplier == r.Player.Creature);
                if (type == typeof(CrystallizationMg)) Check(r.Player.Creature.Block == 4);
                if (type == typeof(CrystallizationNb)) Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 3);
                if (neutron != null) Check(neutron.Pile?.Type == PileType.Hand && neutron.Keywords.Contains(CardKeyword.Exhaust));
                Check(crystal.Pile?.Type == PileType.Exhaust);
            });
        }
        Test("晶罚只增强伤害结算后本人的手牌晶化", async () => {
            var r = new Rig(); var target = Enemy(r); var c = r.Crystal<CrystalPunishment>();
            var left = r.Crystal<Crystallization>(); Put(r, left);
            var drawn = r.Crystal<Crystallization>(); Put(r, drawn, PileType.Draw);
            var discarded = r.Crystal<Crystallization>(); Put(r, discarded, PileType.Discard);
            var exhausted = r.Crystal<Crystallization>(); Put(r, exhausted, PileType.Exhaust);
            var ordinaryStatus = new TypedProbe(CardType.Status) { Owner = r.Player, CombatState = r.State }; Put(r, ordinaryStatus);
            var peer = new Player(); var peerCrystal = new Crystallization { Owner = peer, CombatState = r.State };
            peer.PlayerCombatState!.Hand.Cards.Add(peerCrystal);
            Crystallization? generated = null;
            DamageCmd.AfterAttackExecuted = async command => {
                Check(left.CrystalDoomAmount == 0 && command.ModelSource == c);
                await CardPileCmd.Add(left, PileType.Discard);
                generated = r.Crystal<Crystallization>(); Put(r, generated);
            };
            await c.OnPlayWrapper(r.Choice, target);
            Check(generated!.CrystalDoomAmount == 3 && new[] { left, drawn, discarded, exhausted, peerCrystal }.All(x => x.CrystalDoomAmount == 0));
            Check(ordinaryStatus.TargetType == TargetType.Self);
        });
        Test("晶罚普通升级重复添加层数累加，复制独立，新牌不继承", async () => {
            var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
            var normal = r.Crystal<CrystalPunishment>(); var upgraded = r.Crystal<CrystalPunishment>(); upgraded.Upgrade();
            await normal.OnPlayWrapper(r.Choice, target); await upgraded.OnPlayWrapper(r.Choice, target);
            Check(crystal.CrystalDoomAmount == 7);
            var copy = (Crystallization)crystal.CreateClone(); copy.AddCrystalDoom(4);
            Check(copy.CrystalDoomAmount == 11 && crystal.CrystalDoomAmount == 7);
            Check(r.Crystal<Crystallization>().CrystalDoomAmount == 0);
            await crystal.OnPlayWrapper(r.Choice, target);
            Check(target.GetPowerAmount<DoomPower>() == 7);
        });
        foreach (bool automatic in new[] { false, true }) {
            bool auto = automatic;
            Test($"晶罚强化晶化自动与重放共用效果和手动额度 auto={auto}", async () => {
                var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>();
                crystal.AddCrystalDoom(3); crystal.InitialPlayCount = 2; Put(r, crystal);
                if (auto) await CardCmd.AutoPlay(r.Choice, crystal, null); else await crystal.OnPlayWrapper(r.Choice, target);
                Check(target.GetPowerAmount<DoomPower>() == 6 && PowerCmd.ApplyCount == 2);
                var next = r.Crystal<Crystallization>();
                Check(CrystallizationRules.HasPlayedCrystallizationThisTurn(next) == !auto);
            });
        }
        Test("晶罚强化晶化被消耗不会触发消亡", async () => {
            var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); crystal.AddCrystalDoom(3); Put(r, crystal);
            await CardCmd.Exhaust(r.Choice, crystal);
            Check(crystal.Pile?.Type == PileType.Exhaust && target.GetPowerAmount<DoomPower>() == 0);
        });
        Test("晶罚强化晶化被禁止自动打出不会施加消亡", async () => {
            var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); crystal.AddCrystalDoom(3); Put(r, crystal);
            CardCmd.ShouldAutoPlay = _ => false;
            await CardCmd.AutoPlay(r.Choice, crystal, null);
            Check(target.GetPowerAmount<DoomPower>() == 0 && PowerCmd.ApplyCount == 0);
        });
        foreach (int endMode in new[] { 0, 1 }) {
            int mode = endMode;
            Test($"晶罚伤害结束战斗或本人死亡停止增强 mode={mode}", async () => {
                var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
                DamageCmd.AfterAttackExecuted = _ => { if (mode == 0) CombatManager.Instance.IsOverOrEnding = true;
                    else r.Player.Creature.IsAlive = false; return Task.CompletedTask; };
                await r.Crystal<CrystalPunishment>().OnPlayWrapper(r.Choice, target);
                Check(crystal.CrystalDoomAmount == 0);
            });
        }
        foreach (int invalidMode in new[] { 0, 1, 2, 3 }) {
            int mode = invalidMode;
            Test($"晶罚强化晶化排除死亡友军及异战斗目标 mode={mode}", async () => {
                var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); crystal.AddCrystalDoom(3);
                if (mode == 0) target.IsAlive = false;
                if (mode == 1) target = r.Player.Creature;
                if (mode == 2) target.CombatState = new FakeCombat();
                if (mode == 3) CombatManager.Instance.IsOverOrEnding = true;
                await crystal.Play(r.Choice, new() { Card = crystal, Target = target });
                Check(PowerCmd.ApplyCount == 0);
            });
        }
        Test("晶罚强化晶化先结算原效果，目标死亡不追加消亡", async () => {
            var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<CrystallizationMg>(); crystal.AddCrystalDoom(3);
            CreatureCmd.AfterBlockGained = (_, _, _) => { target.IsAlive = false; return Task.CompletedTask; };
            await crystal.Play(r.Choice, new() { Card = crystal, Target = target });
            Check(r.Player.Creature.Block == 4 && PowerCmd.ApplyCount == 0);
        });
        Test("晶罚空手仅攻击，晶化消亡数值无额外上限且防溢出", async () => {
            var r = new Rig(); var target = Enemy(r); var c = r.Crystal<CrystalPunishment>();
            await c.OnPlayWrapper(r.Choice, target); Check(DamageCmd.Attacks.Count == 1 && PowerCmd.ApplyCount == 0);
            var crystal = r.Crystal<Crystallization>(); crystal.AddCrystalDoom(int.MaxValue); crystal.AddCrystalDoom(3);
            Check(crystal.CrystalDoomAmount == int.MaxValue); crystal.CrystalDoomAmount = -1;
            Check(crystal.CrystalDoomAmount == 0 && crystal.TargetType == TargetType.Self);
        });
        Test("晶罚击杀当前目标但战斗未结束仍增强手牌", async () => {
            var r = new Rig(); var target = Enemy(r); Enemy(r);
            var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
            DamageCmd.AfterAttackExecuted = _ => { target.IsAlive = false; return Task.CompletedTask; };
            await r.Crystal<CrystalPunishment>().OnPlayWrapper(r.Choice, target);
            Check(crystal.CrystalDoomAmount == 3);
        });
        foreach (bool upgraded in new[] { false, true }) {
            bool up = upgraded;
            Test($"晶罚系列重放每段造成伤害并累加附加效果 upgraded={up}", async () => {
                var r = new Rig(); var target = Enemy(r); var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
                var c = r.Crystal<CrystalPunishment>(); if (up) c.Upgrade(); c.InitialPlayCount = 2;
                await c.OnPlayWrapper(r.Choice, target);
                Check(DamageCmd.Attacks.Count == 2 && DamageCmd.Attacks.All(a => a.Damage == 9)
                    && crystal.CrystalDoomAmount == (up ? 8 : 6));
            });
        }
    }
}
