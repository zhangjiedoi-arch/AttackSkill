---
name: combat-hit
description: >-
  AttackSkill 战斗出伤管线：AttackHitRelay、SkillHitProfile、形状检测、HitResolver、VFX/SFX。
  在改普攻扇形、E 技能多段、挂点出伤、去重过滤或跳字事件时使用。
---

# 战斗出伤管线

## 方案

统一：触发（动画 Event / 窗口）→ 形状检测 → `HitResolver` 过滤去重 → `IDamageable.TakeDamage` → VFX/SFX。E 技能用 `SkillHitProfile` 配置多段。

## 关键文件

- `Assets/Scripts/Combat/AttackHitRelay.cs`
- `SkillHitProfile.cs` / `SkillHitSegment.cs` / `SkillHitExecutor.cs` / `SkillHitProfileDefaults.cs`
- `HitResolver.cs` / `HitRequest.cs` / `HitSession.cs`
- `FanHitDetector.cs` / `ShapeHitDetector.cs` / `HitSocketResolver.cs`
- `Health.cs` / `CombatLayers.cs` / `VfxObjectPool.cs` / `SlashArcVfx.cs`
- Editor：`SkillHitProfileMenu.cs`
- Resources：`Combat/SkillHit_Player_E.asset`、`SkillHit_Enemy_Basic.asset`

## 数据流

```text
普攻：OnAttackHit → Fan → HitResolver.DefaultPlayerOffense
E：SkillHit(segmentIndex) → Profile 段 → Socket → Sphere/Cylinder/Fan
  → Executor → Resolver → Applied 事件（世界跳字）
```

## 实现步骤

1. 改数值/形状：编辑 Profile SO 或 Relay `swings[]`。
2. 动画 Event：推荐 `SkillHit(int)`；兼容 `Hit_Chest_R/L`、`Hit_Root`。
3. 新挂点：扩 `HitSocketId` + Avatar + Resolver。
4. 层级：玩家打 `PlayerOffenseHurtboxMask`（Enemy+Default）。
5. 同段去重：`HitSession` 按 root InstanceID。
6. 大招双通道时设 `SuppressAnimHits`。
7. `VfxObjectPool.Prewarm`；SFX 空则 Settings 回填。
8. Gizmo：`drawSocketHitGizmos` 调半径。
9. 菜单：`AttackSkill/Combat/Create Default Skill Hit Profiles`。
10. Relay **必须与 Animator 同物体**，否则 Event 调不到。

## 约定与坑

- 扇形高度用 `HitHeight`，不完全跟 weapon Y。
- 刀光 Instantiate 到世界根，不跟随角色。
- Friendly / Dead / Owner 过滤在 Resolver flags。
- 世界 UI 只订 `HitResolver.Applied` 成功结算。
