---
name: exploration-tools
description: >-
  AttackSkill 探索工具：翅膀/御剑/摩托、ExplorationToolCatalog、Tab 技能轮盘与 T 键切换。
  在加新工具槽、改飞行/摩托、轮盘 UI 或装备存档时使用。
---

# 探索工具（Tab / T）

## 方案

8 槽 `ExplorationToolCatalog`；Tab 按住开轮盘选扇区装备；T 切换当前工具进出 HSM 薄壳。未实现槽为 Stub，确认时 Tip。

## 关键文件

- `Assets/Scripts/Character/Exploration/*`
  - `ExplorationToolCatalog` / `Definition` / `Kind` / `Service`
  - `CharacterExplorationTools` / `FlightTools` / `MotorcycleTool` / `Handlers`
- HSM：`WingFlightState` / `SwordFlightState`（`AirborneStates`）、`MotorcycleState`
- UI：`UISkillWheelDialog` / `BattleSkillWheelState` / `UIBattleCombatPanel`
- Resources：`ExplorationToolCatalog.asset`、`ExplorationTools/*.asset`

## 数据流

```text
Catalog(8) → BattleSkillWheelState.SelectedIndex
Tab → Dialog + GameplayInputGate.PushSoftBlock → 松手 Commit
T → ExplorationToolService.TryToggleEquipped
  → IExplorationTool → GenshinLikeCharacter.TryToggleExplorationTool
  → HSM + CharacterToolAttach + Audio/VFX
```

## 实现步骤

1. 新建 `ExplorationToolDefinition` SO，设真实 `Kind`（非 Stub）。
2. 放入 Catalog 对应槽。
3. 实现 `IExplorationTool` + Handler，在 `CharacterExplorationTools.Get` 注册。
4. 加 HSM 薄壳并 Bind 到状态树。
5. 挂点 / Prefab / 音效走 RuntimeSettings。
6. 配置 `RequiresGroundToActivate` / `BlocksSkillWheelWhenActive`。
7. 本地化 `NameKey`（如 `skill_wheel_7`）。
8. 存档：`equippedSkillIndex`（存档 v3）。
9. 工具激活中禁止开轮盘（已有检查）。
10. Editor：`ExplorationToolCatalogMenu`。

## 约定与坑

- 已实现：翅膀、御剑、摩托；其余 Stub。
- 轮盘软阻塞冻 `timeScale`，不是 `GamePause`。
- 互斥工具同时开时 `TryToggle` 失败。
- F = 滑翔；翅膀起飞是 T，不要混。
- 气流：翅膀左右各一；御剑居中（`WingFlightAirflowVfx` + Sparks blue Prefab）。
