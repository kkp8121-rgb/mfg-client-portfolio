---
read_count: 0
last_read: "never"
status: active
---

# 3직업 가이드 퀘스트 루프 플레이테스트 — 2026-04-20

## 컨텍스트
- SHALLOW 10개 제거 (Artifact/Costume/HeroPower/Ability/Climber/ReturneeGuide/DailyChecklist/AdReward/Inscription/HotDeal) + HUD 퀵메뉴 11→5 감축 직후 검증
- QuestCatalog에서 28개 제거 시스템 참조 퀘스트의 condition 재매핑 완료

## 결과 요약 (GREEN 🟢)

| 직업 | 닉네임 | jobId | 도달 Lv | Stage | 보스 | Auto Run | 에러 |
|------|--------|-------|---------|--------|------|----------|------|
| Warrior | G_Warrior | warrior | 17 | 2-8 | - | ✅ 진행 | 0 |
| Archer | G_Archer | archer | 9 | 2-1 | - | ✅ 진행 | 0 |
| Mage | G_Mage | mage | 8 | 1-BOSS | **CLEAR** | ✅ 진행 | 0 |

3직업 모두 `mkLike/Test/Guide/Auto Run ALL` 가이드 퀘스트 일괄 시뮬레이션 정상 동작. 활성 퀘스트 90개 유지.

## HUD 감축 검증
우측 퀵메뉴 **5버튼만 표시** 확인 (정예/소탕/부스터/아레나/길드). 하단 탭바 **5탭** (소환/캐릭터/장비/스킬/무기) — Costume 탭 제거 반영.

## 직업별 확인
- **Warrior**: 근접 슬래시 공격, 흰머리 갈색 옷 (JobOutfitDatabase tier 0)
- **Archer**: 원거리 발사체 VFX (불 궤적), 빠른 이동
- **Mage**: 대형 폭발 VFX (마력탄/화염폭발), 챕터1 보스 클리어

## UI 이슈 (향후 정리)

### I1 (중요, 비블로킹)
**가이드 퀘스트 displayName 잔재**  
`guide_80` 등이 "영웅의 각성" 이름 그대로 표시됨. condition 재매핑은 완료했으나 displayName 미정리.  
- 대응: GuideQuestGenerator 재실행 또는 28개 asset의 displayName을 "레벨 N 달성/스테이지 N 클리어" 같은 중립 텍스트로 교체.

### I2 (데이터 정리)
**Quest 폴더 병존** (`Resources/Data/Quest/` + `Resources/Data/Quests/`)  
동일 SO가 두 경로에 있어 Resources.LoadAll 시 중복.  
- 대응: `Quests/` 폴더를 원천으로 정리, `Quest/` 폐기.

### I3 (개선 여지)
**가이드 위젯 공간 협소**  
스크린샷 좌상단 `가이드 #80 / 영웅의 각성 / 루비 x400 골드 x20,000` 텍스트가 배경과 어우러져 가독성 낮음.

## 콘솔 에러/경고
3회차 세션 모두 에러 0건, 경고 0건 (관찰된 범위).

## 검증 스크린샷 (Assets/Screenshots/)
- `loop_warrior_01_start.png` — Title 초기
- `loop_warrior_02_midway.png` — Lv.11, 가이드 진행 (HUD/탭바 감축 확인)
- `loop_warrior_03_continued.png` — Lv.17, Stage 2-8
- `loop_archer_01_midway.png` — Lv.9, 활 VFX
- `loop_mage_01_midway.png` — 1-BOSS CLEAR, 레벨업, 마법 VFX

## 후속
- I1 displayName 정리 (단순 텍스트 교체)
- I2 Quest/ vs Quests/ 중복 제거
- 세이브 이벤트 규약 리팩토링 (Task #15 — P0, 별도 세션)
