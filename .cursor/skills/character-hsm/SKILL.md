---
name: character-hsm
description: >-
  AttackSkill 角色分层状态机（HSM）：移动、跳跃、下落、攀爬、普攻连段、闪避、E 技能与探索工具薄壳状态。
  在改角色操控、加状态、改连段/技能入口或 Animator 参数时使用。
---

# 角色 HSM

## 方案

`GenshinLikeCharacter` + `HStateMachine`：按 LCA 差分 Exit/Enter。输入写入 `CharacterContext`，电机 `CharacterMotor` + Animator 执行。支持 Active / Residual 切人模式。

## 关键文件

- `Assets/Scripts/Character/HSM/GenshinLikeCharacter.cs`
- `HStateMachine.cs` / `HState.cs` / `CharacterStateTree.cs`
- `CharacterContext.cs` / `CharacterMotor.cs` / `CharacterAnimParams.cs` / `CharacterInput.cs`
- States：`GroundedStates` / `AirborneStates` / `ClimbSwimStates` / `CombatStates` / `DodgeState` / `MotorcycleState`
- E：`CharacterSkillPlayer.cs`
- 装配：`CharacterRuntimeAssembler.cs`

## 数据流

```text
GameInput（受 GameplayInputGate）
→ GenshinLikeCharacter 写 Context
→ HSM Update/FixedUpdate
→ Motor + Animator

普攻 AttackState → AttackHitRelay.BeginSwing + 动画 Event
E → SkillState / Timeline → SkillHit(n) 或窗口
```

## 实现步骤

1. 新状态：继承 `CharacterState`，在 `CharacterStateTree` 注册并 Bind 父子。
2. 切换用 `GoTo(...)`，读写经 `Ctx`，勿旁路电机。
3. Animator 参数名统一走 `CharacterAnimParams`。
4. 普攻：改 `AttackState` 连段或 `AttackHitRelay.swings[]`。
5. E：改 Timeline Prefab 轨道约定，或 `SkillHit_Player_E` + 动画 Event 下标。
6. HUD E：`CombatSkillInput.Request()`（与键盘 E 同源意图）。
7. 攀爬：`InteractInputEnabled` 当前为 false（R 占用）；恢复需改开关。
8. Residual：技能中切人 `BecomeResidual()`，勿抢相机。
9. 调试：`drawDebugState` / `CurrentStatePath`。
10. 新角色 Prefab 优先只挂 `CharacterAvatar`，Assembler 补玩法组件。

## 约定与坑

- 同叶重进需 `allowReenter`。
- 连段索引在 `Attack.OnEnter` 预写下一段；勿在进入时用 ComboReset 误清。
- Timeline 轨道名：`SwingAnimation` / `SwingAudio` / `SkillCamera` / `Circle`。
- 大招可 `SuppressAnimHits`，防窗口与 Event 双结算。
- F = 滑翔 `Glide`；翅膀起飞是 T 工具，不是 F。
