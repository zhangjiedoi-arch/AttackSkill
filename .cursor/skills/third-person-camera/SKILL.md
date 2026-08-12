---
name: third-person-camera
description: >-
  AttackSkill 第三人称轨道相机（ThirdPersonCamera、YawTransform、锁鼠、防穿模）。
  在改跟随、环视、滚轮缩放、切人跟拍、技能接管相机或 Cursor 锁定时使用。
---

# 第三人称相机

## 方案

类原神 Yaw/Pitch 轨道：跟随目标 + 鼠标环视 + 滚轮距离 + SphereCast 防穿模。角色移动朝向读 `YawTransform`。

## 关键文件

- `Assets/Scripts/Camera/ThirdPersonCamera.cs`（命名空间 `AttackSkill.CameraSystem`）
- 注册：`GameServices.Register` / `ResolveCamera`

## 数据流

```text
Party 设 FollowTarget
→ LateUpdate：更新 yaw/pitch/pivot → ApplyRigTransforms
→ 角色 CharacterContext.cameraYaw = YawTransform
大招/演出 → SetGameplayControlEnabled(false) → 结束后恢复
```

## 实现步骤

1. 调参：`pivotOffset`、distance 范围、pitch 限制、灵敏度。
2. `collisionMask` 排除 UI / 特效 / 玩家层。
3. 锁鼠：`lockCursorOnPlay`；Alt 解锁；UI 关闭后 `RestoreDesiredCursorLock`。
4. 技能 Timeline 接管后务必重新 `enabled` / 恢复控制。
5. 解析相机：`GameServices.ResolveCamera()`，少 Find。
6. Rig（Yaw/Pitch/Camera）可空，Awake `EnsureRigHierarchy` 自动建。
7. 切人只换 `FollowTarget` + 必要时 `SnapToFollowTarget`，勿重建相机。
8. 输入闸：尊重 `GameplayInputGate` / `GamePause`（软阻塞时不转）。

## 约定与坑

- `ControlledCamera` 的 `localRotation` 保持 identity，世界旋转来自父级 pivot。
- 期望锁鼠状态与 `OnEnable` 恢复：避免大招 `enabled=false` 后丢锁。
- 世界 UI / 出伤解析渲染相机时优先 `ControlledCamera`。
