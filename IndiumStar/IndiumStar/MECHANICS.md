# 公共机制开发约定

本轮改动建立晶化、延拓、晶抗/晶逆的公共入口，保留现有卡牌的数值与一般结算。
量子点、污染O/H、硬钙石、磷语等卡牌已接入这些接口。

## 晶化

0.1.4：附加文字通过晶化自身的`CrystalDoomAmount`（实际消亡）、`CrystalDrawAmount`、`CrystalExtensionAmount`保存，重复赋予叠加，克隆独立。不得使用全CardModel默认SavedAttachedState保存这些字段。每段先原效果，再消亡、抽牌、延拓；使用同一原生自动打出/重放入口。附加延拓在此次CardPlay上记录快照，由`ExtensionRules.IsExtensionPlay`统一供反演、锡疑读取，后来修改文字不追溯旧历史。完整战斗存档恢复历史快照及多人仍待专项验收。

键合-酸酐按用户确认只处理附加效果文字，由`AddedCardTextRules`识别晶化三个附加字段、Co/U标记及有额外文字的本体侵蚀/附魔。先赋予延拓，再快照本人手/抽/弃/消耗堆，每张至多尝试一次；不把费用、伤害/段数、普通升级、单独关键词改变或循环状态切换当作附加文字。自动晶化不占手动额度。

滋生在`AfterCardDrawn`处理状态抽牌，每层额外抽1张；用`TryModifyKeywordsInCombat`移除本人的晶化Retain，既有/未来/复制晶化统一生效，不修改永久牌组或其他玩家的牌。移入/生成手牌不触发额外抽牌，外部一次性保留沿本体规则。

新增晶化应继承 `CrystallizationCard`，并照常使用 `RegisterCard` 注册。
公共基类提供0费、状态牌、保留、消耗，以及所有变体共用的手动额度。
需要增加关键字时覆写 `CanonicalKeywords`；需要额外手动条件时覆写
`CanPlayCrystallization`，不要自行替换公共限额。
晶化Pb保留公共的晶化词条、保留和消耗，额外具有虚无。

```csharp
public sealed class MyCrystal : CrystallizationCard
{
    protected override async Task<CardModel?> PlayEffect(
        PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 使用本体指令实现变体效果。
        return null;
    }
}
```

该示例省略注册和资源配置，只展示效果入口。空效果变体可以直接使用基类默认实现。

`ICrystallizationCard` 是牌族身份，和显示名称、语言、卡牌稀有度无关。
外部模型也可实现此标记，但手动额度由公共基类负责，单独声明标记不自动限制出牌。
选牌与计数使用 `CrystallizationRules.IsCrystallizationCard` 和
`CountCrystallizationInHand`，生成使用普通或泛型 `AddToHand`。
泛型入口要求目标模型同时是 `CardModel` 和 `ICrystallizationCard`。

手动额度从本体 `CardPlaysStarted` 读取：同玩家、本回合、非自动、系列第一段。
自动打出不占额度；一次手动出牌的原版重放不额外占额度。
下一回合不需维护自定义重置字段。自动出牌仍遵守本体的其他出牌限制。

## 运用牌族

`ApplicationCard` 是通用效果基类，分析、表征等非运用牌也继承它。
真正的运用牌必须另外声明 `IApplicationCard`。不继承该基类的运用-Co
同样声明接口。`ApplicationRules.IsApplicationCard` 和免费效果只识别这个标记。

## 延拓

所有业务牌通过 `ExtensionRules` 操作延拓：

| 方法 | 行为 |
| --- | --- |
| `Gain(context, player, amount, source)` | 通过本体 `PowerCmd.Apply` 获得层数，保留来源卡与施加方 |
| `GetAmount(player)` | 查询层数，无能力时返回0 |
| `RemoveAll(player)` | 移除全部层数，无能力时无操作 |
| `TryConsumeAll(player, minimum)` | 达到正数阈值才清空全部层数，并返回是否消耗 |

当前继续使用原版 `EnergyNextTurnPower`：各来源合并，能量重置后发放，
然后移除能力。清空延拓也会清空其他来源的下回合能量。
这是保留现有实现的兼容选择，不表示已经决定延拓必须永远共享原版资源。
若设计要求独立资源，未来只更换公共入口的底层能力和兑现钩子。

本轮迁移了运用-In、键合-酯基、键合-亚胺基、键合-硝基、基团迁移的获得逻辑，
以及坍缩的消耗和构象打击的查询。其他临时或延迟力量能力不属于延拓。

获得拦截：实现 `IExtensionGainInterceptor` 的模型（回忆-磷语）可在 `Gain` 里
把每回合第一次延拓换成等量能量。接口与公共入口同文件，入口不认识具体能力。

## 晶抗与晶逆

结算顺序固定为：支付费用 → 等待完整基础效果 → 读取一次能量 → 选择分支 → 执行分支。
默认非0能量选择晶抗，0能量选择晶逆。分支及通知改变能量不重做本次选择。

牌须显式声明 `AvailableCrystalBranches`：

```csharp
public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;
```

| 声明 | 对应方法 |
| --- | --- |
| `None`（默认） | 没有分支，不产生空触发 |
| `Resistance` | `CrystalResistance` |
| `Reversal` | `CrystalReversal` |
| `Both` | 两个方法都实现；通常仍只按能量选择一个 |

`Both` 声明表示牌具备两种效果，不意味着每次都执行两种。
特殊能力通过修改选择结果实现同时执行。已有18张带分支卡牌已显式迁移，
测试脚本检查声明与覆写方法对应，避免漏掉旧牌或给空方法计数。

`CrystalBranchContext` 保存本次牌、`CardPlay`、主效果返回值、主效果后的能量，
以及可用分支。主效果选牌结果继续通过局部返回值传递，不放在跨次出牌字段中。
出牌次数预测阶段（`ModifyCardPlayCount`）没有 `CardPlay` 和主效果返回值，
这两个字段为 null，修改规则必须容忍。

### 修改分支

战斗中的模型实现 `ICrystalBranchModifier`，其方法按本体监听顺序调用。
默认结果来自能量快照，修改结果最终与牌的可用分支取交集。
可以选择 `None`、一个分支或 `Both`，不能凭空为无相应效果的牌制造触发。

例如量子点在自身适用的前两张牌上返回 `Both`。它的玩家范围、计数口径、
回合重置应由量子点模型根据本体出牌历史处理。没有分支的牌仍可能占用
“前两张牌”的位置，不能只按修改接口被调用的次数统计。

修改方法应仅计算结果，不抽牌、不获得能量、不修改计数。选择一旦完成，
双分支固定先晶抗后晶逆，即使第一分支或其通知改变能量也不改判。

### 监听触发

战斗中的模型实现 `ICrystalBranchListener.AfterCrystalBranchTriggered`。
通知在对应分支完成后立即执行并等待完成；双分支的顺序是：

晶抗 → 晶抗通知 → 晶逆 → 晶逆通知。

污染H可以在自身玩家的晶抗通知中累计触发次数，达到阈值后获得延拓并抽牌。
接口只报告已声明并完成执行的分支；没有该分支的卡不通知，抛异常的分支不发送完成通知。
分支已执行但因目标、层数或收益为0而没有数值变化，仍按一次逻辑触发报告。
自动出牌及本体重放在完整分支结算时也报告；具体能力可按 `CardPlay` 自行筛选。

监听者应检查 `context.Card.Owner` 或其生物是否为自身主人，并检查所需牌堆、回合范围。
框架从当前战斗的 `IterateHookListeners()` 发现模型，不保留全局订阅。
战斗已结束或正在结束时停止附加规则和通知，初始化阶段遵循本体钩子例外。

## 晶逆重放的实现

晶流和运用-Th 的晶逆「重放」走本体出牌系列：`ApplicationCard.ModifyCardPlayCount`
在出牌次数生成时按当前能量预测分支，预测到晶逆就延长本次系列
`CrystalReversalReplayCount` 段（与复制药水同路径，每段都过出牌钩子和历史）。
晶流和运用-Th 显式设置 `SuppressCrystalBranchesOnReplay`，在系列重放段只执行主效果。
该开关默认关闭，其他牌由复制药水、附魔等产生的本体重放仍完整执行晶抗/晶逆与通知。
声明重放的牌，主效果不得改变能量（预测在结算前读取能量）。
审查报告 F04 已通过此方式处理。

量子点根据出牌历史中当前系列的序号判断前两张牌：预测阶段把下一次系列计为已开始系列数+1，
实际阶段在当前 `CardPlay` 的历史位置停止，不把主效果中的嵌套出牌算进外层牌的位置。
本体系列重放沿用该牌最近一次系列开始的序号。

## 新能力的结算

污染H的 `Progress` 与能力的 `Amount` 分开，初始进度为0。普通版在第4次晶抗后奖励，
升级版在第3次后奖励，奖励后扣除阈值。重复施加不赠送进度，已有升级阈值不被普通版覆盖。
能力栏的 `DisplayAmount` 显示进度。

运用-Nb在 `AfterCardPlayed` 扣除当前活力至多2层，不使用出牌前快照。
`VigorRetentionPatch` 只在拥有者具有Nb时跳过原版 `VigorPower.AfterAttack` 的全量消耗，
并重置原版攻击缓存，使下一张牌和同一牌的后续攻击能继续使用保留的活力。
补丁在 `Entry.Init` 注册；回归测试使用实际游戏的Harmony，另外校验实际游戏DLL的目标方法和缓存字段。

升级崩碎之歌通过 `CrystallizationRules.TryAutoPlay` 检查自动打出的历史记录，
只有真正开始出牌的晶化计入伤害段数；被待发等钩子阻止的晶化不计入。

## 手牌力量与敏捷光环

晶化Hs和密铺形态通过隐藏的 `CrystalHandAuraPower` 同步真正的 `StrengthPower` / `DexterityPower`。
Hs对所有存活角色提供每张1力量，密铺形态对拥有者提供每张晶化2力量/2敏捷，两个效果可相加。
卡牌移动、变化、移除以及新角色加入战斗后更新贡献；控制器持续存在，不依赖最后一张Hs留在监听列表。
每个控制器单独记录各角色的贡献，克隆时重置字典。离手只移除本光环的贡献，保留其他来源的层数。

“在手中具有”使用本体可变能力和能力变更通知提供精确临时层数，
不作为一次可受倍率修改的力量/敏捷获得指令，也不重复执行伤害/格挡修正，避免双重加成。
能力栏、查询层数与原版力量/敏捷修正共用相同状态。
敌人的力量贡献变化后同步刷新其伤害意图。

## 规则词条的悬浮提示

`Entry.Init` 通过 `IndiumStarKeywords.RegisterAll` 注册晶化、延拓、晶抗、晶逆。
注册先于卡牌模型初始化；重复调用保留已有定义。
RitsuLib 0.6.2 的 `RegisterCardKeywordOwnedByLocNamespace` 使用完整的模组限定ID，
双语 `card_keywords.json` 必须使用 `INDIUM_STAR_KEYWORD_CRYSTALLIZATION`、
`INDIUM_STAR_KEYWORD_EXTENSION`、`INDIUM_STAR_KEYWORD_CRYSTAL_RESISTANCE`、
`INDIUM_STAR_KEYWORD_CRYSTAL_REVERSAL`，分别追加 `.title` / `.description`。
依据：[RitsuLib 0.6.2 关键词说明](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.2/docs/pages/guide/localization-and-keywords.md)。

`ApplicationCard` 根据实际声明的分支与 `IExtensionCard` 标记添加词条，
晶化基类另加晶化词条。覆写 `CanonicalKeywords` 的变体应保留公共词条，
再增删自身的保留、消耗或虚无。词条只提供提示，玩法仍由公共机制实现。

## 验证与尚待处理的部分

工作区 `卡牌开发/机制验证/Run-Tests.ps1` 将这份公共机制的真实源码链接到
小型本体替身，检查规则选择、额度、事件顺序、延拓以及新能力组合。
还检查规则词条身份、变体关键字和双语本地化是否与注册ID对应。
Harmony补丁安装使用游戏实际的 `0Harmony.dll`；API契约测试只加载游戏程序集读取目标方法/字段。
这些测试不启动游戏，也不证明 Godot、多人同步或本体能量兑现流程已实机通过。

`Compile-Mod.ps1 -GameDir '游戏根目录'` 使用实际游戏程序集和指定的RitsuLib程序集
直接进行C#编译。默认RitsuLib引用为团队交付的 `.godot/mono/temp/bin/Release` 文件；
可以用 `-RitsuLibDir` 指定其他引用目录。它只输出验证DLL，不运行项目的自动部署目标。
这不是完整Godot工程构建，不执行源生成器、PCK导出或游戏安装。

本轮不补全新卡。新增能力重复施加的完整叠加规则、完整Godot/PCK构建与实机交互仍需单独验收。

后续已完成：表征的延迟力量区分已发放与待发放（审查报告 F02），晶币的永久递减
现改用晶币专属的原生`[SavedProperty] CrystalCoinPlaysThisRun`随牌组原件进存档；此前F03的`SavedAttachedState<CardModel, int>`方案会给其他卡牌写入零值字段，0.1.3已替换。旧晶币计数在恢复前迁移，原版卡牌的零值残留在写回放前清理。持久化状态必须限定到具体模型，不能挂到全部CardModel；除JSON恢复外还需核对原生PacketWriter序列化。
`Run-Tests.ps1` 已加入这两项的回归测试。
