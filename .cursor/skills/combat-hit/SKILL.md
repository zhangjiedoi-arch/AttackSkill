---
name: combat-hit
description: >-
  AttackSkill 战斗出伤管线：AttackHitRelay、TimedHitProfile、SkillHitProfile、形状检测、HitResolver、VFX/SFX。
  在改普攻扇形、E/R 技能多段、挂点出伤、去重过滤或跳字事件时使用。
---

# 战斗出伤管线

## 方案

统一：触发（**TimedHit normalizedTime** / 遗留窗口）→ 形状检测 → `HitResolver` 过滤去重 → `IDamageable.TakeDamage` → VFX/SFX。

玩家普攻 / E / R：**不依赖 Animation Event**。HSM `BeginSwing` / `BeginTimedPhase` → `AttackHitRelay` 按 `TimedHitProfile` 采样。

## 关键文件

- `Assets/Scripts/Combat/AttackHitRelay.cs`
- `TimedHitProfile.cs` / `TimedHitCue.cs`
- `CombatEngageUtility.cs`（普攻/E/T 贴身最近敌人）
- `SkillHitProfile.cs` / `SkillHitSegment.cs` / `SkillHitExecutor.cs`（段执行与形状）
- `HitResolver.cs` / `HitRequest.cs` / `HitSession.cs`
- `FanHitDetector.cs` / `ShapeHitDetector.cs` / `HitSocketResolver.cs`
- 属性：`Stats/CombatStats.cs`、`DamageCalculator.cs`
- 角色表：`Assets/HitProfile/HitProfile_*.asset`（Settings 引用 + `Resources/HitProfile` 兜底）
- `CharacterRuntimeSettings`：`timedHitWanderer` / `timedHitQianxiao` / `timedHitColetta`

## 数据流

```text
普攻：AttackState.BeginSwing(combo) → phase attack1/2/3 → TimedTick → SkillHitExecutor
E：SkillState → BeginTimedPhase("skill")
R：SkillRState → BeginTimedPhase("Skill_R")
属性：CombatStats.ATK × segment.damage(倍率%) → 防御 × 元素 → 暴击
```

## 实现步骤

1. 改时机/倍率/形状：编辑对应角色 `TimedHitProfile`（`damage` = ATK%，100=100%）。
2. 改角色/怪基础属性：`Resources/Combat/Stats/...`
3. 新挂点：扩 `HitSocketId` + Avatar + Resolver。
4. 层级：玩家打 `PlayerOffenseHurtboxMask`；敌人打 `DefaultPlayerHurtboxMask`（玩家需 `PlayerHurtbox` Trigger；诱敌之树也可受击）。
5. 同段去重：`HitSession`（每次 BeginSwing/BeginTimedPhase 重置）；键为 `EnemyAgent` / 角色单位 Id，**勿用** `transform.root`（肉鸽怪共挂 EnemyGroup 时会一刀只能打一只）。
6. Timeline 大招窗口仍可用 `SuppressAnimHits` 抑制 TimedTick。
7. Relay 与 Animator 同物体（读 `normalizedTime`）。

## 约定与坑

- `damage` 一律为倍率%，不是绝对伤害。
- 扇形高度用 `hitHeight`；刀光默认世界坐标不跟随。
- phase id 须与 HSM 一致：`attack1`/`attack2`/`attack3`/`skill`/`Skill_R`。
- 敌人仍可用 `EnemyAttackHitRelay` 动画 Event；**当前**以 `EnemyCombat` Active 阶段 `EnemyHitbox` 出伤为准（Attack 动画未绑 Event）。
