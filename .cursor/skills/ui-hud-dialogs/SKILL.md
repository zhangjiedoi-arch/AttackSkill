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
- 战斗：`UIBattlePartyPanel` / `UIBattleCombatPanel` / `UIBattleVitalsPanel` / `UIBattleSystemPanel` / `UITaskPanel` / `UIBattleTimePanel` / `UISmallMapUvPanel`
- 轮盘：`UISkillWheelDialog` / `BattleSkillWheelState`
- 其它：`UIPauseMenuDialog` / `UIGameOverDialog` / `UISettingDialog` / `UILogInDialog` / `UIChooseGenderDialog` / Tip&Sure
- Generated：`Assets/Scripts/UI/Views/Generated/*.Bindings.g.cs`
- Loc：`LocalizationService` / `LocalizationCatalog` / `LocalizedText` / `LocalizationBootstrap`
- Resources：`Localization/`（Catalog、Tables、Json Bundle）

## 数据流

```text
UIManager.Open(UIId) → 按层实例化 → partial 视图 + Bindings
Localization 启动载 Bundle → LocaleChanged → LocalizedText 刷新
OpenBattleHud() → 六面板（编队 / 系统 / 战斗键 / 生存 / 任务 / 小地图）；进肉鸽后再开 BattleTime 倒计时
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
8. 暂停：`UIPauseMenuDialog` + `GamePause`。`btnReset` → `GameProgress.RequestBeach`（回海滩、删档重写、任务回到海滩清波）。全灭：`UIGameOverDialog`（`UIId.GameOver`）`btnReset` → `RequestRestartRouge`，ESC 不关。肉鸽 3 分钟倒计时归零 → `RequestGameOver(rescue)`，标题 key `game_over_rescue_title`（派蒙拯救了你！）。
9. HUD E/R 按钮走 `CombatSkillInput` / `CombatSkillRInput`；T/Q/E/R 冷却见 `PartySkillCooldown` + `CombatStats`；`imgFill`：0=进 CD、1=可用；`txtFill` 显示剩余秒（&lt;1s 为 0.x）。
10. 占位技能键可能只 Tip（README 已说明），加功能时接真实输入。
11. 烘焙小地图底图：`工具/地图/烘焙 PNG`。海滩 / 肉鸽各一份 `MapBakeData_Beach`、`MapBakeData_Rouge`（`Resources/SmallMap`）。写入 SO 时请把 Inspector 指到对应资源，以免覆盖错 origin。
12. 全屏大地图 Prefab：`UI_WorldMap_Dialog`。海滩 / 肉鸽两张烘焙按各自 `origin`/`extent` 与共用 `pixelsPerMeter` 拼进同一 `content`（世界 XZ → 图上像素），标记与玩家也走这套坐标；切图列表只定位到对应图，不隐藏另一张。三个子 panel 互斥。切图树 `WorldMapTreeTable.json`。`pal_MapSet`：`srlBarFinished` / `srlBarCustom`。ESC 先关浮层再关大地图。文案 `world_map_*`。不要另开 Panel。点击宝箱/传送点等标记会把 `content` 聚焦到该点（EaseOut 缓动约 0.4s）并弹出右侧说明（Placement `descKey` / 类型 `descKey`）；关闭弹窗或 ESC 后同样缓动回到打开时的玩家位置（切图后则回该图 origin）。手动拖拽/滚轮会打断缓动。

## 约定与坑

- Panel 互斥；Dialog 可叠。
- 运行时文案主源：`Resources/Localization/Json/LocalizationBundle`。
- 肉鸽被动名/描述：`RougePassiveTable.json` 只写 `nameKey`/`descKey`，正文在 `Story.json`（并同步进 Bundle 的 Story 表）。`RougePassiveText` 走 `LocalizationTableType.Story`。三选一描述下追加 `rouge_skill_current_stack`（当前层/上限）。
- 肉鸽倒计时：`UI_BattleTime_Panel` / `UIBattleTimePanel`；`EnterRougeCombat` / `ResetEncounterForRestart` 开，`ResetToCamp` 用 `EndRougeTimer` 清成 -1；结算用 `MarkExpiredAndClose` 保持 0。`txtTime`=`battle_time_rescue`（即将获救：mm:ss），&lt;60s 变红。剩余秒写入 `rougeRun.battleTimeRemaining`（存档 v5），读档续跑。HUD 由 Progress Boot 末打开后再补倒计时 / 三选一。
- Battle HUD 打开时 Tab 给轮盘，不给切人。
- 小地图：`UI_SmallMapUV_Panel` / `UISmallMapUvPanel` / `UIId.SmallMap`。烘焙图钉玩家圆心、ΔXZ 摆怪标；海滩/肉鸽切 `MapBakeData_*`。场景标记走 `MapMarkerTypeTable` + `MapMarkerPlacementTable`（`Resources/SmallMap`），图标预制体 `MarketMapIcon`（`imgIcon` + `imgFinish`）。类型对应 `MarkerIcon`：`chest` / `teleporter` / `grocery_store` / `canteen` / `observation_tower`；完成角标 `map_finished`。`finished` / `MapMarkerCatalog.SetFinished(uid)` 控制 `imgFinish`。动态点挂 `MapMarkerBinder`。`PlayerMapIcon` 整预制体随主相机 `YawTransform` 转。敌人 `EnemyAgentRegistry.Live` + 图标池。旧 RT 面板 `UI_SmallMap_Panel` 保留不用。全屏大地图：`UIWorldMapDialog` / `UIId.WorldMap` / `UI_WorldMap_Dialog`，点小地图或 **M** 打开；ESC 先关标记说明/`pal_MapSelect`/`pal_MapSet` 再关图。海滩与肉鸽按世界 `origin`/`extent` 拼在同一张图上，切图只定位。拖拽平移、滚轮/`sliderZoom`/`btnEnlarge`/`btnReduce` 缩放。`srlBarFinished` 过滤已完成挑战标。标记与烘焙表和小地图共用。大地图标记可点：聚焦该点 + 右侧说明弹窗，关闭后回到打开时的玩家（或切图 origin）。
