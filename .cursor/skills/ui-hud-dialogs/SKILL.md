---
name: ui-hud-dialogs
description: >-
  AttackSkill UI 框架与战斗 HUD、对话框、技能轮盘、本地化（UIManager、UIId、LocalizationService）。
  在加面板/对话框、改战斗 HUD、文案多语言或 Generated Bindings 时使用。
---

# UI：HUD / 对话框 / 本地化

## 方案

单 Canvas 分层（Panel / Dialog / Tip）；`UIManager.Open(UIId)` 实例化。战斗 HUD 一次打开五个面板。文案走 `LocalizationService` + `LocalizedText`。

## 关键文件

- 框架：`UIManager` / `UIBase` / `UIBootstrap` / `UILayer` / `UIId` / `UIPrefabEntry`
- 战斗：`UIBattlePartyPanel` / `UIBattleCombatPanel` / `UIBattleVitalsPanel` / `UIBattleSystemPanel` / `UITaskPanel` / `UIBattleTimePanel`
- 轮盘：`UISkillWheelDialog` / `BattleSkillWheelState`
- 其它：`UIPauseMenuDialog` / `UIGameOverDialog` / `UISettingDialog` / `UILogInDialog` / `UIChooseGenderDialog` / Tip&Sure
- Generated：`Assets/Scripts/UI/Views/Generated/*.Bindings.g.cs`
- Loc：`LocalizationService` / `LocalizationCatalog` / `LocalizedText` / `LocalizationBootstrap`
- Resources：`Localization/`（Catalog、Tables、Json Bundle）

## 数据流

```text
UIManager.Open(UIId) → 按层实例化 → partial 视图 + Bindings
Localization 启动载 Bundle → LocaleChanged → LocalizedText 刷新
OpenBattleHud() → 五面板（编队 / 系统 / 战斗键 / 生存 / 任务）；进肉鸽后再开 BattleTime 倒计时
Tab 轮盘 → SoftBlock + Commit 装备索引
```

## 实现步骤

1. 新界面：Prefab + `UIId` + Manager 映射 + partial 视图类。
2. 编辑器可 `EnsureEntry` 自动补 Prefab 路径。
3. 文案：表 key + Json Bundle；挂 `LocalizedText`。
4. ActiveLocales 当前 ZhHans / En。
5. 阻塞玩法 UI：配对 `GameplayInputGate` Push/Pop（参考轮盘 / GameOver）。
6. **勿手改** Generated Bindings；走生成管线。
7. Tip 用独立层，避免被 Dialog 盖住。
8. 暂停：`UIPauseMenuDialog` + `GamePause`。`btnReset` → `Party.ResetToBeachRun`（回海滩、删档重写、任务回到海滩清波）。全灭：`UIGameOverDialog`（`UIId.GameOver`），ESC 不关。肉鸽 3 分钟倒计时归零同样弹 GameOver，标题 key `game_over_rescue_title`（派蒙拯救了你！）。
9. HUD E/R 按钮走 `CombatSkillInput` / `CombatSkillRInput`；T/Q/E/R 冷却见 `PartySkillCooldown` + `CombatStats`；`imgFill`：0=进 CD、1=可用；`txtFill` 显示剩余秒（&lt;1s 为 0.x）。
10. 占位技能键可能只 Tip（README 已说明），加功能时接真实输入。

## 约定与坑

- Panel 互斥；Dialog 可叠。
- 运行时文案主源：`Resources/Localization/Json/LocalizationBundle`。
- 肉鸽被动名/描述：`RougePassiveTable.json` 只写 `nameKey`/`descKey`，正文在 `Story.json`（并同步进 Bundle 的 Story 表）。`RougePassiveText` 走 `LocalizationTableType.Story`。三选一描述下追加 `rouge_skill_current_stack`（当前层/上限）。
- 肉鸽倒计时：`UI_BattleTime_Panel` / `UIBattleTimePanel`；`EnterRougeCombat` / `ResetEncounterForRestart` 开，`ResetToCamp` 用 `EndRougeTimer` 清成 -1；结算用 `MarkExpiredAndClose` 保持 0。`txtTime`=`battle_time_rescue`（即将获救：mm:ss），&lt;60s 变红。剩余秒写入 `rougeRun.battleTimeRemaining`（存档 v5），读档续跑。Boot 末 `TryOpenPendingAfterBoot` + `TryOpenSkillSelectIfPending`。
- Battle HUD 打开时 Tab 给轮盘，不给切人。
