---
name: character-hsm
description: >-
  AttackSkill 角色分层状态机（HSM）：移动、跳跃、下落、攀爬、普攻连段、闪避、E/R 技能与探索工具薄壳状态。
  在改角色操控、加状态、改连段/技能入口或 Animator 参数时使用。
---

# 角色 HSM

## 方案

`GenshinLikeCharacter` + `HStateMachine`：按 LCA 差分 Exit/Enter。输入写入 `CharacterContext`，电机 `CharacterMotor` + Animator 执行。支持 Active / Residual 切人模式。

玩家普攻 / E / R **只走 Animator + TimedHitProfile**，没有 Timeline 技能播放器。

## 关键文件

- `Assets/Scripts/Character/HSM/GenshinLikeCharacter.cs`
- `HStateMachine.cs` / `HState.cs` / `CharacterStateTree.cs`
- `CharacterContext.cs` / `CombatInputBuffer.cs` / `CharacterMotor.cs` / `CharacterAnimParams.cs` / `CharacterInput.cs`
- States：`GroundedStates` / `AirborneStates` / `ClimbSwimStates` / `CombatStates` / `DodgeState` / `MotorcycleState`
- 出伤：`AttackHitRelay` + 角色 `TimedHitProfile`（见 [combat-hit](../combat-hit/SKILL.md)）
- 装配：`CharacterRuntimeAssembler.cs`

## 数据流

```text
GameInput（受 GameplayInputGate）
→ GenshinLikeCharacter 写 Context + CombatInputBuffer（攻击/闪避/E/R 约 0.1s）
→ HSM Update 从缓冲消费
→ Motor + Animator

普攻 AttackState → AttackHitRelay.BeginSwing(combo) → phase attack1/2/3
E → SkillState：Trigger Skill + BeginTimedPhase("skill")
R → SkillRState：Trigger SkillR + BeginTimedPhase("Skill_R")
     AoE 挂点 R_Hit_Root；不生成脆刃、不接管相机
```

## 实现步骤

1. 新状态：继承 `CharacterState`，在 `CharacterStateTree` 注册并 Bind 父子。
2. 切换用 `GoTo(...)`，读写经 `Ctx`，勿旁路电机。
3. Animator 参数名统一走 `CharacterAnimParams`（含 `SkillR`）。
4. 普攻：改 `AttackState` 连段或对应 `TimedHitProfile` phase；进普攻/E/T 会 `CombatEngageUtility` 贴身 2m 内最近敌人。
5. E：改 `TimedHitProfile` phase `skill`（时机/倍率/形状）；Animator 状态名 `skill`，Trigger `Skill`。
6. HUD E：`CombatSkillInput.Request()`；HUD R：`CombatSkillRInput.Request()`。
7. R：Avatar 绑 `R_Hit_Root`；出伤/VFX 只配 `TimedHitProfile` phase `Skill_R`。`Weapon_Pos` 仅普攻显隐，**R 不往该挂点生成脆刃**（脆刃是御剑探索工具）。
8. 攀爬：`InteractInputEnabled` 当前为 false（R 给技能）；恢复需改开关并另绑键。
9. Residual：技能中切人 `BecomeResidual()`，勿抢相机；HSM 技能播完再回收。
10. 调试：`drawDebugState` / `CurrentStatePath`。

## 约定与坑

- 普攻 / 闪避 / E/R：边沿进 `CombatInputBuffer`（默认 0.1s），状态用 `ConsumeBuffered`；技能 CD 未好不消费。
- 普攻 / 闪避 / E/R 中 T 不能进探索工具；工具内 T 退出仍允许。
- 同叶重进需 `allowReenter`。
- 连段索引在 `Attack.OnEnter` 预写下一段；勿在进入时用 ComboReset 误清。
- F = 滑翔 `Glide`；翅膀起飞是 T 工具，不是 F。
- 普攻 / E / 进入 T：`CombatEngageUtility` 搜索 2m，瞬移到约 1.05m 处并朝向敌人；无目标则不移动。
- R 技能 Animator 需有 Trigger `SkillR`（状态名建议 `skill_r` / `SkillR`）；其他角色可先只加参数，挂点后续补。
- 不要再加 Timeline / `CharacterSkillPlayer` 出伤路径。
