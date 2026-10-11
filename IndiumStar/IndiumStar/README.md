# 铟星 IndiumStar —— 杀戮尖塔2 角色模组

0.1.3修复晶币全卡牌存档属性污染：将计数改为晶币自身的原生SavedProperty，迁移旧晶币计数、清理其他卡牌零值历史残留。铟星禁用时，旧跑局仍可能保留该字段并在战斗结束写回放时失败；修复记录和存档清理结果见工作区`卡牌开发/奖励界面卡死修复_2026-10-07.md`。0.1.2的三项实测修复和0.1.1的初始遗物/启动修复保留；本轮未进行完整实战或多人验收。

《杀戮尖塔2》(Slay the Spire 2) 铟星角色模组。0.1.4按2026-10-10版Word补齐寻迹打击、键合-酸酐、滋生及渊河5/7升级，近身斩显示名保留旧模型ID。当前72张设计卡牌（65主卡、7衍生）具有核心实现；另保留旧版辰海Token模型用于存档兼容，不参与正常奖励/商店。详见工作区`卡牌开发/文档同步记录_2026-10-11.md`；实机、完整存档和多人验收仍在待办。

寻迹打击和键合-酸酐在晶化实例上累加抽牌/延拓文字，与晶罚消亡共存，不占侵蚀槽；原生SavedProperty及克隆保持独立。酸酐按用户定稿只识别附加效果文字，先赋予延拓，再按本人全部战斗牌堆快照自动打出，每张至多一次，不把普通升级、伤害/段数或费用变化作为目标。附加延拓的出牌快照供反演和锡疑识别，后加文字不追溯修改历史。滋生每层在抽到状态时追加抽1张牌，通过原生关键词钩子取消所有本人晶化的保留。

## 这个模组做了什么

| 项目 | 内容 |
| --- | --- |
| 角色 | 铟星 IndiumStar，75 点初始生命，99 金币 |
| 初始牌组 | 4 张**打击** + 4 张**防御** + 1 张**运用-In** + 1 张**近身斩**（共 10 张；近身斩1费，11/15伤害） |
| 打击 | 1 费，造成 6 点伤害；火堆锻造（升级）后改为 9 点 |
| 防御 | 1 费，获得 5 点格挡；火堆锻造（升级）后改为 8 点 |
| 初始遗物 | **缺陷晶胞**：每场战斗第一次实际获得延拓时，获得1点能量 |
| 卡池 / 遗物池 / 药水池 | 各自独立，遗物池含缺陷晶胞，药水池为空 |
| 美术资源 | 暂无自制资源，全部回退到原版铁甲战士的资源 |

## 角色机制

公共机制开发约定见 [MECHANICS.md](MECHANICS.md)。

### 延拓

下回合能量重置后获得对应能量，然后一次性移除。所有卡牌通过
`ExtensionRules.Gain / GetAmount / RemoveAll / TryConsumeAll` 操作延拓。
当前保留原实现：与原版 `EnergyNextTurnPower` 共享层数，多个来源会合并；
坍缩消耗全部层数时也包括其他来源的下回合能量。

### 晶化与每回合额度

新晶化继承 `CrystallizationCard`，自动获得0费状态牌、保留、消耗和公共手动额度。
普通、Mg、Nb、Hs、Pb、U已经迁移，Pb继续额外带虚无。变体可以覆写关键字和基础效果。
牌族身份使用 `ICrystallizationCard`，改名与切换语言不影响身份。

所有晶化共用每玩家每回合1次手动额度。历史统计限定本回合、对应玩家、
`!IsAutoPlay` 和 `IsFirstInSeries`。自动打出不占额度，也不会因为额度用完而被这条规则阻止。
新回合使用本体历史判定自然恢复额度。

空括号生成普通晶化，带元素后缀时生成指定变体：

```csharp
await CrystallizationRules.AddToHand(choiceContext, Owner, CombatState!, n, this);
await CrystallizationRules.AddToHand<CrystallizationMg>(choiceContext, Owner, CombatState!, n, this);
```

泛型生成入口要求目标模型实现晶化标记，避免误传普通牌。
选牌和手牌计数统一使用 `IsCrystallizationCard / CountCrystallizationInHand`。

### 晶抗 / 晶逆

支付费用 → 等待基础效果 → 读取一次能量 → 决定全部分支 → 执行分支。

默认非0能量选择晶抗，0能量选择晶逆。基础效果返还能量会影响选择，
分支或监听者随后改变能量不会重新选择。特殊规则可选择双分支，固定先晶抗后晶逆。

新牌须用 `AvailableCrystalBranches` 显式声明 `None / Resistance / Reversal / Both`，
并实现相应方法。当前有21张带分支的牌。主效果返回的 `CardModel?`
继续传递给分支，避免用卡牌实例字段保存本次选牌结果。

当前战斗中的模型可实现 `ICrystalBranchModifier` 修改分支选择，或实现
`ICrystalBranchListener` 在已声明分支完成后计数。默认空分支不会产生通知。
每个监听者须自己过滤适用玩家、牌堆和回合；不使用跨战斗的全局事件订阅。
量子点和污染H已接入这些接口，并有历史序号、重复施加和计数阈值回归测试。

### 运用牌族

`ApplicationCard` 是通用效果基类，不代表运用牌族。
运用牌须额外实现 `IApplicationCard`；已有16张运用牌全部标记（含衍生中子），包括不继承该基类的运用-Co。
`ApplicationRules.IsApplicationCard` 使用此标记，溯源猛击的免费效果不再依赖卡名。

### 负面能力与临时出牌限制

近身斩的虚弱在本人本回合末清除，易伤在敌方行动结束、原生衰减前清除；只回收本牌实际新增层数，同回合重复打出累计，不清除其他来源的既有状态。
`OnlyAttacksThisTurnPower`在本人回合结束移除，限制本回合只能打出攻击牌。

### 禁止出牌：`ShouldPlay` 钩子

要限制「本回合只能打出攻击牌」这类规则，覆写 `ShouldPlay(CardModel, AutoPlayType)`：

```csharp
public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
{
    if (card.Owner.Creature != base.Owner) return true;   // 只管自己的主人
    return card.Type == CardType.Attack;
}
```

这是原版禁止出牌的通用钩子（`SlothPower`、`RingingPower`、`SmoggyPower`、`VelvetChoker`
都用它），而且它**同时管住两条路径**：

| 路径 | 是否检查 `ShouldPlay` |
| --- | --- |
| 玩家手动点牌 | ✅ `CardModel.CanPlay` → `Hook.ShouldPlay` |
| `CardCmd.AutoPlay` | ✅ `CardCmd.cs:63` |
| 卡牌自己的 `IsPlayable` | ❌ `AutoPlay` **不**检查它 |

最后一行很关键：**自动打出会绕过 `IsPlayable`**。所以「晶化」的「每回合只能直接打出一次」
拦不住「离析」的自动打出——这正是我们要的，否则升级后的「打出 2 张」在同一回合
根本打不出第二张。

### 晶逆「重放」与本体出牌系列

「晶流」「运用-Th」的晶逆重放走**本体出牌系列**：
`ApplicationCard.ModifyCardPlayCount` 在出牌次数生成时（费用已付、主效果未结算）
按当前能量预测分支选择，预测到晶逆就把本次系列延长 `CrystalReversalReplayCount` 段。
这和复制药水（`DuplicationPower`）是同一条路径——每段都经过
`BeforeCardPlayed` / `AfterCardPlayed` 钩子和出牌历史，饰带扇等联动正常计数。

重放段不触发晶抗/晶逆：`ResolveEffect` 在非 `IsFirstInSeries` 的段上只执行主效果，
这正是卡面「重放不会触发晶抗/晶逆的效果」的含义，对本体的复制/附魔重放同样成立。

**约束：声明了 `CrystalReversalReplayCount` 的牌，主效果不得改变能量**
（出牌次数在主效果结算前生成，预测依赖当前能量）。晶流和运用-Th的主效果
都不改能量。晶逆方法体因此是空的——重放已由系列延长交付，
保留空实现只是让分支正常触发并发送通知。

### 下回合临时力量（`CharacterizationStrengthPower`）

原版的 `TemporaryStrengthPower` **没法用**：它是「**立刻**加力量、本回合结束移除」
（基类 `BeforeApplied` 加、`AfterSideTurnEnd` 减），而且它**一创建就把力量发出去了**，
延后不到下一回合。原版也没有「下回合临时力量」这种能力。

所以这里不继承它，直接写一个普通 `PowerModel`，把效果拆成两步、各自挂在明确的钩子上：

| 步骤 | 钩子 | 行为 |
| --- | --- | --- |
| 打出「表征」 | `BeforeApplied`（不重写） | **不发力量**，能力只是挂上 |
| **下回合获得力量** | `AfterSideTurnStart` | 把当前预约（`Amount`）全部发出，并记入 `_grantedAmount` |
| **下回合结束时失去力量** | `AfterSideTurnEnd` | 只收回 `_grantedAmount` 的实发部分；没有预约剩余才移除能力 |

时序（打出当回合记为第 N 回合）：

```
第 N   回合  打出「表征」      → 能力挂上，不发力量
第 N+1 回合开始 AfterSideTurnStart → 发力量
第 N+1 回合结束 AfterSideTurnEnd   → 收回力量，能力移除
```

**为什么是一个计数而不是布尔标记**：这个能力是 `PowerStackType.Counter`，
第 N+1 回合内再打出「表征」会把新预约叠进同一个实例的 `Amount`，但力量只发过一次。
回合结束时必须只收回 `_grantedAmount` 记录的**实际已发放**部分，
并把剩余的预约 `SetAmount` 留到再下一回合——只用一个「已发放」布尔值的话，
结束时会按整个 `Amount` 扣力量（扣掉没发过的部分），还会把新预约一起删掉。

> **注意**：`OriginModel` 不是 `PowerModel` 的成员，它来自 `ITemporaryPower` 接口，
> 只有 `TemporaryStrengthPower` / `TemporaryDexterityPower` / `TemporaryFocusPower`
> 这几个基类实现它。普通 `PowerModel` 的标题走本地化表 `powers` 的
> `{Id.Entry}.title` / `.description`，所以这个能力需要自己写 powers.json。

### 临时敏捷

「键合-亚胺基」的临时敏捷是**当回合**生效、本回合结束时消失，正好是原版基类的行为，
所以直接继承、子类只提供来源卡牌：

```csharp
[RegisterPower]
public sealed class BondingImidogenDexterityPower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<BondingImidogen>();
}
```

原版的 `TemporaryStrengthPower`、`TemporaryDexterityPower` 都是**抽象基类**
（RitsuLib 的注册器会跳过抽象类型，见 `ModContentRegistry.cs:2061`），
所以每张牌都得配一个具体子类。基类的 `Title` 直接取 `OriginModel` 的标题，
所以这类能力会显示成来源卡牌的名字。

### 「晶化」牌怎么判定

使用稳定标记，不使用本地化名称：

```csharp
public static bool IsCrystallizationCard(CardModel card) => card is ICrystallizationCard;
```

新变体继承 `CrystallizationCard` 即可获得标记和手动额度，不需重复声明
`IsPlayable`，也不需为新语言补充名称列表。需要额外手动出牌条件时覆写
`CanPlayCrystallization`。

### 让玩家选牌

凡是「打出/消耗 N 张晶化」「至多打出 N 张」这类效果，都用 `CardSelectCmd.FromHand` +
`CardSelectorPrefs(prompt, minCount, maxCount)` 让玩家自己选：

```csharp
// 恰好 N 张（描述没写「至多」）→ min = max
prefs: new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue),

// 「至多 N 张」→ min = 0，允许一张都不选
prefs: new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
```

- **`minCount` 照描述字面来**：写「打出 N 张」就 `min = max = N`（必须选满）；
  写「至多 N 张」才 `min = 0`。
- `filter` 只让符合条件的牌可点。
- 提示文案里的 `{Amount}` 由 `CardSelectorPrefs` 自动注入，**不要**写 `{Cards}`。
- 需要两种提示文案时（比如「消耗」与「打出」），直接构造另一个 key 的
  `LocString`，例如 `new LocString("cards", Id.Entry + ".playSelectionScreenPrompt")`。

### 「每有一种负面状态就加伤」怎么算（`CalculatedDamageVar`）

「原波穿刺」的「造成 11 点伤害，目标身上每有一种负面状态就额外造成 3 点伤害」用的是原版的
**计算伤害**三件套，和原版「撕裂」（`Rend`）完全同构：

| DynamicVar | 作用 |
| --- | --- |
| `CalculationBaseVar(11m)` | 基础伤害 |
| `ExtraDamageVar(3m)` | 每一种负面状态额外加多少 |
| `CalculatedDamageVar(ValueProp.Move).WithMultiplier(...)` | 把「种数」作为乘数交给上面两项 |

好处是**卡面显示实时伤害**：`CalculatedDamageVar.UpdateCardPreview` 会按当前悬停的目标重算，
所以鼠标停在敌人身上时卡面数字已经是加过成的值，出牌时 `DamageCmd.Attack(DynamicVars.CalculatedDamage)`
用的也是同一个值——不需要在 `OnPlay` 里自己再算一遍。

「负面状态」的判定沿用原版 `Rend` 的口径：能力**当前层数**对应的类型是 `PowerType.Debuff`，
且**排除**临时能力（`ITemporaryPower`，例如「临时敏捷」）。同一能力叠 5 层也只算一种。

> **乘数必须是静态方法。** `WithMultiplier` 会检查委托的 `Target` 是不是模型对象，
> 捕获 `this` 的 lambda 会直接抛异常。所以「原波穿刺」把乘数写成 `private static decimal CountDebuffs(...)`。

### 「在手牌中时」怎么实现（卡牌自己的钩子）

「晶化Hs」的「在手牌中时，所有角色具有 1 点力量」没有用「往角色身上挂一个能力」的做法，
而是**让卡牌自己覆写伤害钩子**：

```csharp
public override decimal ModifyDamageAdditive(
    Creature? target, decimal amount, ValueProp props,
    Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
{
    if (Pile?.Type != PileType.Hand) return 0m;   // 不在手牌里就不生效
    if (dealer == null || !props.IsPoweredAttack()) return 0m;
    return 1m;                                     // 所有角色都 +1 力量
}
```

这能成立是因为**牌堆里的每一张牌都是钩子监听者**：
`CombatState.IterateHookListeners()` 会把所有玩家所有牌堆里的卡（以及它们的附魔/异常）
全部加进监听列表。所以卡牌在手里时也会收到 `ModifyDamageAdditive`、`AfterCardChangedPiles` 等回调。

好处是**动态**：离开手牌的那一刻加成自动消失，不需要监听牌堆变化、也不需要清理残留状态。
代价是能力栏里**不会**显示这个 +1 力量（因为它不是一个真的 `StrengthPower`），
而且它**不区分敌我**——判定里只要求 `dealer != null`，所以怪物和多人模式下的所有玩家都会受益。

> **为什么不用 `ModifyDamageAdditive` 之外的办法：** 原版没有任何「卡牌在手牌中持续生效」的卡，
> 也没有对应的钩子。`HasTurnEndInHandEffect` / `OnTurnEndInHand` 只在**回合结束**时触发一次，
> 不是持续效果，不能用来做这个。

### 让某张牌「免费」：费用修正钩子

「溯源猛击」的「下一张『运用』牌免费」用的是原版 `FreeSkillPower` / `FreeAttackPower` 的同款两件套：

```csharp
// 1. 把符合条件的牌费用改成 0（Late 版本跑在费用修正链最后，能盖过前面所有加成）
public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)

// 2. 真正打出那张牌时减一层，所以只对「下一张」生效
public override async Task BeforeCardPlayed(CardPlay cardPlay)
{
    await PowerCmd.Decrement(this);
}
```

「运用」牌按 `IApplicationCard` 标记判定（`ApplicationRules.IsApplicationCard`）。
新运用牌声明该接口即可被认到；通用的 `ApplicationCard` 基类不代表牌族身份。

> **牌堆判定**只认 `PileType.Hand` 和 `PileType.Play`（前者正常从手里打、后者被自动打出），
> 原版这两个 Power 也是这么判的。

### 让某张牌「每被保留一次就成长」：`AfterFlush` 钩子

回合结束弃牌时，`Hook.AfterFlush` 会同时传出**被弃掉的牌**和**被留下的牌**：

```csharp
public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player,
    IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
{
    if (!retainedCards.Contains(this)) return Task.CompletedTask;   // 没被保留就不成长
    DynamicVars.Damage.BaseValue += DynamicVars["RetainGrowth"].BaseValue;
    return Task.CompletedTask;
}
```

这个钩子走 `IterateCombatHookListeners`，**包含牌堆里的卡**，所以手牌里的牌自己就能收到通知
（和「在手牌中时」用的是同一个事实）。

成长直接写进 `DynamicVars.Damage.BaseValue`：卡牌的 DynamicVars 是**每个实例独立**的
（`CardModel.DynamicVars` 按实例惰性创建），所以只影响这一张牌，且在本场战斗内一直保留。

> **注意防重复成长**：自带「保留」的牌会一直留在手里，如果每回合结束都无条件加伤就会失控。
> 「气相涌流」用了一个 `_boostedThisTurn` 标记，在 `BeforeSideTurnStart` 里重置。

### 「每累计 N 点伤害触发一次」：自己记累计值

原版没有这种机制，做法是给目标挂一个隐藏能力，用 `AfterDamageGiven` 累加**实际掉血**：

```csharp
_accumulatedDamage += result.UnblockedDamage;
while (_accumulatedDamage >= _damageThreshold)
{
    _accumulatedDamage -= _damageThreshold;   // 余数保留
    await PowerCmd.Apply<PoisonPower>(...);
}
```

用 `while` 而不是 `if`，这样一次受到大量伤害时可以触发多次。

> **挂在敌人身上的减益要自己设 `SkipNextDurationTick`。**
> `PowerCmd.Apply` 只会给「挂在**玩家**身上的减益」自动设这个标记，
> 挂在敌人身上的不会，不设的话它会在最近一次持续时长结算时被提前移除。

### 「失去生命」怎么写

没有专门的「失去生命」命令。原版「摄魂」（`CaptureSpirit`）的做法是发一次带这三个 flag 的伤害：

```csharp
new DamageVar(4m, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move)
```

- `Unblockable` —— 无视格挡
- `Unpowered` —— 不吃力量加成
- `Move` —— 算作「攻击」类伤害

然后正常 `CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, this, cardPlay)` 即可。

### 「变化」一张牌：`CardCmd.TransformTo<T>`

「运用-Pb」的「将状态牌变化为晶化-Pb」用的是原版现成的变化命令，
和原版「降神」（`Seance`）把牌变成「灵魂」完全一样：

```csharp
await CardCmd.TransformTo<CrystallizationPb>(card);
```

配套的选择界面用 `CardSelectorPrefs` 的**三参数**重载来表达「至多」：

```csharp
new CardSelectorPrefs(SelectionScreenPrompt, 0, maxCount)   // min = 0 → 可以不选
```

### 「回合结束时若在手中」：`OnTurnEndInHand`

```csharp
public override bool HasTurnEndInHandEffect => true;

protected override Task OnTurnEndInHand(PlayerChoiceContext choiceContext) { ... }
```

**时机很关键**：这个钩子在**弃牌之前**触发
（`CombatManager.cs:1671` 调 `OnTurnEndInHandWrapper`，`Flush` 在后面才跑），
所以此时给别的牌挂「保留」还来得及生效。

「随机取 N 张」用 `TakeRandom` 扩展方法：

```csharp
PileType.Hand.GetPile(Owner).Cards
    .Where(card => card.Type != CardType.Status)
    .TakeRandom(1, Owner.RunState.Rng.CombatCardSelection);
```

「保留」这里用的是**单回合**的 `GiveSingleTurnRetain()` 而不是永久 `AddKeyword`，
因为规格说的是"回合结束时保留 1 张"，只针对这一次。

### 「失去所有层数」：通过延拓公共入口

坍缩使用 `ExtensionRules.TryConsumeAll(Owner, threshold)`：
达到正数阈值才清空全部延拓，否则不改变状态。它随后调用本体指令立即获得能量。

读取层数使用 `ExtensionRules.GetAmount(Owner)`，无延拓时返回0。
业务卡牌不再直接操作 `EnergyNextTurnPower`，以便以后替换延拓的表示方式。

### 「一张牌有多个效果、上下切换」怎么实现

「运用-H」的「晶抗切到下一个效果 / 晶逆切到上一个效果」**原版完全没有**，做法是
**在卡牌实例上存一个下标**：

```csharp
private static readonly Type[] DebuffOrder =
    [typeof(VulnerablePower), typeof(WeakPower), typeof(FrailPower)];
private int _stateIndex;   // 0 = 失去易伤
```

- 卡牌的 DynamicVars 已经是**每实例独立**的，实例字段同理，只影响这一张牌。
- 下一个 = `(i + 1) % 3`，上一个 = `(i + 2) % 3`（等价于 `-1`，避免负数取模的坑）。
- 出牌时按 `_stateIndex` 取对应的减益来扣层。

**「每场战斗开始时重置」是这里的难点。** 试过 `BeforeCombatStart`，但那个钩子走的是
`runState.IterateHookListeners`（`Hook.cs:313`），**只包含遗物/能力/药水，不含牌组里的卡**，
卡牌根本收不到。可行的信号是 `AfterCardChangedPiles`——它走
`runState.IterateHookListeners(combatState)`，牌堆里的卡能收到
（原版「君主之刃」就在这个钩子里直接读 `card.Pile.Type`），
而战斗开始时手牌/抽牌堆必然发生变化。于是记录**战斗状态实例**：

```csharp
public override Task AfterCardChangedPiles(CardModel card, PileType oldPile, AbstractModel? clonedBy)
{
    if (card != this) return Task.CompletedTask;
    ICombatState? current = CombatState;
    if (!ReferenceEquals(_lastCombatState, current))   // 换了实例 → 新战斗
    {
        _lastCombatState = current;
        _stateIndex = 0;
    }
    return Task.CompletedTask;
}
```

用 `ReferenceEquals` 比 ID 更省事：每场战斗的 `ICombatState` 都是新实例。

### 把「当前状态」显示在卡面上

「运用-H」当前会扣哪种减益是动态的，不打出看不出来。解法是覆写
`CardModel.AddExtraArgsToDescription`（`CardModel.cs:1545`，protected virtual），
把状态注入描述：

```csharp
protected override void AddExtraArgsToDescription(LocString description)
{
    description.Add("CurrentEffect", TitleAt(_stateIndex));
    description.Add("NextEffect", TitleAt(NextIndex));          // 晶抗会切到哪
    description.Add("PreviousEffect", TitleAt(PreviousIndex));  // 晶逆会切到哪
}
```

本地化文案里写 `{CurrentEffect}` 就会被替换掉。所以主效果是易伤时，卡面显示：

```
失去1层易伤。
晶抗：切换为失去虚弱。
晶逆：切换为失去脆弱。
```

要点：

- **切换逻辑和显示逻辑共用同一个下标计算**（`NextIndex` / `PreviousIndex`），
  所以卡面显示的必然是实际会发生的结果，不会两边各写一份而分叉。
- **注入 `LocString` 而不是字符串**：`LocString.Add(string, LocString)` 内部走
  `GetFormattedText()`（`LocString.cs:139`），所以名字跟着语言走，也能保留富文本。
- **时机没问题**：`AddExtraArgsToDescription` 在 `GetDescriptionForPile` 里被调用
  （`CardModel.cs:1376`），而 `DynamicVars.AddTo(description)` 在它**之前**执行，
  所以它拿不到 `DebuffAmount`——但它也不需要，它只注入自己的变量。
- **切换后显式刷一次卡面**：卡面本来会在下次 `UpdateVisuals` 时自动重建描述，
  但那时这张牌可能已经离开手牌了。所以切换后主动刷：

  ```csharp
  NCard.FindOnTable(this, pileType)?.UpdateVisuals(pileType, CardPreviewMode.Normal);
  ```

  这和原版 `CardCmd.ApplyKeyword`（`CardCmd.cs:686`）刷新卡面的做法完全一致。
- 取能力模型的本地化标题用 `ModelDb.Power<T>()`；因为需要按运行期下标取，
  数组里存的是 `Func<PowerModel>` 工厂而不是 `Type`（`ModelDb` 只公开泛型的
  `Power<T>()`，没有按 `Type` 取能力的公开方法）。

### 「效果」数量怎么数

「分析」的「你每有一种效果」= **显示在角色下方的那一排能力图标**，
直接用 `PowerModel.IsVisible`（`PowerModel.cs:150`）：

```csharp
private static int CountVisibleEffects(Creature creature)
    => creature.Powers.Count(power => power.IsVisible);
```

好处是**隐藏能力自动被排除**，不需要在卡里维护一份"哪些能力算数"的名单——
本模组那些 `IsVisibleInternal => false` 的辅助能力（`LoseWeakAtTurnEndPower` 等）
都不会被数进去。

敌人的效果只数**这张牌的目标**（卡是单体指向的），而不是所有敌人。

### 「下一次获得的格挡翻倍」怎么实现

用 `ModifyBlockMultiplicative` 返回 2，和原版「影融」（`ShadowmeldPower`）同一个钩子。
**关键区别是"只翻倍一次"**：影融是整个回合都翻倍、回合结束才移除；这里是"下一次"。

实现靠 `AfterModifyingBlockAmount`，因为它**只会对真正修改了数值的能力触发**
（`Hook.cs:1332-1350` 把改动过数值的能力收集进 `modifiers`，
`Hook.cs:661-671` 只回调这些），所以不会误伤无关的格挡：

```csharp
public override async Task AfterModifyingBlockAmount(decimal modifiedAmount, CardModel? cardSource, CardPlay? cardPlay)
{
    await PowerCmd.Decrement(this);
}
```

判定"从卡牌中获得"用 `cardSource != null`：卡牌给的格挡一定带 `cardSource`，
怪物动作 / 遗物等不带。

### 「本场游戏永久 -N」的计数：牌组原件 + 可保存附加状态

「晶币」每次打出后**本场游戏**（整个 run）收益永久 -3，而且升级会改基础值。
这里有三个独立的坑。

**坑一：跨战斗必须落在牌组原件上。** 每场战斗开始时卡牌会被克隆：
`Player.PopulateCombatState`（`Player.cs:806-815`）对牌组里每张牌调用
`state.CloneCard(item)`，把克隆体放进抽牌堆，并把 `DeckVersion` 指向牌组里的原件。
战斗中打的是**克隆体**，克隆体打完就丢。所以晶币读写计数时都先解析到原件：

```csharp
private CrystalCoin PersistentSelf => DeckVersion is CrystalCoin deckVersion ? deckVersion : this;
```

**坑二：写回牌组原件还不够，存档会丢。** 普通实例字段不走
`CardModel.ToSerializable` 的 `SavedProperties` 序列化，保存退出再读档就归零，
玩家可以靠重新载入取消收益递减。所以计数使用晶币专属的原生
`[SavedProperty]`，随跑局存档读写，并由原生模型缓存注册网络字段编号：

```csharp
[SavedProperty]
public int CrystalCoinPlaysThisRun
{
    get => PersistentSelf._playsThisRun;
    set { AssertMutable(); _playsThisRun = Math.Max(0, value); }
}
```

状态按牌实例区分，多张晶币各自独立递减；独立复制继承复制时的递减，之后各自递减。
不能将带默认值的持久化状态注册到所有`CardModel`上，否则原版卡牌、奖励候选和地图历史也可能带入该字段；禁用模组后回放序列化会因字段编号缺失失败。

**坑三：这次出牌不能被算进去。** `CardPlayStarted` 是在 `OnPlay`
**之前**记录的（`CardModel.cs:1929`），所以如果按"历史里的条数"算，
第一次打出就会被算成 1 次，18 变成 15。先取金币再自增计数就没这个问题。

顺带一提：计数和 `DynamicVars` 完全无关，所以升级/降级都不会把它清掉
（降级时 `DowngradeInternal` 只重置 `DynamicVars`）。

### 让 `:diff()` 显示动态值：必须给它一个 `DynamicVar`

「晶币」卡面要显示"当前实际金币"，一开始写成 `{CurrentGold:diff()}` 并注入一个
`decimal`，结果卡面直接显示原文 `[CurrentGold:diff()]`。

原因：**`diff` 格式化器只认 `DynamicVar` 对象。**
`HighlightDifferencesFormatter.TryEvaluateFormat` 第一件事就是
`if (!(formattingInfo.CurrentValue is DynamicVar)) return false;`，
拿到普通数字会直接放弃格式化。

正确做法是改 `Gold` 这个 `DynamicVar` 的显示值，并让文案引用 `{Gold:diff()}`：

```csharp
protected override void AddExtraArgsToDescription(LocString description)
{
    DynamicVars["Gold"].PreviewValue = CurrentGold;
}
```

之所以这样能生效：`DynamicVars.AddTo(description)` 放进描述变量里的是
`DynamicVar` **对象本身**（`DynamicVarSet.cs:122`），不是当时的值；
而 `GetFormattedText()`（`CardModel.cs:1403`）在 `AddExtraArgsToDescription` **之后**才调用。
所以在这里改 `PreviewValue`，格式化时读到的就是新值。

顺带还能拿到正确的变色——`ToHighlightedString` 用的正是 `PreviewValue`，
比基础值低会显示红色、刚升级会显示绿色。

### 「抽到的第 1 张牌获得某个能力」：拿 Draw 的返回值，别让能力去猜

「运用-Co」要给**抽到的第 1 张牌**挂上「晶化 2」和保留。这里踩过两个坑。

**坑一：不能让能力在 `AfterCardDrawn` 里认"第一张"。**
那个钩子对**每一张**抽到的牌都会触发，能力没有"我是第几张"的信息，
结果三张牌全被标记了。正确做法是直接拿 `Draw` 的返回值：

```csharp
IEnumerable<CardModel> drawn = await CardPileCmd.Draw(choiceContext, count, Owner);
CardModel? first = drawn.FirstOrDefault();
```

`CardPileCmd.Draw` 返回 `Task<IEnumerable<CardModel>>`（`CardPileCmd.cs:985`），
而且**返回顺序就是抽牌顺序**（`DrawInternal` 按顺序 `result.Add(card)`，`:1060`）。

**坑二：效果得看得见。** 把"晶化 2"只存在能力里，玩家在卡面上看不到它。
原版有现成的机制：**侵蚀（Affliction）**。`CardModel.GetDescriptionForPile`
会把 `Affliction?.DynamicExtraCardText` 拼到卡面描述后面（`CardModel.cs:1410-1414`），
所以只要写一个侵蚀、覆写 `HasExtraCardText` 返回 true，卡面就会出现那行字：

```csharp
[RegisterAffliction]
public sealed class CrystallizeAffliction : ModAfflictionTemplate
{
    public override bool HasExtraCardText => true;
    public override bool IsStackable => true;
}
```

文案里的 `{Amount}` 由 `AfflictionModel` 自己注入（`AfflictionModel.cs:57`），
所以「2」这个数字来自层数，不用另外处理。挂上去用 `CardCmd.Afflict<T>(card, amount)`。

这样分工就清楚了：**哪张牌带效果由侵蚀记在牌自己身上**，
能力只负责在 `BeforeCardPlayed` 里查一下"这张牌有没有那个侵蚀"然后结算。

`BeforeCardPlayed` 由 `CardModel.cs:1926` 触发，位置在 `CardPlayStarted`（`:1929`）
和 `OnPlay`（`:1933`）**之前**，所以「晶化」会在那张牌自己的效果之前进手牌。

### 「抽了几张牌」：在能力自己身上累加，别去历史里找下标

「回忆-钒永」要统计"本回合抽了几张牌，下回合就多抽几张"。规则是：

| 操作 | 抽牌序列 | 下回合多抽 |
|---|---|---|
| 钒永 | 自己抽 1 | 1 |
| 钒永 ×2 | 自己抽 1 + 自己抽 1 | 2 |
| 钒永 + 抽 2 张的卡 | 自己抽 1 + 2 | 3 |
| 钒永 ×2 + 抽 2 张的卡 | 1 + 1 + 2 | 4 |

也就是**从钒永结算开始，本回合抽的每一张都算**（含它自己抽的那张），
所以累加器要在抽牌**之前**就开始计数。

**试过但不要用的做法：数 `CombatHistory`。** 要先在历史里找到"这张牌的出牌记录"
再往后数：

```csharp
int start = entries.FindIndex(
    e => e is CardPlayStartedEntry s && ReferenceEquals(s.CardPlay, _baseline));
return entries.Skip(start + 1).OfType<CardDrawnEntry>().Count(...);
```

这段代码本身没错（`CardPlayStartedEntry` 存的就是那次出牌的 `CardPlay` 对象本身，
`CardPlay` 没重写 `Equals`，引用相等可靠），但**下标查找太脆弱**：
一旦"同一回合打出第二张"把基线覆盖掉，整个统计就错了。

**正确做法：在能力自己身上累加**，没有任何外部依赖：

```csharp
public void Configure(CardPlay cardPlay) { _counting = true; }

public override Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw)
{
    if (_counting && card.Owner.Creature == base.Owner) _drawnThisTurn++;
    return Task.CompletedTask;
}
```

### 两个容易踩的「叠加」坑

同一回合打出两张同一张牌时，`PowerCmd.Apply` 走的是**叠加**分支
（`PowerCmd.cs:88` 的 `ModifyAmount`），**两次共用一个实例**。这带来两个坑：

**坑一：别在每次施加时重置状态。** 「回忆-钒永」的 `Configure` 如果每次都重置累加器，
第二张就会把第一张数到的清掉。所以 `Configure` 只置一个标志，计数继续累加。

**坑二：别在回合结束用 `SetAmount` 覆盖。** 两次施加共用一个实例，
第二次 `SetAmount` 会把第一次的结果冲掉。所以「回忆-钒永」把统计结果存在
自己的字段 `_carriedDraws` 里，`Amount` 只作显示、不再当"下回合抽几张"的输入：

```csharp
public override decimal ModifyHandDraw(Player player, decimal count)
    => player != base.Owner.Player || _carriedDraws <= 0 ? count : count + _carriedDraws;
```

（顺带一提：`AfterSideTurnStart` 里不能用 `Amount` 当判据去 `Remove` 自己，
因为那时 `Amount` 已经被这回合的抽牌消耗掉了。）

### 「回合结束才确定、下回合生效」的抽牌：自己写，别用原版

原版 `DrawCardsNextTurnPower` 靠 `AmountOnTurnStart` 判断"这张能力是不是本回合开始时就存在"，
所以**只能"本回合施加、下回合生效"**。而「回忆-钒永」要统计整个回合抽了多少张，
**回合结束时**才写数字——那时再挂它就已经晚了（`AmountOnTurnStart` 是 0，不生效）。

所以自己写一个，`ModifyHandDraw` 直接读 `Amount` 而不看 `AmountOnTurnStart`：

```csharp
public override decimal ModifyHandDraw(Player player, decimal count)
    => (player == base.Owner.Player && base.Amount > 0) ? count + base.Amount : count;
```

注意 **`PowerCmd.Apply` 在 `amount == 0` 时直接 return**（`PowerCmd.cs:107`），
能力根本不会创建。所以施加时初始层数要给 1，真正的数字在 `AfterSideTurnEnd`
里用 `SetAmount` 写进去。

## 卡牌一览

| 卡牌 | 费用 | 类型 | 稀有度 | 效果 |
| --- | --- | --- | --- | --- |
| 打击 | 1 | 攻击 | 基础 | 造成 6 点伤害。（升级 9） |
| 防御 | 1 | 技能 | 基础 | 获得 5 点格挡。（升级 8） |
| 晶化 | 0 | 状态 | — | 保留、消耗。牌名中有「晶化」的牌同一回合只能直接打出一次。 |
| 晶化Mg | 0 | 状态 | — | 保留、消耗。获得 4 点格挡。同一回合只能直接打出一次（与「晶化」共用）。 |
| 晶化Nb | 0 | 状态 | — | 保留、消耗。获得 4 层活力。同一回合只能直接打出一次（与「晶化」共用）。 |
| 晶化U | 0 | 状态 | — | 保留、消耗。从任意位置将一张「运用-中子+」放回手牌，并使其获得消耗。同一回合只能直接打出一次（与「晶化」共用）。 |
| 运用-In | 1 | 技能 | 基础 | 晶化 1。延拓 2。（升级：获得**保留**） |
| 近身斩 | 1 | 攻击 | 基础 | 造成11点伤害（升级15，费用仍为1）。<br>晶抗：自身获得1层虚弱，本回合末移除本牌新增层数。<br>晶逆：自身获得1层易伤，敌方行动结束时移除本牌新增层数。 |
| 运用-F | 0 | 攻击 | 普通 | 消耗。造成 6 点伤害。（升级 8）<br>晶逆：将 1 张本牌的复制加入手牌，晶化 2。 |
| 回身踢 | 1 | 攻击 | 普通 | 消耗。造成 2 点伤害 2 次。（升级 3 次，且不再消耗）<br>将弃牌堆中的 1 张牌加入手牌。<br>晶逆：使其本回合具有保留。 |
| 因势利导 | 1 | 攻击 | 普通 | 造成 7 点伤害。（升级 10）<br>晶抗：对同一目标造成 3 点伤害（升级 5），然后给予 1 层易伤。<br>晶逆：给予 1 层虚弱。 |
| 运用-Zn | 1 | 技能 | 普通 | 获得 12 点格挡。（升级 15）<br>晶抗：失去 1 点能量。<br>晶逆：消耗这张牌。 |
| 运用-Mg | 1 | 攻击 | 普通 | 造成 8 点伤害（升级 11）。<br>晶化（Mg）2：向手牌加入 2 张「晶化Mg」。 |
| 离析 | 1 | 技能 | 普通 | 打出 1 张「晶化」（升级 2 张），然后抽 2 张牌。 |
| 待发 | 0 | 技能 | 普通 | 获得 1 点能量，抽 1 张牌（升级 2 张），本回合只能打出攻击牌。 |
| 键合-亚胺基 | 2 | 技能 | 普通 | 消耗至多 2 张手牌中的「晶化」。<br>获得 3 点临时敏捷（本回合结束时消失）。<br>延拓 2。<br>升级：获得**保留**，且改为「打出」而非「消耗」。 |
| 键合-硝基 | 1 | 攻击 | 普通 | 造成 6 点伤害。<br>延拓 1。<br>抽 1 张牌。<br>升级：获得**保留**。 |
| 原波穿刺 | 2 | 攻击 | 普通 | 造成 11 点伤害。<br>目标身上每有一种负面状态，就额外造成 3 点伤害（升级 4）。 |
| 表征 | 1 | 技能 | 普通 | 获得 7 点格挡。（升级 9）<br>晶抗：下回合获得 3 点临时力量（升级 4）。<br>晶逆：下回合抽 2 张牌（升级 3）。 |
| 晶流 | 1 | 技能 | 普通 | 获得 5 点格挡。（升级 6）<br>晶抗：给予 1 层虚弱（升级 2）。<br>晶逆：重放 1（只重复主效果，不会再触发晶抗/晶逆）。 |
| 合斩 | 0 | 攻击 | 普通 | 对所有敌人造成 5 点伤害。（升级 7） |
| 运用-As | 2 | 攻击 | 普通 | 造成 23 点伤害（升级 27）。<br>晶化 2。 |
| 碎晶雨 | 1 | 攻击 | 普通 | 造成 3 点伤害 3 次（升级：3 点 4 次）。<br>晶抗：手牌中每有一张「晶化」，就**额外**再造成 3 点伤害一次（升级 4，所以 1 张晶化共 4 段）。<br>晶逆：对其他敌人造成等量伤害（= 每段伤害 × 基础段数，不含晶化加成）。 |
| 锈蚀之刃 | 1 | 攻击 | 普通 | 消耗。造成 10 点伤害（升级 14），将目标的虚弱层数翻倍。 |
| 键合-酯基 | 1 | 技能 | 普通 | 获得 6 点格挡（升级 8），为手牌中的 1 张非攻击牌添加**保留**。<br>晶逆：延拓 1。 |
| 运用-Hs | 0 | 技能 | 普通 | 消耗。使所有敌人失去 7 点**临时力量**（升级 10，本回合结束时恢复）。<br>晶化（Hs）2。 |
| 晶化Hs | 0 | 状态 | 基础 | 保留、消耗。<br>**在手牌中时**，所有角色（所有玩家含多人模式，以及所有怪物）具有 1 点力量。<br>牌名中有「晶化」的牌同一回合只能直接打出一次。 |
| 运用-Th | 1 | 攻击 | 罕见 | 晶化 1。使一名敌人失去 4 点生命（升级 5）。<br>晶抗：造成等量伤害 2 次（普通攻击伤害）。<br>晶逆：重放 2，不会再触发晶抗/晶逆。 |
| 运用-Cu | 2 | 攻击 | 罕见 | 造成 13 点伤害。本回合，这名敌人每受到 7 点攻击伤害（升级 6），获得 2 层中毒。 |
| 溯源猛击 | 2 | 攻击 | 罕见 | 造成 14 点伤害（升级 20）。你的下一张「运用」牌可以免费打出。 |
| 构象打击 | 0 | 攻击 | 罕见 | 造成 4 点伤害。<br>晶抗：失去 1 点能量；每 1 层延拓额外造成 5 点伤害（升级 7）。<br>晶逆：手牌每 1 张晶化额外造成 3 点伤害（升级 4）。 |
| 温压领域 | X | 攻击 | 罕见 | 获得等同于手牌晶化数量的**临时力量**（本回合结束消失）。<br>造成 7 点伤害 X 次（升级 9）。 |
| 气相涌流 | 1 | 攻击 | 罕见 | 造成 6 点伤害；每被保留一次，本场战斗伤害 +6（升级 +9）。<br>晶抗：回合开始时返回手牌。<br>晶逆：获得保留。 |
| 坍缩 | 3 | 攻击 | 罕见 | 造成 27 点伤害（升级 33）。若有不少于 3 层延拓（升级 2），失去所有延拓，获得 2 点能量。 |
| 运用-Pb | 1 | 攻击 | 罕见 | 造成 9 点伤害（升级 11）。将至多 1 张状态牌变化为晶化-Pb（升级 2）。<br>晶抗：晶化 1。 |
| 晶簇枪 | 0 | 攻击 | 罕见 | 造成 6 点伤害（升级 8）。若你没有晶化，晶化 1，抽 1 张牌。 |
| 晶化-Pb | 0 | 状态 | 基础 | 保留、消耗、**虚无**。<br>牌名中有「晶化」的牌同一回合只能直接打出一次。<br>回合结束时，若在手中，随机保留 1 张非状态牌。 |
| 运用-H | 0 | 技能 | 罕见 | 失去 1 层易伤/虚弱/脆弱（升级 2 层）。**卡面会显示当前是哪种效果。**<br>晶抗：切换到下一个效果。<br>晶逆：切换到上一个效果。 |
| 分析 | 1 | 技能 | 罕见 | 你每有一种效果，获得 2 点格挡；敌人每有一种效果，失去 2 点生命。<br>晶抗：失去 1 点能量。<br>晶逆：消耗。（升级后费用 1→0） |
| 运用-Ca | 0 | 技能 | 罕见 | 获得 3 点格挡（升级 5）。<br>晶抗：失去 1 点能量。下一次从卡牌中获得的格挡翻倍。 |
| 晶币 | 1 | 技能 | 罕见 | 虚无。获得 18 金币（升级 24）。此牌在本场游戏中给予的金币永久 -3（下限 0）。**卡面显示当前实际金币。** |
| 基团迁移 | 1 | 技能 | 罕见 | 延拓 2（升级 3）。<br>晶抗：打出 1 张晶化。<br>晶逆：将手牌中的 1 张牌放在抽牌堆顶（升级：任意张）。 |
| 运用-Co | 1 | 技能 | 罕见 | 抽 3 张牌（升级 4）。抽到的第 1 张牌在本回合获得「晶化 2」和保留。 |
| 运用-Nb | 1 | 能力 | 罕见 | 晶化（Nb）2。活力改为在打出攻击牌后只减少 2 层。（升级：打出时获得 2 层活力） |
| 回忆-污染O | 1 | 能力 | 罕见 | 晶抗：打出「晶化」时获得 4 点格挡。晶逆：打出「晶化」时获得 3 层活力。（升级：固有） |
| 回忆-污染H | 1 | 能力 | 罕见 | 你每触发 4 次晶抗（升级 3），延拓 1 并抽 1 张牌。 |
| 回忆-硬钙石 | 1 | 能力 | 罕见 | 回合结束时，若你本回合消耗过牌，对所有敌人造成 3（升级 4）× 手牌中晶化数量的伤害。 |
| 量子点 | 2 | 能力 | 罕见 | 你每回合打出的前 2 张牌会同时触发晶抗和晶逆效果。（升级：费用降为 1） |
| 延拓展延 | 1 | 能力 | 罕见 | 回合结束时，你每拥有 1 层延拓，获得 3 点格挡。（升级：费用降为 0） |
| 回忆-钒永 | 1 | 技能 | 罕见 | 本回合每当你抽牌时，下回合开始时抽等量牌。抽 1 张牌（升级 2）。 |
| 崩碎之歌 | 2 | 攻击 | 稀有 | 消耗。消耗手牌中所有晶化，每张造成 9 点伤害（升级 11，且以打出代替消耗）。 |
| 运用-U | 3 | 技能 | 稀有 | 消耗。将 3 张具有「晶化（U）1」的「运用-中子+」加入手牌。（升级：费用降为 2） |
| 密铺形态 | 3 | 能力 | 稀有 | 晶化 3。所有晶化牌具有「在手中时，你具有 2 点力量和 2 点敏捷」。（升级：保留） |
| 回忆-锡疑 | 9 | 能力 | 稀有 | 本回合，你每打出一张牌，交替获得 1 点临时力量/临时敏捷。每当你打出延拓牌时，此牌花费 -2（升级 3）。 |
| 嬗变 | 1 | 能力 | 稀有 | 回合开始时，将一张晶化变化为抽牌堆顶的牌。（升级：固有） |
| 回忆-磷语 | 1 | 能力 | 稀有 | 每回合第一次获得延拓时，改为获得等量能量。（升级：同时获得能量和延拓） |
| 晶罚 | 1 | 攻击 | 稀有 | 造成9点伤害，为当前手牌中所有晶化添加给予3层消亡的效果（升级4）；重复强化累计，保留晶化原效果。消亡使用消亡粉末的DemisePower，在敌方回合末失去等层数生命。 |
| 隧穿 | X | 技能 | 稀有 | 消耗。结束当前回合并获得一个额外回合；额外回合至多打出X张牌（升级X+1）。自动出牌计入，系列重放不另计。 |
| 晶髓 | X | 技能 | 稀有 | 延拓X；下回合额外抽X张牌（升级X+1），所有手牌花费+1、重放1。后来抽到或生成的手牌同样生效，X费牌维持X费用。 |

2026-10-06补齐后，共68张定义已有核心实现（61主卡、7衍生卡）。当前规则和历史表中的差异以工作区`卡牌开发/规则定稿_2026-10-05.json`为准；隧穿、晶髓实现及少量验证记录见`卡牌开发/剩余卡牌实现记录_2026-10-06.md`。实战、完整存档和多人验收仍在待办中。

### 卡面文案的写法

- **关键字上色**：用 `[gold]晶抗[/gold]` 这样的标记，效果和原版的「保留」「格挡」一致。
  目前 `晶化`、`延拓`、`晶抗`、`晶逆` 四个词都上了金色。
- **每段效果一行**：用 `\n` 换行，不要把多段效果挤在同一行。
- 原版描述里 `\n` 和 `[gold]` 都是常规用法，可参考 `localization/zhs/cards.json` 里的
  `ABRASIVE.description`、`BIG_BANG.description`。

## 依赖

- **游戏本体**：Slay the Spire 2（本工程按 `min_game_version` 0.111.0 编写）
- **模组前置**：[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) —— 运行期必须把它的模组装进游戏的 `mods` 目录
- **编译**：.NET 9 SDK
- **（可选）导出 .pck**：与游戏同版本的 Godot **Mono** 4.5.1（社区版叫 MegaDot）。
  本地化文本必须打进 `.pck` 才生效，所以**构建时默认会导出**，这一步不能省。

## 目录结构

```
IndiumStar/
├─ IndiumStar.json                     # 模组清单（id / 版本 / 依赖）
├─ IndiumStar.csproj                   # 编译配置，构建后自动复制到游戏 mods 目录
├─ project.godot                       # Godot 工程文件（导出 pck 用）
├─ export_presets.cfg                  # 导出预设
├─ local.props.template                # 本机游戏路径模板（复制成 local.props 使用）
├─ src/
│  ├─ Entry.cs                         # [ModInitializer] 入口：关键字、补丁和程序集注册
│  ├─ Characters/
│  │  └─ IndiumStarCharacter.cs        # [RegisterCharacter] 角色本体
│  ├─ CardPools/
│  │  ├─ IndiumStarCardPool.cs         # [RegisterSharedCardPool]
│  │  ├─ IndiumStarRelicPool.cs        # [RegisterSharedRelicPool]
│  │  └─ IndiumStarPotionPool.cs       # [RegisterSharedPotionPool]
│  └─ Cards/
│     ├─ StrikeIndiumStar.cs           # [RegisterCard] + [RegisterCharacterStarterCard(4)]
│     └─ DefendIndiumStar.cs           # [RegisterCard] + [RegisterCharacterStarterCard(4)]
│  ├─ Powers/                          # [RegisterPower]
│  └─ Afflictions/                     # [RegisterAffliction]
└─ IndiumStar/localization/            # 本地化。注意：这个文件夹名必须等于清单里的 id
   ├─ zhs/{characters,cards,card_keywords,powers,afflictions}.json
   └─ eng/{characters,cards,card_keywords,powers,afflictions}.json
```

## 构建

### 1. 告诉编译器游戏在哪

**只需要配置一次。** 复制 `local.props.template` 为 `local.props`（已被 .gitignore 忽略），
按自己的安装位置改两行：

```xml
<Sts2Dir>D:\Steam\steamapps\common\Slay the Spire 2</Sts2Dir>
<GodotExe>D:\godot\Godot_v4.5.1-stable_mono_win64.exe</GodotExe>
```

也可以临时用别的方式覆盖（优先级从高到低）：

```powershell
# A. 命令行（优先级最高）
dotnet build -c Release -p:Sts2Dir="D:\Steam\steamapps\common\Slay the Spire 2"

# B. 环境变量
$env:STS2_DIR = "D:\Steam\steamapps\common\Slay the Spire 2"
dotnet build -c Release
```

> ⚠️ `Sts2Dir` 要填**游戏根目录**（里面能直接看到 `sts2.dll` 和 `data_sts2_windows_x86_64` 文件夹），
> **不是** `data_sts2_windows_x86_64` 本身。填错了构建会直接报一句中文错误告诉你路径不对，
> 而不是丢出上百条「找不到 MegaCrit 命名空间」——那些错误全是这一个原因引起的。

### 2. 安装 Godot 4.5.1 Mono（只需一次）

本地化文本必须打进 `.pck` 才能被游戏读到，所以构建需要 Godot。

下载 **4.5.1** 的 **Mono / .NET** 版（版本号必须和游戏一致，游戏是 Godot 4.5.1）：

```
https://godotengine.org/download/archive/4.5.1/
```

解压后把 exe 完整路径填进 `local.props` 的 `GodotExe`（见上一节）。

> 注意是**带 Mono/.NET** 的那个（文件名里有 `mono`），不是标准版。
> 标准版没有 C# 支持，导出会失败。

### 3. 编译并自动安装

```powershell
cd IndiumStar
dotnet build -c Release
```

构建分三步：**先在项目内暂存（`build\staging\`），全部成功后才部署到游戏目录**。
导出 PCK 失败不会让游戏目录留下「新 DLL + 旧 PCK」的混合安装。
最终 `游戏目录\mods\IndiumStar\` 里应该有：

```
IndiumStar.dll
IndiumStar.json
IndiumStar.pck          ← 本地化文本在这里面
IndiumStar\
  localization\
    zhs\characters.json
    zhs\cards.json
    eng\characters.json
    eng\cards.json
```

> 那个多出来的一层 `IndiumStar\` 子目录不是多余的：游戏按
> `res://{清单id}/localization/{语言}/{表}.json` 查找，清单 id 是 `IndiumStar`，
> 所以 mod 文件夹里要再套一层同名目录。构建脚本会自动创建。

常用开关：

| 开关 | 行为 |
| --- | --- |
| `dotnet build -c Release` | 编译 → 暂存 → 导出 PCK → 部署到游戏目录 |
| `-p:ExportPck=false` | 只编译并暂存，不导 PCK，**默认也不部署**（避免不完整安装） |
| `-p:DeployMod=false` | 完整构建（含 PCK）但不碰游戏目录 |
| `-p:DeployMod=true` | 配合 `ExportPck=false` 强制部署（确认你不需要 PCK 时才用） |

> RitsuLib 前置的自动部署由 csproj 里的 `RitsuLibDeployDir` 控制：
> 构建会把 STS2.RitsuLib 包装进 `游戏目录\mods\STS2-RitsuLib`。
> 如果你手动管理 RitsuLib 的安装，把那一行删掉或留空即可。

### 4. 进游戏验证

启动游戏 → 新游戏 → 角色选择界面应该出现「铟星」。
进去之后检查：

- 初始牌组是 4 打击 + 4 防御 + 1 运用-In + 1 近身斩
- 初始遗物是「缺陷晶胞」；每场战斗第一次实际获得延拓时获得1点能量
- 在火堆选「锻造」升级打击 → 显示 9 点伤害；升级防御 → 显示 8 点格挡
- 升级运用-In → 卡面多出「保留」；升级近身斩 → 伤害11变15，仍为1费
- 打出近身斩（结算后能量不为0）→ 自己获得1层虚弱，本人本回合末清除本牌新增层数
- 打出近身斩（结算后能量为0）→ 自己获得1层易伤，覆盖敌方行动，敌方回合末原生衰减前清除本牌新增层数
- 能力栏里**不应该**出现「晶抗·虚弱消退」「晶逆·易伤消退」这两个隐藏效果
- 打出运用-In → 手牌多出一张「晶化」，且出现「下回合能量」状态
- 打出「晶化」→ 本回合再想打第二张时会变成打不出（每回合限一张）

> 改了本地化文案后必须**重新构建**（重新导出 pck）并**完全重启游戏**，
> 因为文本在 pck 里，且游戏只在启动时读一次。

## 想改什么，改哪里

| 想改的东西 | 改哪里 |
| --- | --- |
| 角色名 / 描述 / 代词 | `IndiumStar/localization/zhs/characters.json`、`.../eng/characters.json` |
| 初始生命值 | `IndiumStarCharacter.StartingHp` |
| 初始金币 | `IndiumStarCharacter.StartingGold` |
| 卡牌名字 / 描述 | `IndiumStar/localization/zhs/cards.json`、`.../eng/cards.json` |
| 打击伤害 / 升级加成 | `StrikeIndiumStar.CanonicalVars`（`DamageVar(6m, ...)`）与 `OnUpgrade`（`UpgradeValueBy(3m)`） |
| 防御格挡值 / 升级加成 | `DefendIndiumStar.CanonicalVars`（`BlockVar(5m, ...)`）与 `OnUpgrade`（`UpgradeValueBy(3m)`） |
| 原波穿刺基础伤害 / 每种负面状态的加成 | `PrimordialPierce.CanonicalVars`（`CalculationBaseVar(11m)` / `ExtraDamageVar(3m)`）与 `OnUpgrade`（`ExtraDamage.UpgradeValueBy(1m)`） |
| 晶化Mg 的格挡值 | `CrystallizationMg.CanonicalVars`（`BlockVar(4m, ...)`） |
| 「晶化」牌每回合能直接打出几张 | `CrystallizationRules.MaxPerTurn`（默认 1） |
| 晶化（Mg）加入几张 | `ApplicationMg.CanonicalVars`（`CardsVar(2)`） |
| 初始牌组张数 | 两个卡类上的 `[RegisterCharacterStarterCard(typeof(IndiumStarCharacter), N, Order = ...)]` |
| 初始遗物与效果 | `src/Relics/DefectiveCrystalCell.cs`；`RegisterCharacterStarterRelic`声明初始遗物，双语`relics.json`定义名称和描述 |
| 卡图 | 两个卡类的 `AssetProfile` 里的 `PortraitPath`，指向 `res://IndiumStar/images/cards/xxx.png` |
| 角色立绘 / 选择界面 / 地图标记 / 音效 | `IndiumStarCharacter.AssetProfile`，用 `CharacterAssetProfiles.Merge(CharacterAssetProfiles.Ironclad(), new(...))` 覆盖 |
| 角色主题色 | `IndiumStarCharacter.NameColor` / `MapDrawingColor`、`IndiumStarCardPool.DeckEntryCardColor` |
| 能量图标 | `IndiumStarCardPool`：设 `TextEnergyIconPath` / `BigEnergyIconPath` 后，把 `EnergyColorName` 改成 `"indiumstar"` |

## 几个容易踩的坑

1. **本地化文件夹名必须等于清单 `id`**，不是工程目录名。这里是 `IndiumStar/localization/`。
2. **本地化键名不是随便起的**。RitsuLib 会把内容 ID 规范化成
   `{MOD_ID}_{类别}_{类型名}`（全大写蛇形）。例如类 `StrikeIndiumStar` 的 ID 是
   `INDIUM_STAR_CARD_STRIKE_INDIUM_STAR`，所以键必须是
   `INDIUM_STAR_CARD_STRIKE_INDIUM_STAR.title` / `.description`。
   改类名就要同步改本地化键。
3. **原版类型加不了特性**（改不了原版代码），所以「初始遗物用原版遗物」只能走
   `RitsuLibFramework.CreateContentPack(...).CharacterStarterRelic<TCharacter, TVanillaRelic>()`。
4. **`has_pck` 要跟实际一致**。没导出 `.pck` 就写 `false`，否则游戏会报「声明了 pck 但找不到文件」。
5. **依赖版本号**要 ≥ 你实际安装的 RitsuLib 版本，否则会被判定为依赖不满足。
6. 不要自己 `new` 模型类，一律通过 `ModelDb` 取实例（RitsuLib 的模板已经帮你处理了）。
7. **`MegaCrit.Sts2.Core.Combat` 不在 `ImplicitUsings` 里**。`CombatSide`、`ICombatState`
   都在这个命名空间，写新卡时容易漏（本模组已经因此编译失败过两次）。常用的隐式命名空间只有
   `System.Linq` 那一批，`MegaCrit.Sts2.Core.Models` 也要自己加。
8. **`CardModel.CombatState` 标了可空**（`CardModel.cs:1018`，因为牌组里的卡没有战斗状态），
   直接传给 `AddToHand`、`TargetingAllOpponents`、`.Enemies` 会产生 CS8604 / CS8602 警告。
   但**出牌时它必然非空**——原版所有群攻牌（`AstralPulse`、`Shatter`、`Whirlwind` 等 29 处）
   都是直接传 `base.CombatState` 的，警告只是因为游戏源码带了可空注解。
   所以本模组统一写 `CombatState!` 显式标注这个不变量，而不是加一堆运行时判空。
9. **`Math.Pow` 返回 `double`**，赋给 `decimal` 要显式转换：`(decimal)Math.Pow(2.0, amount)`
   （原版 `ShadowmeldPower` 也是这么转的）。
10. **`{X:diff()}` 里的 `X` 必须是 `DynamicVar` 对象**，注入普通数字会导致卡面直接显示
    原文 `[X:diff()]`。详见上面「让 `:diff()` 显示动态值」。
11. **每场战斗开始时牌组里的卡会被克隆**（`Player.cs:806-815`），
    实例字段会重置。需要跨战斗保留的状态要落在 `DeckVersion`（牌组原件）上；
    需要**跨过保存/读档**的还要声明在具体模型上的原生`[SavedProperty]`（见上文「晶币」一节）；
    普通字段不进存档，带默认值的`SavedAttachedState<CardModel, T>`会污染无关卡牌，禁止作为单卡状态存储。
12. **`PowerCmd.Apply` 在 `amount == 0` 时直接 return**（`PowerCmd.cs:107`），
    能力不会被创建。需要"先挂上、之后再写数字"的能力，初始层数要给 1。
13. **`MegaCrit.Sts2.Core.Entities.Players` 不在 `ImplicitUsings` 里**。
    只是把 `Owner.Player` 当值传出去时不需要它（类型是推断的），
    但一旦把 `Player` 写成参数类型或字段类型就必须 `using`。
14. **`AfterCardDrawn` 是"每张抽到的牌触发一次"**，不是"每次抽牌触发一次"。
    所以不能用它来认"抽到的第一张牌"——它没有"我是第几张"的信息。
    要第一张就直接拿 `CardPileCmd.Draw` 的返回值。
15. **要给任意一张牌加可见标记，用侵蚀（Affliction）**。
    `CardModel.GetDescriptionForPile` 会把 `Affliction?.DynamicExtraCardText`
    拼到卡面描述后面（`CardModel.cs:1410-1414`），这是原版唯一
    "挂在任意牌上、并且能显示文字"的机制。
16. **侵蚀只负责"显示和记住"，不负责"结算"。**
    这条坑很隐蔽：卡面明明显示「晶化 2」，打出却没有任何效果——
    因为「运用-Co」漏掉了 `PowerCmd.Apply<ApplicationCoPower>` 那一行，
    侵蚀成了"有名无实"的标记。
    **诊断办法**：在能力的钩子里打一行 `GD.Print`，看它到底有没有被调用。
    这次就是靠日志里 `ApplicationCoPower.BeforeCardPlayed` 一次都没出现定位到的。
17. **`AfterSideTurnStart` 里不能拿 `Amount` 当判据去 `Remove` 自己**。
    那时 `Amount` 可能已经被这回合的抽牌消耗掉了（`ModifyHandDraw` 加过之后
    又减掉了），所以要用自己的字段判断。

## 参考

- [杀戮尖塔2 mod 制作教程](https://tutorials.sts2modding.com/)
- [GlitchedReme/SlayTheSpire2ModdingTutorials](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)
- [STS2-RitsuLib 文档](https://sts2-ritsulib.ritsukage.com/)
- [BaseLib Wiki](https://alchyr.github.io/BaseLib-Wiki/)
