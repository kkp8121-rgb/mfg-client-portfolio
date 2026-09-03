---
read_count: 0
last_read: "never"
status: active
---

# SPUM 3경로 일관성 수정 — 2026-04-20

## 사용자 제보
> "SPUM 캐릭터가 도감이랑 캐릭터 창이랑 실제 몬스터나 플레이어 캐릭터랑 일치하지 않아. 프리셋을 그대로 쓰는데도. 정규화 기록이 있는데 일치 안 하는 건 명백한 버그."

## 근본 원인 (4종 확인)

| # | 위치 | 증상 |
|---|------|------|
| 1 | `SpumCharacterManager.ApplyMonsterVisual(Transform, MonsterDataSO)` | `data.spumPrefabPath` (에디터 전용 AssetDatabase 경로)만 로드. 런타임 빌드에서 Warrior fallback으로 떨어짐 |
| 2 | `MonsterSpawner.cs:306-318` | `_chapterConfig.GetRandomMonsterPrefab()`이 `MonsterDataSO.prefab`을 덮어씀. **챕터 랜덤 풀**과 **정규화 SO** 병존 → 도감과 스폰 몬스터 근본적 불일치 |
| 3 | `MonsterPortraitEditor.cs:40-66` | `spumPrefabPath` (SwordMan 등 SPUM 샘플) 우선 → 슬라임 entry가 검 캐릭터 아이콘으로 캡처됨 |
| 4 | `PortraitCaptureEditor.JOB_PREFABS` | Warrior→SwordMan, Archer→BowMan, Mage→MagicianMan **SPUM 샘플 하드코딩**. 런타임 Player(`JobOutfitDatabaseSO`)와 완전 다른 프리팹으로 캐릭터 창 초상화 캡처 |
| 5 | `CharacterPreviewRenderer.cs:152-186` | `CurrentSpumInstance` 복제 방식. Player 없으면 깨짐 |

## 수정 내용

### 정규화 원천 확정
- **직업 프리팹**: `JobOutfitDatabaseSO` (Warrior/Archer/Mage × Tier 0~4 = 15개 GUID 매핑)
- **몬스터 프리팹**: `MonsterDataSO.prefab` (GUID 참조, 4개 Monster_*.prefab)

### 코드 변경
1. **`SpumCharacterManager.cs:392-400`** — `ApplyMonsterVisual`에서 `data.prefab` GUID 우선, `spumPrefabPath`는 editor legacy fallback.
2. **`MonsterSpawner.cs:306-312`** — `_chapterConfig` 랜덤 오버라이드 제거. `_monsterData`로 단일 원천 통일.
3. **`MonsterPortraitEditor.cs:40-52`** — `data.prefab` GUID 우선 캡처.
4. **`PortraitCaptureEditor.cs:18-56`** — `SwordMan/BowMan/MagicianMan` 하드코딩 제거, `JobOutfitDatabaseSO.GetPrefab(job, 0)` 참조.
5. **`PortraitCaptureEditor.CapturePortrait`** — `ApplyEquipmentDesign` 제거 (런타임 Player와 동일 비주얼 보장).
6. **`CharacterPreviewRenderer.cs:144-174`** — `JobOutfitDatabaseSO.GetPrefab(job, tier)` 직접 조회를 주 경로로, Player 복제는 fallback.

### 에셋 재생성
- `Resources/Icons/Monsters/monster_*.png` 4종 → `MonsterDataSO.prefab` 기반 재캡처 (22:41)
- `Resources/Icons/Portraits/portrait_*.png` 3종 → `JobOutfitDatabaseSO[job][0]` 기반 재캡처 (22:51)

## 검증 스크린샷 (Assets/Screenshots/)
- `spum_fix_01_gameplay.png` — Warrior 런타임 Player (흰머리 갈색 옷, Lv.4, 전투력 735)
- `spum_fix_02_collection.png` — 도감 몬스터 탭 (3 entry)
- `spum_fix_03_character_tab.png` — 수정 전 프로필 (갑옷 전사 = SwordMan SPUM 샘플) ❌
- `spum_fix_04_after_char.png` — 수정 후 프로필 (JobOutfitDatabase tier 0 캐릭터) ✅ 소스 일치
- `spum_fix_05_player_runtime.png` — 실제 Player Lv.9 (tier 승급으로 프리팹 변경된 상태)

## Tier 승급 동적 반영 — 완료 (Option B 적용)

**수정**: `CharacterStatTabUI.RefreshInfoRow` — `CharacterPreviewRenderer.Instance.CapturePlayerPreview()` 우선, 실패 시 정적 PNG fallback.

**효과**:
- Tier 승급 시 런타임 Player 프리팹 교체 → 프로필도 자동 재캡처 → 항상 일치
- 정적 `portrait_*.png`는 fallback으로만 유지 (Renderer 미준비 시나리오)

**검증 스크린샷**:
- `spum_fix_06_main_warrior.png` — 실제 런타임 Player (흰머리 갈색 옷 + 빨간 검)
- `spum_fix_07_char_realtime.png` — 캐릭터 탭 실시간 프로필 (동일 실루엣, Lv.7 기준)

## 도감 Entry Key 노이즈
도감 스크린샷에서 "SPUM ba..." entry 관찰됨 (MonsterDataSO.id 등록 규칙과 챕터 랜덤 풀 프리팹 이름이 병존한 흔적).
→ MonsterSpawner 수정 후 새 세이브에서는 `_monsterData.id`만 등록될 것. 기존 세이브 잔재 무시해도 OK.

## 컴파일 상태
에러 0, 경고 0 (수정 영역 기준).

## 후속 TODO
1. CharacterStatTabUI 프로필을 CharacterPreviewRenderer 실시간 렌더로 교체 (Tier 승급 반영)
2. 3직업 Tier 4까지 승급 시나리오 /run 봇 테스트로 검증
3. `MonsterDataSO.spumPrefabPath` 필드 `[System.Obsolete]` 처리 (후속 청소)
4. `ChapterMonsterConfigSO.GetRandomMonsterPrefab` / `MonsterTint/Scale` 사용처 없으면 제거
