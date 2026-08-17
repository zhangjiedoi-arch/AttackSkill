---
name: avatar-sockets
description: >-
  AttackSkill 角色 Avatar 挂点与工具 Prefab 运行时装配（CharacterAvatar、CharacterToolAttach）。
  在接线武器/翅膀/御剑/摩托挂点、出伤 Socket 或气流挂点时使用。
---

# Avatar 挂点 / 工具装配

## 方案

表现与玩法分离：`CharacterAvatar` 只绑模型 / Animator / 挂点；工具 Prefab 由 `CharacterToolAttach` 运行时挂到固定节点，退出只 `SetActive(false)`，不 Destroy。

## 关键文件

- `Assets/Scripts/Character/CharacterAvatar.cs`
- `Assets/Scripts/Character/CharacterToolAttach.cs`
- `Assets/Scripts/Editor/CharacterToolWiringMenu.cs`
- Prefab 引用：`CharacterRuntimeSettings` 的 `motorcyclePrefab` / `swordPrefab` / `wingsPrefab` / `skillRAoeVfxPrefab`

## 挂点命名（敏感）

| 用途 | 节点名 |
|------|--------|
| 摩托 | `Motorcycle_pos` |
| 御剑 | `Sword_pos` |
| 翅膀 | `wings_pos`（小写 w） |
| 出伤 | `Hit_Chest_R` / `Hit_Chest_L` / `Hit_Root` 等 |
| R 技能 AOE | `R_Hit_Root`（`HitSocketId.R_Hit_Root`） |
| 普攻可见武器 | `Weapon_Pos`（`AttackHitRelay.SetWeaponVisible`） |

## 数据流

```text
Assembler / OnValidate AutoBind
→ 工具激活 CharacterToolAttach.Show*
→ 子物体名 Tool_Motorcycle / Tool_Sword / …
→ 退出 Hide*（失活保留实例）
```

## 实现步骤

1. 模型下加精确命名挂点。
2. RuntimeSettings 填 Tools Prefab。
3. 用 Editor 菜单 `CharacterToolWiring` 接线。
4. 新工具：扩 Socket + Show/Hide API。
5. 出伤挂点给 `HitSocketResolver` / `AttackHitRelay`。
6. 气流：`WingFlightAirflowVfx`（可被 Avatar 偏移覆盖）。
7. 确认 `animator.applyRootMotion = false`。
8. 御剑挂点缺失时回退 `weapon`。
9. 固定实例名，避免重复 Instantiate。
10. Prefab 只从 Settings 取，不在运行时扫 AssetDatabase。

## 约定与坑

- 挂点名大小写敏感。
- `Weapon_Pos` 默认隐藏，普攻 `BeginSwing` 显示，`EndCombat` / 闪避隐藏；R 技能不再往该挂点生成脆刃。
- 工具网格不要拷进 Resources 重复一份。
