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
→ 仅玩家在 RouGeLikePlane 内时，EnemyGroup 子节点随机刷怪
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
- Profile 空则运行时默认球形出伤。
- **出伤**：`EnemyCombat` 进入 Active 时 `EnemyHitbox.EnableHit`；动画若再绑 `SkillHit` 会双倍，勿两套同时开。
- 玩家需 `PlayerHurtbox`（Overlap 路径）或可被 Trigger 扫到的 CharacterController。
- 肉鸽：初始波 `InitialWaveClearedEvent` 后关刷新并传送；刷怪仅 `IsPlayerInArea`。`PlayerSpawn` / `EnemyGroup` 与 `RouGeLikePlane` 同级。
- **肉鸽刷怪池按角色等级解锁**（`RougeEnemySpawnCatalog` / `Resources/Rouge/RougeEnemySpawnCatalog`）：  
  1–3 云海妖精/铲子布偶/流放者女/流放者男；4+ 卡迪安特；5+ 朔雷之麟；6+ 荣耀狮像；7+ 踏光兽；8+ 鳞人。菜单：`工具/Rouge/重建肉鸽刷怪等级表`。
- **肉鸽批量刷怪**：每波在多个空闲 `EnemyGroup` 子节点同时生成（默认 2–4，受 `maxAlive` 限制），不再逐个刷。
- **掉落**：死亡 30% 掉 `Healing circle`（`EnemyDeathLoot`）；圈内 Active 玩家每秒回 100；`Hit_Root` 挂池化 `Healing` 特效，离圈回收。
- **死亡表现分流**：`EnemyDeathDirector` 按 `EnemyDefinition.echoChance` 掷骰  
  - **Echo**：`EnemyDeathGoldVisual` 金色透明残留（后续 F 吸收）  
  - **Dissolve**：`EnemyDeathDissolveVisual` 噪声溶解 + 上浮后隐藏网格  
  - 调试：`deathForceMode` 强制 Echo / Dissolve  
  - 材质：`Resources/Enemy/Mat_EnemyDeathGold|Dissolve` + Always Included Shaders；贴图兼容 `_MainTex/_BaseMap`  
  - 死亡立即关碰撞；金透 `Play()` 失败不挂 F 交互，改溶解/短销毁  
- 初始化后挂 `WorldUiService.AttachEnemyBlood`。
- 尸体销毁由 `EnemyDeathDirector`：声骸 `echoCorpseLifetime`（默认 20s）；飘散结束立刻 `Destroy`。
