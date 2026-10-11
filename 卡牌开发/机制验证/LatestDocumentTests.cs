using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// 只验本批变化；命令、抽牌与选择为替身，不等于实机效果验收。
public static class LatestDocumentTests
{
    static void Check(bool value) { if (!value) throw new Exception("Latest Word sync assertion failed"); }
    static void Put(Rig r, CardModel card, PileType pile = PileType.Hand)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    static MegaCrit.Sts2.Core.Entities.Creatures.Creature Enemy(Rig r)
    { var e = new MegaCrit.Sts2.Core.Entities.Creatures.Creature { CombatState = r.State }; r.State.CreatureList.Add(e); return e; }

    public static void Register(List<(string Name, Func<Task> Run)> tests)
    {
        void Test(string name, Func<Task> run) => tests.Add(("最新文档：" + name, run));
        foreach (bool value in new[] { false, true })
        {
            bool up = value;
            Test($"寻迹打击伤害及抽牌附加，复制独立 upgraded={up}", async () => {
                var r = new Rig(); var c = r.Crystal<SeekingStrike>(); if (up) c.Upgrade();
                var crystal = r.Crystal<CrystallizationMg>(); Put(r, crystal);
                await c.Play(r.Choice, new() { Card = c, Target = Enemy(r) });
                var copy = (CrystallizationMg)crystal.CreateClone(); copy.AddCrystalDraw(1);
                for (int i = 0; i < 4; i++) Put(r, r.Crystal<TestAttack>(), PileType.Draw);
                await crystal.OnPlayWrapper(r.Choice);
                Check(DamageCmd.Attacks.Single().Damage == (up ? 12 : 10) && crystal.CrystalDrawAmount == (up ? 2 : 1)
                    && copy.CrystalDrawAmount == (up ? 3 : 2) && r.Player.Creature.Block == 4
                    && CardPileCmd.DrawCount == (up ? 2 : 1) && crystal.Pile?.Type == PileType.Exhaust);
            });
        }
        Test("寻迹打击没有晶化仍造成伤害，结束战斗不追加效果", async () => {
            var r = new Rig(); var c = r.Crystal<SeekingStrike>(); var e = Enemy(r);
            await c.Play(r.Choice, new() { Card = c, Target = e });
            var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
            DamageCmd.AfterAttackExecuted = _ => { MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding = true; return Task.CompletedTask; };
            await c.Play(r.Choice, new() { Card = c, Target = e });
            Check(DamageCmd.Attacks.Count == 2 && crystal.CrystalDrawAmount == 0);
        });
        Test("酸酐只打附加文字，涵盖抽弃消耗堆，自动晶化不占额度", async () => {
            var r = new Rig(); Enemy(r); var c = r.Crystal<BondingAnhydride>();
            var chosen = r.Crystal<Crystallization>(); Put(r, chosen);
            var draw = r.Crystal<Crystallization>(); draw.AddCrystalDraw(1); Put(r, draw, PileType.Draw);
            var exhausted = r.Crystal<CrystallizationNb>(); exhausted.AddCrystalExtension(2); Put(r, exhausted, PileType.Exhaust);
            var attack = r.Crystal<TestAttack>(); ApplicationCoPower.MarkForCrystallize(attack, 2); Put(r, attack, PileType.Discard);
            var unchanged = r.Crystal<Crystallization>(); Put(r, unchanged, PileType.Discard);
            var changedNumber = r.Crystal<MacroDendrite>(); changedNumber.DynamicVars.Damage.UpgradeValueBy(99); Put(r, changedNumber, PileType.Discard);
            await c.Play(r.Choice, new() { Card = c });
            Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 3 && chosen.CrystalExtensionAmount == 1
                && new CardModel[] { chosen, draw, exhausted, attack }.All(card =>
                    MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysFinished.Any(h => h.CardPlay.Card == card && h.CardPlay.IsAutoPlay))
                && !MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysFinished.Any(h => h.CardPlay.Card == changedNumber || h.CardPlay.Card == unchanged)
                && unchanged.Pile?.Type == PileType.Discard && unchanged.CanPlayManually);
        });
        Test("酸酐空手仍打出既有文字牌，不拉永久牌组", async () => {
            var r = new Rig(); var c = r.Crystal<BondingAnhydride>();
            var combat = r.Crystal<Crystallization>(); combat.AddCrystalExtension(1); Put(r, combat, PileType.Exhaust);
            var deck = r.Crystal<Crystallization>(); deck.AddCrystalExtension(7); Put(r, deck, PileType.Deck);
            await c.Play(r.Choice, new() { Card = c });
            Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 1 && deck.Pile?.Type == PileType.Deck);
        });
        Test("酸酐识别附魔及Co/U标记，不把普通升级数值或费用当附加文字", () => {
            var r = new Rig(); var c = r.Crystal<SeekingStrike>(); c.Upgrade(); c.EnergyCost.AddThisCombat(-1);
            Check(!AddedCardTextRules.HasAddedText(c));
            c.Enchantment = new EnchantmentModel { DynamicExtraCardText = new() }; Check(AddedCardTextRules.HasAddedText(c));
            c.Enchantment = null; ApplicationUCrystallizePower.MarkForCrystallizeU(c, 1); Check(AddedCardTextRules.HasAddedText(c));
            return Task.CompletedTask;
        });
        Test("附加延拓按出牌时记录，后来加文字不追溯；反演和锡疑识别", async () => {
            var r = new Rig(); Enemy(r); var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
            await crystal.OnPlayWrapper(r.Choice);
            var before = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysFinished.Single().CardPlay;
            crystal.AddCrystalExtension(1); Check(!ExtensionRules.IsExtensionPlay(before));
            var tin = r.Crystal<ReminiscenceTinDoubt>(); Put(r, tin);
            await CardCmd.AutoPlay(r.Choice, crystal, null);
            Check(tin.EnergyCost.Value == 7);
            var inversion = r.Crystal<Inversion>(); await inversion.Play(r.Choice, new() { Card = inversion, Target = r.State.Enemies.First() });
            Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 2);
        });
        Test("滋生状态抽牌链及叠加，生成牌不触发；取消既有未来晶化保留且隔离他人", async () => {
            var r = new Rig(); var c = r.Crystal<Proliferation>(); await c.Play(r.Choice, new() { Card = c });
            var first = r.Crystal<Crystallization>(); Put(r, first, PileType.Draw);
            Put(r, r.Crystal<CrystallizationNb>(), PileType.Draw); Put(r, r.Crystal<TestAttack>(), PileType.Draw);
            await CardPileCmd.Draw(r.Choice, 1, r.Player);
            Check(CardPileCmd.DrawCount == 3 && r.Player.PlayerCombatState!.Hand.Cards.Count == 3 && !first.Keywords.Contains(CardKeyword.Retain));
            var generated = r.Crystal<Crystallization>(); await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, r.Player);
            Check(CardPileCmd.DrawCount == 3 && !generated.Keywords.Contains(CardKeyword.Retain));
            var other = r.Crystal<Crystallization>(); other.Owner = new(); Check(other.Keywords.Contains(CardKeyword.Retain));
            await c.Play(r.Choice, new() { Card = c }); Check(r.Player.Creature.GetPowerAmount<ProliferationPower>() == 2);
        });
        Test("渊河5/7升级、滋生固有升级、酸酐未定义升级及辰海退出奖励稀有度", () => {
            var r = new Rig(); var river = r.Crystal<AbyssalRiver>(); Check(river.DynamicVars.Damage.BaseValue == 5); river.Upgrade();
            var c = r.Crystal<Proliferation>(); Check(!c.Keywords.Contains(CardKeyword.Innate)); c.Upgrade();
            Check(river.DynamicVars.Damage.BaseValue == 7 && c.Keywords.Contains(CardKeyword.Innate)
                && !r.Crystal<BondingAnhydride>().IsUpgradable && r.Crystal<StellarSea>().Type == CardType.Attack
                && r.Crystal<StellarSea>().EnergyCost.Value == 1);
            return Task.CompletedTask;
        });
    }
}
