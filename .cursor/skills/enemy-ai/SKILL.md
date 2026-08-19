---
name: enemy-ai
description: >-
  AttackSkill 敌人 AI、EnemyDefinition、刷怪组与敌人出伤（EnemyBrain、EnemyAgent、SpawnGroup）。
  在加新怪、调感知/追击/交战、刷怪点或敌人 SkillHit 时使用。
---

# 敌人 AI / 刷怪

## 方案

数据驱动：`EnemyDefinition` 配置属性与 AI 参数；`EnemyBrain` 状态 Idle→Alert→Chase→Combat→Return/Dead。距离激活刷怪组；出伤经 `EnemyAttackHitRelay` → `HitResolver.DefaultEnemyOffense`（只打 Active 玩家）。

## 关键文件

- `Assets/Scripts/Enemy/Data/EnemyDefinition.cs` / `SpawnGroupDefinition.cs`
- Runtime：`EnemyAgent` / `EnemySensor` / `EnemyAggro` / `EnemyCombat` / `EnemyMotor` / `EnemyHitbox` / `EnemyAttackHitRelay` / `EnemyDeathDirector` / `EnemyDeathGoldVisual` / `EnemyDeathDissolveVisual` / `EnemyDeathLoot`
- 治疗圈：`HealingCircleZone.cs`（`CharacterRuntimeSettings` 配 Prefab / 概率）
- Shader：`EnemyDeathGold.shader`、`EnemyDeathDissolve.shader`
- AI：`EnemyBrain.cs`
- Spawn：`EnemySpawnGroup.cs` / `EnemySpawnPoint.cs`
- 肉鸽：`RouGeLikeFlowController.cs` / `RouGeLikeFlowBootstrap.cs`
- 索敌：`IPlayerTargetProvider` / `PlayerTargetLocator`
- SO：`Assets/ScriptableObjects/Enemy/`

## 数据流

```text
SpawnGroup 距玩家激活 → SpawnPoint 生成
→ Agent 套 Enemy 层 + Hitbox/Relay + WorldUi 血条
→ Brain Tick（感知结果本帧复用）
→ 动画 Event SkillHit（可选）或 Active 阶段 EnemyHitbox
→ Resolver（仅 Active 玩家）

肉鸽：
初始 EnemySpawnGroup 清场 → RouGeLikeFlowController 传送至 PlayerSpawn
→ 仅玩家在 RouGeLikePlane 内时，于玩家 10m 半径内随机刷怪
```

## 实现步骤

1. 新建 `EnemyDefinition`（属性/感知/战斗/可选 SkillHitProfile/`level`）。
2. Prefab：`EnemyAgent` + `Health` + `CharacterController` + Animator。
3. 挂点与 Relay 同 Animator 物体；事件 `SkillHit` / 兼容名（可选；无 Event 时靠 Active Hitbox）。
4. SpawnGroup slots 或场景摆 `EnemySpawnPoint`。
5. 调 `disengageRange` / `returnHomeRange`（leash ≥ disengage）。
6. 交战中组不得强制休眠（看 `IsInCombat`）。
7. 血条等级读 `definition.level`。
8. 索敌只用 `PlayerTargetLocator`，勿乱 Find。
9. 动画参数与 Brain 一致（`EnemySpeed` / `InCombat` 等）。
10. Override：Agent `definitionOverride`、Relay damageOverride。
11. 肉鸽平面：`RouGeLikePlane` / `PlayerSpawn` / `EnemyGroup`（同级）；流程见 `RouGeLikeFlowController`（Boot 自动挂）。

## 约定与坑

- Agent 会递归设 Enemy 层。
- 本帧感知结果缓存，Brain 内勿重复 Raycast。
- 发现范围：`sightRange` 20m（扇形）+ `hearRange` 20m（全向），20m 内即可发现角色。
- Profile 空则运行时默认球形出伤。
- **出伤**：`EnemyCombat` 进入 Active 时 `EnemyHitbox.EnableHit`；动画若再绑 `SkillHit` 会双倍，勿两套同时开。
- 玩家需 `PlayerHurtbox`（Overlap 路径）或可被 Trigger 扫到的 CharacterController。
- 肉鸽：初始波 `InitialWaveClearedEvent` 后关刷新并传送；刷怪仅 `IsPlayerInArea`。`PlayerSpawn` / `EnemyGroup` 与 `RouGeLikePlane` 同级。读档若 `hasTeleported` 或坐标已在平面内，走 `ApplyRestoredEntry`（不 ResetRun、关掉海滩 intro 组）。暂停 `btnReset` 走 `ResetToCamp`：开 intro 组、任务回到海滩清波。
- **海滩 intro 刷怪**：按**最近 SpawnPoint** 20m 激活（`SpawnGroup_Wild.activateRadius`）；`Start` / 重置后立刻 `EvaluateActivation`，玩家已在范围内也会生成。
- **肉鸽刷怪池按角色等级解锁**（`RougeEnemySpawnCatalog` / `Resources/Rouge/RougeEnemySpawnCatalog`）：  
  1–3 云海妖精/铲子布偶/流放者女/流放者男；4+ 卡迪安特；5+ 朔雷之麟；6+ 荣耀狮像；7+ 踏光兽；8+ 鳞人。菜单：`工具/Rouge/重建肉鸽刷怪等级表`。
- **肉鸽批量刷怪**：每波在玩家 10m 半径内随机落点同时生成（默认 8–16）。场上上限 `30 + 5*(Level-1)`，封顶 100（`RouGeLikeFlowController.MaxAliveNow`）。走 `EnemyObjectPool`；点会夹在平面内，并避开贴身/已有怪。传送后 BGM 切 `drone`，并打开 `UIBattleTimePanel` 3 分钟获救倒计时。
- **等级缩放**：敌人 `CombatStats` 攻/防/血与玩家相同，乘 `RougePassiveEffects.LevelStatMul`（每级 +10%）。`EnemyDefinition.maxHp` / `attackDamage` 是 1 级表内值（当前表内 HP 已按一倍加强，如云海妖精 400、鳞人 1800）。升级时 `CombatStats.RefreshAllHealthForRougeLevel` 同步场上血量。
- **掉落**：死亡 30% 掉 `Healing circle`（`EnemyDeathLoot`）；圈内 Active 玩家每秒回 100；`Hit_Root` 挂池化 `Healing` 特效，离圈回收。经验球仅肉鸽区域 / `IsRougeEncounter` 敌人掉落。生成时向下射线贴地（略抬 0.28m），不持续上浮。`PartyRougeProgress.ResetRun` → `OnRunReset` 会 `ExpOrbPickup.ClearAll` + `HealingCircleZone.ClearAll`（重开/回海滩一并清掉落）。
- **死亡表现分流**：`EnemyDeathDirector` 按 `EnemyDefinition.echoChance` 掷骰  
  - **Echo**：`EnemyDeathGoldVisual` 金色透明残留（后续 F 吸收）；肉鸽区域强制不走此分支  
  - **Dissolve**：`EnemyDeathDissolveVisual` 噪声溶解 + 上浮后隐藏网格；肉鸽敌人一律溶解  
  - 调试：`deathForceMode` 强制 Echo / Dissolve（肉鸽区域仍强制溶解）  
  - 材质：`Resources/Enemy/Mat_EnemyDeathGold|Dissolve` + Always Included Shaders；贴图兼容 `_MainTex/_BaseMap`  
  - 死亡立即关碰撞；金透 `Play()` 失败不挂 F 交互，改溶解/短销毁  
- **肉鸽生成物**：`RougeConstructDriver` 不跟身。冰之哀伤/火之高兴/雪之哀霜在角色 5–10m 随机落点，持续 2/5/4 秒后换点，按 Prefab 碰撞体每秒 120% ATK 元素伤（每层 +10%，满 5）。诱敌之树在 10–20m 随机生成，10m 嘲讽、2000 血（每层 +10%），死后 5 秒在新位置重生。
- 初始化后挂 `WorldUiService.AttachEnemyBlood`。
- 尸体生命周期由 `EnemyDeathDirector`：声骸 `echoCorpseLifetime`（默认 20s）；飘散结束立刻回收/销毁。
