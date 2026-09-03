---
read_count: 0
last_read: "never"
status: active
---

# Dead/Orphan 시스템 감사 — 2026-04-20

## 확실한 제거 대상 (참조 0건)

### 1. `PortraitCaptureEditor.ApplyEquipmentDesign()`
- `Editor/PortraitCaptureEditor.cs:73~82`
- 이번 세션에 호출부 제거. 메서드 + `LoadDesignFromJson`/`ApplyDesign` 인터페이스만 남음
- 조치: 메서드 삭제

### 2. `MonsterDataSO.spumPrefabPath`
- `Data/SO/MonsterDataSO.cs:34`
- 이번 세션 이후 `data.prefab` 단일 원천. `spumPrefabPath`는 fallback 주석만 있고 실 호출 0건
- 조치: 필드 제거 (기존 SO .asset에 값 남아 있지만 JsonUtility는 자동 무시)

## 조건부 제거 (fallback/legacy)

### 3. `CharacterDesignLoader` + `CharacterDesignData` JSON 시스템
- `Data/CharacterDesignLoader.cs`, `Data/CharacterDesignData.cs`
- 호출처 1건: `CharacterVisual.ApplyAnimationType()` (애니메이션 타입 읽기용)
- `LoadAllDesigns`, `GetItemAssetPath`, `GetBodyAssetPath` 등은 이미 명목적
- 조치: `attackAnimType`을 `MonsterDataSO.attackAnimType`으로 통합 후 JSON 삭제

### 4. `PortraitProvider` + `portrait_*.png`
- `Core/PortraitProvider.cs`
- `CharacterStatTabUI.RefreshInfoRow` fallback 1건만 사용
- Primary: `CharacterPreviewRenderer.CapturePlayerPreview()` 실시간
- 조치: Renderer 안정화 후 fallback 제거 (현재 세션에서 정상 동작 확인됨)

### 5. `ChapterMonsterConfigSO` 메서드/필드
- `Data/SO/ChapterMonsterConfigSO.cs:63-88`
- `GetMonsterPrefabs/GetBossPrefabs/GetRandomMonsterPrefab/MonsterTint/MonsterScale` — 이번 세션에 MonsterSpawner에서 제거됨
- 필드는 SO 자체에만 존재, 외부 참조 미확인
- 조치: SO 자체 폐기 검토 (Chapter별 테마 시스템 부활 시 재설계)

## 이중 관리

### 6. 정적 초상화 vs 런타임 렌더 (상기 4번과 중복)
- 단일 원천화: CharacterPreviewRenderer 유지 → PortraitProvider + PNG 3종 폐기

### 7. MonsterDataSO 스탯 vs 런타임 몬스터 배율
- SO 필드 `hp/atk/def` + MonsterSpawner의 `_miniBossHpMultiplier` 등 중복
- 조치: MonsterDataSO에 배율 구조 통합 또는 MonsterSpawner 오버라이드 축소

## 의심 (enum 슬롯 세이브 호환)

### 8. Relic enum 슬롯
- `CollectionCategory.Relic`, `ModifierSource.Relic`, `AchievementCondition.RelicCount`, `QuestCondition.RelicEquip` — 이전 세션 "세이브 호환 위해 유지"
- 실 참조 0건
- 조치: 기존 저장 파일 샘플 확인 후 제거. 현재 테스트 save에 relic 필드가 이미 없으면 즉시 제거 가능

### 9. `CharacterPanel._relicEquipSlots/_relicListRoot/_relicRowPrefab`
- SerializeField 선언만, 코드 참조 0건
- 씬(Main.unity) 직렬화 값 확인 필요 — 빈 참조면 필드 제거 OK

## 즉시 정리 가능 리스트
1. `MonsterDataSO.spumPrefabPath` 필드 삭제
2. `PortraitCaptureEditor.ApplyEquipmentDesign` 메서드 삭제 (+ LoadDesignFromJson/ApplyDesign 호출처 0이면 같이)
3. `ChapterMonsterConfigSO` 메서드군 제거 또는 SO 폐기
4. Relic enum 슬롯 4종 제거
5. `CharacterPanel` SerializeField 3종 제거
6. `PortraitProvider` + 3 PNG 제거 (fallback 폐기)
7. `CharacterDesignLoader`/`CharacterDesignData` JSON 시스템 — attackAnimType을 MonsterDataSO로 통합 후 삭제

**총 9 orphan 시스템 / 확실 2 / 조건부 4 / 이중 2 / 의심 1**

## 미확인 (Unity 씬 바인딩 필요)
- `CharacterPanel` SerializeField 실 씬 바인딩 (inspector에서 확인)
- 기존 save.json에 남은 relic 필드 여부
