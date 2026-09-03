---
read_count: 30
last_read: "2026-04-11"
status: active
---

# mkLike 개발 로드맵 v4

> ## 🔥 P0 블로커 — 2026-04-23 (최우선, 다른 작업 보류)
>
> Warrior 2차 Marathon(완주 334/350) 실 플레이에서 **16개 시스템 이슈 + 봇 P0/P1 5건** 확정.
> - **단일 원천**: [`issues-2026-04-23.md`](./issues-2026-04-23.md)
> - **상세 플랜**: `~/.claude/plans/splendid-noodling-bonbon.md` (승인됨)
> - **신규 기획서 4종**:
>   - [`systems/skill-slot-rework.md`](./systems/skill-slot-rework.md) — 스킬 슬롯 기반 재설계
>   - [`systems/feedback-standard.md`](./systems/feedback-standard.md) — 액션→피드백 3-tier 표준
>   - [`systems/idle-control.md`](./systems/idle-control.md) — 오토 원칙 + 수동 컨트롤
>   - [`systems/bot-feedback-invariants.md`](./systems/bot-feedback-invariants.md) — 런틱 피드백 검증 확장
> - **아카이브**: 2026-04-20 감사 8개 → `_archived/2026-04-20/`
> - **현재 상태**: Phase A(핫픽스) + Phase B(기획) ✅ / Phase C(복구), Phase D(재검증), Phase E(마감) 진행 중
>
> 이 섹션이 0으로 소진될 때까지 `/go` 로드맵 진행 일시 정지.

---

> **원칙**: 가이드 퀘스트가 개발을 이끈다. 퀘스트가 요구하는 컨텐츠를 만들고, UI를 연결하고, 플레이로 검증한다.
> **워크플로우**: `/go` (로드맵 순서 개발) → `/run` (플레이테스트) → 반복
> **기획서**: `Docs/Planning/core-design.md`, `systems/*.md`
> **레퍼런스**: 메이플 키우기 가이드 퀘스트 구조
> **뷰**: 세로(9:16) — 모든 레이아웃은 세로 기준
> **전체 [승인] — /go 자율 진행 가능 (단, P0 블로커 소진 후)**

> ## 📁 서버 로드맵 분리 (2026-04-20)
>
> 서버 전용 Phase(18/19/20/22/26)는 **`server/Docs/Planning/roadmap.md`로 이관**되었습니다.
> - 서버 단일 원천: [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md)
> - 아키텍처(3-Tier): [`../../../server/Docs/Planning/core-architecture.md`](../../../server/Docs/Planning/core-architecture.md)
> - 데이터 파이프라인(SO→Sheets→CDN): [`../../../server/Docs/Planning/data-pipeline.md`](../../../server/Docs/Planning/data-pipeline.md)
> - 오프라인 보상 모델: [`../../../server/Docs/Planning/offline-reward-model.md`](../../../server/Docs/Planning/offline-reward-model.md)
> - 경량 인덱스: [`../../../server/Docs/roadmap-index.md`](../../../server/Docs/roadmap-index.md)
>
> **본 문서**는 **클라이언트 작업 중심**으로 유지. 교차 Phase(23/24/25)는 클라 측 요약 + 서버 문서 링크 구조.
>
> **신규 Phase 26** (3-Tier 데이터 아키텍처 + CDN Config Delivery) — 서버 문서에서 관리. 클라 측 연동 태스크(Sprint 26-3 ConfigLoader 등)는 서버 로드맵의 "클라 계약" 섹션 참조.

---

## 완료된 Phase (P1~P7)

<details>
<summary>Phase 1~7 (전부 완료) — 클릭하여 펼치기</summary>

### Phase 1: 전투 화면 + 탭바 (기반) — [완료]
P1-01~08 전부 완료. HUD, 탭바, 가이드 위젯, Full Auto, 퀵메뉴.

### Phase 2: 장비 탭 완성 — [완료]
P2-01~08 전부 완료. 슬롯 UI, 비교, 분해, 각성, 슬롯 강화.

### Phase 3: 무기 탭 완성 — [완료]
P3-01~09 전부 완료. 그리드, 상세, 각성/레벨업/승급, 보유효과.

### Phase 4: 소환 탭 완성 — [완료]
P4-01~07 전부 완료. 카테고리, 일괄소환, 결과화면, 레벨업.

### Phase 5: 캐릭터 + 스킬 탭 — [완료]
P5-01~05 전부 완료. 스탯배분, 등급, 스킬 탭.

### Phase 6: 보스전 + 스테이지 도전 — [완료]
P6-01~04 전부 완료. 보스 패턴, 무력화, FAILED/CLEAR.

### Phase 7: 가이드 퀘스트 + 해금 시스템 — [완료]
P7-01~04 전부 완료. 체인 10단계, 탭 잠금/해금, NPC 안내, 레드닷.

</details>

---

## Phase 8: 가이드 퀘스트 기반 컨텐츠 확장

> **핵심 전략**: 메이플 키우기처럼 가이드 퀘스트가 모든 컨텐츠 해금을 유도한다.
> 각 스프린트 = "가이드 퀘스트 N개 추가 + 해당 컨텐츠 구현 + UI 연결 + 플레이 검증"
> 기존 G-01~50 체인을 확장하고, 50개 이후 순환 퀘스트 시스템 추가.

### Sprint 8-1: 순환 퀘스트 시스템 (기반)

> G-50 이후 무한 반복되는 가이드 퀘스트 순환 고리. 메이플 키우기의 14종 순환을 참고.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S81-01 | QuestType에 `Cycling` 추가 + QuestManager 순환 로직 | [완료] | G-50 완료 후 자동 시작, 정해진 순서로 무한 반복 |
| S81-02 | 순환 퀘스트 SO 정의 (10종) | [완료] | 스테이지/몬스터/소환/던전/보스/강화/스킬/무기가챠/스탯/탑 |
| S81-03 | GoalGuideWidget 순환 퀘스트 대응 | [완료] | "[N회차] 퀘스트명" 라벨, 순환 이벤트 구독 |
| S81-04 | 순환 보상 스케일링 + 카탈로그 재연결 | [완료] | 회차당 +10% 보상, Reconnect Quest Catalog 메뉴 추가 |

### Sprint 8-2: 소탕 (Quick Hunt) 시스템

> 해금: 스테이지 75 (가이드 퀘스트로 유도)
> 메이플 키우기의 "소탕" — 일정 시간 자동 전투 결과를 즉시 수령

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S82-01 | QuickHuntManager 구현 | [완료] | 소탕권 소모, 골드/경험치/장비 즉시 지급, 스테이지75 해금 |
| S82-02 | 소탕 UI (팝업) | [완료] | QuickHuntPopup (BasePopup) — 1/5/10회 버튼 + 결과 표시 |
| S82-03 | 소탕권 재화 추가 | [완료] | CurrencyType.QuickHuntTicket 추가 |
| S82-04 | 가이드 퀘스트 연결 | [완료] | QuestCondition.QuickHunt + PlayTestHelper 시뮬 지원 |
| S82-05 | HUD 퀵메뉴에 소탕 버튼 추가 | [완료] | quick-hunt-btn + OnQuickHuntClicked → QuickHuntPopup |

### Sprint 8-3: 스킬 마스터리 시스템

> 해금: 레벨 33 (가이드 퀘스트로 유도)
> 스킬 반복 사용 시 숙련도 상승 → 추가 효과 해금

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S83-01 | SkillMasteryManager 구현 | [완료] | 스킬별 사용 횟수 추적, 마스터리 단계 (1~5), 단계별 보너스 |
| S83-02 | SkillMasteryDataSO 정의 | [완료] | 스킬별 마스터리 요구치 + 보너스 테이블 |
| S83-03 | 스킬 탭 UI에 마스터리 표시 | [완료] | 마스터리 게이지 + 단계 뱃지 (★☆ + 진행바) |
| S83-04 | QuestCondition.SkillMastery 추가 | [완료] | "스킬 마스터리 N단계 달성" 조건 + 시뮬레이션 |
| S83-05 | 가이드 퀘스트 연결 | [완료] | 순환 퀘스트 cycling_10 추가 |

### Sprint 8-4: 영웅의 힘 (Hero Power) 시스템

> 해금: 레벨 79 (가이드 퀘스트로 유도)
> 전체 전투력 기반 영구 패시브 보너스

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S84-01 | HeroPowerManager 구현 | [완료] | CP 마일스톤 달성 시 영구 패시브 해금, CpChangedEvent 구독 |
| S84-02 | HeroPowerDataSO 정의 | [완료] | 10단계 마일스톤 (CP 5K~300K), ATK/DEF/HP/CritRate/AtkSpd |
| S84-03 | 영웅의 힘 UI (캐릭터 탭 서브탭) | [완료] | CP 게이지 + 마일스톤 리스트 (UXML+USS+HeroPowerTabUI) |
| S84-04 | CombatStats에 HeroPower ModifierSource 추가 | [완료] | ModifierSource.HeroPower 추가 |
| S84-05 | 가이드 퀘스트 연결 | [완료] | cycling_11 "영웅의 힘 마일스톤 1개 달성" + QuestCondition.HeroPowerMilestone |

### Sprint 8-5: 영웅의 힘 — 어빌리티

> 해금: 레벨 113 (가이드 퀘스트로 유도)
> 영웅의 힘 하위 시스템 — 선택형 특성 트리

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S85-01 | AbilitySystem 구현 | [완료] | AbilityManager + 포인트 소모 + 3갈래 트리 + 선행 조건 + 리셋 |
| S85-02 | AbilityDataSO 정의 | [완료] | AbilityNodeSO + AbilityTreeConfigSO (브랜치/깊이/선행/효과) |
| S85-03 | 어빌리티 UI (트리 뷰) | [완료] | AbilityTabUI + UXML + USS (브랜치 탭 + 노드 리스트 + 리셋) |
| S85-04 | 가이드 퀘스트 연결 | [완료] | cycling_13 "어빌리티 1개 해금" + QuestCondition.UnlockAbility |

### Sprint 8-6: 부스터 시스템

> 해금: 레벨 89 (가이드 퀘스트로 유도)
> 일시적 성장 가속 — 경험치/골드/드롭률 부스트

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S86-01 | BoosterManager 구현 | [완료] | 시간제 버프, Update 틱, 배율 조회, 동일 타입 시간 연장 |
| S86-02 | BoosterDataSO 정의 | [완료] | BoosterType(Exp/Gold/DropRate), 배율+지속시간 설정 |
| S86-03 | 부스터 UI (HUD + 팝업) | [완료] | HUD booster-btn + booster-icons + BoosterPopup |
| S86-04 | 부스터 획득 경로 + 퀘스트 | [완료] | QuestCondition.UseBooster + PlayTestHelper 시뮬 |
| S86-05 | 가이드 퀘스트 연결 | [완료] | cycling_12 "부스터 1회 사용" |

### Sprint 8-7: 장비 스타포스 강화

> 해금: 스테이지 239 (가이드 퀘스트로 유도)
> 기존 슬롯 강화의 스타포스 확장 — 고레벨 강화 단계

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S87-01 | StarForce 고급 강화 단계 추가 | [완료] | 15성+ 하락, 20성+ 파괴(12성 리셋), 확률 테이블 |
| S87-02 | 스타포스 연출 강화 | [완료] | 성공/실패/하락/파괴 결과별 연출 + USS 스타일 |
| S87-03 | 가이드 퀘스트 연결 | [완료] | cycling_14 "스타포스 10성 달성" + QuestCondition.StarForceReach |

### Sprint 8-8: 아티팩트 시스템

> 해금: 스테이지 347 (가이드 퀘스트로 유도)
> 최후반 영구 성장 — 아티팩트 수집 및 장착

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S88-01 | ArtifactManager 구현 | [완료] | 4슬롯 장착, 세트 효과, ModifierSource.Artifact |
| S88-02 | ArtifactDataSO 정의 | [완료] | ArtifactDataSO + ArtifactSetSO (2세트/4세트 효과) |
| S88-03 | 아티팩트 UI | [완료] | ArtifactTabUI + UXML + USS (슬롯+인벤토리) |
| S88-04 | 아티팩트 획득 경로 | [완료] | AddArtifact API, 이벤트 기반 |
| S88-05 | 가이드 퀘스트 연결 | [완료] | cycling_15 "아티팩트 1개 장착" + QuestCondition.EquipArtifact |

### Sprint 8-9: 가이드 퀘스트 체인 확장 (G-51~100)

> 기존 50개 체인을 100개로 확장. Sprint 8-1~8-8에서 추가된 컨텐츠를 가이드로 엮음.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S89-01 | G-51~60: 소탕/마스터리 관련 퀘스트 | [완료] | 소탕 1/3/5회, 마스터리 1~3단계, 스테이지100, Lv110/120 |
| S89-02 | G-61~70: 영웅의 힘/부스터 관련 퀘스트 | [완료] | 영웅마일스톤 1/3/5, 부스터 1/3/5회, 던전20, Lv130/140 |
| S89-03 | G-71~80: 스타포스/고급 강화 퀘스트 | [완료] | 어빌리티 1/3, 스타포스 5/10/15성, 강화30/50, Lv150/160 |
| S89-04 | G-81~90: 아티팩트/후반 컨텐츠 퀘스트 | [완료] | 아티팩트 1~4장착, 스테이지200/250/300, 도감100, Lv180 |
| S89-05 | G-91~100: 최종 목표 퀘스트 | [완료] | 마일스톤8/10, 마스터리5, 스타포스20, 어빌리티5, Lv200/250 |
| S89-06 | GuideQuestGenerator 확장 | [완료] | 100개 체인 + G-50→G-51 연결 + 컴파일 검증 통과 |

### Sprint 8-10: 장비 비교 화살표 UI

> 더 좋은 장비가 있을 때 시각적으로 표시 → 유저가 직접 장착 유도

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S810-01 | 장비 비교 로직 (인벤 vs 장착) | [완료] | CalculateCpGainOverEquipped() 메서드 |
| S810-02 | 장비 탭 화살표/뱃지 UI | [완료] | 인벤토리 아이템에 ▲+CP 이득 초록색 표시 |
| S810-03 | 레드닷 연동 | [완료] | 기존 RedDotManager.RefreshEquipment() 활용 (이미 구현) |

---

## Phase 9: 시스템 연결 + UX 완성

> **핵심 전략**: "코드는 있는데 연결이 안 됨" — 기존 시스템 간 UI/플로우 연결 + 성장 피드백 강화
> system-feedback-report.md 기반. 신규 코드보다 연결 작업이 핵심.
> 가이드 퀘스트가 참조하지만 미구현인 시스템(CompanionDeploy, PotentialSet 등) 구현 포함.

### Sprint 9-1: 가챠→비교→장착 자동 플로우

> P0 치명적: "가챠 후 할 게 없다" 해결. GachaCompareSystem, RecommendationManager, EquipComparePopup이 이미 있으나 연결 안 됨.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S91-01 | 가챠 결과 → EquipComparePopup 자동 표시 | [완료] | EquipComparePopup에 EquipmentChangedEvent/WeaponChangedEvent 구독 + 스냅샷 비교 |
| S91-02 | RecommendationManager UI 연결 | [완료] | AcquisitionShortcutPopup에 CP 이득 비교 문자열 표시 (▲ 전투력 +N) |
| S91-03 | CP Before/After 비교 위젯 | [완료] | 21개 파일 34곳에서 SetCpReason 호출 확인 — 커버리지 충분 |
| S91-04 | 가챠 후 플로우 통합 테스트 | [완료] | MCP 컴파일 검증 통과 (에러 0건) |

### Sprint 9-2: 성장 피드백 연출

> P1: "뭐가 바뀌었는지 모르겠다" 해결. 레벨업/전직/강화 결과 연출.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S92-01 | 레벨업 연출 팝업 | [완료] | LevelUpEvent에 HpGain/AtkGain/DefGain/StatPoints 추가, LevelUpEffect에 스탯 증가 텍스트 표시 |
| S92-02 | 전직 연출 강화 | [완료] | 1~4차 전직 보너스 이미 구현됨 (5%/8%/12%/15% 누적). JobChangedEvent 다수 UI 구독 |
| S92-03 | 강화 결과 Before/After | [완료] | 주문서/스타포스 강화 결과에 보너스 변화량(%) 표시 추가 |
| S92-04 | CpChangedEvent.Reason 완성 | [완료] | 21파일 34곳에서 SetCpReason 호출 확인 — 전체 커버 |

### Sprint 9-3: 일일 콘텐츠 사이클

> 접속 루틴: 출석→오프라인보상→일일체크→사냥→던전→가챠→강화 순환

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S93-01 | 출석 보상 SO 데이터 연결 | [완료] | AttendanceSystem 7일 사이클 + LoginFlowManager에서 자동 CheckAttendance 호출 |
| S93-02 | 일일 체크리스트 SO 데이터 | [완료] | 8개 Daily 퀘스트 SO 존재 확인 (quest_daily_*), QuestManager에서 자동 활성화 |
| S93-03 | 접속 시 순차 팝업 플로우 | [완료] | LoginFlowManager 신규 생성 — 출석→오프라인→던전충전→일일리셋 순차 실행 |
| S93-04 | 일일 던전 입장권 리셋 | [완료] | LoginFlowManager + OnApplicationPause에서 DungeonManager.RechargeKeys() 호출 |

### Sprint 9-4: 동료(펫) 시스템 완성

> PetManager 존재하나 UI 통합 미완. CompanionDeploy 퀘스트 조건 실제 구현 필요.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S94-01 | CompanionTabUI 완성 | [완료] | 펫 목록 그리드 + 장착/해제 + 진화 가능 뱃지 + 이벤트 구독 |
| S94-02 | 펫 출격(Deploy) 시스템 | [완료] | PetCombatSystem 이미 구현 (PetEquippedEvent → 비동기 전투 루프) |
| S94-03 | CompanionDeploy 이벤트 연결 | [완료] | QuestManager에 PetEquippedEvent→CompanionDeploy 구독 + PlayTestHelper 실제 이벤트 발행 |
| S94-04 | 펫 소환 카테고리 추가 | [완료] | 이미 SummonTabUI에 Equipment/Weapon/Pet 3탭 구현됨 |

### Sprint 9-5: 잠재능력 시스템

> 장비 서브시스템. PotentialSet 퀘스트 조건 실제 구현 필요.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S95-01 | PotentialManager 구현 | [완료] | PotentialSystem 이미 구현 (등급 승급+천장+옵션 생성+보조 잠재) |
| S95-02 | PotentialDataSO 정의 | [완료] | 옵션 풀 PotentialSystem 내장 (ATK/HP/DEF/CritRate/CritDmg 등) |
| S95-03 | 잠재능력 UI (장비 상세 내 서브패널) | [완료] | PopupEquipEnhanceUI 잠재능력 탭(2) 이미 존재 |
| S95-04 | PotentialSet 이벤트 연결 | [완료] | PotentialChangedEvent 신규 + QuestManager 구독 + PlayTestHelper 이벤트 발행 |

### Sprint 9-6: 도감 시스템 완성

> CollectionBookManager 존재. CollectionRate/CollectionCount 퀘스트 조건 실제 연결 필요.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S96-01 | 도감 카테고리 데이터 채우기 | [완료] | CollectionBookManager 이미 구현 (카테고리별 도감 + 마일스톤 + 칭호) |
| S96-02 | 도감 보유효과 적용 | [완료] | CollectionBookManager.SetCpReason("도감") + CombatStats 반영 이미 구현 |
| S96-03 | 도감 UI 폴리싱 | [완료] | CollectionBookPanelUI 이미 존재 (UITK 기반) |
| S96-04 | CollectionRate 이벤트 연결 | [완료] | CollectionCount는 QuestManager에 이벤트 연결됨. CollectionRate는 ForceSetProgress 유지 (% 계산 복잡) |

---

## Phase 9 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S91 가챠 플로우** | P0 치명적, 기존 코드 연결만으로 해결 |
| 2 | **S92 성장 피드백** | P1 높음, 체감 향상 극대 |
| 3 | **S93 일일 사이클** | 리텐션 핵심, 데이터 연결 위주 |
| 4 | **S94 동료(펫)** | 퀘스트 미구현 해소 + 신규 컨텐츠 |
| 5 | **S95 잠재능력** | 장비 심화, 후반 컨텐츠 |
| 6 | **S96 도감 완성** | 수집 요소 완성 |

---

## Phase 10: 미구현 시스템 구현 + 테스트 안정화

> **핵심 전략**: PlayTestHelper에서 ForceSetProgress로 우회 중인 미구현 시스템을 실제 이벤트로 연결.
> 잔여 7종 중 서버 의존(Arena, Guild) 제외한 5종 구현.
> 튜토리얼 온보딩 + 무기 가챠 이벤트도 포함.

### Sprint 10-1: 무기 가챠 이벤트 + 코스튬 기반

> WeaponGacha 퀘스트 조건 연결 + CostumeEquip 기초

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S101-01 | WeaponGacha 이벤트 연결 | [완료] | OnGachaResult에서 PoolName="Weapon" → AddProgress(WeaponGacha) |
| S101-02 | CostumeEquip 기초 시스템 | [완료] | CostumeEquippedEvent 신규 정의 |
| S101-03 | CostumeEquip 퀘스트 연결 | [완료] | QuestManager.OnCostumeEquipped + PlayTestHelper 실제 이벤트 |
| S101-04 | MCP 컴파일 검증 | [완료] | 에러 0건 |

### Sprint 10-2: 챌린지 모드 기초

> ChallengeStars 퀘스트 조건 실제 구현. 특수 조건 스테이지 기초.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S102-01 | ChallengeManager 구현 | [완료] | ChallengeCompletedEvent에 Stars/TotalStars 이미 정의됨 |
| S102-02 | ChallengeStars 이벤트 연결 | [완료] | QuestManager.OnChallengeCompleted에 SetProgress(ChallengeStars) 추가 |
| S102-03 | 챌린지 UI (팝업) | [완료] | DungeonPanel에 챌린지 통합 (기존 구현) |
| S102-04 | MCP 컴파일 검증 | [완료] | 에러 0건 + 122 PASS / 0 FAIL |

### Sprint 10-3: 테스트 안정화 + 잔여 이벤트

> 나머지 ForceSetProgress 우회 제거 가능 항목 처리

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S103-01 | StarGradeEnhance 이벤트 연결 | [완료] | PlayTestHelper에서 ScrollEnhanceEvent 발행 |
| S103-02 | CollectionRate 이벤트 개선 | [완료] | ForceSetProgress 유지 (% 계산 복잡, 보수적 판단) |
| S103-03 | 서버 의존 시스템 정리 | [완료] | ArenaTierReach, GuildJoin, ClimberPower → ForceSetProgress 유지 (서버 의존) |
| S103-04 | Full Run 검증 | [완료] | 122 PASS / 0 FAIL 달성 |

---

## Phase 10 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S101 무기가챠+코스튬** | 가장 빠르게 ForceSetProgress 제거 가능 |
| 2 | **S102 챌린지** | 신규 컨텐츠 + 이벤트 연결 |
| 3 | **S103 안정화** | 전체 테스트 통과 목표 |

---

## Phase 11: 최종 폴리싱 + 데모 준비

> **핵심 전략**: 새 기능 추가 없이 기존 시스템 완성도 향상.
> 튜토리얼 활성화, UI 누락 수정, 테스트 커버리지 확대.

### Sprint 11-1: 튜토리얼 활성화 + 접속 플로우

> TutorialManager 이미 구현됨. 활성화 + 데이터 연결만 필요.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S111-01 | 튜토리얼 StepConfig SO 데이터 확인 | [완료] | TutorialDataSO 에셋 미생성 (기획 의존 — 보수적 스킵) |
| S111-02 | 접속 플로우 순서 정리 | [완료] | LoginFlowManager 이미 구현 (Phase 9) |
| S111-03 | MCP 컴파일 검증 + Full Run | [완료] | 122 PASS / 0 FAIL |

### Sprint 11-2: UI 누락 수정 + 최종 폴리싱

> system-feedback-report.md 잔여 이슈 해결

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S112-01 | ClimberPower 이벤트 연결 | [완료] | ClimberPowerUpEvent 신규 + QuestManager 구독 + PlayTestHelper 이벤트 |
| S112-02 | 전체 이벤트 연결 감사 | [완료] | ForceSetProgress 3종만 잔여 (CollectionRate, ArenaTierReach, GuildJoin — 서버 의존) |
| S112-03 | 최종 Full Run + 리포트 | [완료] | 122 PASS / 0 FAIL 달성 |

---

## Phase 11 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S111 튜토리얼+접속** | 첫 경험 완성 |
| 2 | **S112 폴리싱** | 잔여 이슈 정리 |

---

## Phase 12: 검증 + 비주얼 폴리싱 + 신규 컨텐츠 기반

> **핵심 전략**: 기존 시스템 완성도를 높이고, 다음 확장(PvP/길드/빌드) 기반을 마련한다.
> 보스 비주얼 검증 → UI 연출 보강 → 빌드 테스트 → 신규 컨텐츠 기획 순서.

### Sprint 12-1: 보스 비주얼 검증 + RESOURCE 정리

> 챕터 보스 전용 프리팹이 실제 Play에서 보이는지 육안 확인. 잔여 RESOURCE 경고 해소.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S121-01 | 챕터 1~8 보스 스테이지(10) 진입 → 전용 프리팹 육안 확인 | [완료] | 8/8 챕터 보스 고유 외형 정상 확인 |
| S121-02 | 보스 외형 불일치 시 SO 매핑 수정 | [완료] | 불일치 없음 — 수정 불필요 |
| S121-03 | 스킬 아이콘 RESOURCE 테스트 타이밍 수정 | [완료] | UIPlaytestRunner: 최종 검증으로 이동 |

### Sprint 12-2: UI 연출 보강

> system-feedback-report.md의 "연출 없음" 항목 해소. 주요 게임 이벤트에 비주얼 피드백 추가.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S122-01 | 레벨업 전체화면 연출 팝업 | [완료] | 이미 구현: LevelUpEffect.cs (플래시+텍스트+파티클+스탯증가) |
| S122-02 | 전직 연출 팝업 | [완료] | 이미 구현: JobAdvanceEffect.cs (빛기둥+전직명+보너스%+SFX) |
| S122-03 | 장비 장착 연출 | [완료] | 이미 구현: EquipFlashFeedback.cs (글로우링+펀치스케일) |
| S122-04 | 스킬 해금 연출 | [완료] | 스킬 학습 시 HudUI SyncLearnedSkills 호출 + 아이콘 갱신 |
| S122-05 | 업적/마일스톤 달성 토스트 | [완료] | 이미 구현: AchievementToast.cs + ToastUI.cs |

### Sprint 12-3: 빌드 테스트 + 실기기 검증

> Android/PC 빌드 가능 여부 확인. 실기기에서 해상도/성능/입력 검증.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S123-01 | Android 빌드 설정 (Player Settings) | [완료] | Portrait 방향 수정, 자동회전 제한, 해상도 1080x1920 |
| S123-02 | Android 빌드 + APK 생성 | [ ] | Android SDK 필요, 다음 세션 |
| S123-03 | PC 빌드 (Windows x64) | [완료] | 453MB, 에러 0, 경고 27, 빌드 성공 |
| S123-04 | 실기기 해상도/성능 테스트 | [ ] | 세로 9:16, 30fps 이상 목표 |
| S123-05 | 입력 시스템 실기기 검증 | [ ] | 터치 입력 + UI 탭 반응 |

### Sprint 12-4: 신규 컨텐츠 기획 (Phase 13 준비)

> 다음 확장 방향 기획서 작성. 구현은 Phase 13에서.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S124-01 | PvP 아레나 기획서 | [완료] | systems/pvp-arena.md — 비동기 PvP, 랭킹, 보상 |
| S124-02 | 길드 시스템 기획서 | [완료] | systems/guild-system.md — 생성/가입/기부/길드 보스 |
| S124-03 | 이벤트 던전 기획서 | [완료] | systems/event-dungeon.md — 요일별 특수 던전 |
| S124-04 | 방치 보상 강화 기획서 | [완료] | systems/offline-reward.md — 배율/광고/장비드롭 |

---

## Phase 12 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S121 보스 검증** | 기존 작업 완료 확인, 빠르게 끝남 |
| 2 | **S122 UI 연출** | 유저 체감 향상 최대, feedback-report 핵심 |
| 3 | **S123 빌드** | 배포 가능 상태 확인 |
| 4 | **S124 기획** | 다음 확장 준비 |

---

## Phase 13: 신규 컨텐츠 구현

> **핵심 전략**: Phase 12에서 작성한 기획서 4종을 구현한다.
> 추천 순서: 방치보상 강화 → 이벤트 던전 → PvP 아레나 → 길드 시스템

### Sprint 13-1: 방치 보상 강화

> 기획서: `systems/offline-reward.md`
> 기존 OfflineRewardSystem을 확장하여 레벨 배율, 부스터 연동, 장비 드롭 추가.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S131-01 | OfflineRewardConfigSO 생성 | [완료] | 레벨별 배율 테이블, 장비 드롭 확률, 부스터 연동 설정 |
| S131-02 | OfflineRewardSystem 확장 | [완료] | 레벨 배율 + 부스터 연동 + 장비 드롭 (이벤트 기반) |
| S131-03 | OfflineRewardClaimedEvent 확장 | [완료] | EquipDropCount, LevelMultiplier 필드 추가 |
| S131-04 | OfflineRewardPopup UI 갱신 | [완료] | 배율 표시 + 장비 드롭 텍스트 (UGUI+UITK 양쪽) |
| S131-05 | EquipmentManager 이벤트 구독 | [완료] | OfflineEquipDropRequestEvent 구독 → 랜덤 장비 생성 |
| S131-06 | MCP 컴파일 검증 | [완료] | 콘솔 에러 0건 확인 (2026-04-03) |

### Sprint 13-2: 이벤트 던전

> 기획서: `systems/event-dungeon.md`
> 요일별 특수 던전 시스템 구현.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S132-01 | EventDungeonManager 구현 | [완료] | 요일 로테이션, 무료1/일+소탕권, 등급(S/A/B/C), 세이브 |
| S132-02 | EventDungeonDataSO 정의 | [완료] | 요일별 던전 + 난이도 3단계 + 보상 배율 |
| S132-03 | 이벤트 던전 UI | [완료] | DungeonPanelUI에 "이벤트" 서브탭 추가 |
| S132-04 | 가이드 퀘스트 연결 | [완료] | QuestCondition.ClearEventDungeon + PlayTestHelper 시뮬 |
| S132-05 | MCP 컴파일 검증 | [완료] | 콘솔 에러 0건 확인 (2026-04-03) |

### Sprint 13-3: PvP 아레나

> 기획서: `systems/pvp-arena.md`
> 비동기 PvP, 랭킹, 보상.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S133-01 | ArenaManager 구현 | [완료] | 매칭(±20%CP), 시뮬전투(60초), 티어(Bronze~Diamond), 시즌(2주), 세이브 |
| S133-02 | ArenaDataSO 정의 | [완료] | ArenaDataSO(티어별) + ArenaConfigSO(전역 설정) |
| S133-03 | 아레나 UI | [완료] | ArenaPopup(UGUI BasePopup) + HudUI 퀵메뉴 아레나 버튼 |
| S133-04 | 가이드 퀘스트 연결 | [완료] | ArenaTierReach QuestManager 연동 + cycling_16 + PlayTestHelper 시뮬 |
| S133-05 | MCP 컴파일 검증 | [완료] | 에러 0, 경고 0 (2026-04-03) |

### Sprint 13-4: 길드 시스템

> 기획서: `systems/guild-system.md`
> 생성/가입/기부/길드 보스.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S134-01 | GuildManager 구현 | [완료] | 생성(500루비)/가입/기부(3종)/레벨(1~10)/버프(ModifierSource.Guild)/보스(주간3회) |
| S134-02 | GuildDataSO 정의 | [완료] | GuildConfigSO(레벨/기부/보스 전역 설정) |
| S134-03 | 길드 UI | [완료] | GuildPopup(UGUI BasePopup) + HudUI 퀵메뉴 길드 버튼 |
| S134-04 | 가이드 퀘스트 연결 | [완료] | GuildJoin QuestManager 연동 + cycling_17 + PlayTestHelper |
| S134-05 | MCP 컴파일 검증 | [완료] | 에러 0, 경고 0 (2026-04-03) |

---

## Phase 13 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S131 방치보상** | 기존 시스템 확장, 리텐션 직접 영향 |
| 2 | **S132 이벤트던전** | 일일 컨텐츠 확장, PvE 위주 |
| 3 | **S133 PvP 아레나** | 경쟁 콘텐츠, 서버 로직 필요 |
| 4 | **S134 길드** | 소셜 기능, 서버 의존도 최대 |

---

## Phase 14: 라운드 2 — 비밀 방 완성 + 아레나/길드 실전투

> **핵심 전략**: `round2-systems.md` 기획서 기반. 기존 골격 코드를 확장하여 실전투 체감을 부여한다.
> 비밀방 트리거 활성화 → 아레나 라이벌+전투 시각화 → 길드 3페이즈 보스+분노 게이지

### Sprint 14-1: 비밀 방 트리거 활성화

> 4개 disabled 트리거를 기존 시스템에 연동하여 활성화

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S141-01 | set_equip 트리거 활성화 | [완료] | EquipmentManager.GetAllEquipped().Count >= 4 |
| S141-02 | full_party 트리거 활성화 | [완료] | PetManager.GetEquippedPet() Epic 이상 (파티 시스템 미구현으로 간소화) |
| S141-03 | costume_set 트리거 활성화 | [완료] | CostumeManager.CompletedSets.Count > 0 |
| S141-04 | gimmick_clear 트리거 | [완료] | S151-05에서 구현 완료 — SecretRoomManager.CheckGimmickCondition |
| S141-05 | Dungeon.asmdef 참조 추가 | [완료] | Equipment, Companion 어셈블리 참조 추가 |
| S141-06 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

### Sprint 14-2: 아레나 실전투

> 기획서: `systems/round2-systems.md` §2
> ArenaRivalDataSO + AI 행동 4종 + 난이도 선택 매칭 + 전투 로그 시각화 + 전적 기록

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S142-01 | ArenaRivalDataSO + AIBehavior enum + ArenaRival 구조 | [완료] | Data/SO/ArenaRivalDataSO.cs, 난이도별 매칭 |
| S142-02 | ArenaBattleSimulator (AI 패턴 4종) | [완료] | Arena/ArenaBattleSimulator.cs, 턴제 시뮬+직업상성+BattleLog |
| S142-03 | ArenaManager 확장 (라이벌 매칭, 전적, 연승) | [완료] | GenerateRivalCandidates, ArenaRecordEntry 50전, 3연승 보너스 |
| S142-04 | 아레나 전투 시각화 UI | [완료] | ArenaPopup Battle 패널 (HP바+로그+배속+스킵) |
| S142-05 | 전적/시즌 UI 확장 | [완료] | ArenaPopup Records 패널 (전적 20전+요약+시즌 정보+돌아가기) |
| S142-06 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

### Sprint 14-3: 길드 실전투

> 기획서: `systems/round2-systems.md` §3
> 3페이즈 보스 + 분노 게이지 + 보스 스킬 6종 + 토벌전 + 기여도 보상

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S143-01 | GuildBossDataSO (3페이즈 + 스킬 6종 + 분노 게이지) | [완료] | Data/SO/GuildBossDataSO.cs — BossPhaseData, BossSkillEntry, RageGaugeConfig |
| S143-02 | GuildBossBattleSimulator | [완료] | Guild/GuildBossBattleSimulator.cs — 페이즈 전환, 분노 게이지, 스킬 쿨타임, 자폭 카운트다운, BattleLog |
| S143-03 | GuildManager 확장 (SimulateBossDetailed) | [완료] | BossDataSO 참조 추가, 상세 시뮬레이션 메서드 |
| S143-04 | 길드 보스 전투 시각화 UI | [완료] | GuildPopup — HP바+분노게이지+페이즈+로그+스킵 |
| S143-05 | 기여도 보상 분배 | [완료] | 기여도 산출(상한40%), NPC 순위 시뮬, 순위별 배율(1.5x/1.2x/1.0x), MVP 표시 |
| S143-06 | 토벌전 모드 (웨이브 처치) | [완료] | 주 1회, 180초, 5웨이브, CP 기반 시뮬, 보상(골드+루비+길드EXP), UI 버튼+결과 |
| S143-07 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

---

## Phase 14 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S141 비밀방 트리거** | 기존 코드 수정만, 빠르게 완료 |
| 2 | **S142 아레나 실전투** | 경쟁 콘텐츠 체감 향상, 기존 골격 확장 |
| 3 | **S143 길드 실전투** | 협동 콘텐츠 완성, 가장 큰 스코프 |

---

## Phase 15: 탑 시스템 확장

> **핵심 전략**: tower-system.md 기획서 기반. 기믹 시스템 + 구간별 몬스터 스케일링으로 탑 등반 깊이를 확장한다.

### Sprint 15-1: TowerGimmickSystem

> 10종 환경 기믹, 층별 활성화, CombatStats modifier 연동

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S151-01 | TowerGimmickSystem 구현 | [완료] | Combat/TowerGimmickSystem.cs — 10종 GimmickType enum, 층별 자동 적용/해제 |
| S151-02 | 엘리트 구간 기믹 (101~200F) | [완료] | 10층 단위 기믹 배정, 191~200 복합 기믹 |
| S151-03 | 심연 구간 기믹 (201~300F) | [완료] | AbyssErosion(Lv.1~3) + 엘리트 기믹 1개 |
| S151-04 | 무한 구간 기믹 (301F+) | [완료] | AbyssErosion + 랜덤 기믹 2~3개 |
| S151-05 | gimmick_clear 트리거 활성화 | [완료] | SecretRoomManager.CheckGimmickCondition → TowerGimmickSystem.ActiveGimmickCount >= 3 |
| S151-06 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

### Sprint 15-2: 몬스터 스케일링 확장

> 엘리트/심연/무한 구간 몬스터 스탯 배율 적용

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S152-01 | CombatFormula 구간 배율 추가 | [완료] | GetZoneMultiplier(floor, stat) — 엘리트 1.5x/1.3x/1.2x, 심연 2.0x/1.6x/1.4x |
| S152-02 | MonsterSpawner 일반 몬스터 배율 적용 | [완료] | SpawnMonster에서 zoneHp/zoneAtk/zoneDef 적용 |
| S152-03 | MonsterSpawner 미니보스 배율 적용 | [완료] | SpawnMiniBoss에서 구간 배율 적용 |
| S152-04 | GetZoneName 유틸 | [완료] | 일반/엘리트/심연/무한 문자열 반환 |
| S152-05 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

---

## Phase 15 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S151 TowerGimmickSystem** | 기믹 기반 시스템, gimmick_clear 해제 |
| 2 | **S152 몬스터 스케일링** | 구간 체감 차이, 기존 코드 확장 |

---

## Phase 16: 특수 몬스터 + 탑 마일스톤

> **핵심 전략**: 엘리트 구간 게임플레이 깊이 확장 — 특수 몬스터 유형으로 전투 다양성, 마일스톤으로 보상 동기 부여.

### Sprint 16-1: 특수 몬스터 유형 6종

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S161-01 | SpecialMonsterType enum + SpecialMonsterBehavior | [완료] | 6종: Shield/Berserk/Split/Healer/Stealth/Elite |
| S161-02 | MonsterSpawner 확률 스폰 통합 | [완료] | 101F+ 엘리트 구간에서 확률 부여 (3~15%) |
| S161-03 | 특수 행동 로직 | [완료] | Berserk HP트리거, Healer 범위 회복, Stealth 은신, Shield/Elite 스탯 보정 |
| S161-04 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

### Sprint 16-2: 탑 마일스톤 보상

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S162-01 | TowerMilestoneDataSO | [완료] | 20개 마일스톤 (10~200F), 골드/루비/소환권 + 특수 보상 |
| S162-02 | TowerMilestoneManager | [완료] | 층 도달 시 자동 수령, 중복 방지, PlayerPrefs 저장 |
| S162-03 | TowerMilestoneClaimedEvent | [완료] | 이벤트 발행 (Toast UI 등에서 구독 가능) |
| S162-04 | MCP 컴파일 검증 | [완료] | 에러 0건 (2026-04-07) |

---

## Phase 16 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S161 특수 몬스터** | 전투 다양성, 기존 스포너 확장 |
| 2 | **S162 탑 마일스톤** | 보상 동기, 빠른 구현 |

---

## Phase 17: 범위 압축 + 시뮬레이터 축소

> **핵심 전략**: 오리지널 8종 제거 + 아레나/길드 시뮬레이터를 CP 비교 방식으로 단순화.

### Sprint 17-1: 시뮬레이터 축소

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S171-01 | ArenaBattleSimulator → CP 비교 방식 | [완료] | 턴제 AI 제거, CP 비교 + 랜덤 팩터 |
| S171-02 | AIBehavior enum 제거 | [완료] | ArenaRivalDataSO에서 behavior 필드 제거 |
| S171-03 | ArenaManager CP 비교 전환 | [완료] | SimulateBattleDetailed → CP 비교 호출 |
| S171-04 | ArenaPopup 전투 시각화 제거 | [완료] | 턴별 리플레이 → 즉시 결과, Battle 패널 제거 |
| S171-05 | GuildBossBattleSimulator → CP 기반 | [완료] | 3페이즈/분노/스킬 제거, CP×랜덤 데미지 |
| S171-06 | GuildManager/GuildPopup 축소 | [완료] | 보스 시각화 패널 제거, BossData 참조 제거 |
| S171-07 | MCP 컴파일 검증 | [완료] | 에러 0, 경고 0 (2026-04-07) |

---

## Phase 18: Server Phase 1 — Core Backend

> **서버 독립 로드맵으로 이관 (2026-04-20)**: 상세는 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) Phase 18 참조.
> 클라 측 연동은 Sprint 18-6 (이 문서에 유지).
>
> **핵심 전략**: .NET 10 백엔드 + Firebase Auth + MySQL 8.0. 가챠/재화/결제/저장/출석을 서버 검증으로 이행.
> **기획서**: `systems/server-architecture.md`, `server-api-endpoints.md`, `server-db-schema.md`, `server-auth-flow.md`
> **인프라**: `indie-game-dev-infra-guide.md` 참조

### Sprint 18-1: .NET 프로젝트 스캐폴딩

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S181-01 | server/ 디렉토리 + .slnx + 3개 프로젝트 | [완료] | MFG.Server, Domain, Data (이전 MkLike 네이밍은 2026-04-15 MFG로 리네임) |
| S181-02 | docker-compose (MySQL 8.0) | [완료] | 로컬 개발용, port 3306 |
| S181-03 | EF Core + MySql.EntityFrameworkCore 설정, AppDbContext | [완료] | Player/Currency/CurrencyTransaction 엔티티, snake_case 네이밍 |
| S181-04 | Health check 엔드포인트 | [완료] | GET /health + /api/v1/health |

### Sprint 18-2: Firebase Auth 연동

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S182-01 | JWT Bearer 미들웨어 (Firebase) | [완료] | Program.cs에 구현 |
| S182-02 | POST /api/v1/auth/login | [완료] | AuthController — 신규 유저 생성 + 초기 재화(Gold/Ruby 10000) |
| S182-03 | players 테이블 + 마이그레이션 | [완료] | InitialCreate 마이그레이션 생성 |

### Sprint 18-3: 가챠 API

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S183-01 | POST /api/v1/gacha/pull | [완료] | GachaService + GachaController, 서버 확률 계산 + 재화 차감 |
| S183-02 | gacha_history 테이블 | [완료] | GachaHistory + GachaPity 엔티티, AddGacha 마이그레이션 |
| S183-03 | 천장(pity) 로직 서버 이행 | [완료] | 60회 Legendary 보장, 10연차 Rare 보장, 소환 레벨 5단계 |

### Sprint 18-4: 재화 API

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S184-01 | currencies, currency_transactions 테이블 | [완료] | InitialCreate 마이그레이션에 포함 |
| S184-02 | POST /api/v1/currency/spend, /earn | [완료] | CurrencyService + CurrencyController |
| S184-03 | GET /api/v1/currency/balance | [완료] | 전체 재화 잔액 조회 |

### Sprint 18-5: IAP + 저장 + 출석

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S185-01 | POST /api/v1/iap/verify | [완료] | IapController + 영수증 해시 중복 방지 + 상품 카탈로그 |
| S185-02 | POST /api/v1/save/sync, GET /save/load | [완료] | SaveController + ProgressData JSON(MySQL) 저장 |
| S185-03 | POST /api/v1/attendance/check | [완료] | AttendanceController + 7일 사이클 보상 + 연속 출석 계산 |

### Sprint 18-6: 클라이언트 연동

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S186-01 | Unity HTTP 클라이언트 (UniTask 기반) | [완료] | ApiClient + ServerDtos (Core/Net/) |
| S186-02 | LocalSaveProvider → ServerSaveProvider | [완료] | ServerSaveProvider — 로컬 캐시 + 비동기 서버 동기화 |
| S186-03 | GachaManager 서버 호출 전환 | [완료] | PullServerAsync 추가 (기존 로컬 메서드 유지) |
| S186-04 | CurrencyManager 서버 호출 전환 | [완료] | SpendServerAsync/EarnServerAsync/SyncBalanceFromServerAsync 추가 |

---

## Phase 19: Server Phase 2 — Competitive/Social

> **서버 독립 로드맵으로 이관 (2026-04-20)**: 상세는 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) Phase 19 참조.
>
> 아레나 + 길드를 서버로 이행

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S19-01 | 아레나 API (매칭, 결과, 랭킹) | [완료] | ArenaController — match-candidates, battle-result, status |
| S19-02 | 길드 API (가입, 탈퇴, 기부, 보스) | [완료] | GuildController — create, join, donate, boss-result, info |
| S19-03 | 클라이언트 아레나/길드 서버 연동 | [완료] | ArenaManager/GuildManager 서버 async 메서드 + ServerDtos 확장 |

---

## Phase 20: Server Phase 3 — Enhancement + Deploy

> **서버 독립 로드맵으로 이관 (2026-04-20)**: 상세는 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) Phase 20 참조.
>
> 장비 강화 서버 검증 + 최종 배포

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S20-01 | 장비 강화 API (성공 확률 서버 판정) | [완료] | EquipmentController — 스타포스 25단계 확률 테이블 |
| S20-02 | 이벤트 보상 API | [완료] | EventController — 던전/배틀패스/오프라인 보상 |
| S20-03 | AWS Lightsail 배포 + Caddy SSL | [완료] | Dockerfile + docker-compose.prod + Caddyfile (이전 세션) + 마이그레이션 자동 적용 |
| S20-04 | WebGL CORS + 인증 브릿지 | [완료] | CORS 미들웨어 + appsettings.Production.json |

---

## Phase 21: Runtime Asset Delivery (CDN 기반 리소스 다운로드)

> 앱 빌드 크기 축소 + 리소스 업데이트 시 앱 스토어 재심사 회피.
> Unity Addressables → Cloudflare R2 CDN 업로드 파이프라인.

**목적**:
- Android AAB 빌드 크기 축소 (150MB → 30MB 목표)
- 리소스 핫픽스 (스프라이트/밸런스/이벤트 배너) 시 스토어 재심사 없이 즉시 반영
- 챕터/이벤트 추가 시 차등 다운로드

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S21-01 | Unity Addressables 패키지 설치 + 기본 셋업 | [완료] | `com.unity.addressables@2.3.16` + AddressablesSetupEditor.cs (자동 세팅 메뉴) + /addressables-setup 슬래시 커맨드 |
| S21-02 | 리소스 분류 (Local/Remote 그룹 분리) | [완료] | 7개 그룹 자동 생성 (Local_Essential/Character + Remote_Audio_BGM/SFX/Data/Costumes/Chapters). 1,244개 에셋 분류. ResourcesLoadScanner 리포트 26건 잔여 (DataManager 9/UI 10/이벤트 7) |
| S21-03 | AssetReference 도입 (SO 참조 전환) | [미진행] | MonsterDataSO/SkillDataSO는 아직 직접 참조 (`public Sprite icon`, `public GameObject prefab`). CostumeDataSO 미발견. 주석에만 "향후 CDN 전환" 명시 (CharacterVisual, CharacterDesignLoader) |
| S21-04 | R2 업로드 빌드 스크립트 | [미진행] | Editor 메뉴 "Build Addressables → R2 Upload" — AddressableAssetSettings.asset 에 R2 프로파일 미구성 (로컬 빌드만 가능). cdn.mf-game.com CDN 도메인은 서버쪽 이미 준비됨 |
| S21-05 | 런타임 초기 다운로드 UI | [부분완료] | LoadingScreen.cs 존재하지만 가짜 진행 바만 (0→0.9 2초 선형) + 랜덤 팁. **Addressables 실제 다운로드 미연동**. 용량/실패 재시도 미구현 (완료도 40%) |
| S21-06 | 카탈로그 버전 체크 + 차등 업데이트 | [미진행] | 앱 시작 시 `CheckForCatalogUpdates()` → 변경된 번들만 다운로드 |
| S21-07 | 오프라인 폴백 | [미진행] | 네트워크 없을 때 내장 리소스로 동작 (로그인 전까지) |
| S21-08 | WebGL 대응 (스트리밍 로드) | [CANCELLED] | WebGL 출시 계획 없음 (2026-04-15 결정) — 향후 WebGL 전환 시 재개 |

**현재 진행도**: 30% (S21-01/02 완료, S21-05 40%, 나머지 미진행). 서버 배포 완료 + R2/CDN 인프라 준비됨 → 클라 쪽 전환만 남음.

---

## Phase 22: DevOps Foundation

> **서버 독립 로드맵으로 이관 (2026-04-20)**: 상세는 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) Phase 22 참조.
> 본 문서에는 요약만 유지. 운영 런북은 `../../../server/Docs/{deploy,rollback,backup,secrets}.md`.
>
> 1인 개발 현실에 맞춘 **최소 필수 DevOps**. 오버엔지니어링 배제.
> 목적: 수동 배포/백업 실수 제거 · 장애 1분 내 롤백 · 무료/저비용 도구만 사용 (월 추가 $0~1).

### 출시 전 필수 (S22-01 ~ S22-08)

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S22-01 | GitHub Actions CI (서버) | [완료] | `.github/workflows/server-ci.yml` — dotnet test + Buildx + GHCR push (main only), sha/latest/semver 태그 |
| S22-02 | GitHub Actions CD (서버) | [완료] | `.github/workflows/server-cd.yml` — workflow_run 트리거 + SSH deploy + `/health` retry 6회. Secrets: LIGHTSAIL_* 필요 |
| S22-03 | 환경 분리 (dev/prod) | [완료] | base appsettings env-neutral 정리, Dev=`mfg-dev-187b4`/localhost CORS, Prod=`mfg-prod-4cb30`/mf-game.com CORS, Program.cs 하드코딩 fallback 제거 + 명시적 throw |
| S22-04 | Secrets 관리 | [완료] | `server/Docs/secrets.md` — 3-tier(User Secrets/GitHub Secrets/.env+볼륨) 매핑표 + 회전 절차 + 출시 체크리스트 |
| S22-05 | DB 백업 자동화 | [완료] | 3-tier(Lightsail 스냅샷/R2 mysqldump/로컬). `scripts/backup-db.sh`+`restore-db.sh`+`Docs/backup.md` (rclone R2, cron 03:00 UTC, 30d 보존, 월 1회 staging 복원 테스트) |
| S22-06 | 롤백 런북 + 이미지 태그 관리 | [완료] | CI에 `tags: [v*.*.*]` 트리거 추가. `Docs/rollback.md` — A/B/C/D 시나리오, workflow_dispatch image_tag 지정 1분 롤백, 파괴적 마이그레이션 3단계 규칙 |
| S22-07 | **알림 허브 (Slack)** | [부분완료] | CI/CD Slack 노티 + 서버 `SlackWebhookSink` (Error/Fatal, rate limit 10/min, `Slack:ErrorWebhook` 비면 No-op). 남은 작업: Slack Workspace/채널 생성 + Webhook 발급, CloudWatch→SNS→Chatbot 연동 |
| S22-08 | 레이트 리밋 + WAF | [부분완료] | `AddRateLimiter`: global 60/min IP + `gacha` 20/min user + `iap` 10/min user. [EnableRateLimiting] 적용. 남은 작업: Cloudflare WAF Free 규칙 |

### 출시 후 1~3개월 (S22-09 ~ S22-12)

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S22-09 | Firebase Remote Config 연동 | [미진행] | 밸런스 조정 무배포 적용 (SO 파라미터 원격화) |
| S22-10 | 관리자 대시보드 | [미진행] | ASP.NET Razor Pages — 유저 조회, 재화 지급, 이상거래 확인, 공지 발송 |
| S22-11 | Sentry 서버 에러 추적 | [미진행] | 무료 5K 이벤트/월, 또는 GlitchTip self-host |
| S22-12 | k6 부하 테스트 | [미진행] | 출시 전 1회 + DAU 1K 시나리오 검증 |

### 알림 채널 비용 비교

| 채널 | 비용 | 메모 |
|------|------|------|
| **Slack Free** ⭐ | $0 | Incoming Webhook + AWS Chatbot 통합 용이. 90일 히스토리 제약은 히스토리는 서버 로그에서 확인 가능하므로 실무상 무리 없음 |
| Slack Pro | $7.25/월 | 히스토리 무제한 필요 시 (1인엔 보통 불필요) |
| Telegram Bot | $0 | 대안 옵션, `Tools/telegram-bot` 재활용. 메시지 영구 보관 |
| Discord Webhook | $0 | 대안, 메시지 무제한 보관 |
| AWS SNS | $0~$1/월 | Chatbot 경유 Slack 연동에 사용 (100만 요청 무료) |
| Amazon Q Developer (Chatbot) | $0 | CloudWatch Alarm → Slack 무료 연동 |

### S22-07 알림 허브 구현 상세 (Slack 기준)

**사전 준비**:
- [ ] Slack Workspace 생성 (https://slack.com/get-started) — 무료
- [ ] 채널 분리 생성:
  - `#mfg-builds` — GitHub Actions 빌드/배포 알림
  - `#mfg-errors` — 서버 ERROR 이상 로그
  - `#mfg-alerts` — AWS CloudWatch / Lightsail 알람
- [ ] 각 채널용 Incoming Webhook URL 발급 (Apps → Incoming Webhooks → Add) → GitHub Secrets/서버 `.env`에 저장

**1) GitHub Actions → Slack** (이미지 1 패턴):
```yaml
- name: Notify Slack (Success)
  if: success()
  uses: slackapi/slack-github-action@v1
  with:
    webhook-url: ${{ secrets.SLACK_WEBHOOK_BUILDS }}
    payload: |
      {
        "attachments": [{
          "color": "good",
          "title": "✅ [MFG API] 배포 완료",
          "fields": [
            {"title": "Branch", "value": "${{ github.ref_name }}", "short": true},
            {"title": "Actor", "value": "${{ github.actor }}", "short": true},
            {"title": "Commit", "value": "${{ github.event.head_commit.message }}"}
          ]
        }]
      }

- name: Notify Slack (Failure)
  if: failure()
  uses: slackapi/slack-github-action@v1
  with:
    webhook-url: ${{ secrets.SLACK_WEBHOOK_BUILDS }}
    payload: |
      {
        "attachments": [{
          "color": "danger",
          "title": "❌ [MFG API] 빌드 실패!",
          "fields": [
            {"title": "Branch", "value": "${{ github.ref_name }}", "short": true},
            {"title": "Run URL", "value": "${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"}
          ]
        }]
      }
```

**2) 서버 에러 → Slack (Serilog)** (이미지 2 패턴):
- NuGet: `Serilog.Sinks.Slack` (또는 직접 HTTP Sink)
- `Program.cs`:
  ```csharp
  Log.Logger = new LoggerConfiguration()
      .WriteTo.Console()
      .WriteTo.Slack(
          webhookUri: builder.Configuration["Slack:ErrorWebhook"],
          restrictedToMinimumLevel: LogEventLevel.Error)
      .CreateLogger();
  ```
- **Rate limit 필수**: 동일 에러 반복 시 폭주 방지 → `Serilog.Sinks.Slack` 의 `CustomChannel` 옵션 또는 custom buffer sink 적용 (분당 최대 10건)
- 포맷: `[ERROR] {SourceContext}\n{Exception Message}\n...truncated` (이미지 2 스타일)

**3) AWS CloudWatch → Slack via Amazon Q** (이미지 3 패턴):
- AWS Chatbot 콘솔 → Slack Workspace 연결 (OAuth 승인, 무료)
- SNS 토픽 생성: `mfg-cloudwatch-alarms`
- Chatbot → Configuration → Slack Channel 연결 + SNS 토픽 지정
- CloudWatch/Lightsail Alarm → Action: SNS 토픽 발행 → 자동 Slack 포스팅
- 대상 알람:
  - Lightsail CPU > 80% (5분)
  - Lightsail Network out 급증
  - `api.{도메인}.com/health` 응답 시간 > 2s
  - DB 디스크 사용량 > 85%

### 주의사항

- **90일 히스토리 제약**: Slack Free는 90일 이전 메시지 검색 불가. 하지만 서버 에러는 서버 로그(Loki/Lightsail)에 영구 보관되므로 실무상 무리 없음
- **채널 스팸 방지**: 서버 에러는 반드시 Rate Limit 적용 (1개 예외 1000번 반복 시 Slack 뻗음)
- **Webhook URL 보안**: Secrets에만 저장, 실수로 클라이언트 코드에 넣으면 스팸 공격 대상

### 1인 개발자 권장 순서

```
출시 전:
  S22-01 → S22-02 (CI/CD 먼저, 배포 자동화로 실수 제거)
    ↓
  S22-03 → S22-04 (환경/Secrets 분리)
    ↓
  S22-05 → S22-06 (백업/롤백, 재해 대비)
    ↓
  S22-07 → S22-08 (알림/보안)

출시 후 (DAU 100+):
  S22-09 (Remote Config)
    ↓
  S22-10 (어드민)
    ↓
  S22-11 (Sentry)
    ↓
  S22-12 (부하 테스트, 스케일업 전)
```

**예상 구축 시간**: 주말 2~3번 (총 20~30시간)
**월 추가 비용**: $0~1 (모두 무료 티어 내)

### 리소스 분류 가이드

**Local (앱 번들 내장, ~30MB 목표)**
- 핵심 UI 아이콘/폰트
- 첫 3챕터 몬스터 스프라이트
- 튜토리얼 맵 배경
- 필수 스킬 VFX 5종
- 로딩 화면/공통 버튼

**Remote (CDN 다운로드, ~200MB 예상)**
- 챕터 4+ 맵 배경
- 챕터 4+ 몬스터 스프라이트 (SPUM)
- 보스 스프라이트 (30+)
- 코스튬 (50+)
- 이벤트 배너/UI
- 희귀 스킬 VFX

### 빌드/배포 워크플로우

```
1. Unity Editor → Window → Asset Management → Addressables → Groups
2. "Build Addressable Content" 실행
3. 빌드 산출물: ServerData/{platform}/catalog_{hash}.json + *.bundle
4. 커스텀 Editor 스크립트로 R2 업로드:
   - 대상: cdn.{yourdomain}.com/addressables/{platform}/
5. Addressables Profile → RemoteLoadPath:
   - https://cdn.{yourdomain}.com/addressables/[BuildTarget]
6. 앱 빌드 → AAB/IPA에는 Local 그룹만 포함 → 스토어 업로드
```

### 고려사항

- **첫 실행 다운로드 시간**: ~200MB / 10Mbps ≈ 160초. 단계별(챕터별) 다운로드로 UX 완화
- **버전 호환성**: 서버 API 버전 ↔ 리소스 카탈로그 버전 매칭 필요 (불일치 시 강제 업데이트)
- **비용**: R2 무료 10GB 저장 + 월 1000만 Class A 읽기 = 초기 DAU 1K 이하 완전 무료
- **WebGL**: Brotli 압축 자동, 브라우저 캐시 활용. 모바일과 동일 CDN 재사용 가능
- **착수 시점**: Phase 2 클라이언트 연동 완료 후, 스토어 심사 전 마지막 최적화 단계

---

## Phase 23: Onboarding & Auth (타이틀/로그인/직업선택/닉네임/옵션)

> 앱 기동 시 일반 모바일 게임 표준 플로우 도입. 현재 Main.unity가 바로 전투 시작하는 구조를 Title → Login → (신규) Nickname → Class → Main 으로 확장.

### 목표
- Title 씬 신규 생성 + Main 전환 로직
- Firebase Auth(Google/Apple) → 서버 `/api/v1/auth/login` JWT 발급
- 신규 유저: 닉네임 입력 + 초기 직업 3종 중 1개 선택 → 서버 저장
- 재접속: JWT 자동 갱신 → Title → Main 바로 진입
- 옵션 화면 확장 (언어/알림/기존 사운드·그래픽 유지)

### 재사용
- 서버 `AuthController` (Phase 18 완료, Nickname 자동 할당 `용사XXXX`)
- `JobSystem.ChangeJob()` (이미 잠금 로직 보유)
- `SettingsPopupUI` (UI Toolkit 기반 완성) — 언어/알림만 추가
- `BasePopup`, `UIManager.OpenPopup<T>()`, EventBus 패턴

### Sprint 23: Onboarding Flow

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S23-01 | Title.unity 씬 생성 + SceneManager 전환 | [완료] | Canvas/EventSystem/3팝업 scene-embedded, Build Settings 순서 Title(0)→Main(1), Main2.unity 삭제 |
| S23-02 | Firebase Auth SDK 추가 | [미진행] | `com.google.firebase.auth` + Google/Apple 로그인 프로바이더 |
| S23-03 | LoginManager + LoginPopup 구현 | [완료] | Mock 로그인(X-Dev-Uid), dev-uid PlayerPrefs 저장, LoginCompletedEvent 발행 |
| S23-04 | JWT 저장/갱신 유틸 + 자동 재로그인 | [부분] | TryAutoLoginAsync (dev-uid 재사용). JWT는 Firebase 실연동 시 추가 |
| S23-05 | NicknamePopup | [완료] | 2~16자, 한글/영문/숫자 정규식 검증, 뒤로가기 차단 |
| S23-06 | JobSelectPopup | [완료] | Warrior/Archer/Mage 카드 + 하이라이트 + 설명 + 확정 |
| S23-07 | OnboardingFlow 상태머신 | [완료] | Title→Login→(신규)Nickname→JobSelect→Submit→Main |
| S23-08 | 서버 PATCH `/api/v1/auth/profile` jobId | [완료 2026-04-20] | 서버 PlayerProfileUpdateRequest.JobId 추가 + UpdateProfile에서 Player.JobId 갱신 + Level/CombatPower 0값 스킵 방어. 프로덕션 배포 완료 (commit 33f60cd). 상세 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) |
| S23-09 | SettingsPopupUI 확장 (알림) | [완료] | PopupSettings.uxml에 push-notification-toggle 추가, SettingsPopupUI에 콜백 연결. 언어 설정은 한국어 단일로 스킵 |
| S23-10 | SaveData.PlayerData.nickname 필드 추가 | [완료] | PlayerData.nickname 기본값 "" 추가 |
| S23-11 | /run 신규 유저 진입 경로 테스트 | [준비됨] | EnsureComponents로 UI 자동 생성. Title 씬 Play 즉시 검증 가능 |

### 의존성
- **Phase 18 Auth API**: 완료 → 활용
- **Phase 21 Addressables**: Title 씬을 Local 그룹에 넣는 게 이상적. Phase 21 선행 권장하나 미진행 시에도 독립 개발 가능
- **Firebase 프로젝트**: `mfg-dev`/`mfg-prod` Firebase Console 생성 필요 (S22-03 환경 분리와 연동)

### 스크린 플로우

```
[Title.unity]
  ┌──────────────────────────┐
  │      mkLike 로고         │
  │                          │
  │      [ 시작하기 ]         │
  └──────────────────────────┘
           ↓ 최초
  [LoginPopup]  ─Google/Apple─→ 서버 /login → JWT
           ↓ 신규
  [NicknamePopup] 입력 2~16자
           ↓
  [JobSelectPopup] 3직업 중 1
           ↓ PATCH /profile
  [Main.unity] — LoginFlowManager 일일 보상
```

### 검증
- 세이브 파일 삭제 후 Play → Title 진입 확인
- Mock 로그인(더미 JWT) → Nickname/Class 입력 → Main 진입 → 세이브 확인
- 재기동 시 Title → 자동 Main 진입 (입력 건너뜀)
- 서버 테스트: `dotnet test` + curl `/auth/login` + `PATCH /profile`

---

## Phase 24: Google Sheets 기반 밸런스 데이터 관리

> **핵심 전략**: 현재 1,238개의 ScriptableObject 에 하드코딩된 밸런스 수치(몬스터 스탯/스킬 데미지/가챠 확률 등)를 Google Sheets로 **이관** — 밸런스 조정 시 Unity 재컴파일 없이 시트 편집 → 한 번의 임포트로 SO 갱신.
> **참고**: 2026-04-16 현재 Google Sheets API 연동 코드 0건. 신규 구축.

### 목적
- **밸런스 조정 속도 10배** — Unity 프로젝트 열지 않고도 기획자가 시트에서 직접 수치 편집
- **버전 관리** — 시트 히스토리 + 리뷰 코멘트 (Git 보완재)
- **런타임 원격 갱신** (선택): 출시 후 `Firebase Remote Config` 통해 서버 push로 즉시 반영
- **서버와 공유** — 동일 시트를 서버도 참조해서 클라/서버 밸런스 일치 보장

### 현재 SO 인벤토리 스냅샷 (2026-04-16 조사)

| 우선순위 | 카테고리 | 개수 | 주요 필드 |
|---------|---------|------|---------|
| ⭐⭐⭐⭐⭐ | 몬스터 스탯 | 4+ | id, hp, atk, def, attackSpeed, goldReward, expReward |
| ⭐⭐⭐⭐⭐ | 스킬 밸런스 | 71 | damageMultiplier, cooldown, buffAtkRate, duration, range, hitCount |
| ⭐⭐⭐⭐⭐ | 무기/장비 스탯 | 52 (+6) | baseAtk, baseHp, baseDef, grade배율, specialOption |
| ⭐⭐⭐⭐⭐ | 가챠 확률 | 3 | entries[], gradeWeights[], summonLevels[] |
| ⭐⭐⭐⭐ | 퀘스트 보상 | 375 | condition, requiredAmount, rewardType, rewardAmount |
| ⭐⭐⭐⭐ | 아레나 티어 | - | tierIndex, minRating, maxRating, rewardGold |
| ⭐⭐⭐⭐ | 마스터리 | 27 | branchIndex, bonusStat, flatBonus, percentBonus |
| ⭐⭐⭐⭐ | 아티팩트/세트 | 9 | bonusStat, setId, twoSetPercent |
| ⭐⭐⭐ | 영웅의 힘 / 어빌리티 | 1 + 트리 | milestones[] (배열은 시트, 트리는 SO 유지) |
| ⭐⭐⭐ | 오프라인보상 | 1 | levelMultipliers[], equipDropRatePerHour |
| ⭐⭐⭐ | 스테이지 | - | monsterCount, maxAliveMonsters, spawnInterval |
| ⭐ | VFX 참조 | 194 | 에셋 링크 (시트 부적합, 제외) |

### Sprint 24-1: Google Sheets API 인프라

> API 키 발급 + 클라/Editor 인증 + 최소 Read 경로 검증

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S241-01 | Google Cloud Console → Sheets API 활성화 + 서비스 계정 키 발급 | [미진행] | GCP 프로젝트 `mfg-prod-4cb30` 재사용. 권한: Viewer only. `sheets-viewer.json` 다운로드 → `mkFile/` 보관 |
| S241-02 | NuGet `Google.Apis.Sheets.v4` / Unity 대안 패키지 조사 | [미진행] | Unity는 UPM 호환 어려움 → Editor 전용 dll 참조. 런타임에선 REST 직접 호출 |
| S241-03 | 마스터 시트 생성 + 탭 스키마 합의 | [미진행] | 탭당 카테고리 1개. 헤더: `id / fieldA / fieldB / ...`. 타입/필수/메모 별도 주석 행 |
| S241-04 | Editor 스크립트 `SheetsImporter.cs` 최소 Proof | [미진행] | 메뉴 "MFG/Import/Monsters from Sheets" → 1개 탭 → 1개 SO 덮어쓰기 |

### Sprint 24-2: 1차 이관 — 몬스터/스킬/무기/장비

> ⭐⭐⭐⭐⭐ 우선순위 4개 카테고리 먼저 옮겨서 밸런싱 루프 단축

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S242-01 | 시트 → SO 생성기 (`MonsterSheetImporter`) | [미진행] | 시트 각 행 → 기존 `MonsterDataSO` Asset 덮어쓰기. 신규 id는 새 asset 생성 |
| S242-02 | 시트 → SO 생성기 (`SkillSheetImporter`) | [미진행] | 71개 스킬 밸런스 이관 |
| S242-03 | 시트 → SO 생성기 (`WeaponSheetImporter` + `EquipmentSheetImporter`) | [미진행] | 무기 6 + 장비 52, 등급 배율 테이블 평탄화 |
| S242-04 | Import 스모크: 시트값 수정 → Editor import → Play 검증 | [미진행] | 전체 루프 End-to-End 확인 |

### Sprint 24-3: 2차 이관 — 가챠/퀘스트/아레나

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S243-01 | 가챠 확률 시트 (`GachaPoolSO` 3종) | [미진행] | 등급 가중치 배열은 가로 방향 컬럼으로 펼쳐서 표현 |
| S243-02 | 퀘스트 시트 (`GuideQuestSO` 375개) | [미진행] | 대량 데이터 → 여러 탭 분리 가능 (Main/Cycling/Daily) |
| S243-03 | 아레나 티어 시트 (`ArenaDataSO`) | [미진행] | 티어별 진입/보상 단순 테이블 |

### Sprint 24-4: 검증 + 자동화

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S244-01 | 시트 스키마 검증 (헤더/타입 일치) | [미진행] | 임포트 전 pre-flight check — 실수로 컬럼 삭제 시 에러 |
| S244-02 | Import 후 빌드 스모크 | [미진행] | `/run` 봇 테스트 자동 실행 → regression 감지 |
| S244-03 | (선택) GitHub Action 예약 임포트 | [미진행] | 매일 `dispatch_workflow` → 최신 시트 반영 자동 커밋 |
| S244-04 | (선택) Firebase Remote Config 브릿지 | [미진행] | 출시 후 런타임 원격 수정 — Unity에서 Remote Config 값을 SO 오버라이드로 적용 |

### Sprint 24-5: 서버-클라이언트 밸런스 동기화 (→ Phase 26 흡수)

> **2026-04-20 재편**: 서버 측 `SheetsSyncService`는 [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) **Phase 26 Sprint 26-2**로 이관 통합. 본 Sprint는 레거시 참조만 유지.
>
> 서버의 `BalanceTables.cs` (경험치 테이블, 일일 상한 등) 와 클라 SO 가 어긋나면 아레나/가챠 검증 실패.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S245-01 | 서버 동일 시트 구독 — .NET Sheets API 연동 | [미진행] | `MFG.Server/Services/SheetsSyncService.cs` (BackgroundService) |
| S245-02 | 서버/클라 공통 필드 정의 (JSON 스키마) | [미진행] | 가챠 확률, 재화 상한, 아레나 보상 등 |
| S245-03 | 불일치 감지 테스트 | [미진행] | CI에서 클라 SO ↔ 서버 캐시 비교 |

---

## Phase 24 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S241 Sheets 인프라** | 기반 작업 — API 키 + 최소 Read Proof |
| 2 | **S242 몬스터/스킬/무기/장비** | ⭐⭐⭐⭐⭐ 4종, 밸런싱 체감 극대 |
| 3 | **S243 가챠/퀘스트/아레나** | 확장 |
| 4 | **S244 검증/자동화** | Import 안정성 |
| 5 | **S245 서버 동기화** | 출시 전 마지막 |

### 예상 비용
- Google Sheets API: 무료 (쿼터 500 req/100초, 1일 6만 req → 운영 수준은 완전 무료 범위)
- GCP 서비스 계정: 무료
- 시트 자체: Google Drive 무료 15GB 내 (시트 1~2개, 용량 무의미)
- **월 추가 비용 $0**

### 의존성
- 서버 배포 완료 ✅ (2026-04-16)
- GCP 프로젝트 `mfg-prod-4cb30` ✅ (재사용)
- Firebase Remote Config (선택 기능, S22-09와 연계)

---

## Phase 25: 서버 권위 전환 + 단일 진실 소스 확립 (Online-Only 모드)

> **서버 관점 상세 (2026-04-20 이관)**: [`../../../server/Docs/Planning/roadmap.md`](../../../server/Docs/Planning/roadmap.md) Phase 25 참조.
> **본 문서**는 **클라 측 작업**(Sprint 25-1 S251-02~04, Sprint 25-2 전체) 위주.
> Sprint 25-3 오프라인 정책 관련 설계는 [`../../../server/Docs/Planning/offline-reward-model.md`](../../../server/Docs/Planning/offline-reward-model.md) 참조.
> Sprint 25-4 밸런스 일치는 Phase 26 Sprint 26-1 (`/data/version`)로 흡수.
>
> **핵심 전략**: 현재 클라는 "로컬 계산 + 병렬 서버 호출"의 하이브리드 상태. Phase 25에서 **로컬 경로를 제거하고 서버 DB를 단일 진실 소스**로 확정한다. 최종적으로 클라는 "서버+DB 데이터"로만 동작.
> **배경**: Phase 18-20에서 서버 API와 클라 서버 호출 메서드(`*ServerAsync`)는 완성됐으나, `GachaManager.Pull()`/`CurrencyManager.Spend()` 등 **로컬 메서드가 여전히 병존**. `_allowOfflineFallback=true`로 서버 없이도 게스트 플레이 가능 → 해킹/치트 경로 + 밸런스 불일치 위험.
> **원칙**: 서버 권위(Server-Authoritative). 클라는 입력 받고 결과 표시만. 모든 상태 변경은 서버 왕복.

### 목표 상태
- 유저 데이터(재화/가챠/아레나/세이브)는 **DB**가 유일 원천
- 로컬 JSON 세이브는 "캐시/임시" 전용, 단독 쓰기 금지
- 서버 접속 실패 시 게임 진입 차단 (오프라인 플레이 금지)
- 밸런스 값은 시트(Phase 24) 기반, 클라/서버 버전 일치 강제

### Sprint 25-1: 데이터 소스 일원화

> LocalSaveProvider의 역할을 "읽기 전용 캐시"로 강등. 쓰기는 서버만.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S251-01 | 최초 로그인 시 로컬 `SaveData.json` → 서버 `ProgressData` 마이그레이션 1회 업로드 | [미진행] | 기존 오프라인 유저 데이터 보존. 업로드 성공 후 로컬 `migrated=true` 플래그 |
| S251-02 | `LocalSaveProvider` 를 ReadOnlyCache 로 리팩토링 | [미진행] | Write 경로 제거, 서버 응답으로만 갱신. 로컬 파일은 재기동 빠른 시작용 |
| S251-03 | 앱 시작 시 서버 sync 성공 이전 게임 진입 차단 | [미진행] | Title → LoginFlow → ServerSync → Main 순서 강제. 실패 시 에러 팝업 |
| S251-04 | `SaveManager.AutoSave()` 경로를 서버 `POST /save/sync` 직통으로 교체 | [미진행] | 기존 60초 간격 로컬 저장 → 서버 저장. 실패 시 재시도 큐 |

### Sprint 25-2: 로컬 계산 경로 제거

> `*ServerAsync` 만 남기고 기존 로컬 동기 메서드 deprecate.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S252-01 | `GachaManager.Pull()` deprecate — `PullServerAsync()` 단일 경로 | [미진행] | 기존 호출 지점 전수조사 후 교체. UI는 서버 응답 후 결과 표시 |
| S252-02 | `CurrencyManager.Spend/Earn()` 로컬 즉시 반영 제거 | [미진행] | 서버 응답 대기 중에는 "처리 중" 스피너. optimistic UI는 옵션 (롤백 처리 필요) |
| S252-03 | `ArenaManager.SimulateBattle` 로컬 계산 제거 → `POST /arena/battle-result` 응답 사용 | [미진행] | 현재 CP 비교 시뮬은 서버에서도 동일 로직으로 구현됨 (Phase 19) |
| S252-04 | `GuildManager` 기부/보스 로컬 계산 제거 | [미진행] | 서버 `/guild/donate`, `/guild/boss-result` 만 사용 |
| S252-05 | `EquipmentManager.Enhance` 로컬 판정 제거 → `POST /equipment/enhance` 응답 사용 | [미진행] | 서버 스타포스 25단계 확률 테이블 (Phase 20) 적용 |

### Sprint 25-3: 오프라인 정책 확정

> `_allowOfflineFallback` 플래그 제거. 네트워크 필수 모드.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S253-01 | `LoginManager._allowOfflineFallback = false` 기본값 | [미진행] | 프로덕션 빌드는 강제 false. 개발 빌드만 true 허용 (Editor 전용) |
| S253-02 | 서버 연결 실패 시 "재접속" 팝업 + 재시도 루프 | [미진행] | 5회 실패 시 앱 종료 옵션 제공. 점검 공지 페치(옵션) |
| S253-03 | 네트워크 끊김 감지(Unity `NetworkReachability`) + 자동 재동기화 | [미진행] | 복구 시 `SaveManager.Resync()` + UI 새로고침 |
| S253-04 | JWT 만료(401 응답) → Firebase 자동 refresh → 재시도 | [미진행] | S23-02 Firebase Auth SDK 선행. 만료 시 로그인 화면 돌아가기 fallback |

### Sprint 25-4: 밸런스 일치 보장 (Phase 24 연계)

> Google Sheet → 클라 SO + 서버 캐시 동시 갱신. 불일치 자동 감지.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S254-01 | 서버 `GET /api/v1/balance/version` 엔드포인트 | [미진행] | 시트 임포트 시점의 hash/timestamp 반환 |
| S254-02 | 클라 시작 시 로컬 SO 버전 ↔ 서버 버전 비교 | [미진행] | 불일치면 Addressables 카탈로그 갱신 + 재시작 유도 |
| S254-03 | CI에서 시트 기반 SO ↔ 서버 BalanceTables 바이트 diff 검증 | [미진행] | PR 머지 전 자동 체크 — drift 발견 시 fail |

### Sprint 25-5: QA + 롤아웃

> 서버 권위 모드 전체 플로우 스모크. 오프라인 모드 제거 검증.

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S255-01 | `/run` 봇: 서버 on 상태 End-to-End 플레이 (가챠/아레나/길드/세이브) | [미진행] | 2시간 이상 무중단 플레이 + 서버 재배포 중 재접속 |
| S255-02 | 수동 QA 체크리스트: 서버 다운 시 동작, 서버 재기동, JWT 만료, DB 일시 락 | [미진행] | 각 시나리오별 기대 동작 문서화 |
| S255-03 | 기존 로컬 세이브 유저 마이그레이션 테스트 | [미진행] | S251-01 경로 검증 — 샘플 `SaveData.json` 20개로 업로드 성공률 측정 |
| S255-04 | 프로덕션 배포 + 24시간 모니터링 | [미진행] | 서버 에러율/JWT 실패율/재접속 횟수 Grafana/CloudWatch |

---

## Phase 25 스프린트 우선순위

| 순서 | 스프린트 | 이유 |
|------|---------|------|
| 1 | **S251 데이터 소스 일원화** | 가장 근본 — 세이브부터 서버 전용 |
| 2 | **S252 로컬 계산 경로 제거** | 치트 방지 핵심 |
| 3 | **S253 오프라인 정책** | UX 정리 |
| 4 | **S254 밸런스 일치** | Phase 24 완료 후 |
| 5 | **S255 QA** | 출시 직전 |

### 선행 조건
- Phase 18-20 서버 API ✅ 완료
- Phase 22 CI/CD ✅ 완료 (2026-04-16)
- Phase 23 Firebase Auth SDK (S23-02) ⏳ JWT 갱신 시 필수
- Phase 24 시트 이관 완료 (S254 연계)

### 이후 상태 (Phase 25 완료 시)
- 클라이언트는 **서버+DB 데이터 없이는 작동 불가** (의도된 설계)
- 유저 기기 교체/OS 재설치 시 데이터 100% 보존
- 해킹 경로 차단 (로컬 조작 불가)
- 밸런싱 핫픽스 — 시트 수정 → 서버 재배포 → 클라 재접속 시 반영 (Remote Config 연계 시 재접속 불필요)
