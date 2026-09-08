---
name: rouge-like
description: >-
  AttackSkill 肉鸽闭环：海滩 intro 清场传送、RouGeLikePlane 刷怪、经验升级三选一、
  等级攻防血缩放、被动生成物、3 分钟获救倒计时与结算存档。
  在改刷怪上限、被动表、倒计时、三选一 UI 或肉鸽读档时使用。
---

# 肉鸽（RouGeLike）

## 方案

海滩 intro 清波后传送 `RouGeLikePlane`；仅玩家在平面内时按等级池批量刷怪。经验升级弹出三选一被动；玩家与敌人攻防血同乘 `LevelStatMul`（每级 +10%）。3 分钟获救倒计时，全灭或归零结算。

## 关键文件

- 流程：`Assets/Scripts/Enemy/RouGeLikeFlowController.cs`（由 `GameProgress.EnsureRougeFlow` 在开局前挂到 `RouGeLikePlane`）
- 进度：`Assets/Scripts/Rouge/RougeRun.cs`（实例 + `Current`）/ `PartyRougeProgress.cs`（门面）/ `RougeCatalog.cs` / `RougeSkillRoller.cs`
- 导演：`GameProgressController`（`RunPhase`；intro 清场走 `RequestEnterRougeFromIntro`）
- 被动效果：`RougePassiveEffects.cs` / `RougeOrbitWeaponDriver.cs` / `OrbitingBlade.cs`
- 生成物：`RougeConstructDriver.cs` / `RougeAuraZone.cs` / `RougeDecoyTree.cs`
- 掉落：`ExpOrbPickup.cs`；治疗圈见 `HealingCircleZone`（`EnemyDeathLoot`）
- 刷怪池：`RougeEnemySpawnCatalog`（`Resources/Rouge/`）
- UI：`UISkillSelectPanel` / `UIBattleTimePanel` / `UIGameOverDialog`
- 表：`Resources/Rouge/*.json`（被动、等级、刷怪目录）
- 敌人 AI / 海滩 intro：[enemy-ai](../enemy-ai/SKILL.md)
- 存档字段：[save-pause-input-gate](../save-pause-input-gate/SKILL.md)

## 数据流

```text
海滩 EnemySpawnGroup intro 清场
→ GameProgress.RequestEnterRougeFromIntro → Flow.EnterFromIntro
→ 玩家在 RouGeLikePlane 内：10m 半径批量刷怪（对象池）
→ 击杀掉经验球 → RougeRun / PartyRougeProgress 升级
→ 三选一被动（RougeSkillRoller）→ RougePassiveEffects
→ 倒计时 UIBattleTimePanel（剩余秒写入存档）
→ 全灭 / 倒计时 0 → Progress.RequestGameOver → UIGameOverDialog
```

## 实现步骤

1. 改刷怪节奏 / 半径 / 场上上限：`RouGeLikeFlowController` Inspector（默认上限 `30 + 5*(Level-1)`，封顶 100）。
2. 改解锁池：`RougeEnemySpawnCatalog` 或菜单 `工具/Rouge/重建肉鸽刷怪等级表`。
3. 改被动：`Resources/Rouge` JSON（`nameKey`/`descKey` 正文在 Story 表）；代码效果在 `RougePassiveEffects`。
4. 环绕刃挂 `R_Hit_Root`；切人先 `BindToActiveImmediate` 再 Destroy 旧角色。
5. 倒计时：`BeginRougeTimer`；结算 `MarkExpiredAndClose`（保持 0）；回海滩 `EndRougeTimer`（-1）。
6. 读档：`hasTeleported` 或已在平面内 → `ApplyRestoredEntry`，勿让 intro 清场 `ResetRun`。开局由 Progress 调 `Party.BeginPlay(save)`。
7. NewGame / 暂停回海滩：`Progress.RequestBeach` → `Party.ResetToBeachRun` → `PartyRougeProgress.ResetRun`（清经验球与治疗圈）。
8. 场景节点：`RouGeLikePlane` / `PlayerSpawn` / `EnemyGroup` 同级。Progress 找不到平面打 Error。intro 清场需 `Party.PlayStarted`；阶段 `Transition` 时 intro 回调直接 return。
9. 战斗读等级走 `PartyRougeProgress` 或 `RougeRun.Current`；Progress 负责 `Bind`/`Unbind`。`LevelStatMulFor(int)` 可按指定等级算缩放。

## 约定与坑

| 项 | 说明 |
|----|------|
| 场上上限 | `MaxAliveNow`；Inspector 可覆盖基数/每级/封顶 |
| 刷怪 | 仅 `IsPlayerInArea`；每波默认 8–16，避开贴身与已有怪 |
| 解锁 | 1–3 基础怪；4+ 卡迪安特 … 8+ 鳞人（详见 enemy-ai） |
| 缩放 | 敌我攻防血 × `LevelStatMul`（内部 `LevelStatMulFor(level)`）；升级 `CombatStats.RefreshAllHealthForRougeLevel` |
| 导演 | `RunPhase` 在 Progress；intro 传送失败保持 `BeachExplore`。不要再加与 Progress 平级的 DDOL 循环管理器 |
| 死亡 | 肉鸽区域强制 Dissolve，不走 Echo |
| 经验球 | 仅肉鸽区域 / `IsRougeEncounter` 掉落 |
| 倒计时存档 | `<0` 未开表，`0` 已结算（勿回填满时长） |
| 生成物 | 冰/火/霜光环不跟身；诱敌之树可嘲讽、可受击 |

去重键用 `EnemyAgent`，勿用 `transform.root`（怪共挂 `EnemyGroup`）。
