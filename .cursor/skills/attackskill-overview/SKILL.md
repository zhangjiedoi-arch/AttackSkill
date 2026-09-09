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
| `Assets/Scenes/GameScene.scene` | 主玩法（海滩 + 肉鸽平面） |

总配置：`Assets/Resources/CharacterRuntimeSettings.asset`

## 玩法闭环

```text
OpenScene（登录 / 选性别）
→ GameScene 海滩清波 intro
→ 传送 RouGeLikePlane（肉鸽刷怪 + 升级三选一 + 3 分钟倒计时）
→ 全灭 / 倒计时归零结算 → 读档续玩或回海滩
```

## Skill 索引

| Skill | 功能 |
|-------|------|
| [core-services](../core-services/SKILL.md) | GameServices / SceneSingleton |
| [save-pause-input-gate](../save-pause-input-gate/SKILL.md) | 存档、暂停、输入闸（含肉鸽档字段） |
| [open-scene-flow](../open-scene-flow/SKILL.md) | OpenScene / 性别 |
| [character-hsm](../character-hsm/SKILL.md) | HSM 移动 / 战斗状态（E/R 走 TimedHit，无 Timeline） |
| [party-switch](../party-switch/SKILL.md) | 小队切人 |
| [avatar-sockets](../avatar-sockets/SKILL.md) | Avatar 挂点 / 工具装配 |
| [exploration-tools](../exploration-tools/SKILL.md) | 翅膀 / 御剑 / 摩托 + Tab/T |
| [combat-hit](../combat-hit/SKILL.md) | 出伤 / TimedHitProfile |
| [enemy-ai](../enemy-ai/SKILL.md) | 敌人 AI / 海滩刷怪 |
| [rouge-like](../rouge-like/SKILL.md) | 肉鸽传送、刷怪升级、被动、倒计时结算 |
| [third-person-camera](../third-person-camera/SKILL.md) | 第三人称相机 |
| [ui-hud-dialogs](../ui-hud-dialogs/SKILL.md) | HUD / 对话框 / 本地化 / 小地图 |
| [world-ui](../world-ui/SKILL.md) | 头顶血条 / 伤害跳字 |
| [audio-bgm](../audio-bgm/SKILL.md) | BGM / 角色音效 |

## 工程脚本根

`Assets/Scripts/` → `Character` / `Combat` / `Enemy` / `Rouge` / `UI` / `Camera` / `Game` / `Core` / `Audio` / `Localization`

## 改功能时的顺序建议

1. 先读本索引 → 打开对应 skill  
2. 改数据优先动 SO / RuntimeSettings / `Resources/Rouge`，再改代码  
3. 涉及输入阻塞用 `GameplayInputGate`，勿直接乱改 `timeScale`  
4. 出伤统一进 `HitResolver`，世界跳字订 `HitResolver.Applied`  
5. 玩家 E/R 只改 `TimedHitProfile`，不要接 Timeline 技能播放器  
6. 开局只走 `GameProgress.BeginPlay`；Party 不要在 `Start` 里生成角色。局内阶段切转换 `GameProgress.Request*`，不要再加平级 DDOL 循环管理器
