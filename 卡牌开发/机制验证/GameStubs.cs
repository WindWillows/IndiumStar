// 仅供独立机制测试。使用小型本体替身链接真实公共机制源码，不进入模组编译。
namespace MegaCrit.Sts2.Core.Models
{
    public abstract class AbstractModel
    {
        public AbstractModel MutableClone() => (AbstractModel)MemberwiseClone();
        public int FinishedCount { get; private set; }
        public void InvokeExecutionFinished() => FinishedCount++;
        public virtual int ModifyCardPlayCount(CardModel card,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, int playCount) => playCount;
        public virtual Task AfterCardChangedPiles(CardModel card, MegaCrit.Sts2.Core.Entities.Cards.PileType oldPile,
            AbstractModel? clonedBy) => Task.CompletedTask;
        public virtual Task AfterCreatureAddedToCombat(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => Task.CompletedTask;
        public virtual Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
        public virtual Task BeforePowerAmountChanged(PowerModel power, decimal amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature target, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
        public virtual Task AfterDamageGiven(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? dealer, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
            MegaCrit.Sts2.Core.ValueProps.ValueProp props, MegaCrit.Sts2.Core.Entities.Creatures.Creature target, CardModel? source) => Task.CompletedTask;
        public virtual Task AfterCardPlayed(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => Task.CompletedTask;
        public virtual Task BeforeCardPlayed(MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => Task.CompletedTask;
        public virtual Task BeforeSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) => Task.CompletedTask;
        public virtual Task BeforeSideTurnStart(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants,
            MegaCrit.Sts2.Core.Combat.ICombatState combatState) => Task.CompletedTask;
        public virtual bool TryModifyEnergyCostInCombatLate(CardModel card, decimal original, out decimal modified) { modified = original; return false; }
        public virtual bool TryModifyEnergyCostInCombat(CardModel card, decimal original, out decimal modified) { modified = original; return false; }
        public virtual bool ShouldTakeExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) => false;
        public virtual Task AfterTakingExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) => Task.CompletedTask;
        public virtual decimal ModifyBlockMultiplicative(MegaCrit.Sts2.Core.Entities.Creatures.Creature target, decimal block,
            MegaCrit.Sts2.Core.ValueProps.ValueProp props, CardModel? cardSource, MegaCrit.Sts2.Core.Entities.Cards.CardPlay? play) => 1m;
        public virtual Task AfterModifyingBlockAmount(decimal amount, CardModel? cardSource, MegaCrit.Sts2.Core.Entities.Cards.CardPlay? play) => Task.CompletedTask;
        public virtual Task BeforeHandDraw(MegaCrit.Sts2.Core.Entities.Players.Player player,
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.ICombatState combat) => Task.CompletedTask;
        public virtual Task AfterCardDrawn(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            CardModel card, bool fromHandDraw) => Task.CompletedTask;
        public virtual Task AfterApplied(MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
        public virtual decimal ModifyHandDraw(MegaCrit.Sts2.Core.Entities.Players.Player player, decimal count) => count;
        public virtual Task AfterModifyingHandDraw() => Task.CompletedTask;
        public virtual Task AfterPlayerTurnStart(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Players.Player player) => Task.CompletedTask;
        public virtual Task AfterFlush(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Players.Player player, IReadOnlyCollection<CardModel> flushed,
            IReadOnlyCollection<CardModel> retained) => Task.CompletedTask;
        public virtual bool ShouldPlay(CardModel card, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType type) => true;
    }

    public abstract class CardModel : AbstractModel
    {
        protected MegaCrit.Sts2.Core.Entities.Cards.TargetType CanonicalTargetType;
        public virtual MegaCrit.Sts2.Core.Entities.Cards.TargetType TargetType => CanonicalTargetType;
        protected void AssertMutable() { }
        protected virtual IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips => [];
        private MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet? _dynamicVars;
        private HashSet<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> _addedKeywords = [];
        private HashSet<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> _removedKeywords = [];
        public IEnumerable<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> Keywords => CanonicalKeywords.Concat(_addedKeywords).Except(_removedKeywords).Distinct();
        public TestModelId Id => new(GetType().Name);
        protected virtual HashSet<MegaCrit.Sts2.Core.Entities.Cards.CardTag> CanonicalTags => [];
        public IEnumerable<MegaCrit.Sts2.Core.Entities.Cards.CardTag> Tags => CanonicalTags;
        public MegaCrit.Sts2.Core.Entities.Players.Player Owner { get; set; } = null!;
        public MegaCrit.Sts2.Core.Combat.ICombatState? CombatState { get; set; }
        public CardModel? DeckVersion { get; set; }
        public string Title { get; set; } = "";
        public MegaCrit.Sts2.Core.Entities.Cards.CardType Type { get; protected set; }
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile? Pile { get; set; }
        public bool IsUpgraded { get; private set; }
        public bool IsDupe { get; set; }
        public AfflictionModel? Affliction { get; set; }
        public virtual bool GainsBlock => false;
        public TestEnergyCost EnergyCost { get; } = new();
        protected virtual bool HasEnergyCostX => false;
        public int ResolveEnergyXValue() => HasEnergyCostX ? EnergyCost.CapturedXValue + EnergyCost.XModifier : throw new InvalidOperationException();
        public virtual IEnumerable<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> CanonicalKeywords => [];
        protected virtual IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars => [];
        public MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet DynamicVars =>
            _dynamicVars ??= new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet(CanonicalVars, this);
        public virtual STS2RitsuLib.Scaffolding.Content.CardAssetProfile AssetProfile => new("");
        protected virtual bool IsPlayable => true;
        public bool CanPlayManually => IsPlayable;
        public virtual bool HasTurnEndInHandEffect => false;
        protected virtual Task OnTurnEndInHand(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context) => Task.CompletedTask;
        public bool SingleTurnRetain { get; private set; }
        public void GiveSingleTurnRetain() => SingleTurnRetain = true;
        public object SelectionScreenPrompt { get; } = new();
        public void AddKeyword(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword keyword) => _addedKeywords.Add(keyword);
        public void RemoveKeyword(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword keyword) => _removedKeywords.Add(keyword);
        protected virtual void OnUpgrade() { }
        public void Upgrade() { IsUpgraded = true; OnUpgrade(); }
        public CardModel Clone() => (CardModel)MutableClone();
        public CardModel CreateClone() {
            var clone = Clone(); clone.DeckVersion = null; clone.Pile = null;
            clone._dynamicVars = DynamicVars.Clone(clone); clone._addedKeywords = new(_addedKeywords); clone._removedKeywords = new(_removedKeywords); return clone;
        }
        protected virtual void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description) { }
        protected abstract Task OnPlay(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play);
        public Task Play(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => OnPlay(context, play);
        public int InitialPlayCount { get; set; } = 1;
        public int CurrentPlayIndex { get; private set; }
        public int SeriesCleanupCount { get; private set; }
        // 仅复制原版循环及其指令边界，不调用模组重放helper；真正的Harmony transpiler负责插入。
        public async Task OnPlayWrapper(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? target = null, bool isAutoPlay = false,
            MegaCrit.Sts2.Core.Entities.Cards.ResourceInfo resources = default)
        {
            var combatState = CombatState!;
            var manager = MegaCrit.Sts2.Core.Combat.CombatManager.Instance;
            var resultPile = Keywords.Contains(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword.Exhaust)
                ? MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust : MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard;
            var oldPile = Pile?.Type ?? MegaCrit.Sts2.Core.Entities.Cards.PileType.None;
            Pile?.Cards.Remove(this); Owner.PlayerCombatState!.Hand.Cards.Remove(this);
            Pile = Owner.PlayerCombatState.PlayPile; Pile.Cards.Add(this);
            foreach (var listener in combatState.IterateHookListeners().ToArray())
                await listener.AfterCardChangedPiles(this, oldPile, null);
            int playCount = InitialPlayCount;
            foreach (var listener in combatState.IterateHookListeners().ToArray())
                playCount = listener.ModifyCardPlayCount(this, target, playCount);
            await Task.Yield();
            for (int i = 0; i < playCount; i++)
            {
                if (manager.IsOverOrEnding || !Owner.Creature.IsAlive) break;
                CurrentPlayIndex = i;
                var cardPlay = new MegaCrit.Sts2.Core.Entities.Cards.CardPlay {
                    Card = this, Player = Owner, Target = target, IsAutoPlay = isAutoPlay, PlayIndex = i,
                    PlayCount = playCount, Resources = resources, ResultPile = resultPile
                };
                foreach (var listener in combatState.IterateHookListeners().ToArray()) await listener.BeforeCardPlayed(cardPlay);
                manager.History.CardPlaysStarted.Add(new(combatState, combatState.Turn, cardPlay));
                await OnPlay(choiceContext, cardPlay);
                if (!Owner.Creature.IsAlive) return;
                InvokeExecutionFinished();
                manager.History.CardPlaysFinished.Add(new(combatState, combatState.Turn, cardPlay));
                if (!manager.IsOverOrEnding)
                    foreach (var listener in combatState.IterateHookListeners().ToArray()) await listener.AfterCardPlayed(choiceContext, cardPlay);
                if (!Owner.Creature.IsAlive) return;
            }
            if (Pile?.Type == MegaCrit.Sts2.Core.Entities.Cards.PileType.Play)
                await MegaCrit.Sts2.Core.Commands.CardPileCmd.Add(this, resultPile);
            SeriesCleanupCount++;
            CurrentPlayIndex = 0;
        }
    }

    public class PowerModel : AbstractModel
    {
        protected void AssertMutable() { }
        public MegaCrit.Sts2.Core.Localization.LocString Title { get; } = new();
        private object? _internalData;
        public PowerModel() { _internalData = InitInternalData(); }
        protected virtual object? InitInternalData() => null;
        protected T GetInternalData<T>() => (T)_internalData!;
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Owner { get; set; } = null!;
        public int Amount { get; set; }
        public virtual int DisplayAmount => Amount;
        protected void InvokeDisplayAmountChanged() { }
        public virtual STS2RitsuLib.Scaffolding.Content.PowerAssetProfile AssetProfile => new("");
        protected virtual bool IsVisibleInternal => true;
        public bool IsVisible => IsVisibleInternal;
        public virtual bool AllowNegative => false;
        public MegaCrit.Sts2.Core.Entities.Powers.PowerType TypeForCurrentAmount => AllowNegative && Amount < 0
            ? MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff : Type;
        protected virtual void DeepCloneFields() { }
        public PowerModel ToMutable() { var clone = (PowerModel)MemberwiseClone(); clone.DeepCloneFields(); return clone; }
        public void ApplyInternal(MegaCrit.Sts2.Core.Entities.Creatures.Creature owner, decimal amount, bool silent = false)
        { Owner = owner; Amount = (int)amount; owner.Powers.Add(this); }
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType => MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        public void SetAmount(int amount, bool silent = false) => Amount = amount;
        public virtual Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side,
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants,
            MegaCrit.Sts2.Core.Combat.ICombatState combatState) => Task.CompletedTask;
        public virtual Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
            MegaCrit.Sts2.Core.Combat.CombatSide side,
            IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) => Task.CompletedTask;
        public virtual Task BeforeApplied(MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
            decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
    }

    public static class ModelDb
    {
        public static T Power<T>() where T : PowerModel => Activator.CreateInstance<T>();
        public static T Card<T>() where T : CardModel => Activator.CreateInstance<T>();
    }
    public class TestEnergyCost {
        public int Value { get; set; }
        public bool CostsX { get; set; }
        public int CapturedXValue { get; set; }
        public int XModifier { get; set; }
        public int GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers modifiers) => Value;
        public void UpgradeBy(int amount) => Value += amount;
        public void AddThisCombat(int amount) => Value = Math.Max(0, Value + amount);
    }
    public class AfflictionModel : AbstractModel { }
}

namespace MegaCrit.Sts2.Core.Models.Powers
{
    public class EnergyNextTurnPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class StrengthPower : MegaCrit.Sts2.Core.Models.PowerModel { public override bool AllowNegative => true; }
    public class DexterityPower : MegaCrit.Sts2.Core.Models.PowerModel { public override bool AllowNegative => true; }
    public class DrawCardsNextTurnPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class WeakPower : MegaCrit.Sts2.Core.Models.PowerModel { public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff; }
    public class VulnerablePower : MegaCrit.Sts2.Core.Models.PowerModel { public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff; }
    public class FrailPower : MegaCrit.Sts2.Core.Models.PowerModel { public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff; }
    public class PoisonPower : MegaCrit.Sts2.Core.Models.PowerModel { public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff; }
    public class DoomPower : MegaCrit.Sts2.Core.Models.PowerModel { public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff; }
    public abstract class TemporaryDexterityPower : MegaCrit.Sts2.Core.Models.PowerModel, MegaCrit.Sts2.Core.Entities.Powers.ITemporaryPower {
        public abstract MegaCrit.Sts2.Core.Models.AbstractModel OriginModel { get; }
        protected virtual bool IsPositive => true;
        private int Sign => IsPositive ? 1 : -1;
        public override Task BeforeApplied(MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
            decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source) =>
            MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<DexterityPower>(new(), target, Sign * amount, applier, source);
        public override Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier,
            MegaCrit.Sts2.Core.Models.CardModel? source) => power == this && amount != Amount
                ? MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<DexterityPower>(context, Owner, Sign * amount, applier, source) : Task.CompletedTask;
        public override async Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) {
            if (!participants.Contains(Owner)) return;
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(this);
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<DexterityPower>(context, Owner, -Sign * Amount, Owner, null);
        }
    }
    // 按本体TemporaryStrengthPower的授予、追加与本回合回收顺序模拟。
    public abstract class TemporaryStrengthPower : MegaCrit.Sts2.Core.Models.PowerModel, MegaCrit.Sts2.Core.Entities.Powers.ITemporaryPower {
        public abstract MegaCrit.Sts2.Core.Models.AbstractModel OriginModel { get; }
        protected virtual bool IsPositive => true;
        private int Sign => IsPositive ? 1 : -1;
        public override Task BeforeApplied(MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
            decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source) =>
            MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(new(), target, Sign * amount, applier, source);
        public override Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier,
            MegaCrit.Sts2.Core.Models.CardModel? source) => power == this && amount != Amount
                ? MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(context, Owner, Sign * amount, applier, source) : Task.CompletedTask;
        public override async Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) {
            if (!participants.Contains(Owner)) return;
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(this);
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(context, Owner, -Sign * Amount, Owner, null);
        }
    }
    public class VigorPower : MegaCrit.Sts2.Core.Models.PowerModel
    {
        private class Data { public object? Command; public int AmountAtStart; }
        protected override object InitInternalData() => new Data();
        public bool HasAttackCache => GetInternalData<Data>().Command != null;
        public Task BeforeAttack(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
        { var data = GetInternalData<Data>(); if (data.Command == null) { data.Command = command; data.AmountAtStart = Amount; } return Task.CompletedTask; }
        public async Task AfterAttack(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
        { var data = GetInternalData<Data>(); if (ReferenceEquals(data.Command, command))
            await MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(context, this, -data.AmountAtStart, null, null); }
    }
}

namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public class DamageResult(Creature receiver, MegaCrit.Sts2.Core.ValueProps.ValueProp props)
    {
        public Creature Receiver { get; } = receiver;
        public MegaCrit.Sts2.Core.ValueProps.ValueProp Props { get; } = props;
        public int UnblockedDamage { get; init; }
        public int BlockedDamage { get; set; }
    }
    public class Creature
    {
        public MegaCrit.Sts2.Core.Combat.ICombatState? CombatState { get; set; }
        public MegaCrit.Sts2.Core.Entities.Players.Player? Player { get; set; }
        public bool IsAlive { get; set; } = true;
        public MegaCrit.Sts2.Core.Models.MonsterModel? Monster { get; set; }
        public int Block { get; set; }
        public bool IsMonster => Player == null;
        public MegaCrit.Sts2.Core.Combat.CombatSide Side => Player == null ? MegaCrit.Sts2.Core.Combat.CombatSide.Enemy : MegaCrit.Sts2.Core.Combat.CombatSide.Player;
        public bool CanReceivePowers { get; set; } = true;
        public List<MegaCrit.Sts2.Core.Models.PowerModel> Powers { get; } = [];
        public T? GetPower<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => Powers.OfType<T>().FirstOrDefault();
        public int GetPowerAmount<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => GetPower<T>()?.Amount ?? 0;
    }
}

namespace MegaCrit.Sts2.Core.Entities.Players
{
    public class Player
    {
        public Player() { Creature.Player = this; }
        public PlayerCombatState? PlayerCombatState { get; set; } = new();
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Creature { get; } = new();
        public FakeRunState RunState { get; } = new();
    }

    public class FakeRunState { public FakeRng Rng { get; } = new(); }
    public class FakeRng { public object CombatCardSelection { get; } = new(); }

    public class PlayerCombatState
    {
        public int Energy { get; set; }
        public int TurnNumber { get; set; } = 1;
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile Hand { get; } = new();
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile DrawPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Draw };
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile DiscardPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard };
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile ExhaustPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust };
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile PlayPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Play };
        public IEnumerable<MegaCrit.Sts2.Core.Models.CardModel> AllCards => Hand.Cards.Concat(DrawPile.Cards).Concat(DiscardPile.Cards).Concat(ExhaustPile.Cards).Concat(PlayPile.Cards);
    }
}

namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum CardType { None, Attack, Skill, Power, Status, Curse, Quest }
    public enum CardRarity { Common, Basic, Uncommon, Rare, Token }
    public enum TargetType { Self, AnyEnemy, AllEnemies }
    public enum CardKeyword { Retain, Exhaust, Ethereal, Innate, Unplayable }
    public enum CostModifiers { All }
    public enum CardTag { Strike, Defend }
    public enum CardPreviewMode { Normal }
    public enum PileType { Hand, Play, Exhaust, Discard, None, Draw }
    public enum CardPilePosition { Top, Bottom }
    public struct CardPileAddResult { public bool success; public PileType targetPile; }
    public enum AutoPlayType { Default }
    public class CardPile { public PileType Type { get; set; } = PileType.Hand; public bool IsCombatPile => Type != PileType.None; public List<MegaCrit.Sts2.Core.Models.CardModel> Cards { get; } = []; }
    public static class PileExtensions
    {
        public static CardPile GetPile(this PileType type, MegaCrit.Sts2.Core.Entities.Players.Player player) =>
            type switch {
                PileType.Play => player.PlayerCombatState!.PlayPile,
                PileType.Draw => player.PlayerCombatState!.DrawPile,
                PileType.Discard => player.PlayerCombatState!.DiscardPile,
                PileType.Exhaust => player.PlayerCombatState!.ExhaustPile,
                _ => player.PlayerCombatState!.Hand
            };
    }

    public struct ResourceInfo {
        public int EnergySpent { get; init; }
        public int EnergyValue { get; init; }
        public int StarsSpent { get; init; }
        public int StarValue { get; init; }
    }
    public class CardPlay
    {
        public MegaCrit.Sts2.Core.Models.CardModel Card { get; init; } = null!;
        private MegaCrit.Sts2.Core.Entities.Players.Player? _player;
        public MegaCrit.Sts2.Core.Entities.Players.Player Player { get => _player ?? Card.Owner; init => _player = value; }
        public bool IsAutoPlay { get; init; }
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target { get; init; }
        public int PlayIndex { get; init; }
        public int PlayCount { get; init; } = 1;
        public ResourceInfo Resources { get; init; }
        public PileType ResultPile { get; init; }
        public bool IsFirstInSeries => PlayIndex == 0;
        public bool IsLastInSeries => PlayIndex == PlayCount - 1;
    }
}

namespace MegaCrit.Sts2.Core.GameActions.Multiplayer
{
    public class PlayerChoiceContext { }
    public class ThrowingPlayerChoiceContext : PlayerChoiceContext { }
}

namespace MegaCrit.Sts2.Core.Entities.Powers
{
    public enum PowerType { Buff, Debuff }
    public enum PowerStackType { Counter, Single }
}

namespace MegaCrit.Sts2.Core.Combat
{
    public enum CombatSide { Player, Enemy }

    public interface ICombatState
    {
        int Turn { get; }
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Creatures { get; }
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Allies { get; }
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Enemies { get; }
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> HittableEnemies { get; }
        MegaCrit.Sts2.Core.Models.CardModel CloneCard(MegaCrit.Sts2.Core.Models.CardModel card);
        IEnumerable<MegaCrit.Sts2.Core.Models.AbstractModel> IterateHookListeners();
        T CreateCard<T>(MegaCrit.Sts2.Core.Entities.Players.Player owner) where T : MegaCrit.Sts2.Core.Models.CardModel;
    }

    public class CombatManager
    {
        public static CombatManager Instance { get; } = new();
        public bool IsOverOrEnding { get; set; }
        public bool IsStarting { get; set; }
        public FakeHistory History { get; } = new();
    }

    public class FakeHistory {
        public List<MegaCrit.Sts2.Core.Combat.History.Entries.CardExhaustedEntry> Entries { get; } = [];
        public List<HistoryEntry> CardPlaysStarted { get; } = [];
        public List<HistoryEntry> CardPlaysFinished { get; } = [];
    }
    public record HistoryEntry(ICombatState State, int Turn, MegaCrit.Sts2.Core.Entities.Cards.CardPlay CardPlay)
    {
        public bool HappenedThisTurn(ICombatState state) => ReferenceEquals(state, State) && state.Turn == Turn;
    }
}

namespace MegaCrit.Sts2.Core.Commands
{
    public static class CardPileCmd
    {
        public static int DrawCount { get; set; }
        public static int HandCapacity { get; set; } = 10;
        public static bool DrawBlocked { get; set; }
        public static Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult> Add(MegaCrit.Sts2.Core.Models.CardModel card,
            MegaCrit.Sts2.Core.Entities.Cards.PileType pile, MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition position = MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Bottom) {
            card.Pile?.Cards.Remove(card); return AddGeneratedCardToCombat(card, pile, card.Owner, position);
        }
        public static async Task<IEnumerable<MegaCrit.Sts2.Core.Models.CardModel>> Draw(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, decimal count,
            MegaCrit.Sts2.Core.Entities.Players.Player owner, bool fromHandDraw = false) {
            DrawCount += (int)count;
            var drawn = new List<MegaCrit.Sts2.Core.Models.CardModel>();
            if (DrawBlocked) return drawn;
            for (int i = 0; i < count; i++) {
                var state = owner.PlayerCombatState!;
                if (state.DrawPile.Cards.Count == 0 || state.Hand.Cards.Count >= HandCapacity) break;
                var card = state.DrawPile.Cards[0]; state.DrawPile.Cards.RemoveAt(0);
                state.Hand.Cards.Add(card); card.Pile = state.Hand; drawn.Add(card);
                foreach (var listener in card.CombatState!.IterateHookListeners().ToArray())
                    await listener.AfterCardDrawn(context, card, fromHandDraw);
            }
            return drawn;
        }
        public static async Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult> AddGeneratedCardToCombat(MegaCrit.Sts2.Core.Models.CardModel card,
            MegaCrit.Sts2.Core.Entities.Cards.PileType pile, MegaCrit.Sts2.Core.Entities.Players.Player owner,
            MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition position = MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Bottom)
        {
            // 模拟本体Add在结束阶段或拥有者死亡时拒绝入堆；不模拟完全退出战斗后的引擎清理。
            if (!owner.Creature.IsAlive || (MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding
                && !MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsStarting))
                return new() { success = false, targetPile = pile };
            if (pile == MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand && owner.PlayerCombatState!.Hand.Cards.Count >= HandCapacity)
                pile = MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard;
            var destination = MegaCrit.Sts2.Core.Entities.Cards.PileExtensions.GetPile(pile, owner);
            if (position == MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Top) destination.Cards.Insert(0, card);
            else destination.Cards.Add(card);
            card.Pile = destination;
            foreach (var listener in card.CombatState!.IterateHookListeners().ToArray())
                await listener.AfterCardChangedPiles(card, MegaCrit.Sts2.Core.Entities.Cards.PileType.None, null);
            return new() { success = true, targetPile = pile };
        }
    }

    public static class PowerCmd
    {
        public static MegaCrit.Sts2.Core.Models.CardModel? LastSource { get; private set; }
        public static MegaCrit.Sts2.Core.Entities.Creatures.Creature? LastApplier { get; private set; }
        public static int ApplyCount { get; set; }
        public static Func<MegaCrit.Sts2.Core.Models.PowerModel, decimal, decimal>? ModifyAmountReceived { get; set; }
        public static async Task<T> Apply<T>(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature recipient, decimal amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source,
            bool silent = false)
            where T : MegaCrit.Sts2.Core.Models.PowerModel, new()
        {
            ApplyCount++;
            LastSource = source;
            LastApplier = applier;
            if (MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding || !recipient.CanReceivePowers || amount == 0)
                return null!;
            T? power = recipient.GetPower<T>();
            bool newlyApplied = power == null;
            power ??= new T { Owner = recipient };
            if (recipient.CombatState != null) foreach (var listener in recipient.CombatState.IterateHookListeners().ToArray())
                await listener.BeforePowerAmountChanged(power, amount, recipient, applier, source);
            amount = ModifyAmountReceived?.Invoke(power, amount) ?? amount;
            if (newlyApplied) { await power.BeforeApplied(recipient, amount, applier, source); recipient.Powers.Add(power); }
            power.Amount += (int)amount;
            if (amount == 0) return power;
            if (newlyApplied) await power.AfterApplied(applier, source);
            if (recipient.CombatState != null) foreach (var listener in recipient.CombatState.IterateHookListeners().ToArray())
                await listener.AfterPowerAmountChanged(context, power, amount, applier, source);
            return power;
        }
        public static async Task<int> ModifyAmount(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.PowerModel power, decimal amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source)
        { power.SetAmount(power.Amount + (int)amount); if (power.Amount == 0) await Remove(power); return power.Amount; }
        public static Task<int> Decrement(MegaCrit.Sts2.Core.Models.PowerModel power) => ModifyAmount(new(), power, -1, null, null);
        public static Task Remove(MegaCrit.Sts2.Core.Models.PowerModel power)
        {
            power.Owner.Powers.Remove(power);
            return Task.CompletedTask;
        }
    }

    public static class PlayerCmd
    {
        public static MegaCrit.Sts2.Core.Entities.Players.Player? LastTurnEnded { get; set; }
        public static void EndTurn(MegaCrit.Sts2.Core.Entities.Players.Player player, bool canBackOut,
            Func<Task>? actionDuringEnemyTurn = null) => LastTurnEnded = player;
        public static Func<MegaCrit.Sts2.Core.Entities.Players.Player, decimal, Task>? AfterModifyingEnergyGain { get; set; }
        public static async Task GainEnergy(decimal amount, MegaCrit.Sts2.Core.Entities.Players.Player player) {
            if (amount <= 0 || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding) return;
            if (AfterModifyingEnergyGain != null) await AfterModifyingEnergyGain(player, amount);
            player.PlayerCombatState!.Energy += (int)amount;
        }
        public static Task LoseEnergy(decimal amount, MegaCrit.Sts2.Core.Entities.Players.Player player) {
            if (amount > 0 && !MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding)
                player.PlayerCombatState!.Energy = Math.Max(0, player.PlayerCombatState.Energy - (int)amount);
            return Task.CompletedTask;
        }
        public static List<decimal> GoldGranted { get; } = [];
        public static Task GainGold(decimal amount, MegaCrit.Sts2.Core.Entities.Players.Player player)
        {
            GoldGranted.Add(amount);
            return Task.CompletedTask;
        }
    }
}

namespace STS2RitsuLib.Scaffolding.Content
{
    public record CardAssetProfile(string PortraitPath);
    public abstract class ModCardTemplate : MegaCrit.Sts2.Core.Models.CardModel
    {
        protected virtual IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> AdditionalHoverTips => [];
        protected sealed override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips => AdditionalHoverTips;
        public int BaseCost { get; }
        protected ModCardTemplate(int cost, MegaCrit.Sts2.Core.Entities.Cards.CardType type,
            MegaCrit.Sts2.Core.Entities.Cards.CardRarity rarity, MegaCrit.Sts2.Core.Entities.Cards.TargetType target)
        { BaseCost = cost; Type = type; EnergyCost.Value = cost; CanonicalTargetType = target; }
    }
    public record PowerAssetProfile(string Path);
    public abstract class ModPowerTemplate : MegaCrit.Sts2.Core.Models.PowerModel { }
    public static class ContentAssetProfiles { public static PowerAssetProfile Power(string path) => new(path); }
}

namespace MegaCrit.Sts2.Core.Localization
{
    public class LocString { public LocString() { } public LocString(string table, string key) { } public void Add(string key, object value) { } }
}

namespace MegaCrit.Sts2.Core.Localization.DynamicVars
{
    public class DynamicVar
    {
        public string Name { get; }
        public decimal BaseValue { get; set; }
        public int IntValue => (int)BaseValue;
        public decimal PreviewValue { get; set; }
        public DynamicVar(string name, decimal baseValue)
        {
            Name = name;
            BaseValue = baseValue;
            PreviewValue = baseValue;
        }
        public void UpgradeValueBy(decimal amount) => BaseValue += amount;
        public DynamicVar Clone() => (DynamicVar)MemberwiseClone();
    }

    public class DynamicVarSet
    {
        private readonly Dictionary<string, DynamicVar> _vars;
        public DynamicVarSet(IEnumerable<DynamicVar> vars, MegaCrit.Sts2.Core.Models.CardModel? owner = null) {
            _vars = vars.ToDictionary(v => v.Name);
            foreach (var calculated in _vars.Values.OfType<CalculatedDamageVar>()) calculated.Owner = owner!;
        }
        public DynamicVar this[string name] => _vars[name];
        public DamageVar Damage => (DamageVar)_vars["Damage"];
        public BlockVar Block => (BlockVar)_vars["Block"];
        public CardsVar Cards => (CardsVar)_vars["Cards"];
        public EnergyVar Energy => (EnergyVar)_vars["Energy"];
        public DynamicVar Strength => _vars["StrengthPower"];
        public DynamicVar Weak => _vars["WeakPower"];
        public DynamicVar Vulnerable => _vars["VulnerablePower"];
        public RepeatVar Repeat => (RepeatVar)_vars["Repeat"];
        public CalculatedDamageVar CalculatedDamage => (CalculatedDamageVar)_vars["CalculatedDamage"];
        public ExtraDamageVar ExtraDamage => (ExtraDamageVar)_vars["ExtraDamage"];
        public DynamicVarSet Clone(MegaCrit.Sts2.Core.Models.CardModel? owner = null) => new(_vars.Values.Select(v => v.Clone()), owner);
    }
    public class DamageVar : DynamicVar {
        public DamageVar(decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props) : this("Damage", amount, props) { }
        public DamageVar(string name, decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props) : base(name, amount) { Props = props; }
        public MegaCrit.Sts2.Core.ValueProps.ValueProp Props { get; }
    }
    public class PowerVar<T>(decimal amount) : DynamicVar(typeof(T).Name, amount) { }
    public class BlockVar(decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props) : DynamicVar("Block", amount)
    { public MegaCrit.Sts2.Core.ValueProps.ValueProp Props { get; } = props; }
    public class CardsVar(decimal amount) : DynamicVar("Cards", amount) { }
    public class EnergyVar(decimal amount) : DynamicVar("Energy", amount) { }
    public class RepeatVar(decimal amount) : DynamicVar("Repeat", amount) { }
    public class CalculationBaseVar(decimal amount) : DynamicVar("CalculationBase", amount) { }
    public class ExtraDamageVar(decimal amount) : DynamicVar("ExtraDamage", amount) { }
    public class CalculatedDamageVar(MegaCrit.Sts2.Core.ValueProps.ValueProp props) : DynamicVar("CalculatedDamage", 0) {
        public MegaCrit.Sts2.Core.Models.CardModel Owner { get; set; } = null!;
        private Func<MegaCrit.Sts2.Core.Models.CardModel, MegaCrit.Sts2.Core.Entities.Creatures.Creature?, decimal> _multiplier = null!;
        public CalculatedDamageVar WithMultiplier(Func<MegaCrit.Sts2.Core.Models.CardModel, MegaCrit.Sts2.Core.Entities.Creatures.Creature?, decimal> multiplier) {
            if (multiplier.Target is MegaCrit.Sts2.Core.Models.AbstractModel) throw new InvalidOperationException();
            _multiplier = multiplier; return this;
        }
        public decimal Calculate(MegaCrit.Sts2.Core.Entities.Creatures.Creature? target) =>
            Owner.DynamicVars["CalculationBase"].BaseValue + Owner.DynamicVars["ExtraDamage"].BaseValue * _multiplier(Owner, target);
    }
}

namespace STS2RitsuLib.Utils
{
    public class AttachedState<TKey, TValue> where TKey : notnull {
        private readonly Dictionary<TKey, TValue> _values = [];
        public AttachedState(Func<TValue>? factory = null) { }
        public TValue GetValueOrDefault(TKey key, TValue fallback) => _values.GetValueOrDefault(key, fallback);
        public void Set(TKey key, TValue value) => _values[key] = value;
        public void Remove(TKey key) => _values.Remove(key);
    }
    public class SavedAttachedState<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _values = [];
        public SavedAttachedState(string name, Func<TValue>? defaultValueFactory = null, int order = 0) { }
        public TValue GetValueOrDefault(TKey key, TValue defaultValue) =>
            _values.TryGetValue(key, out var value) ? value : defaultValue;
        public TValue Set(TKey key, TValue value) { _values[key] = value; return value; }
    }
}

namespace STS2RitsuLib.Keywords
{
    public enum ModKeywordCardDescriptionPlacement { None, BeforeCardDescription, AfterCardDescription }

    public class ModKeywordDefinition
    {
        public string Id { get; set; } = "";
        public MegaCrit.Sts2.Core.Entities.Cards.CardKeyword CardKeywordValue { get; set; }
    }

    public class ModKeywordRegistry
    {
        private static readonly Dictionary<string, ModKeywordRegistry> Registries = [];
        private static int _nextValue = 100;
        private readonly Dictionary<string, ModKeywordDefinition> _definitions = [];
        private string _modId = "";
        public static ModKeywordRegistry For(string modId) {
            if (!Registries.TryGetValue(modId, out var registry)) {
                registry = new() { _modId = modId }; Registries[modId] = registry;
            }
            return registry;
        }
        public ModKeywordDefinition RegisterCardKeywordOwnedByLocNamespace(string localKeywordStem,
            string? iconPath, ModKeywordCardDescriptionPlacement cardDescriptionPlacement, bool includeInCardHoverTip) {
            if (_definitions.TryGetValue(localKeywordStem, out var existing)) return existing;
            var prefix = System.Text.RegularExpressions.Regex.Replace(_modId, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
            var definition = new ModKeywordDefinition { Id = prefix + "_KEYWORD_" + localKeywordStem.ToUpperInvariant(),
                CardKeywordValue = (MegaCrit.Sts2.Core.Entities.Cards.CardKeyword)_nextValue++ };
            _definitions.Add(localKeywordStem, definition); return definition;
        }
    }
}

namespace MegaCrit.Sts2.Core.Extensions
{
    public static class SelectionExtensions
    {
        // Pb的关键字回归不执行随机选牌；这里仅提供其源码需要的签名。
        public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> values, int count, object rng) => values.Take(count);
    }
}

namespace STS2RitsuLib.Interop.AutoRegistration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterCardAttribute(Type pool) : Attribute
    {
        public Type Pool { get; } = pool;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterPowerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterCharacterStarterCardAttribute(Type character, int count) : Attribute { public int Order { get; set; } }
}

namespace IndiumStar.CardPools { public class IndiumStarCardPool { } }
namespace IndiumStar.Characters { public class IndiumStarCharacter { } }
namespace IndiumStar.Afflictions {
    public class CrystallizeAffliction : MegaCrit.Sts2.Core.Models.AfflictionModel { }
    public class CrystallizeUAffliction : MegaCrit.Sts2.Core.Models.AfflictionModel { }
}
namespace MegaCrit.Sts2.Core.Nodes.Cards {
    public class NCard {
        public static NCard? FindOnTable(MegaCrit.Sts2.Core.Models.CardModel card,
            MegaCrit.Sts2.Core.Entities.Cards.PileType pile) => null;
        public void UpdateVisuals(MegaCrit.Sts2.Core.Entities.Cards.PileType pile,
            MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode mode) { }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.CommonUi { public enum CardPreviewStyle { None, HorizontalLayout } }

namespace MegaCrit.Sts2.Core.ValueProps
{
    [Flags]
    public enum ValueProp { Unblockable = 2, Unpowered = 4, Move = 8, SkipHurtAnim = 16 }
    public static class ValuePropExtensions {
        public static bool IsPoweredCardOrMonsterMoveBlock(this ValueProp props) => true;
        public static bool IsPoweredAttack(this ValueProp props) => props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
    }
}
namespace MegaCrit.Sts2.Core.Commands
{
    public static class CardCmd
    {
        // 仅模拟单张弃牌的牌堆移动；Sly及真实弃牌钩子需要实机验收。
        public static Task Discard(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.CardModel card) =>
            CardPileCmd.Add(card, MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard);
        public static Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?> Transform(MegaCrit.Sts2.Core.Models.CardModel original,
            MegaCrit.Sts2.Core.Models.CardModel replacement) {
            var pile = original.Pile!; int index = pile.Cards.IndexOf(original);
            pile.Cards[index] = replacement; replacement.Pile = pile; original.Pile = null;
            return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(new() { success = true, targetPile = pile.Type });
        }
        public static Func<MegaCrit.Sts2.Core.Models.CardModel, bool> ShouldAutoPlay { get; set; } = _ => true;
        public static void ApplySingleTurnRetain(MegaCrit.Sts2.Core.Models.CardModel card) => card.GiveSingleTurnRetain();
        public static void Upgrade(MegaCrit.Sts2.Core.Models.CardModel card, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle style) => card.Upgrade();
        public static Task Afflict<T>(MegaCrit.Sts2.Core.Models.CardModel card, decimal amount)
            where T : MegaCrit.Sts2.Core.Models.AfflictionModel, new() { card.Affliction = new T(); return Task.CompletedTask; }
        // 固定目标变化，不模拟随机牌池/UI/存档；保留原牌所在堆与位置。
        public static Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?> TransformTo<T>(MegaCrit.Sts2.Core.Models.CardModel original)
            where T : MegaCrit.Sts2.Core.Models.CardModel {
            var replacement = original.CombatState!.CreateCard<T>(original.Owner);
            var pile = original.Pile!; int index = pile.Cards.IndexOf(original);
            pile.Cards[index] = replacement; replacement.Pile = pile; original.Pile = null;
            return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(new() { success = true, targetPile = pile.Type });
        }
        public static void ClearAffliction(MegaCrit.Sts2.Core.Models.CardModel card) => card.Affliction = null;
        public static async Task AutoPlay(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.CardModel card, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
        {
            if (MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding || !card.Owner.Creature.IsAlive) return;
            var state = card.CombatState!;
            if (card.TargetType == MegaCrit.Sts2.Core.Entities.Cards.TargetType.AnyEnemy && target == null)
                target = state.HittableEnemies.FirstOrDefault(); // 不模拟本体随机目标RNG。
            bool allowed = ShouldAutoPlay(card) && state.IterateHookListeners().All(model =>
                model.ShouldPlay(card, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType.Default));
            if (card.TargetType == MegaCrit.Sts2.Core.Entities.Cards.TargetType.AnyEnemy && target == null) allowed = false;
            card.Owner.PlayerCombatState!.Hand.Cards.Remove(card); card.Pile?.Cards.Remove(card);
            card.Pile = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Play };
            if (!allowed) {
                await CardPileCmd.Add(card, MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard); return;
            }
            await card.OnPlayWrapper(context, target, true);
        }
        public static bool ExhaustBlocked { get; set; }
        public static MegaCrit.Sts2.Core.Entities.Cards.PileType? ExhaustDestinationOverride { get; set; }
        public static Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?> Exhaust(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, MegaCrit.Sts2.Core.Models.CardModel card)
        {
            if (ExhaustBlocked || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding)
                return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(null);
            card.Pile?.Cards.Remove(card);
            card.Owner.PlayerCombatState!.Hand.Cards.Remove(card);
            var destination = ExhaustDestinationOverride ?? MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust;
            card.Pile = MegaCrit.Sts2.Core.Entities.Cards.PileExtensions.GetPile(destination, card.Owner); card.Pile.Cards.Add(card);
            if (destination == MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust)
                MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.Entries.Add(new(card.CombatState!, card.CombatState!.Turn, card.Owner.Creature));
            return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(new() { success = true, targetPile = destination });
        }
    }
    public static class DamageCmd
    {
        public static Func<Builders.AttackCommand, Task>? AfterAttackExecuted { get; set; }
        public static List<int> HitCounts { get; } = [];
        public static List<(decimal Damage, int Hits, MegaCrit.Sts2.Core.Models.AbstractModel? Source, MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target,
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext? Choice)> Attacks { get; } = [];
        public static Builders.AttackCommand Attack(decimal amount) => new(amount);
        public static Builders.AttackCommand Attack(MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedDamageVar amount) => new() { CalculatedDamage = amount };
    }
}
namespace MegaCrit.Sts2.Core.Commands.Builders
{
    public class AttackCommand(decimal damage = 0m)
    {
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Attacker { get; private set; } = null!;
        private int _hits = 1;
        public MegaCrit.Sts2.Core.Models.AbstractModel? ModelSource { get; private set; }
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target { get; private set; }
        public IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Targets { get; private set; } = [];
        public MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedDamageVar? CalculatedDamage { get; init; }
        public decimal Damage => CalculatedDamage?.Calculate(Target) ?? damage;
        public AttackCommand WithHitCount(int count) { _hits = count; return this; }
        public AttackCommand FromCard(MegaCrit.Sts2.Core.Models.CardModel card, MegaCrit.Sts2.Core.Entities.Cards.CardPlay play)
        { Attacker = card.Owner.Creature; ModelSource = card; return this; }
        public AttackCommand FromMonster(MegaCrit.Sts2.Core.Models.MonsterModel monster)
        { Attacker = monster.Creature; ModelSource = monster; return this; }
        public AttackCommand Targeting(MegaCrit.Sts2.Core.Entities.Creatures.Creature target) { Target = target; return this; }
        public AttackCommand TargetingAllOpponents(MegaCrit.Sts2.Core.Combat.ICombatState combat) {
            Targets = combat.Creatures.Where(c => c.Side != Attacker.Side && c.IsAlive).ToArray(); return this;
        }
        public AttackCommand WithHitFx(string path) => this;
        public async Task Execute(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext? choiceContext)
        {
            MegaCrit.Sts2.Core.Commands.DamageCmd.HitCounts.Add(_hits);
            MegaCrit.Sts2.Core.Commands.DamageCmd.Attacks.Add((Damage, _hits, ModelSource, Target, choiceContext));
            var vigor = Attacker.GetPower<MegaCrit.Sts2.Core.Models.Powers.VigorPower>();
            if (vigor != null) { await vigor.BeforeAttack(this); await vigor.AfterAttack(choiceContext!, this); }
            if (MegaCrit.Sts2.Core.Commands.DamageCmd.AfterAttackExecuted != null)
                await MegaCrit.Sts2.Core.Commands.DamageCmd.AfterAttackExecuted(this);
        }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public class NCombatRoom
    {
        public static NCombatRoom? Instance { get; set; }
        public FakeCreatureNode Node { get; } = new();
        public FakeCreatureNode GetCreatureNode(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => Node;
    }
    public class FakeCreatureNode
    {
        public int RefreshCount { get; private set; }
        public Task UpdateIntent(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
        { RefreshCount++; return Task.CompletedTask; }
    }
}
