---
name: core-services
description: >-
  AttackSkill 场景服务注册表与单例约定（GameServices、SceneSingleton）。
  在新增场景级服务、替换 FindObjectOfType、处理 DontDestroyOnLoad 或重复实例时使用。
---

# Core：GameServices / SceneSingleton

## 方案

用静态注册表替代到处 `FindObjectOfType`；单例 Awake 认领，可选 DDOL。

## 关键文件

- `Assets/Scripts/Core/GameServices.cs`
- `Assets/Scripts/Core/SceneSingleton.cs`
- 输入：`IGameplayInput.cs` / `InputSystemGameplayInput.cs` / `GameInputBootstrap.cs`

## 数据流

```text
服务 Awake → Register / ShouldKeep
运行时 → GameServices.Resolve* / Instance
OnDestroy → Unregister
```

当前注册：`OpenSceneFlow`、`ThirdPersonCamera`；`Party` / `UI` 为 `Instance` 别名。

## 实现步骤

1. 新场景级服务：在 `GameServices` 增加属性 + `Register` / `Unregister`。
2. 服务 `Awake`：先 `SceneSingleton.ShouldKeep(this, Instance)`，失败直接 return。
3. 需要跨场景：`SceneSingleton.ApplyDontDestroyOnLoad(this, true)`。
4. 解析：优先传入的 preferred，再读 Services；少直接 Find。
5. 重复注册打 `LogWarning`，后注册者覆盖。
6. `OnDestroy` 必须 Unregister，避免持有已销毁对象。
7. UI DDOL 交给 `UIBootstrap`，勿双重 DontDestroy。

## 约定

| 组件 | ExecutionOrder（参考） |
|------|------------------------|
| UIManager | -150 |
| GameProgressController | -100 |
| WorldUiService | -80 |

`GameServices.Party` / `UI` 只是别名，真正生命周期在各自 `Instance`。
