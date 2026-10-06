using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Patches;
using IndiumStar.Powers;
using IndiumStar.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class StartupRelicTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Startup/relic assertion failed"); }
    private static DefectiveCrystalCell Add(Rig r)
    { var relic = new DefectiveCrystalCell { Owner = r.Player }; r.State.Listeners.Add(relic); return relic; }
    private static readonly MethodInfo OnPlay = typeof(CardModel).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static int _wrappedCalls;
    private static Task WrappedPlay(CardModel card, PlayerChoiceContext context, CardPlay play)
    { _wrappedCalls++; return (Task)OnPlay.Invoke(card, [context, play])!; }
    private static IEnumerable<CodeInstruction> ExternalWrapper(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions) {
            if (instruction.Calls(OnPlay)) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(StartupRelicTests).GetMethod(nameof(WrappedPlay), BindingFlags.Static | BindingFlags.NonPublic)!;
            }
            yield return instruction;
        }
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests)
    {
        void Test(string name, Func<Task> run) => tests.Add(("启动修复：" + name, run));
        Test("重放补丁兼容第三方OnPlay包装且保留两段原生系列", async () => {
            var foreign = new Harmony("IndiumStar.Test.ExternalWrapper");
            var target = CrystalReplaySeriesPatch.TargetMethod();
            try {
                foreign.Patch(target, transpiler: new HarmonyMethod(typeof(StartupRelicTests).GetMethod(nameof(ExternalWrapper), BindingFlags.Static | BindingFlags.NonPublic)!));
                var r = new Rig(energy:0); var card = r.Crystal<CrystalFlow>(); _wrappedCalls=0;
                await card.OnPlayWrapper(r.Choice, new() { CombatState=r.State });
                Check(_wrappedCalls==2 && CreatureCmd.BlockGains.Sum()==10 && card.FinishedCount==2);
            } finally { foreign.Unpatch(target, HarmonyPatchType.All, foreign.Id); }
        });
        Test("缺陷晶胞实际获得延拓只奖励一次", async () => {
            var r=new Rig(energy:0); var relic=Add(r);
            await ExtensionRules.Gain(r.Choice,r.Player,2,r.Card);
            await ExtensionRules.Gain(r.Choice,r.Player,3,r.Card);
            Check(r.Energy==1 && relic.TriggeredThisCombat && ExtensionRules.GetAmount(r.Player)==5 && relic.FlashCount==1);
        });
        Test("缺陷晶胞跨回合不重置，新战斗重置", async () => {
            var r=new Rig(energy:0); var relic=Add(r);
            await ExtensionRules.Gain(r.Choice,r.Player,1,r.Card); r.State.Turn++;
            await ExtensionRules.Gain(r.Choice,r.Player,1,r.Card); Check(r.Energy==1);
            await relic.BeforeCombatStart(); await ExtensionRules.Gain(r.Choice,r.Player,1,r.Card); Check(r.Energy==2);
        });
        Test("缺陷晶胞零、受阻或负层数不消费资格", async () => {
            var r=new Rig(energy:0); var relic=Add(r);
            await ExtensionRules.Gain(r.Choice,r.Player,0,r.Card);
            PowerCmd.ModifyAmountReceived=(_,_)=>0;
            await ExtensionRules.Gain(r.Choice,r.Player,1,r.Card);
            await relic.AfterPowerAmountChanged(r.Choice,new EnergyNextTurnPower { Owner=r.Player.Creature,Amount=1 },-1,null,null);
            Check(!relic.TriggeredThisCombat && r.Energy==0);
        });
        Test("缺陷晶胞排除他人、其他能力、死亡及战斗结束", async () => {
            var r=new Rig(energy:0); var relic=Add(r); var other=new Player();
            await relic.AfterPowerAmountChanged(r.Choice,new EnergyNextTurnPower { Owner=other.Creature,Amount=1 },1,null,null);
            await relic.AfterPowerAmountChanged(r.Choice,new StrengthPower { Owner=r.Player.Creature,Amount=1 },1,null,null);
            r.Player.Creature.IsAlive=false;
            await relic.AfterPowerAmountChanged(r.Choice,new EnergyNextTurnPower { Owner=r.Player.Creature,Amount=1 },1,null,null);
            r.Player.Creature.IsAlive=true; MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding=true;
            await relic.AfterPowerAmountChanged(r.Choice,new EnergyNextTurnPower { Owner=r.Player.Creature,Amount=1 },1,null,null);
            Check(!relic.TriggeredThisCombat && r.Energy==0);
        });
        Test("缺陷晶胞能量钩子嵌套获得延拓不会再次奖励", async () => {
            var r=new Rig(energy:0); var relic=Add(r);
            PlayerCmd.AfterModifyingEnergyGain=(_,_)=>ExtensionRules.Gain(r.Choice,r.Player,2,r.Card);
            await ExtensionRules.Gain(r.Choice,r.Player,1,r.Card);
            Check(r.Energy==1 && ExtensionRules.GetAmount(r.Player)==3 && relic.FlashCount==1);
        });
        foreach (bool upgraded in new[] { false,true }) {
            bool mode=upgraded;
            Test("缺陷晶胞磷语转换仅实际延拓触发 upgrade="+mode,async()=>{
                var r=new Rig(energy:0); var relic=Add(r); var card=r.Crystal<ReminiscencePhosphorus>();
                if(mode) card.Upgrade(); await ReminiscencePhosphorusPower.ApplyFromCard(r.Choice,card);
                await ExtensionRules.Gain(r.Choice,r.Player,2,r.Card);
                Check(r.Energy==(mode?3:2) && relic.TriggeredThisCombat==mode && ExtensionRules.GetAmount(r.Player)==(mode?2:0));
            });
        }
    }
}

namespace MegaCrit.Sts2.Core.Entities.Relics { public enum RelicRarity { None,Starter,Common,Uncommon,Rare } }
namespace MegaCrit.Sts2.Core.Models
{
    public abstract class RelicModel : AbstractModel
    {
        public Player Owner { get; set; }=null!;
        public virtual MegaCrit.Sts2.Core.Entities.Relics.RelicRarity Rarity=>default;
        protected virtual IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars=>[];
        public MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet DynamicVars=>new(CanonicalVars);
        public int FlashCount { get; private set; }
        protected void Flash()=>FlashCount++;
    }
}
namespace STS2RitsuLib.Scaffolding.Content
{
    public record RelicAssetProfile(string Path);
    public abstract class ModRelicTemplate : RelicModel
    {
        protected virtual bool IncludeEnergyHoverTip=>false;
        public virtual RelicAssetProfile AssetProfile=>new("");
    }
}
namespace STS2RitsuLib.Interop.AutoRegistration
{
    public class RegisterRelicAttribute(Type pool) : Attribute { }
    public class RegisterCharacterStarterRelicAttribute(Type character,int count=1) : Attribute { }
}
namespace IndiumStar.CardPools { public class IndiumStarRelicPool { } }
