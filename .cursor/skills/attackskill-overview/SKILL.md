---
name: attackskill-overview
description: >-
  AttackSkill 项目功能总览与 Skill 索引。在需要定位某条玩法管线、查找对应实现文档、
  或不确定该读哪份 skill 时使用。
---

# AttackSkill 功能总览

第三人称动作探索 Demo（团结引擎）。运行时配置优先走 `Resources` / ScriptableObject，禁止运行时 `AssetDatabase`。

## 场景入口

| 场景 | 作用 |
|------|------|
| `Assets/Scenes/OpenScene.scene` | 开场、登录、选性别 |
| `Assets/Scenes/GameScene.scene` | 主玩法 |

总配置：`Assets/Resources/CharacterRuntimeSettings.asset`

## Skill 索引

| Skill | 功能 |
|-------|------|
| [core-services](../core-services/SKILL.md) | GameServices / SceneSingleton |
| [save-pause-input-gate](../save-pause-input-gate/SKILL.md) | 存档、暂停、输入闸 |
| [open-scene-flow](../open-scene-flow/SKILL.md) | OpenScene / 性别 |
| [character-hsm](../character-hsm/SKILL.md) | HSM 移动 / 战斗状态 |
| [party-switch](../party-switch/SKILL.md) | 小队切人 |
| [avatar-sockets](../avatar-sockets/SKILL.md) | Avatar 挂点 / 工具装配 |
| [exploration-tools](../exploration-tools/SKILL.md) | 翅膀 / 御剑 / 摩托 + Tab/T |
| [combat-hit](../combat-hit/SKILL.md) | 出伤 / SkillHitProfile |
| [enemy-ai](../enemy-ai/SKILL.md) | 敌人 AI / 刷怪 |
| [third-person-camera](../third-person-camera/SKILL.md) | 第三人称相机 |
| [ui-hud-dialogs](../ui-hud-dialogs/SKILL.md) | HUD / 对话框 / 本地化 |
| [world-ui](../world-ui/SKILL.md) | 头顶血条 / 伤害跳字 |
| [audio-bgm](../audio-bgm/SKILL.md) | BGM / 角色音效 |

## 工程脚本根

`Assets/Scripts/` → `Character` / `Combat` / `Enemy` / `UI` / `Camera` / `Game` / `Core` / `Audio` / `Localization`

## 改功能时的顺序建议

1. 先读本索引 → 打开对应 skill  
2. 改数据优先动 SO / RuntimeSettings，再改代码  
3. 涉及输入阻塞用 `GameplayInputGate`，勿直接乱改 `timeScale`  
4. 出伤统一进 `HitResolver`，世界跳字订 `HitResolver.Applied`
