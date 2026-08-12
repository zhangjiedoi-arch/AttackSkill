# AttackSkill

第三人称动作探索 Demo，基于**团结引擎（Tuanjie）**开发。聚焦角色操控、战斗出伤、探索工具与小队切换，适合作为动作 / 开放世界玩法原型与管线实验项目。

## 引擎与环境

| 项 | 说明 |
|----|------|
| 引擎 | 团结引擎 **1.9.0**（`2022.3.62t8`） |
| 渲染 | Built-in Render Pipeline |
| 产品名 | AttackSkill |
| 主场景 | `Assets/Scenes/GameScene.scene` |
| 登录 / 入口 | `Assets/Scenes/OpenScene.scene` |
| 脚本规模 | `Assets/Scripts` 下约 **150+** C# 文件 |

> 请使用与 `ProjectSettings/ProjectVersion.txt` 一致的团结引擎版本打开工程，避免序列化与包版本不匹配。

## 玩法概览

### 角色与操控

- **HSM（分层状态机）** 驱动移动、跳跃、下落、攀爬、普攻连段、闪避与 **E 技能**
- 小队切人：前台角色切换（含技能残留等规则）
- Avatar 挂点装配：武器、翅膀、御剑、摩托等工具 Prefab 运行时挂接
- 第三人称轨道相机：跟随、环视、滚轮缩放、切人跟拍

### 战斗

- 普攻：扇形判定 + 刀光 / 挥砍音效
- **E 技能**：动画 Event 驱动多段出伤（左右拳 + 砸地 AOE）
- 出伤配置化：`SkillHitProfile`（挂点、形状、伤害、特效、音效）
- 敌人：感知 → 追击 → 交战；数据走 `EnemyDefinition`；出伤经动画 Event → `HitResolver`

### 敌人死亡表现

死亡后由 `EnemyDeathDirector` 按 `EnemyDefinition.echoChance` 分流（可用 `deathForceMode` 强制）：

| 结果 | 表现 | 尸体 |
|------|------|------|
| **声骸（Echo）** | 金色半透明残影（`EnemyDeathGold`） | 默认约 20s 后销毁 |
| **飘散（Dissolve）** | 噪声溶解 + 上浮（`EnemyDeathDissolve`） | 特效结束立即销毁 |

- 死亡立刻关闭碰撞，不再挡路
- 声骸附近（约 1m）显示获取提示；**F** 暂为占位 Tip（`echo_obtain_wip`），并优先于滑翔

### 世界 UI

- 敌人头顶血条（等级 + 血量）：玩家约 **20m** 内显示，遮挡时隐藏
- 伤害跳字：低伤白 / 中伤黄 / 高伤红，字号随档位变化
- **Screen Space Overlay** 投影（`WorldUiService`），不用 World Space Billboard

### 探索工具（Tab 轮盘 + T 键）

| 工具 | 说明 |
|------|------|
| 翅膀飞行 | 滑翔移动，左右气流特效 |
| 御剑飞行 | 御剑姿态空中移动，居中气流特效 |
| 摩托 | 骑乘移动与音效 |

### UI / 系统

- 战斗 HUD：血条、小队、技能键位、探索工具轮盘
- 世界挂点 UI：敌人血条、伤害跳字、声骸获取提示
- 开场登录 / 选性别、设置、本地化（多语言）
- 场景 BGM、角色移动 / 探索 / 战斗音效
- 存档：小队、装备工具等进度读写；暂停与玩法输入闸

## 操作（默认）

| 输入 | 功能 |
|------|------|
| WASD | 移动 |
| Space | 跳跃 |
| Left Shift | 冲刺 |
| 鼠标左键 | 普攻 |
| 鼠标右键 | 闪避 |
| **E** | 技能 |
| **Tab** | 探索工具轮盘 |
| **T** | 切换当前装备的探索工具 |
| **F** | 滑翔；靠近声骸时优先为获取交互 |

战斗 HUD 上的 E 按钮与键盘 E 均可释放技能。

## 工程结构

```text
AttackSkill/
├── Assets/
│   ├── Scripts/           # 玩法与框架代码
│   │   ├── Character/     # HSM、小队、探索工具、音效、Avatar
│   │   ├── Combat/        # 出伤、HitProfile、判定、对象池
│   │   ├── Enemy/         # 敌人 AI、生成、定义、死亡表现
│   │   ├── UI/            # HUD / 对话框 / 世界 UI
│   │   ├── Localization/  # 本地化
│   │   ├── Audio/         # 场景 BGM 等
│   │   ├── Camera/        # 第三人称相机
│   │   ├── Game/          # 暂停、存档、输入门控
│   │   └── Core/          # 通用服务
│   ├── Shaders/           # 刀光、死亡金透 / 飘散等
│   ├── Resources/         # RuntimeSettings、SkillHit、WorldUI、死亡材质等
│   ├── Prefabs/           # 角色、工具、VFX、WorldUI
│   ├── ScriptableObjects/ # 敌人定义、刷怪组等
│   ├── Audio/             # 音效 / BGM
│   ├── Scenes/            # OpenScene / GameScene
│   └── ...
├── Packages/
├── ProjectSettings/
└── .cursor/skills/        # 各玩法管线说明（Agent Skill）
```

## 关键配置入口

多数运行时资源通过 ScriptableObject / Resources 装配，避免运行时 `AssetDatabase`：

| 资源 | 路径 / 说明 |
|------|-------------|
| `CharacterRuntimeSettings` | `Assets/Resources/CharacterRuntimeSettings.asset` |
| `SkillHit_Player_E` | `Assets/Resources/Combat/SkillHit_Player_E.asset` |
| `ExplorationToolCatalog` | 探索工具目录 |
| `EnemyDefinition_*` | `Assets/ScriptableObjects/Enemy/`（含声骸概率、溶解参数等） |
| 死亡材质 / Shader Refs | `Assets/Resources/Enemy/` |
| WorldUI Prefab | `Assets/Resources/UI/WorldUI/` |

编辑器菜单（示例）：

- `工具/敌人/重建死亡特效材质`
- `GameObject/AttackSkill/...`（刷怪组、训练木桩等）
- `Assets/Create/AttackSkill/...`（敌人定义等）

更细的管线说明见仓库内 `.cursor/skills/`（如 `enemy-ai`、`world-ui`、`combat-hit`）。

## 主要依赖（节选）

- Input System
- Cinemachine
- Timeline
- Animation Rigging
- TextMesh Pro / uGUI
- Visual Effect Graph
- 团结相关：AI Graph、Codely Bridge 等（见 `Packages/manifest.json`）

## 如何运行

1. 安装对应版本的**团结引擎**
2. 用引擎打开本仓库根目录
3. 等待导入与包解析完成
4. 打开 `Assets/Scenes/OpenScene`（推荐）或直接进 `GameScene`
5. 进入 Play 模式试玩

本地 PC 构建产物默认输出到 `Output/`（已在 `.gitignore` 中忽略）。

## 参考素材

本 Demo 使用或参考了以下公开资源站点（请遵守各站授权与署名要求）：

| 类型 | 来源 |
|------|------|
| 角色模型 / 场景 | [模之屋 aplaybox](https://www.aplaybox.com/) |
| 角色动作 | [Mixamo](https://www.mixamo.com/) |
| 模型 / 动作包 | [Quaternius](https://quaternius.com/) |
| 音效 | [Freesound](https://freesound.org/) |
| 引擎插件 / 官方素材文档 | [Unity 文档](https://docs.unity3d.com/) |

## 状态说明

当前为**可玩 Demo / 原型**：核心战斗、探索、世界 UI 与敌人死亡分流已接通。声骸 **F 获取**仍为占位提示；部分 HUD 技能键与工具轮盘槽位也可能仅为展示、尚未实现。

## 许可证

未指定开源许可证时，默认保留所有权利。对外使用或开源前，请自行确认第三方模型、动作、音频、插件的授权范围，并在需要时补充 `LICENSE`。
