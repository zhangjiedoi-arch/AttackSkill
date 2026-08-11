# AttackSkill

第三人称动作探索 Demo，基于**团结引擎（Tuanjie）**开发。聚焦角色操控、战斗出伤、探索工具与小队切换，适合作为动作 / 开放世界玩法原型与管线实验项目。

## 引擎与环境

| 项 | 说明 |
|----|------|
| 引擎 | 团结引擎 **1.9.0**（`2022.3.62t8`） |
| 产品名 | AttackSkill |
| 主场景 | `Assets/Scenes/GameScene.scene` |
| 登录 / 入口 | `Assets/Scenes/OpenScene.scene` |
| 脚本规模 | `Assets/Scripts` 下约 140+ C# 文件 |

> 请使用与 `ProjectSettings/ProjectVersion.txt` 一致的团结引擎版本打开工程，避免序列化与包版本不匹配。

## 玩法概览

### 角色与操控

- **HSM（分层状态机）** 驱动移动、跳跃、下落、攀爬相关状态、普攻连段、闪避与 **E 技能**
- 多人小队：可切换前台角色（含技能残留等切换规则）
- Avatar 挂点装配：武器、翅膀、御剑、摩托等工具 Prefab 运行时挂接

### 战斗

- 普攻：扇形判定 + 刀光 / 挥砍音效
- **E 技能**：动画 Event 驱动多段出伤（左右拳 + 砸地 AOE）
- 出伤配置化：`SkillHitProfile`（挂点 ID、形状、伤害、特效、音效）
- 敌人：感知、追击、动画 Event 出伤；定义走 `EnemyDefinition` ScriptableObject

### 探索工具（Tab 轮盘 + T 键）

| 工具 | 说明 |
|------|------|
| 翅膀飞行 | 滑翔移动，左右气流特效（Sparks blue） |
| 御剑飞行 | 御剑姿态空中移动，居中气流特效 |
| 摩托 | 骑乘移动与音效 |

### UI / 系统

- 战斗 HUD：血条、小队、技能键位、探索工具轮盘
- 登录、设置、本地化（多语言表）
- 场景 BGM、角色移动 / 探索 / 战斗音效
- 存档相关读写（小队、装备工具等）

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
| F | 滑翔（相关状态） |

战斗 HUD 上的 E 按钮与键盘 E 均可释放技能。

## 工程结构

```text
AttackSkill/
├── Assets/
│   ├── Scripts/           # 玩法与框架代码
│   │   ├── Character/     # HSM、小队、探索工具、音效、Avatar
│   │   ├── Combat/        # 出伤、HitProfile、判定、对象池
│   │   ├── Enemy/         # 敌人 AI、生成、定义
│   │   ├── UI/            # HUD / 对话框
│   │   ├── Localization/  # 本地化
│   │   ├── Audio/         # 场景 BGM 等
│   │   ├── Camera/        # 相机相关
│   │   ├── Game/          # 暂停、输入门控等
│   │   └── Core/          # 通用服务
│   ├── Resources/         # RuntimeSettings、SkillHitProfile、工具目录等
│   ├── Prefabs/           # 角色、工具、VFX
│   ├── Audio/             # 音效 / BGM 资源
│   ├── Scenes/            # 场景
│   └── ...
├── Packages/              # UPM 依赖
├── ProjectSettings/
└── Docs/                  # 需求与设计文档
```

## 关键配置入口

多数运行时资源通过 ScriptableObject / Resources 装配，避免运行时 `AssetDatabase`：

| 资源 | 路径 / 说明 |
|------|-------------|
| `CharacterRuntimeSettings` | `Assets/Resources/CharacterRuntimeSettings.asset`（Prefab、VFX、音效、SkillHit Profile 等） |
| `SkillHit_Player_E` | `Assets/Resources/Combat/SkillHit_Player_E.asset`（E 技能多段出伤） |
| `ExplorationToolCatalog` | 探索工具目录 |
| `EnemyDefinition_*` | `Assets/ScriptableObjects/Enemy/` |

编辑器菜单（示例）：

- `AttackSkill/Combat/Create Default Skill Hit Profiles`
- `AttackSkill/Character/...`（工具 Prefab / 挂点接线）

## 主要依赖（节选）

- Input System
- Cinemachine
- Timeline
- Animation Rigging
- TextMeshPro / uGUI
- Visual Effect Graph
- 团结相关：AI Graph、Codely Bridge 等（见 `Packages/manifest.json`）

## 如何运行

1. 安装对应版本的**团结引擎**
2. 用引擎打开本仓库根目录
3. 等待导入与包解析完成
4. 打开 `Assets/Scenes/OpenScene` 或直接进 `GameScene`
5. 进入 Play 模式试玩

## Git 提交建议

上传前建议：

1. 添加 Unity / 团结常用 `.gitignore`（忽略 `Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/` 等）
2. **不要**提交 `Library`、本地缓存与大型中间产物
3. 确认 `Assets`、`Packages`、`ProjectSettings` 已纳入版本库
4. 大体积模型 / 音视频可按团队规范使用 Git LFS

## 状态说明

当前为**可玩 Demo / 原型**：核心战斗与探索链路已接通，部分 HUD 功能（如部分技能键）仍为占位提示，工具轮盘中也可能存在仅展示未实现的槽位。

## 许可证

未指定开源许可证时，默认保留所有权利。若对外开源，请补充 `LICENSE` 并确认第三方资源（模型、VFX、音频包等）的授权范围。
