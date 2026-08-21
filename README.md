# AttackSkill

第三人称动作探索 Demo，基于**团结引擎（Tuanjie）**开发。涵盖角色 HSM 操控、小队切人、统一出伤管线、探索工具、敌人 AI，以及**肉鸽刷怪升级**闭环；适合作为动作 / 开放世界玩法原型与管线实验项目。

## 演示

[Bilibili 肉鸽向演示视频](https://www.bilibili.com/video/BV1ZC8i6CEYG/)

## 引擎与环境

| 项 | 说明 |
|----|------|
| 引擎 | 团结引擎 **1.9.0**（Unity `2022.3.62t8`） |
| 渲染 | Built-in Render Pipeline |
| 产品名 | AttackSkill |
| 开场 / 登录 | `Assets/Scenes/OpenScene.scene` |
| 主玩法 | `Assets/Scenes/GameScene.scene` |
| 脚本根 | `Assets/Scripts/`（Character / Combat / Enemy / Rouge / UI / Game / …） |

> 请使用与 `ProjectSettings/ProjectVersion.txt` 一致的团结引擎版本打开工程，避免序列化与包版本不匹配。

## 玩法闭环

```text
OpenScene（登录 / 选性别）
    → GameScene（探索 + 战斗 + 小队）
        → 海滩清波 intro
            → 传送 RouGeLikePlane（肉鸽）
                → 刷怪升级三选一 + 3 分钟获救倒计时
                    → 全灭 / 倒计时归零结算
                        → 读档续玩或回海滩重开
```

## 功能概览

### 角色与操控

- **HSM（分层状态机）**：移动、跳跃、下落、攀爬、普攻连段、闪避、E / R 技能、探索工具薄壳状态
- **小队切人**：性别阵容（漂泊者男女 + 千咲 + 柯莱塔）；新角立刻可控，旧角 Residual 播完再回收
- **Avatar 挂点**：武器 / 翅膀 / 御剑 / 摩托等 Prefab 运行时装配
- **第三人称轨道相机**：跟随、环视、滚轮缩放、切人 Snap、墙体防穿

### 战斗出伤

- 普攻扇形判定；技能多段走 `TimedHitProfile` / `SkillHitProfile`（挂点、形状、伤害、VFX/SFX）
- 统一结算：`AttackHitRelay` → 形状检测 → `HitResolver`（过滤去重）→ `CombatStats` 伤害公式
- VFX 对象池；世界跳字订阅 `HitResolver.Applied`

### 敌人

- 数据驱动：`EnemyDefinition` + `EnemyBrain`（Idle → Alert → Chase → Combat → Return / Dead）
- 刷怪：`EnemySpawnGroup` / `SpawnPoint`（海滩 intro 距离激活）
- 死亡分流：`EnemyDeathDirector`
  - **Echo（声骸）**：金色半透明残影
  - **Dissolve（飘散）**：噪声溶解；**肉鸽区域强制溶解**
- 掉落：约 30% 治疗圈；肉鸽区另掉经验球

### 肉鸽（RouGeLike）

| 项 | 说明 |
|----|------|
| 流程 | 清海滩 intro → 传送 `PlayerSpawn` → 平面内批量刷怪 |
| 场上上限 | `30 + 5 × (等级 - 1)`，封顶 **100**（Inspector 可调） |
| 成长 | 经验升级 → 三选一被动；攻防血 **每级 +10%**（玩家与敌同乘） |
| 倒计时 | **3 分钟**获救；剩余秒写入存档（`-1` 未开表，`0` 已结算） |
| 结算 | 全灭 / 倒计时归零 → `UIGameOverDialog`（救援标题可区分） |
| 数据 | `PartyRougeProgress` + `Resources/Rouge/*.json` + 等级解锁刷怪表 |

### 探索工具（Tab 轮盘 + T）

| 工具 | 说明 |
|------|------|
| 翅膀 | 飞行移动 + 气流特效 |
| 御剑 | 御剑姿态空中移动 |
| 摩托 | 骑乘移动与音效 |

未实现槽位为 Stub，确认时 Tip 提示。

### UI / 系统

- 战斗 HUD：编队头像、生存、技能键、系统、任务；进肉鸽后再开倒计时面板
- 对话框：暂停、设置、技能轮盘、三选一、GameOver、登录 / 选性别等
- 世界 UI：头顶血条、伤害跳字（Screen Overlay 投影，非 World Billboard）
- 本地化：中 / 英（`LocalizationService` + Json Bundle）
- 账号资料（`LocalAccountStore`）与进度档（`GameSaveData` JSON）分离
- 硬暂停 `GamePause` ≠ 轮盘软阻塞 `GameplayInputGate.SoftBlock`
- 场景 BGM（海滩 / 肉鸽 drone）+ 角色 SFX

## 操作（默认）

| 输入 | 功能 |
|------|------|
| WASD | 移动 |
| Space | 跳跃 |
| Left Shift | 冲刺 |
| 鼠标左键 | 普攻 |
| 鼠标右键 | 闪避 |
| **E** | 技能 |
| **R** | 大招（若角色配置） |
| **1 / 2 / 3** | 切人 |
| **Tab** | 探索工具轮盘（按住选扇区） |
| **T** | 切换当前装备的探索工具 |
| **F** | 滑翔；靠近声骸时优先为获取交互 |
| **Esc** | 暂停菜单 |
| **F5** | 快速存档 |
| **F6** | 删除本地进度档（调试） |

战斗 HUD 打开时 **Tab 优先给技能轮盘**，不占用切人。

## 工程结构

```text
AttackSkill/
├── Assets/
│   ├── Scripts/
│   │   ├── Character/     # HSM、小队、探索工具、Avatar、音效
│   │   ├── Combat/        # 出伤、HitProfile、Stats、VFX 池
│   │   ├── Enemy/         # AI、刷怪、死亡、肉鸽 Flow
│   │   ├── Rouge/         # 等级 / 被动 / 经验球 / 环绕刃等
│   │   ├── UI/            # HUD、Dialog、世界 UI、开场流程
│   │   ├── Localization/  # 多语言
│   │   ├── Audio/         # 场景 BGM
│   │   ├── Camera/        # 第三人称相机
│   │   ├── Game/          # 存档、暂停、Boot、输入闸
│   │   └── Core/          # GameServices、SceneSingleton、GameInput
│   ├── Resources/         # RuntimeSettings、Combat、Rouge、WorldUI、Localization…
│   ├── ScriptableObjects/ # 敌人定义、刷怪组等
│   ├── Prefabs/           # 角色、工具、UI、VFX
│   ├── Scenes/            # OpenScene / GameScene
│   └── Shaders/           # 刀光、死亡金透 / 溶解等
├── Packages/
├── ProjectSettings/
├── Output/                # 本地 PC 构建输出（gitignore）
└── .cursor/skills/        # 各玩法管线说明（给 Agent / 开发者）
```

## 关键配置入口

运行时优先走 **Resources / ScriptableObject**，禁止运行时 `AssetDatabase`。

| 资源 | 路径 / 说明 |
|------|-------------|
| `CharacterRuntimeSettings` | `Assets/Resources/CharacterRuntimeSettings.asset` |
| 探索工具目录 | `Assets/Resources/` 下 `ExplorationToolCatalog` 等 |
| 玩家技能出伤 | `Assets/Resources/Combat/` |
| 肉鸽表 | `Assets/Resources/Rouge/`（被动、等级、刷怪目录） |
| 敌人定义 | `Assets/ScriptableObjects/Enemy/` |
| 死亡材质 | `Assets/Resources/Enemy/` |
| WorldUI Prefab | `Assets/Resources/UI/WorldUI/` |
| 本地化 | `Assets/Resources/Localization/Json/` |

编辑器菜单（示例）：

- `工具/敌人/重建死亡特效材质`
- `工具/Rouge/重建肉鸽刷怪等级表`
- `工具/UI/刷新场景 UIManager 条目`
- `GameObject/AttackSkill/...`、`Assets/Create/AttackSkill/...`

更细的管线说明见 `.cursor/skills/`（总览：`attackskill-overview`）。

## 存档说明（进度 v5）

| 内容 | 说明 |
|------|------|
| 账号 / 性别 | `LocalAccountStore`（不进进度 JSON） |
| 进度文件 | 持久化目录下 `game_progress.json` |
| 含字段 | 场景、位姿、队员、HP、轮盘技能下标、肉鸽局状态 |
| 肉鸽字段 | 等级 / 经验 / 被动 / 是否已进平面 / 阵亡槽 / **倒计时剩余秒** |

- **NewGame**：清 Pending + 重置轮盘与肉鸽进度  
- **Continue**：`GameProgress` Awake 挂 Pending → 先 `ApplyRestoredEntry` 再生成，避免 intro 清场把进度 `ResetRun` 掉  

## 主要依赖（节选）

- Input System
- Cinemachine
- Timeline
- Animation Rigging
- TextMesh Pro / uGUI
- Visual Effect Graph
- 团结相关包（见 `Packages/manifest.json`）

## 如何运行

1. 安装对应版本的**团结引擎**
2. 用引擎打开本仓库根目录
3. 等待导入与包解析完成
4. 打开 `Assets/Scenes/OpenScene`（推荐）或直接进 `GameScene`
5. 进入 Play 模式试玩

本地 PC 构建产物默认输出到 `Output/`（已在 `.gitignore` 中忽略）。

## 开发约定（简）

1. 改功能先查 `.cursor/skills/attackskill-overview`，再进对应子 Skill  
2. 改数据优先动 SO / Resources，再改代码  
3. 阻塞玩法输入用 `GameplayInputGate`，勿随意改 `timeScale`  
4. 出伤统一进 `HitResolver`；世界跳字订 `HitResolver.Applied`  
5. 去重键用单位 / `EnemyAgent`，勿用共父节点的 `transform.root`

## 参考素材

本 Demo 使用或参考了以下公开资源站点（请遵守各站授权与署名要求）：

| 类型 | 来源 |
|------|------|
| 角色模型 / 场景 | [模之屋 aplaybox](https://www.aplaybox.com/) |
| 角色动作 | [Mixamo](https://www.mixamo.com/) |
| 模型 / 动作包 | [Quaternius](https://quaternius.com/) |
| 音效 | [Freesound](https://freesound.org/) |
| 引擎文档 | [Unity 文档](https://docs.unity3d.com/) |

## 状态说明

当前为**可玩 Demo / 原型**：核心战斗、探索、小队、存档、肉鸽循环与结算已接通。声骸 **F 获取**仍可能为占位提示；部分轮盘槽位为 Stub 展示。

## 许可证

未指定开源许可证时，默认保留所有权利。对外使用或开源前，请自行确认第三方模型、动作、音频、插件的授权范围，并在需要时补充 `LICENSE`。
