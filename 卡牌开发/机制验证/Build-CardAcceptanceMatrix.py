"""生成可追溯的验收工作矩阵，不把源码链接/命名测试/模型通过当成完整效果验收。"""
from pathlib import Path
from zipfile import ZipFile
from xml.etree import ElementTree as ET
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "IndiumStar/IndiumStar"
EVIDENCE = ROOT / "卡牌开发/审查证据_2026-10-05"
DOCUMENT = ROOT / "晶塔世界角色.docx"
DECISIONS = ROOT / "卡牌开发/规则定稿_2026-10-05.json"
decisions = json.loads(DECISIONS.read_text(encoding="utf-8"))
if hashlib.sha256(DOCUMENT.read_bytes()).hexdigest() != decisions["document_sha256"]:
    raise ValueError("最新规则定稿对应的DOCX哈希已改变，请先重新核对")
N = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
with ZipFile(DOCUMENT) as archive:
    document = ET.fromstring(archive.read("word/document.xml"))
paragraphs = ["".join(t.text or "" for t in p.findall(".//w:t", N)) for p in document.findall(".//w:p", N)]
body_start = next(i for i, paragraph in enumerate(paragraphs) if paragraph.strip() == "初始卡：")
body = "\n".join(paragraphs[body_start:])
model_probe = json.loads((EVIDENCE / "native-model-smoke.json").read_text(encoding="utf-8"))
localization = json.loads((PROJECT / "IndiumStar/localization/zhs/cards.json").read_text(encoding="utf-8"))
script = (ROOT / "卡牌开发/机制验证/Run-Tests.ps1").read_text(encoding="utf-8")
linked_sources = {path.replace("\\", "/") for path in re.findall(r"'(Cards\\[^']+\.cs|Powers\\[^']+\.cs)'", script)}
log = (ROOT / "卡牌开发/机制验证/output/tests/test-results.txt").read_text(encoding="utf-8")
passed_cases = [line.removeprefix("PASS ") for line in log.splitlines() if line.startswith("PASS ")]
if len(passed_cases) != int(re.search(r"(\d+)/\d+ passed", log).group(1)):
    raise ValueError("回归日志的通过计数不一致")
external = {
    "ShadowChainBreath": "用户增补：0c罕见技能；抽取时复制加入手牌；手牌中每张分影连气分别获得3（4）格挡；消耗。",
    "NeighboringGroupParticipation": "用户增补：0c罕见技能；抽1张；能力再抽2，技能获3（5）格挡，攻击获3（4）临时力量。",
    "Crystallization": "用户通则：普通无额外效果晶化，0c状态、保留、消耗；同一玩家每回合共用1次手动额度，自动打出不计次数。",
}
short_names = {
    "ApplicationCo": ["Co标记"], "ApplicationU": ["U标记"], "ApplicationCa": ["Ca两份"],
    "ApplicationNb": ["Nb"], "ReminiscencePollutionH": ["污染H"],
    "ReminiscenceVanadiumEternal": ["钒永"], "BondingTripleBond": ["键合三键"],
    "DeferredRelease": ["未命名技能", "预约"], "ApplicationTh": ["运用Th"],
    "ReminiscenceTinDoubt": ["锡疑"], "ReminiscencePhosphorus": ["磷语"],
}
anchors = []
records = []
native_by_class = {card["type"]: card for card in model_probe["cards"]}
for native in model_probe["cards"]:
    cls = native["type"]
    key = "INDIUM_STAR_CARD_" + native["id"]
    title = localization[key + ".title"]
    record = {"class": cls, "title": title, "model_id": key,
              "source_file": "IndiumStar/IndiumStar/src/Cards/" + cls + ".cs"}
    if cls in external:
        record.update(specification_source="用户确认的通则/增补消息", specification_text=external[cls])
    elif cls == "DeferredRelease":
        raw = "1c 普通 技能\n（保留）消耗1张牌。下个回合开始时，将其打出。"
        pos = body.find(raw)
        if pos < 0: raise ValueError("未命名技能文档锚点缺失")
        anchors.append((pos, cls)); record["document_anchor"] = pos
    else:
        prefix = r"\*" if cls.startswith("Crystallization") else ""
        pattern = prefix + re.escape(title) + r"(?=\s|[0-9Xx]|$)"
        found = re.search(pattern, body)
        if found is None: raise ValueError("文档未匹配：" + cls + " / " + title)
        anchors.append((found.start(), cls)); record["document_anchor"] = found.start()
    records.append(record)
anchors.sort()
span_by_class = {}
for index, (start, cls) in enumerate(anchors):
    end = anchors[index + 1][0] if index + 1 < len(anchors) else len(body)
    span_by_class[cls] = body[start:end].strip()
for row in records:
    cls = row["class"]
    key = row["model_id"]
    native = native_by_class[cls]
    if cls in span_by_class:
        row.update(specification_source="当前DOCX正文；括号升级以该文档格式和原文核对",
                   specification_text=span_by_class[cls])
    row["global_user_rules"] = decisions["global_rules"]
    if cls in decisions["card_specification_overrides"]:
        row["document_specification_text"] = row["specification_text"]
        row.update(specification_source="最新用户定稿消息；优先于DOCX正文",
                   specification_text=decisions["card_specification_overrides"][cls])
    if cls in decisions["card_rule_overrides"]:
        row["latest_user_rule"] = decisions["card_rule_overrides"][cls]
        row["specification_source"] += "；最新用户规则补充优先"
    row["rule_decision_file"] = "卡牌开发/规则定稿_2026-10-05.json"
    source_path = ROOT / row["source_file"]
    source = source_path.read_text(encoding="utf-8")
    constructor = re.search(r": base\(([^,]+), CardType\.(\w+), CardRarity\.(\w+), TargetType\.(\w+)\)", source)
    row["observed_source_metadata"] = ({"base_cost_expression": constructor[1], "type": constructor[2],
        "rarity": constructor[3], "target": constructor[4]} if constructor else {"inherited": "晶化基类；还需核对实际注册/可打出属性"})
    row["observed_source_metadata"]["has_energy_cost_x"] = bool(re.search(r"HasEnergyCostX\s*=>\s*true", source))
    if row["observed_source_metadata"]["has_energy_cost_x"]:
        row["observed_source_metadata"]["effective_cost_display"] = "X；base_cost_expression=0是本体X牌构造方式"
    row["localized_description"] = localization[key + ".description"]
    row["card_source_linked_by_regression"] = "Cards/" + cls + ".cs" in linked_sources
    aliases = [row["title"]] + short_names.get(cls, [])
    row["named_case_evidence"] = [name for name in passed_cases if any(name.startswith(alias) for alias in aliases)]
    row["model_probe"] = native["mutable_clone"] + "；仅构造、升级、MutableClone；非实机效果"
    row["effect_acceptance"] = "部分源码回归；尚未完整验收" if row["card_source_linked_by_regression"] and row["named_case_evidence"] else "需要补齐直接卡牌效果回归；相关命名用例不自动证明整牌效果"
    row["live_combat"] = "未验证"
    row["save_and_multiplayer"] = "未验证"
    row["required_audit"] = [
        "费用、类型、稀有度、目标与词条；逐项核对括号升级", "基础效果顺序与各分支，能量/资源读取时点",
        "目标死亡/战斗结束、空/满手、空牌堆、选牌和指令受阻的实际结果",
        "自动出牌、系列重放与嵌套回调，真实选择上下文暂停/恢复",
        "克隆状态独立、牌组写回政策、新战斗/本人/其他玩家回合隔离",
        "真实游戏运行、存档恢复、多人数值和动作同步",
    ]
    row["source_sha256"] = hashlib.sha256(source_path.read_bytes()).hexdigest()
report = {
    "schema_version": 2, "document_sha256": hashlib.sha256(DOCUMENT.read_bytes()).hexdigest(),
    "rule_decisions_sha256": hashlib.sha256(DECISIONS.read_bytes()).hexdigest(),
    "precedence": decisions["precedence"],
    "scope": "64张注册卡牌的规格/源码/现有证据工作矩阵；命名用例和源码链接仅定位证据，不自动认定语义阅读或完整验收",
    "cards": records, "total": len(records), "regression_cases": len(passed_cases),
    "direct_card_sources_linked": sum(row["card_source_linked_by_regression"] for row in records),
    "complete_effect_acceptances": 0,
}
if report["total"] != 64 or len({row["class"] for row in records}) != 64: raise ValueError("卡池身份数量异常")
(EVIDENCE / "逐牌验收矩阵.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
lines = ["# 当前卡池逐牌验收矩阵", "", f"文档SHA256：`{report['document_sha256']}`。注册卡牌64张；当前来源代码回归{report['regression_cases']}项；直接链接卡牌源码{report['direct_card_sources_linked']}张。完整效果验收仍为0，不能用模型检查替代。", "", "此表是工作矩阵：JSON保留各牌原文、观察到的源码元数据、卡面、命名用例与待验证边界。名字匹配仅定位相关测试，不自动判定整牌功能通过。", "", "| 卡牌 | 类 | 直接源码 | 命名相关用例 | 效果验收 | 实机/同步 |", "| --- | --- | --- | --- | --- | --- |"]
for row in records:
    lines.append(f"| {row['title']} | `{row['class']}` | {'已链接' if row['card_source_linked_by_regression'] else '未链接'} | {len(row['named_case_evidence'])} | {row['effect_acceptance']} | 未验证 |")
(EVIDENCE / "逐牌验收矩阵.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Matrix: {report['total']} cards; {report['direct_card_sources_linked']} directly linked card sources; {report['regression_cases']} regression cases; no complete-effect claims.")
