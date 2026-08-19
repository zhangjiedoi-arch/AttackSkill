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
OpenScene 连接 → 有档 Continue / 无档 NewGame
GameScene Progress.ConsumeIntent → Load / Clear → Party 恢复
F5 / 退出(暂停·结算) / OnDestroy / 定时 → 写档
暂停 btnReset → 删档并写海滩新档
UI SoftBlock Push → 角色/相机输入变 default → OnClose Pop
```

## 实现步骤

1. 新存档字段：升 `GameSaveData.CurrentVersion`，补序列化与 Party 恢复逻辑。
2. 账号/性别只用 `LocalAccountStore`，不要写入 `GameSaveData`。
3. 打开阻塞玩法的 UI：严格 `PushSoftBlock` / `PopSoftBlock`（`OnClose` 必 Pop）。
4. 退出 Play：注意 `ForceClear`，避免编辑器 `timeScale=0` 残留。
5. NewGame：清 pending + `BattleSkillWheelState.ResetToDefault` + `PartyRougeProgress.ResetRun`。开场连接仅在无档时走这条。
6. Continue：Awake 即 TryLoad 挂 Pending（早于 Party.Start）。恢复 `rougeRun`。已进平面则先 `ApplyRestoredEntry` 再生成，避免海滩 intro `ResetRun`。读档坐标不贴地、不继承当前位姿。
7. `TrySave` 失败必须打 Warning；F5 成功用 Tip `progress_saved_at`。`QuitGame` 先写档再退出。
8. 直接进 GameScene：配 `defaultSceneName` / `loadSaveOnStart`。
9. 校验 `equippedSkillIndex` 与轮盘一致；HP `<0` 表示未记录。

## 约定与坑

- SoftBlock ≠ `GamePause.IsPaused`（轮盘是软阻塞）。
- 存档 v4 起含 `rougeRun`（`PartyRougeProgress` + 是否已进肉鸽平面 + 阵亡槽）。
- 存档 v5 起含 `rougeRun.battleTimeRemaining`（肉鸽获救倒计时剩余秒；未进战斗为 -1）。
- 存档 v3 起含 `equippedSkillIndex`。
- 输入后端抽象为 `GameInput`，优先 Input System。
