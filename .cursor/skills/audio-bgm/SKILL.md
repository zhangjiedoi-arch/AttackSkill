---
name: audio-bgm
description: >-
  AttackSkill 场景 BGM 与角色音效（SceneBgmPlayer、CharacterAudio、RuntimeSettings clips）。
  在改战斗场景音乐、移动/探索/战斗 SFX、循环音或切人静音时使用。
---

# 音频 / BGM

## 方案

- 场景 BGM：`SceneBgmPlayer` DDOL，监听 `sceneLoaded`，GameScene 播 SeaBGM；肉鸽传送后切 `drone` 
- 角色 SFX：`CharacterAudio`（OneShot + 独立 Loop Source）  
- Clip 多挂在 `CharacterRuntimeSettings` / Profile.segment

## 关键文件

- `Assets/Scripts/Audio/SceneBgmPlayer.cs`
- `Assets/Scripts/Character/CharacterAudio.cs`
- 资源目录：`Assets/Audio/`
- 配置：`CharacterRuntimeSettings` 音频字段；技能段 `SkillHitSegment.sfxClip`

## 数据流

```text
Progress EnsureExists → SceneBgmPlayer
→ GameScene 循环 seaBgm
→ 肉鸽传送 `RouGeLikeFlowController` → `SceneBgmPlayer.PlayRougeDrone`（drone.mp3）
角色状态 / Relay / 工具 → CharacterAudio Play*
```

## 实现步骤

1. BGM：Settings `seaBgm` / `droneBgm` 或 Inspector 指定。
2. 新场景规则：扩 `ApplyForActiveScene`。
3. 角色 clip 空：Assembler/Settings 回填路径。
4. 探索循环：进状态 PlayLoop，出 Stop（勿反复 Create AudioSource）。
5. 切人：`SuppressEnterSfx` 避免重复起飞音。
6. `ignoreListenerPause` 按需（BGM 当前跟随暂停）。
7. 技能段 SFX 写在 Profile。
8. 调各类 volume 字段；后续可再上 AudioMixer 分组。

## 约定与坑

- BGM 单例 DDOL；进战斗 Progress 会再 EnsurePlaying。
- 首次播放大文件注意 Preload / 预热，避免卡顿。
- 气流等 VFX 与 SFX 分开管，不要绑死同一生命周期硬编码。
