---
name: open-scene-flow
description: >-
  AttackSkill 开场流程、登录门闩、性别选择与进入 GameScene（OpenSceneFlowController、LocalAccountStore）。
  在改开场 Timeline、登录/选性别 UI、NewGame 进战斗场景时使用。
---

# OpenScene 流程 / 性别

## 方案

分镜面板推进 → 登录 / 选性别门闩 → 选角 Timeline → `GameBoot.NewGame` + 加载 `GameScene`。性别写入 `LocalAccountStore`，供 Party 组装漂泊者男女。

## 关键文件

- `Assets/Scripts/UI/OpenSceneFlowController.cs`
- `Assets/Scripts/UI/LocalAccountStore.cs`
- `Assets/Scripts/UI/Views/UIOpenScene1Panel.cs` … `UIOpenScene4Panel.cs`
- `UILogInDialog.cs` / `UIChooseGenderDialog.cs` / `UIChangeScenePanel.cs`
- `Assets/Scripts/Game/GameBoot.cs`

## 数据流

```text
Scene1 → 2 → 3(+Timeline_Open) → 4
→ 登录门闩 → 性别门闩 → Link
→ Timeline_Select_Female|Male
→ GameBoot.NewGame + Load GameScene
```

## 实现步骤

1. 调整各段时长 / Space 跳过：改 Flow 内秒数与 skip 逻辑。
2. Timeline 物体名约定：`Timeline_Open`、`Timeline_Select_Female`、`Timeline_Select_Male`。
3. 新门闩：仿 `_loginGateDone` / `_genderGateDone`。
4. 进游戏是否关 UI：`closeUiOnEnter`。
5. OpenScene `UnlockGender`；进 GameScene 后再 Lock。
6. Continue 入口设 `GameBoot.Continue`，勿走 NewGame Link。
7. `gameSceneName` 与 Build Settings 一致。
8. 验证性别 → Party 0 号槽男女 Prefab。
9. 注册/注销：`GameServices.OpenSceneFlow`。
10. 旧账号：`MigrateLegacySecrets`。

## 约定与坑

- 开场允许改性别；局内锁定。
- Flow 与 Link 协程互斥，防重入。
