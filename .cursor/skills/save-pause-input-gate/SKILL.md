---
name: save-pause-input-gate
description: >-
  AttackSkill 存档进度、暂停、软阻塞与玩法输入闸（GameProgress、GameSave、GamePause、GameplayInputGate）。
  在改存档字段、读写档、暂停菜单、轮盘冻时或拦截角色/相机输入时使用。
---

# 存档 / 暂停 / 输入闸

## 方案

- **进度存档**：JSON（`GameSaveData`），与账号资料分离  
- **硬暂停**：`GamePause`（ESC 菜单等）  
- **软阻塞**：`GameplayInputGate` SoftBlock（技能轮盘），可冻 `timeScale` 但不设 `IsPaused`  
- 玩法输入统一看 `GameplayInputGate.IsBlocked`

## 关键文件

- `Assets/Scripts/Game/GameProgressController.cs`
- `Assets/Scripts/Game/GameSaveData.cs`（含 `GameSaveService`）
- `Assets/Scripts/Game/GameBoot.cs`
- `Assets/Scripts/Game/GamePause.cs` / `GamePauseController.cs`
- `Assets/Scripts/Game/GameplayInputGate.cs`
- `Assets/Scripts/UI/LocalAccountStore.cs`（账号/性别，勿塞进进度档）

## 数据流

```text
OpenScene → GameBoot.SetIntent(NewGame|Continue)
GameScene Progress.ConsumeIntent → Load / Clear → Party 恢复
F5 / 退出 / 定时 → 写档
UI SoftBlock Push → 角色/相机输入变 default → OnClose Pop
```

## 实现步骤

1. 新存档字段：升 `GameSaveData.CurrentVersion`，补序列化与 Party 恢复逻辑。
2. 账号/性别只用 `LocalAccountStore`，不要写入 `GameSaveData`。
3. 打开阻塞玩法的 UI：严格 `PushSoftBlock` / `PopSoftBlock`（`OnClose` 必 Pop）。
4. 退出 Play：注意 `ForceClear`，避免编辑器 `timeScale=0` 残留。
5. NewGame：清 pending + `BattleSkillWheelState.ResetToDefault`。
6. Continue：场景名不一致则 `LoadSceneAsync`。
7. 直接进 GameScene：配 `defaultSceneName` / `loadSaveOnStart`。
8. 校验 `equippedSkillIndex` 与轮盘一致；HP `<0` 表示未记录。

## 约定与坑

- SoftBlock ≠ `GamePause.IsPaused`（轮盘是软阻塞）。
- 存档 v3 起含 `equippedSkillIndex`。
- 输入后端抽象为 `GameInput`，优先 Input System。
