using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class UpdatedSpecificationTests
{
    private static void Check(bool value, string message = "Updated specification assertion failed")
    {
        if (!value) throw new Exception(message);
    }
    private static void Put(Rig rig, CardModel card, PileType pile)
    {
        card.Pile = pile.GetPile(rig.Player); card.Pile.Cards.Add(card);
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (CardType type in Enum.GetValues<CardType>()) foreach (bool upgraded in new[] { false, true })
        {
            var selectedType = type; bool up = upgraded;
            Test($"新版成环按状态和诅咒类别判定 {selectedType} upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<Cyclization>(); if (up) card.Upgrade();
                var selected = new TypedProbe(selectedType) { Owner = r.Player, CombatState = r.State };
                Put(r, selected, PileType.Hand);
                await card.Play(r.Choice, new() { Card = card });
                bool eligible = selectedType is not (CardType.Status or CardType.Curse);
                Check(r.Player.Creature.Block == (eligible ? (up ? 8 : 5) : 0));
                Check(r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == (up ? 3 : 2));
                Check(selected.Pile?.Type == PileType.Exhaust && !r.Player.PlayerCombatState!.Hand.Cards.Contains(selected));
                Check(CardPileCmd.DrawCount == 0);
            });
        }
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            Test($"成环消耗被改送弃牌堆不会得到格挡 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<Cyclization>(); if (up) card.Upgrade();
                var selected = r.Crystal<Reiteration>(); Put(r, selected, PileType.Hand);
                CardCmd.ExhaustDestinationOverride = PileType.Discard;
                await card.Play(r.Choice, new() { Card = card });
                Check(selected.Pile?.Type == PileType.Discard && r.Player.Creature.Block == 0);
                Check(r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == (up ? 3 : 2));
            });
        }
        foreach (int count in new[] { 0, 1, 3 }) foreach (int energy in new[] { 0, 1 })
            foreach (bool upgraded in new[] { false, true })
        {
            int n = count, e = energy; bool up = upgraded;
            Test($"键合三键资源数量与分支 n={n} energy={e} upgraded={up}", async () =>
            {
                var r = new Rig(energy: e); var card = r.Crystal<BondingTripleBond>(); if (up) card.Upgrade();
                for (int i = 0; i < n; i++) Put(r, r.Crystal<CrystalA>(), PileType.Hand);
                if (n > 0) await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, n, null, null);
                await card.Play(r.Choice, new() { Card = card });
                int expected = (up ? 15 : 12) + n * (e == 0 ? (up ? 8 : 6) : (up ? 6 : 4));
                Check(r.Player.Creature.Block == expected);
                Check(CreatureCmd.BlockGains.Count == (n == 0 ? 1 : 2));
                Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == n);
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == n);
                Check(card.EnergyCost.Value == 2 && card.Type == CardType.Skill && card.GainsBlock);
            });
        }
        Test("键合三键晶化只按牌族及本人手牌统计", async () =>
        {
            var r = new Rig(); var card = r.Crystal<BondingTripleBond>();
            Put(r, r.Crystal<CrystalA>(), PileType.Hand); Put(r, r.Crystal<CrystallizationHs>(), PileType.Hand);
            Put(r, new TypedProbe(CardType.Status) { Owner = r.Player, CombatState = r.State }, PileType.Hand);
            Put(r, r.Crystal<CrystalA>(), PileType.Draw); Put(r, r.Crystal<CrystalA>(), PileType.Discard);
            var other = new MegaCrit.Sts2.Core.Entities.Players.Player();
            other.PlayerCombatState!.Hand.Cards.Add(new CrystalA { Owner = other, CombatState = r.State });
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 12, 8 }));
        });
        Test("键合三键以基础格挡钩子之后的能量与晶化数量结算", async () =>
        {
            var r = new Rig(energy: 0); var card = r.Crystal<BondingTripleBond>();
            await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 3, null, null);
            CreatureCmd.AfterBlockGained = (_, _, _) =>
            {
                if (CreatureCmd.BlockGains.Count == 1) { r.Energy = 1; Put(r, r.Crystal<CrystalA>(), PileType.Hand); }
                return Task.CompletedTask;
            };
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 12, 4 }));
        });
        Test("键合三键基础格挡钩子耗尽能量后选择晶逆", async () =>
        {
            var r = new Rig(); var card = r.Crystal<BondingTripleBond>();
            Put(r, r.Crystal<CrystalA>(), PileType.Hand);
            await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 2, null, null);
            CreatureCmd.AfterBlockGained = (_, _, _) => { r.Energy = 0; return Task.CompletedTask; };
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 12, 12 }));
        });
        Test("键合三键量子点先晶抗后晶逆并保留分支开始前的资源变化", async () =>
        {
            var r = new Rig(energy: 0); var card = r.Crystal<BondingTripleBond>();
            Put(r, r.Crystal<CrystalA>(), PileType.Hand);
            await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 1, null, null);
            await PowerCmd.Apply<QuantumDotPower>(r.Choice, r.Player.Creature, 1, null, null);
            r.Record(card);
            CreatureCmd.AfterBlockGained = async (_, _, _) =>
            {
                if (CreatureCmd.BlockGains.Count == 2)
                {
                    r.Energy = 1;
                    await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 1, null, null);
                }
            };
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 12, 4, 12 }));
        });
        Test("键合三键基础和追加格挡分别走敏捷及Ca额度", async () =>
        {
            var r = new Rig(); var card = r.Crystal<BondingTripleBond>();
            Put(r, r.Crystal<CrystalA>(), PileType.Hand); Put(r, r.Crystal<CrystalA>(), PileType.Hand);
            await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, 2, null, null);
            await PowerCmd.Apply<ApplicationCaBlockPower>(r.Choice, r.Player.Creature, 1, null, null);
            await card.Play(r.Choice, new() { Card = card });
            Check(CreatureCmd.BlockGains.SequenceEqual(new[] { 28, 10 }));
            Check(r.Player.Creature.GetPower<ApplicationCaBlockPower>() == null);
        });
        Test("键合三键负敏捷和战斗结束不生成无效格挡", async () =>
        {
            var r = new Rig(); var card = r.Crystal<BondingTripleBond>();
            Put(r, r.Crystal<CrystalA>(), PileType.Hand);
            await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, -100, null, null);
            await card.Play(r.Choice, new() { Card = card });
            Check(r.Player.Creature.Block == 0 && CreatureCmd.BlockGains.Count == 0);
            MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding = true;
            await card.Play(r.Choice, new() { Card = card }); Check(r.Player.Creature.Block == 0);
        });
        Test("更新规格卡牌的双语文本完整且成环排除两种类别", () =>
        {
            foreach (string language in new[] { "zhs", "eng" })
            {
                using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(args[1], "IndiumStar", "localization", language, "cards.json")));
                foreach (string suffix in new[] { ".title", ".description" })
                    Check(document.RootElement.TryGetProperty("INDIUM_STAR_CARD_BONDING_TRIPLE_BOND" + suffix, out _));
                var cycle = document.RootElement.GetProperty("INDIUM_STAR_CARD_CYCLIZATION.description").GetString()!;
                Check(language == "zhs" ? cycle.Contains("状态或诅咒") : cycle.Contains("Status nor a Curse"));
            }
            return Task.CompletedTask;
        });
        Test("回归规格与当前DOCX哈希一致，防止继续使用旧设计", () =>
        {
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(args[1], "..", ".."));
            using var specification = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(root, "卡牌开发", "规格更新_2026-10-05.json")));
            string expected = specification.RootElement.GetProperty("source_sha256").GetString()!;
            string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "晶塔世界角色.docx"))));
            Check(actual.Equals(expected, StringComparison.OrdinalIgnoreCase), "卡牌文档已改变，请先核对规格再运行旧回归");
            return Task.CompletedTask;
        });
        Test("测试卡牌类别枚举及格挡重载与实际游戏DLL一致", () =>
        {
            var directory = System.IO.Path.Combine(args[0], "data_sts2_windows_x86_64");
            var context = new System.Runtime.Loader.AssemblyLoadContext("UpdatedSpecContract", isCollectible: true);
            context.Resolving += (_, name) =>
            {
                string path = System.IO.Path.Combine(directory, name.Name + ".dll");
                return System.IO.File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            try
            {
                var assembly = context.LoadFromAssemblyPath(System.IO.Path.Combine(directory, "sts2.dll"));
                var nativeEnum = assembly.GetType(typeof(CardType).FullName!, true)!;
                Check(Enum.GetNames(nativeEnum).SequenceEqual(Enum.GetNames<CardType>()));
                Check(Enum.GetValues(nativeEnum).Cast<object>().Select(Convert.ToInt32)
                    .SequenceEqual(Enum.GetValues<CardType>().Select(value => (int)value)));
                var command = assembly.GetType("MegaCrit.Sts2.Core.Commands.CreatureCmd", true)!;
                var overload = command.GetMethods().Single(method => method.Name == "GainBlock"
                    && method.GetParameters()[1].ParameterType == typeof(decimal));
                Check(overload.GetParameters().Select(parameter => parameter.Name)
                    .SequenceEqual(new[] { "creature", "amount", "props", "cardPlay", "fast" }));
                Check(overload.ReturnType == typeof(Task<decimal>));
                var hook = assembly.GetType("MegaCrit.Sts2.Core.Models.AbstractModel", true)!.GetMethod("BeforeHandDraw")!;
                Check(hook.ReturnType == typeof(Task) && hook.GetParameters().Select(parameter => parameter.Name)
                    .SequenceEqual(new[] { "player", "choiceContext", "combatState" }));
                var player = assembly.GetType("MegaCrit.Sts2.Core.Entities.Players.Player", true)!;
                Check(player.GetProperty("PlayerCombatState")!.PropertyType.GetProperty("TurnNumber")!.PropertyType == typeof(int));
            }
            finally { context.Unload(); }
            return Task.CompletedTask;
        });
    }
}
