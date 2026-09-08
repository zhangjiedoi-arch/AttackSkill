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
- `Assets/Scripts/Game/RunPhase.cs`
- `Assets/Scripts/Game/GameSaveData.cs`（含 `GameSaveService`）
- `Assets/Scripts/Game/GameBoot.cs`
- `Assets/Scripts/Game/GamePause.cs` / `GamePauseController.cs`
- `Assets/Scripts/Game/GameplayInputGate.cs`
- `Assets/Scripts/Rouge/RougeRun.cs`（局内实例；`Current` 给战斗读）
- `Assets/Scripts/UI/LocalAccountStore.cs`（账号/性别，勿塞进进度档）

## 数据流

```text
OpenScene 连接 → GameBoot.Continue / NewGame（跨场景仅此邮箱）
GameScene Progress.Awake：Bind RougeRun + 读盘到 _bootSave
→ Start：Ensure Flow → Party.BeginPlay(save) → 开 HUD → 补倒计时/三选一 → RunPhase
F5 / 退出(暂停·结算) / OnDestroy / 定时 → 写档
暂停 btnReset → Progress.RequestBeach（删档并写海滩新档）
全灭 / 倒计时 0 → Progress.RequestGameOver；重开 → RequestRestartRouge
UI SoftBlock Push → 角色/相机输入变 default → OnClose Pop
```

## 实现步骤

1. 新存档字段：升 `GameSaveData.CurrentVersion`，补序列化与 Party 恢复逻辑。
2. 账号/性别只用 `LocalAccountStore`，不要写入 `GameSaveData`。
3. 打开阻塞玩法的 UI：严格 `PushSoftBlock` / `PopSoftBlock`（`OnClose` 必 Pop）。
4. 退出 Play：注意 `ForceClear`，避免编辑器 `timeScale=0` 残留。
5. NewGame：`BattleSkillWheelState.ResetToDefault` + `PartyRougeProgress.ResetRun`，`_bootSave = null`。开场连接仅在无档时走这条。
6. Continue / 直接进场景读档：Awake `TryLoad` 写入 `_bootSave`。`Party.Start` **不再开局**；仅 Progress `BeginPlay(save)`。已进平面则 `BeginPlay` 内先 `ApplyRestoredEntry` 再生成。
7. `TrySave` 失败必须打 Warning；F5 成功用 Tip `progress_saved_at`。`QuitGame` 先写档再退出。
8. 直接进 GameScene：配 `defaultSceneName` / `loadSaveOnStart`。场景需有 `GameProgress`。
9. 校验 `equippedSkillIndex` 与轮盘一致；HP `<0` 表示未记录。
10. HUD / 三选一只在 Progress Boot 末开；`GameBoot` 保留（Progress 在 GameScene 才存在）。

## 约定与坑

- SoftBlock ≠ `GamePause.IsPaused`（轮盘是软阻塞）。
- 存档 v4 起含 `rougeRun`（`PartyRougeProgress` + 是否已进肉鸽平面 + 阵亡槽）。
- 存档 v5 起含 `rougeRun.battleTimeRemaining`（肉鸽获救倒计时剩余秒；未进战斗为 -1；**0=已结算**，勿回填满时长）。肉鸽闭环见 [rouge-like](../rouge-like/SKILL.md)。
- 计时：`BeginRougeTimer` 无 UI 也先记镜像，HUD 后再 `TryOpenPendingAfterBoot`。结算用 `MarkExpiredAndClose`（保持 0）。
- 读档：`ApplyRestoredEntry` 区内容差 2m；Flow 由 Progress 在 `BeginPlay` 前 `EnsureRougeFlow`。intro 清场需 `Party.PlayStarted`；已有等级/经验/被动则跳过 `ResetRun`。
- 无 `PendingRestore`：开局档只活在 Progress 实例字段。
- `RunPhase`：`Booting | BeachExplore | RougeCombat | GameOver | Transition`（无 Title）。回海滩 / intro 进肉鸽 / 重开 / 结算入口走 `GameProgress.Request*`。
- `RougeRun` 由 Progress `Bind`；战斗公式读 `RougeRun.Current`，`PartyRougeProgress` 是门面。Core 的 `GameServices` **不要**引用 Progress（循环依赖）。
- 存档 v3 起含 `equippedSkillIndex`。
- 输入后端抽象为 `GameInput`，优先 Input System。
