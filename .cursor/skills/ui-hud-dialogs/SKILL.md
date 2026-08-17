---
name: ui-hud-dialogs
description: >-
  AttackSkill UI 框架与战斗 HUD、对话框、技能轮盘、本地化（UIManager、UIId、LocalizationService）。
  在加面板/对话框、改战斗 HUD、文案多语言或 Generated Bindings 时使用。
---

# UI：HUD / 对话框 / 本地化

## 方案

单 Canvas 分层（Panel / Dialog / Tip）；`UIManager.Open(UIId)` 实例化。战斗 HUD 一次打开四个面板。文案走 `LocalizationService` + `LocalizedText`。

## 关键文件

- 框架：`UIManager` / `UIBase` / `UIBootstrap` / `UILayer` / `UIId` / `UIPrefabEntry`
- 战斗：`UIBattlePartyPanel` / `UIBattleCombatPanel` / `UIBattleVitalsPanel` / `UIBattleSystemPanel`
- 轮盘：`UISkillWheelDialog` / `BattleSkillWheelState`
- 其它：`UIPauseMenuDialog` / `UISettingDialog` / `UILogInDialog` / `UIChooseGenderDialog` / Tip&Sure
- Generated：`Assets/Scripts/UI/Views/Generated/*.Bindings.g.cs`
- Loc：`LocalizationService` / `LocalizationCatalog` / `LocalizedText` / `LocalizationBootstrap`
- Resources：`Localization/`（Catalog、Tables、Json Bundle）

## 数据流

```text
UIManager.Open(UIId) → 按层实例化 → partial 视图 + Bindings
Localization 启动载 Bundle → LocaleChanged → LocalizedText 刷新
OpenBattleHud() → 四面板
Tab 轮盘 → SoftBlock + Commit 装备索引
```

## 实现步骤

1. 新界面：Prefab + `UIId` + Manager 映射 + partial 视图类。
2. 编辑器可 `EnsureEntry` 自动补 Prefab 路径。
3. 文案：表 key + Json Bundle；挂 `LocalizedText`。
4. ActiveLocales 当前 ZhHans / En。
5. 阻塞玩法 UI：配对 `GameplayInputGate` Push/Pop（参考轮盘）。
6. **勿手改** Generated Bindings；走生成管线。
7. Tip 用独立层，避免被 Dialog 盖住。
8. 暂停：`UIPauseMenuDialog` + `GamePause`。
9. HUD E/R 按钮走 `CombatSkillInput` / `CombatSkillRInput`；T/Q/E/R 冷却见 `PartySkillCooldown` + `CombatStats`；`imgFill`：0=进 CD、1=可用；`txtFill` 显示剩余秒（&lt;1s 为 0.x）。
10. 占位技能键可能只 Tip（README 已说明），加功能时接真实输入。

## 约定与坑

- Panel 互斥；Dialog 可叠。
- 运行时文案主源：`Resources/Localization/Json/LocalizationBundle`。
- Battle HUD 打开时 Tab 给轮盘，不给切人。
