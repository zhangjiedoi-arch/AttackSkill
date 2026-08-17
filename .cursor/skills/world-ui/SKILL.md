---
name: world-ui
description: >-
  AttackSkill 世界挂点 UI：敌人头顶血条与伤害跳字（Screen Overlay 投影、遮挡隐藏）。
  在改血条/跳字 Prefab、挂点高度、可见距离、遮挡层或 HitResolver 跳字时使用。
---

# 世界 UI（血条 / 跳字）

## 方案

**不要用 World Space Canvas 做 Billboard**（团结下旋转不可靠、易镜像）。  
采用 **Screen Space Overlay**：世界挂点 → `WorldToScreenPoint` → 屏幕定位；距离与遮挡控制显隐。

运行时 `WorldUiService.EnsureExists()` 创建 DDOL 根 `WorldUI_Runtime`（不必预挂场景）。

## 关键文件

- `Assets/Scripts/UI/World/WorldUiService.cs`
- `WorldUiScreen.cs` — 相机解析、投影、遮挡、挂点
- `EnemyBloodHud.cs` / `DamageNumberView.cs` / `DamageNumberPool.cs`
- Prefab：`Resources/UI/WorldUI/` 或 `CharacterRuntimeSettings` 引用（含 `ObtainRemains`）
- 声骸：`EchoRemainInteract.cs` / `ObtainRemainsHud.cs`

## 数据流

```text
Progress/Party Awake → WorldUiService.EnsureExists（DDOL Overlay Canvas）
敌人 Initialize → AttachEnemyBlood → LateUpdate 投影 + 距离/遮挡
HitResolver.Applied → DamageNumberPool 跳字（元素色；暴击橙黄 + 字号×2；播放中再判遮挡）
```

## 跳字样式

| 条件 | 颜色 | 字号 |
|------|------|------|
| 光 | 黄 | 正常 |
| 暗 | 黑 | 正常 |
| 雷 | 紫 | 正常 |
| 冰 | 蓝白 | 正常 |
| 火 | 红 | 正常 |
| 暴击 | 橙黄 | ×2 |

## 挂点

`CharacterController` 顶部 + **0.4m**（`WorldUiScreen.ResolveEnemyHeadWorldPos`）。无 CC 时回退 `bloodWorldOffset`。

## 实现步骤

1. Prefab 配到 RuntimeSettings（`enemyBloodPrefab` / `damageNumberPrefab`）。
2. 调 `bloodVisibleRange`、`bloodWorldOffset`、`damageNumberLifetime`。
3. `occlusionMask` 含环境，排除 Ignore Raycast / UI。
4. 渲染相机：`ResolveRenderCamera`（优先 `ThirdPersonCamera.ControlledCamera`）。
5. 血条显隐用 `CanvasGroup.alpha`，**禁止** `SetActive(false)` 停掉 LateUpdate。
6. 跳字 Pool Prewarm；回收只失活。
7. 等级文案读 `EnemyDefinition.level`。
8. 服务销毁时 Unbind `HitResolver.Applied`。
9. Overlay 子项：`PrepareOverlayItem`（居中锚点、scale=1）。
10. 与战斗 HUD Overlay 分离（独立 Canvas sortingOrder）。

## 约定与坑

- ExecutionOrder：Service -80；Hud/跳字 1000（相机之后投影）。
- 不要再引入 World Space Billboard / pivot 180° 方案。
- 只对成功 `Applied` 的命中跳字。
- **声骸获取提示**：死亡 Echo 后挂 `EchoRemainInteract` + `ObtainRemains` Overlay；玩家 ≤1m 显示；F 弹 Tip（loc key `echo_obtain_wip`），并压制滑翔（`ShouldPreferInteract`）。
