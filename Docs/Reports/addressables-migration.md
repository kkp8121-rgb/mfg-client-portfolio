# Addressables Migration Report

생성 시각: 2026-04-15 21:29:49
총 호출 수: 26

## 파일별 Resources.Load 호출

### client/Assets/Scripts/Combat/MonsterSpawner.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 198 | `LoadAll` | `Data/Chapters` | `_allChapterConfigs = Resources.LoadAll<ChapterMonsterConfigSO>("Data/Chapters");` |

### client/Assets/Scripts/Combat/OfflineRewardSystem.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 50 | `Load` | `Data/OfflineRewardConfig` | `_config = Resources.Load<OfflineRewardConfigSO>("Data/OfflineRewardConfig");` |

### client/Assets/Scripts/Combat/ZoneVisualManager.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 332 | `LoadAll` | `Data/Chapters` | `var configs = Resources.LoadAll<Data.ChapterMonsterConfigSO>("Data/Chapters");` |

### client/Assets/Scripts/Data/CharacterDesignLoader.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 22 | `LoadAll` | `CharacterDesigns` | `var jsonFiles = Resources.LoadAll<TextAsset>("CharacterDesigns");` |

### client/Assets/Scripts/Data/DataManager.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 70 | `LoadAll` | `Data/Characters` | `var characters = Resources.LoadAll<CharacterDataSO>("Data/Characters");` |
| 94 | `LoadAll` | `Data/Monsters` | `var monsters = Resources.LoadAll<MonsterDataSO>("Data/Monsters");` |
| 118 | `LoadAll` | `Data/Stages` | `var stages = Resources.LoadAll<StageDataSO>("Data/Stages");` |
| 137 | `LoadAll` | `Data/Skills` | `var skills = Resources.LoadAll<SkillDataSO>("Data/Skills");` |
| 158 | `LoadAll` | `Data/Equipment` | `var items = Resources.LoadAll<EquipmentDataSO>("Data/Equipment");` |
| 170 | `LoadAll` | `Data/Weapon` | `var items = Resources.LoadAll<WeaponDataSO>("Data/Weapon");` |
| 182 | `LoadAll` | `Data/Dungeon` | `var items = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");` |
| 194 | `LoadAll` | `Data/Quest` | `var items = Resources.LoadAll<QuestDataSO>("Data/Quest");` |
| 206 | `LoadAll` | `Data/Relic` | `var items = Resources.LoadAll<RelicDataSO>("Data/Relic");` |

### client/Assets/Scripts/Editor/DungeonPanelSetupEditor.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 164 | `Load` | `UI/Stone/Popup/popup_btn_close` | `var closeSpr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");` |

### client/Assets/Scripts/Equipment/EquipmentManager.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 56 | `LoadAll` | `Data/Equipment` | `_catalog = Resources.LoadAll<EquipmentDataSO>("Data/Equipment");` |

### client/Assets/Scripts/UI/CharacterStatTabUI.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 322 | `Load` | `Icons/Portraits/portrait_warrior` | `sprite = Resources.Load<Sprite>("Icons/Portraits/portrait_warrior");` |

### client/Assets/Scripts/UI/CollectionBookPanel.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 421 | `Load` | `UI/Stone/Popup/popup_btn_close` | `var closeSpr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");` |

### client/Assets/Scripts/UI/DungeonPanel.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 152 | `LoadAll` | `Data/Dungeons` | `var loaded = Resources.LoadAll<DungeonDataSO>("Data/Dungeons");` |
| 154 | `LoadAll` | `Data/Dungeon` | `loaded = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");` |
| 832 | `Load` | `UI/Stone/Popup/popup_btn_close` | `var spr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");` |

### client/Assets/Scripts/UI/DungeonPanelUI.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 211 | `LoadAll` | `Data/Dungeons` | `_dungeonCatalog = Resources.LoadAll<DungeonDataSO>("Data/Dungeons");` |
| 214 | `LoadAll` | `Data/Dungeon` | `_dungeonCatalog = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");` |

### client/Assets/Scripts/UI/HudPanel.cs

| Line | Method | Path | Snippet |
|------|--------|------|---------|
| 1033 | `Load` | `UI/Stone/Gage/gage_orange` | `fill = Resources.Load<Sprite>("UI/Stone/Gage/gage_orange");` |
| 1035 | `Load` | `UI/Stone/Gage/gage_bg` | `bg = Resources.Load<Sprite>("UI/Stone/Gage/gage_bg");` |
| 1049 | `Load` | `Icons/Stone/icon_setting` | `: Resources.Load<Sprite>("Icons/Stone/icon_setting");` |
| 1060 | `Load` | `UI/Stone/Button/btn_normal` | `btnSprite = Resources.Load<Sprite>("UI/Stone/Button/btn_normal");` |

## 추천 마이그레이션 패턴

```csharp
// Before
var clip = Resources.Load<AudioClip>("Audio/BGM/Main");

// After (동기 유지 — 호환 레이어)
var clip = ResourcesCompatLoader.Load<AudioClip>("Audio/BGM/Main");

// After (권장 — 비동기)
var handle = Addressables.LoadAssetAsync<AudioClip>("Assets/Resources/Audio/BGM/Main.mp3");
var clip = await handle.ToUniTask();
```
