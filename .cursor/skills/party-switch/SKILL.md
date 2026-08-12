---
name: party-switch
description: >-
  AttackSkill 小队切人（PartyController、Residual、性别阵容、战斗头像 HUD）。
  在改阵容 Prefab、切人规则、头像、死亡重生或与相机/存档联动时使用。
---

# 小队切人

## 方案

鸣潮式切人：新角色立刻 Active；旧角色若在放大招则 Residual 播完再销毁。阵容按 `LocalAccountStore` 性别组装。

## 关键文件

- `Assets/Scripts/Character/PartyController.cs`
- `Assets/Scripts/Character/PartyPortraitId.cs`
- `Assets/Scripts/Character/CharacterRuntimeAssembler.cs`
- `Assets/Scripts/UI/Views/UIBattlePartyPanel.cs`
- Prefab：`CharacterRuntimeSettings` Gender Roster

## 数据流

```text
GameProgress（可 defer）→ Party 按性别组装 [漂泊者, 千咲, 柯莱塔]
→ Assembler Spawn → 绑定 ThirdPersonCamera
1/2/3 或 HUD → Residual 规则 → 相机跟 Active
```

## 实现步骤

1. 改阵容：RuntimeSettings / Inspector Gender Roster Prefab。
2. 新角色：Prefab + `CharacterAvatar` 挂点，确保 Assembler 可 Spawn。
3. 头像：`PartyPortraitId` + Settings 四个 Sprite + `UIBattlePartyPanel`。
4. 调 `switchCooldown` / `residualTimeout` / 继承坐标（无横向偏移）。
5. 死亡重生：看 solo respawn 协程与 delay。
6. 读档：对齐 `GameSaveData.activeIndex` + pending restore。
7. 实现/保持 `IPlayerTargetProvider` 供敌人索敌。
8. 切人时探索工具注意 `SuppressEnterSfx`。
9. 验证：技能中切人 Residual 不抢相机；`SnapToFollowTarget`。
10. `deferBootToGameProgress` 避免与读档抢跑。

## 约定与坑

- Battle HUD 打开时 Tab 给技能轮盘，不给切人。
- 单例：`PartyController.Instance` / `GameServices.Party`。
- 切人强制继承旧坐标，取消横向偏移。
