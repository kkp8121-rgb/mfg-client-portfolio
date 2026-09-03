# mkLike 성장 중심 UX 플로우 설계서

> 모든 시스템은 "성장"을 위해 유기적으로 연결되어야 한다.
> 유저의 모든 행동은 "더 강해지기 위한 것"이어야 하고, 그 결과가 즉시 눈에 보여야 한다.

---

## 1. 가챠 직후 UX 플로우 (핵심)

### 1.1 현재 상태 분석

현재 구현된 가챠 관련 시스템:
- **GachaEffect**: 등급별 카드 연출 (회전 + 글로우 + 펀치스케일). NEW 뱃지 + "장착하면 전투력 UP!" 힌트 텍스트 존재
- **GachaCompareSystem**: 에픽 이상 아이템을 배치 수집 후 CP 추정치 기반 EquipComparePopup 호출 (3.5초 대기 후)
- **AcquisitionShortcutPopup**: 가챠/장비/동료 획득 이벤트 구독, "장착하기" / "강화하기" 바로가기 제공
- **EquipComparePopup**: 스탯 행별 전후 비교 + CP 비교

### 1.2 문제점

1. **연출 끝나면 끊긴다**: 카드 연출 → 오버레이 닫힘 → 전투 화면 복귀. 비교 팝업은 3.5초 후 별도 등장하여 연결감 부족
2. **"장착하면 전투력 UP!" 텍스트만 있고 실제 장착으로 이어지지 않음**: 유저가 직접 장비 패널을 찾아가야 함
3. **10연차 결과 요약 후 한 번에 비교 불가**: 최고 1개만 비교 팝업으로 넘어감
4. **CP 카운터 변화가 가챠 시점에 보이지 않음**: 장착 후에야 CP가 올라감

### 1.3 이상적인 플로우 (단계별)

```
[Phase 1] 가챠 카드 연출 (기존 GachaEffect — 유지)
   ├── 등급별 회전/글로우/SFX
   ├── NEW 뱃지 (에픽+)
   └── 10연차: 순차 표시 + 요약

        ↓ (카드 연출 종료 직후, 0.3초 지연)

[Phase 2] "즉시 비교" 오버레이 (NEW — GachaQuickCompare)
   ├── 현재 장착 아이콘  vs  새 아이템 아이콘
   ├── 핵심 스탯 3줄 (공격력/방어력/체력) + 증감 화살표
   ├── CP 델타 대문자: "CP +523"  (초록 or 빨강)
   ├── [원터치 장착] 버튼  ←  핵심!
   ├── [나중에] 버튼
   └── 10연차: "추천 장착 3건" 리스트

        ↓ [원터치 장착] 탭 시

[Phase 3] 장착 즉시 피드백 (NEW — EquipFlashFeedback)
   ├── 비교 오버레이 → 캐릭터 실루엣 + 빛나는 슬롯 연출 (0.5초)
   ├── CP 카운터 카운트업 애니메이션 (CpCounterWidget 연동)
   ├── 이유 텍스트: "전설 장갑 장착 — CP +523"
   ├── SFX: sfx_equip_legendary
   └── 비교 오버레이 페이드아웃

        ↓ (1초 후 자동 전환)

[Phase 4] 전투 복귀 + 체감 피드백 (NEW — PostGrowthHint)
   ├── 화면 상단 플로팅 배너: "데미지가 올라갔다! +23%"
   ├── 첫 3회 공격에 데미지 텍스트 크기 1.5배 + 골드 글로우
   └── 2초 후 페이드아웃
```

### 1.4 구현 계획: GachaQuickCompare

```
클래스: GachaQuickCompare : MonoBehaviour
위치: Assets/Scripts/UI/GachaQuickCompare.cs
역할: GachaEffect 연출 종료 후 즉시 비교 + 원터치 장착 제공

핵심 필드:
- _currentEquipIcon, _newItemIcon (Image)
- _statRows[3] (공격력/방어력/체력 고정)
- _cpDeltaText (TMP_Text) — "+523" 스타일
- _equipButton, _laterButton (Button)
- _characterSilhouette (Image) — 장착 연출용

핵심 메서드:
- ShowComparison(GachaResultEvent result) — 현재 장착 vs 새 아이템 자동 비교
- OnEquipClicked() — EquipmentManager.Equip() 호출 + 장착 연출 + CP 갱신
- OnLaterClicked() — 닫기

이벤트 흐름:
1. GachaEffect.ProcessQueue 종료 시 GachaQuickCompare.ShowComparison() 호출
2. ShowComparison에서 RecommendationManager.GetRecommendedEquipment() 활용
3. 장착 시 EquipmentChangedEvent 발행 → CpCounterWidget 자동 반응
4. CpChangedEvent에 Reason 필드 활용하여 이유 텍스트 표시
```

### 1.5 구현 계획: PostGrowthHint

```
클래스: PostGrowthHint : MonoBehaviour
위치: Assets/Scripts/UI/PostGrowthHint.cs
역할: 성장 이벤트 직후 전투 화면에서 "체감" 피드백 제공

구독 이벤트:
- EquipmentChangedEvent
- LevelUpEvent
- JobChangedEvent
- CompanionEquippedEvent

동작:
1. 이벤트 수신 → CombatStats 변화량 스냅샷
2. 화면 상단 배너: "데미지 +23% | 체력 +1,200"
3. _boostRemainingHits = 3 → DamageTextManager에 크기/색상 오버라이드 전달
4. 3회 또는 2초 후 오버라이드 해제
```

---

## 2. 성장 피드백 루프 설계

### 2.1 성장 순간별 UX 매트릭스

| 순간 | 트리거 이벤트 | 현재 구현 | 이상적인 UX | 우선순위 |
|------|-------------|----------|------------|---------|
| **가챠 후** | GachaResultEvent | 카드 연출 + CP 힌트 텍스트 | 비교 → 원터치 장착 → CP 카운트업 → 전투 체감 | P0 |
| **레벨업** | LevelUpEvent | 이벤트 발행만 (LevelUpEffect.cs 없음) | 화면 전체 골드 플래시 + "Lv.XX!" 대문자 + 새 스킬 예고 팝업 + 스탯 포인트 알림 | P0 |
| **강화 후** | EnhanceResultEvent (간접) | EnhanceResultEffect: 성공/실패 색상 플래시 | 전후 비교 팝업 (StatCompareView) + CP 카운트업 + "다음 강화까지 N개" | P1 |
| **전직 후** | JobChangedEvent | JobAdvanceEffect: 빛 기둥 + 텍스트 | 컷씬 + 새 스킬 4개 시연 + "새로운 힘을 얻었다!" + 스킬트리 바로가기 | P1 |
| **보스 처치** | StageChangedEvent (보스→다음) | 다음 스테이지 자동 전환 | 보상 샤워 연출 + "챕터 X 클리어!" + 보상 카운트업 + "다음 보스까지 9스테이지" | P0 |
| **동료 획득** | CompanionObtainedEvent | AcquisitionShortcutPopup | 동료 입장 연출 + 스킬 미리보기 + 파티 편성 바로가기 + CP 예상치 | P1 |
| **탑 클리어** | TowerFloorReachedEvent | GoalGuideWidget 갱신 | 층 클리어 배너 + 랭킹 순위 변동 + 마일스톤 보상 카운트업 | P2 |
| **오프라인 보상** | OfflineRewardClaimedEvent | OfflineRewardPopup (카운트업) | 카운트업 유지 + "접속하지 않은 동안 XX마리 처치" + 성장 요약 | P2 |
| **장비 장착** | EquipmentChangedEvent | (없음) | StatCompareView 자동 팝업 + CP 카운트업 | P1 |
| **스킬 레벨업** | SkillLevelUpEvent | (없음) | 스킬 아이콘 빛나기 + 데미지 예상 증가량 + 다음 레벨 비용 | P2 |

### 2.2 레벨업 UX 상세 설계 (P0)

현재 LevelUpEffect.cs가 존재하지 않으므로 신규 구현 필요.

```
[LevelUpEvent 수신]
    ↓
[Phase A] 화면 연출 (0.8초)
    ├── 화면 전체 백색→골드 플래시 (CanvasGroup alpha 0→0.6→0, 0.3초)
    ├── "LEVEL UP!" 대문자 (중앙, OutBack 스케일, 황금색)
    ├── "Lv.34 → Lv.35" 아래에 표시
    ├── 캐릭터 위치에 빛 기둥 파티클 (1초)
    └── SFX: sfx_level_up

[Phase B] 보상 미리보기 (1.2초)
    ├── "스탯 포인트 +5" (초록 텍스트)
    ├── 마일스톤 체크:
    │   ├── Lv.10 → "1차 전직 가능!" (황금 펄스)
    │   ├── Lv.30 → "2차 전직 가능!" (보라 펄스)
    │   ├── Lv.60 → "3차 전직 가능!" (빨강 펄스)
    │   └── Lv.100 → "4차 전직 가능!" (무지개 펄스)
    ├── 새 스킬 해금 시: "새 스킬: [아이콘] 화염 폭발!"
    └── 새 콘텐츠 해금 시: "던전 해금!" / "아레나 해금!"

[Phase C] 자동 닫힘 (탭으로 스킵 가능)
    ├── 1.5초 후 페이드아웃
    └── CP 카운터 자동 갱신 (스탯 포인트 미분배 시에도 기본 스탯 증가 반영)
```

### 2.3 보스 처치 UX 상세 설계 (P0)

```
[보스 HP 0 도달]
    ↓
[Phase A] 보스 사망 연출 (1초)
    ├── 보스 스프라이트 깜빡임 (DeathEffect 활용)
    ├── 화면 슬로우 (Time.timeScale = 0.3, 0.5초)
    ├── 보스 위치에서 폭발 VFX
    └── SFX: sfx_boss_death

[Phase B] 승리 배너 (1.5초)
    ├── 화면 중앙 "STAGE CLEAR!" (OutElastic 스케일)
    ├── 아래에 "챕터 X 보스 처치!"
    └── 골드/장비/재화 아이콘이 보스 위치에서 화면 하단으로 날아감 (LootDrop 연출)

[Phase C] 보상 요약 (2초)
    ├── 보상 목록 카운트업 (골드 XXXX, 경험치 XXXX, [장비])
    ├── 고급 장비 드롭 시 별도 카드 연출 (GachaEffect 재활용)
    └── "다음 챕터로!" 버튼 (또는 3초 후 자동 진행)

[Phase D] 다음 목표 갱신
    ├── GoalGuideWidget: "챕터 X+1 보스 클리어"
    ├── 보스까지 남은 스테이지 수 표시
    └── 추천 전투력 표시 (현재 CP vs 다음 보스 권장 CP)
```

### 2.4 강화 성공 후 UX (P1)

```
[EnhanceResultEffect.PlaySuccess() 호출]
    ↓
[기존] 성공 텍스트 + 오버레이 플래시
    ↓
[추가 Phase] 전후 비교 (0.5초 후)
    ├── StatCompareView.Show() 호출
    │   ├── 스탯: 공격력 120 → 138 (+18)
    │   ├── 스탯: 방어력 45 → 52 (+7)
    │   └── CP: 4,523 → 4,612 (+89)
    ├── "다음 강화: 주문서 3개 필요" 하단 텍스트
    └── 재화 부족 시 CurrencyShortageEvent 자동 발행
```

### 2.5 전직 후 UX (P1)

```
[JobAdvanceEffect 연출 종료]
    ↓
[추가 Phase] 새 스킬 시연 (2초)
    ├── 획득한 4개 스킬 아이콘 순차 등장 (0.3초 간격)
    ├── 각 스킬에 이름 + 한줄 설명
    ├── "스킬 장착하기" 버튼 → SkillPanel 바로가기
    └── CP 예상 변화: "새 스킬 장착 시 CP +2,400 예상"
```

---

## 3. "다음에 뭘 하지?" 제거 설계

### 3.1 현재 시스템 분석

| 위젯 | 역할 | 문제점 |
|------|------|--------|
| GoalGuideWidget | 가이드 퀘스트 체인 + 폴백 목표 표시 | 퀘스트 완료 후 다음 목표 전환이 있으나, 우선순위 로직이 단순 (스테이지→레벨→탑) |
| DailyChecklistWidget | 일일 퀘스트 목록 + 체크 연출 | 접이식이라 평소에 접혀있으면 존재감 부족 |
| RecommendationManager | 장비/동료/스탯/던전 추천 | 추천 결과를 보여주는 UI가 패널 내부에만 있음. 메인 화면에서 보이지 않음 |
| CpCounterWidget | CP 변동 카운트업 + 마일스톤 | CP "숫자"만 보임. "이 CP로 뭘 할 수 있는지"가 안 보임 |

### 3.2 "항상 다음 행동이 보이는" UI 계층 설계

```
┌─────────────────────────────────────────────────────┐
│  메인 전투 화면 (HUD 레이어)                          │
│                                                       │
│  [좌상단] CP 카운터                                   │
│    ├── CP: 12,345                                    │
│    ├── 다음 CP 마일스톤: 15,000 (진행도 바)            │
│    └── 해금 예고: "CP 15,000 → 아레나 은장 진입"       │
│                                                       │
│  [우상단] 목표 가이드 (GoalGuideWidget)               │
│    ├── 현재 가이드 퀘스트: "스킬 3개 레벨업"           │
│    ├── 진행도: 1/3 [====------]                       │
│    └── 보상: 루비 x500                                │
│                                                       │
│  [우중단] 빨간 점 알림 허브 (NEW)                      │
│    ├── [장비] "더 좋은 장비 장착 가능" (빨간 점)        │
│    ├── [스킬] "레벨업 가능한 스킬 2개" (빨간 점)        │
│    └── [동료] "새 동료 편성 가능" (빨간 점)             │
│                                                       │
│  [좌하단] 일일 체크리스트 (접이식)                     │
│    ├── 일일 미션 (3/11) — 미완료 시 펼쳐진 상태 유지    │
│    └── 완료 수에 따라 보상 단계 표시                    │
│                                                       │
│  [하단 탭바] 각 탭에 빨간 점 표시                      │
│    ├── [전투] [장비]* [동료]* [스킬]* [던전] [상점]     │
│    └── * = 빨간 점 (실행 가능한 액션 존재)              │
│                                                       │
│  [화면 중앙-하단] 컨텍스트 추천 배너 (NEW)             │
│    ├── 상황별 자동 표시 (3초마다 로테이션)              │
│    ├── "장비 강화 가능! 공격력 +15%" [바로가기]         │
│    ├── "던전 입장권 3개 있음! 골드 던전 추천" [바로가기]  │
│    └── "스탯 포인트 25 미분배!" [바로가기]               │
└─────────────────────────────────────────────────────┘
```

### 3.3 새로운 위젯: RedDotManager

```
클래스: RedDotManager : MonoBehaviour (싱글톤)
위치: Assets/Scripts/UI/RedDotManager.cs
역할: 전역 빨간 점(알림 뱃지) 상태를 관리

데이터 구조:
enum RedDotCategory { Equipment, Skill, Companion, Quest, Dungeon, Shop, StatPoint }

Dictionary<RedDotCategory, bool> _activeRedDots

갱신 트리거:
- EquipmentInventoryChangedEvent → 더 좋은 장비가 있는지 RecommendationManager에 질의
- SkillLevelUpEvent → 레벨업 가능한 스킬이 있는지 체크
- CompanionObtainedEvent → 편성되지 않은 동료가 있는지 체크
- LevelUpEvent → 미분배 스탯 포인트가 있는지 체크
- CurrencyChangedEvent → 던전 입장권 보유 여부

출력 이벤트:
- RedDotChangedEvent { Category, IsActive }
- 하단 탭바 버튼, 메인화면 알림 허브가 구독
```

### 3.4 새로운 위젯: ContextRecommendBanner

```
클래스: ContextRecommendBanner : MonoBehaviour
위치: Assets/Scripts/UI/ContextRecommendBanner.cs
역할: 메인 전투 화면에서 상황별 추천 액션을 로테이션 표시

동작:
1. 5초마다 RecommendationManager에 질의
2. 우선순위:
   (1) 미분배 스탯 포인트 > 0 → "스탯 포인트 XX 미분배!"
   (2) 더 좋은 장비 장착 가능 → "장비 교체로 CP +XXX 가능!"
   (3) 던전 입장권 보유 → "던전 추천: [골드 던전] — 골드 부족"
   (4) 레벨업 임박 (90%+) → "경험치 X만 더!"
   (5) 가이드 퀘스트 완료 가능 → "목표 달성 임박: [퀘스트명]"
3. 탭 시 해당 패널 바로가기
4. 연출: 좌→우 슬라이드인, 5초 유지, 좌→우 슬라이드아웃
```

### 3.5 CP 마일스톤 + 콘텐츠 게이팅 연동

현재 CpCounterWidget에 마일스톤 연출이 있으나 (1000 단위), "이 CP로 뭘 할 수 있는지"가 보이지 않는다.

```
CP 마일스톤 테이블 (SO 또는 GameConstants):

CP 5,000  → 아레나 입장
CP 10,000 → 챌린지 던전 입장
CP 15,000 → 아레나 은장
CP 25,000 → 탑 50층 권장
CP 50,000 → 길드 보스 참여
CP 100,000 → 아레나 금장

CpCounterWidget 확장:
- _nextMilestoneText: "다음: CP 15,000 → 아레나 은장"
- _milestoneProgressBar: 현재 CP / 다음 마일스톤 CP
- 마일스톤 도달 시 "아레나 은장 진입 가능!" 축하 배너 + SFX
```

---

## 4. "성장 욕구 자극" UI 패턴

### 4.1 빨간 점 (Red Dot) 시스템

```
위치: 하단 탭바 아이콘 우상단에 빨간 원 (6~8px)

규칙:
- 장비 탭: 현재 장착보다 CP 높은 장비 보유 시
- 동료 탭: 미편성 동료 중 현재 파티보다 CP 높은 동료 존재 시
- 스킬 탭: 스킬 포인트/재화로 레벨업 가능한 스킬 존재 시
- 던전 탭: 입장 가능한 던전 존재 시 (쿨다운 완료/입장권 보유)
- 상점 탭: 무료 아이템 수령 가능 시
- 퀘스트 탭: 보상 수령 가능한 퀘스트 존재 시

시각적 연출:
- 빨간 원 + 부드러운 펄스 (scale 1.0 → 1.2, 0.5초 주기)
- 숫자 뱃지 (3+ 시): 빨간 원 안에 흰색 숫자
```

### 4.2 CP 비교 유도

```
장비 목록 내:
┌──────────────────────────────┐
│ [아이콘] 전설 대검           │
│ ATK 245  DEF 12             │
│ ┌─────────────────┐         │
│ │ 장착 시 CP +523  │  ← 초록색 태그, 항상 보임
│ └─────────────────┘         │
└──────────────────────────────┘

동료 목록 내:
┌──────────────────────────────┐
│ [아이콘] 드래곤 나이트        │
│ ★★★★☆                     │
│ ┌─────────────────┐         │
│ │ 편성 시 CP +1,200 │  ← 초록색 태그
│ └─────────────────┘         │
└──────────────────────────────┘
```

### 4.3 랭킹 + CP 갭 표시

```
아레나 패널:
┌──────────────────────────────────────┐
│  내 순위: 247위  |  CP: 12,345       │
│  ─────────────────────────────────── │
│  [246위] 용사킴  CP: 12,890          │
│          ┌──────────────────┐        │
│          │ CP 545만 더 올리면 │  ← 빨간색 태그
│          │ 순위 상승 가능!   │
│          └──────────────────┘        │
│  ─────────────────────────────────── │
│  다음 등급: 은장 (CP 15,000)          │
│  [==========-----] 82%              │
└──────────────────────────────────────┘
```

### 4.4 잠금 해제 예고 (Teaser)

```
잠긴 콘텐츠에는 "해금 조건"을 항상 표시:

┌──────────────────────────────┐
│  🔒 2차 전직                 │
│  해금 조건: Lv.30            │
│  현재: Lv.27 (3레벨 남음)     │
│  [===========------] 90%    │
│                              │
│  해금 시 획득:                │
│  - 새 스킬 4개               │
│  - 전직 전용 장비 슬롯        │
│  - CP +3,000~5,000 예상      │
└──────────────────────────────┘

스킬 패널에서 잠긴 스킬:
┌──────────────────────────────┐
│  🔒 화염 폭발 (2차 전직 스킬)  │
│  "적 전체에 200% 화염 피해"    │
│  해금: 2차 전직 시 자동 획득   │
│  [회색 스킬 아이콘 미리보기]    │
└──────────────────────────────┘
```

### 4.5 진행도 바 (Collection Progress)

```
도감 패널:
┌──────────────────────────────────────┐
│  몬스터 도감: 23/50 (46%)            │
│  [==========----------] 46%         │
│  ─────────────────────────────────── │
│  50% 보상: 루비 x1,000              │
│  75% 보상: 전설 소환권 x1            │
│  100% 보상: 신화 장비 선택권          │
│  ─────────────────────────────────── │
│  다음 보상까지: 2마리 더 수집!        │
└──────────────────────────────────────┘
```

---

## 5. 일간 사이클 UX

### 5.1 접속 → 종료 전체 플로우

```
┌─────────────────────────────────────────────────────┐
│ [0:00] 앱 시작                                       │
│   ├── 로딩 화면 (LoadingScreen)                      │
│   └── 팁 텍스트: "장비 강화는 CP를 크게 올립니다!"     │
│                                                       │
│ [0:05] 오프라인 보상 팝업 (OfflineRewardPopup)        │
│   ├── "2시간 30분 동안 자동 사냥!"                    │
│   ├── 골드: 125,000 (카운트업)                       │
│   ├── 경험치: 45,000 (카운트업)                      │
│   ├── [수령] 버튼                                    │
│   └── [광고 시청으로 2배!] 버튼                       │
│                                                       │
│ [0:10] 출석 체크 팝업 (NEW — AttendancePopup)         │
│   ├── 7일 캘린더 (오늘 보상 하이라이트)               │
│   ├── 연속 출석 보너스 표시                           │
│   └── [수령] → 보상 아이콘 날아가는 연출              │
│                                                       │
│ [0:15] 메인 전투 화면 복귀                            │
│   ├── 일일 체크리스트 자동 펼침 (DailyChecklistWidget) │
│   │   ├── "일일 미션 (0/11)"                        │
│   │   ├── 첫 번째 미션 하이라이트: "몬스터 100마리"    │
│   │   └── x2 보너스 미션 황금 표시                    │
│   ├── 목표 가이드 (GoalGuideWidget)                  │
│   │   └── "가이드: [현재 퀘스트명]"                   │
│   └── 컨텍스트 배너: "던전 입장권 3개 보유!"           │
│                                                       │
│ [0:15~3:00] 능동 플레이 시간 (핵심 3분 사이클)         │
│                                                       │
│   ── 30초: 자동 전투 관전 + 몬스터 처치 ──            │
│   ├── 일일: "몬스터 100마리" 진행도 자동 갱신          │
│   └── 드롭 루트: 골드/경험치/장비 자동 수집            │
│                                                       │
│   ── 60초: 일일 던전 소화 ──                          │
│   ├── 컨텍스트 배너 탭 → 던전 패널                    │
│   ├── 추천 던전 하이라이트 (RecommendationManager)     │
│   ├── "골드 던전 3회 입장 가능" → 탭 → 자동 전투       │
│   ├── 던전 결과: 골드 +50,000 (카운트업)              │
│   └── 일일: "던전 3회 클리어" 체크!                   │
│                                                       │
│   ── 90초: 장비/스킬 강화 ──                          │
│   ├── 빨간 점 탭 → 장비 패널                         │
│   ├── "추천 장착" 원터치 → CP +500 카운트업            │
│   ├── 남은 재화로 강화 → 성공! CP +89                 │
│   ├── 스킬 탭 → 레벨업 가능 스킬 2개 → 탭탭 → 완료    │
│   └── 일일: "장비 강화 1회" 체크!  "스킬 레벨업 3회"   │
│                                                       │
│   ── 120초: 가챠/소환 ──                              │
│   ├── 무료 소환 1회 (일일 보너스)                     │
│   ├── 카드 연출 → 에픽 장갑!                          │
│   ├── 즉시 비교 → 원터치 장착 → CP +200               │
│   └── 일일: "소환 1회" 체크!                          │
│                                                       │
│   ── 150초: 아레나/PvP 소화 ──                        │
│   ├── 아레나 입장 → 자동 전투 3회                     │
│   ├── 결과: 승2 패1 → 순위 +15                       │
│   └── 일일: "아레나 참여 1회" 체크!                   │
│                                                       │
│   ── 180초 (3분): 일일 체크리스트 대부분 완료 ──       │
│   ├── 체크리스트: "일일 미션 (9/11)" → "보너스 보상!"  │
│   ├── 남은 2개: 시간 기반 (탑 도전, 길드 보스)         │
│   └── GoalGuideWidget: "다음 목표: 스테이지 5-BOSS"   │
│                                                       │
│ [3:00~5:00] 추가 플레이 (선택)                        │
│   ├── 탑 도전 → 현재 최고층 갱신 시도                  │
│   ├── 길드 보스 (쿨다운 맞으면)                       │
│   └── 도감 수집 확인                                  │
│                                                       │
│ [5:00] 이탈 시점                                      │
│   ├── 일일 체크리스트 전체 완료: "일일 미션 올클리어!"  │
│   ├── "오프라인 보상이 쌓이는 중..." 플로팅 텍스트      │
│   └── 다음 접속 유인: "XX시간 후 던전 입장권 충전!"     │
│                                                       │
│ [다음 접속] 사이클 반복                                │
└─────────────────────────────────────────────────────┘
```

### 5.2 접속 시 팝업 우선순위 큐

접속 시 여러 팝업이 경합하면 유저 경험이 나빠진다. 우선순위 큐로 순서 제어:

```
Priority 1: 서버 공지 (긴급 패치, 이벤트 종료 등)
Priority 2: 오프라인 보상 (OfflineRewardPopup) — 항상
Priority 3: 출석 체크 (오늘 첫 접속 시만)
Priority 4: 이벤트 배너 (시즌 이벤트 기간에만)
Priority 5: 복귀 유저 가이드 (3일+ 미접속 시만)

규칙:
- 동시에 최대 1개 팝업만 표시
- 닫기/수령 시 다음 팝업 자동 표시
- 모든 팝업 소진 후 전투 화면 복귀
- 총 팝업 시간 10초 이내 (체감 중요)
```

### 5.3 "3분 사이클" 핵심 원칙

방치형 RPG에서 일일 할당 시간은 **3분 이내**여야 한다. (리서치 기반)

| 시간 | 행동 | 터치 수 | 피드백 |
|------|------|---------|--------|
| 0~30초 | 오프라인 보상 수령 + 출석 | 2~3회 | 골드/경험치 카운트업 |
| 30~60초 | 던전 소화 (추천 버튼 1탭) | 3~5회 | 전투 결과 + 보상 |
| 60~90초 | 장비 정리 (추천 장착 1탭) | 2~3회 | CP 카운트업 |
| 90~120초 | 가챠 (무료 1회) | 1~2회 | 카드 연출 + 장착 |
| 120~150초 | 아레나 (자동 전투) | 2회 | 순위 변동 |
| 150~180초 | 일일 체크리스트 수령 | 1회 | 올클리어 연출 |

**총 터치 수: 11~16회.** 3분 안에 모든 일일 할당 완료.

### 5.4 주간 사이클

| 요일 | 특별 콘텐츠 | 유인 |
|------|-----------|------|
| 매일 | 일일 던전 3회, 아레나 3회, 무료 가챠 1회 | 기본 루프 |
| 월 | 주간 퀘스트 갱신 + 주간 보상 수령 | "지난주 보상!" 팝업 |
| 수 | 중간 보너스 (던전 입장권 2배) | "오늘만 던전 2배!" 배너 |
| 금 | 주말 이벤트 시작 (경험치 1.5배) | "주말 이벤트!" 배너 |
| 토~일 | 길드 보스 레이드, 특별 챌린지 | "주말 한정 보스!" |

### 5.5 장기 사이클 (월간/시즌)

```
[1주차] 가입 보너스 기간
  ├── 일일 보상 3~5배 (파격적)
  ├── 가이드 퀘스트 체인 집중
  └── "신규 유저 특별 가챠" (확률 UP)

[2~3주차] 핵심 성장기
  ├── 1~3차 전직 달성
  ├── 장비 시스템 풀 활용
  └── 아레나/탑 본격 진입

[4주차] 시즌 마감
  ├── 시즌 랭킹 보상
  ├── 배틀패스 마감 임박 알림
  └── "다음 시즌 예고" 배너

[매 시즌] 프레스티지/환생
  ├── 1차 환생 해금 (Lv.100+)
  ├── 영구 보너스 획득
  └── "더 강해져서 돌아오세요!" 연출
```

---

## 6. 구현 우선순위 로드맵

### P0 (즉시 구현 — 체감 임팩트 최대)

| ID | 태스크 | 파일 | 의존성 |
|----|--------|------|--------|
| GF-01 | GachaQuickCompare (원터치 장착) | UI/GachaQuickCompare.cs | GachaEffect, RecommendationManager |
| GF-02 | LevelUpEffect (레벨업 전체 연출) | Combat/LevelUpEffect.cs | LevelUpEvent |
| GF-03 | PostGrowthHint (전투 체감 배너) | UI/PostGrowthHint.cs | CombatStats |
| GF-04 | CP 마일스톤 + 콘텐츠 게이팅 표시 | UI/CpCounterWidget.cs 확장 | CpCounterWidget |
| GF-05 | 보스 처치 보상 연출 강화 | Combat/BossDefeatEffect.cs | StageManager |

### P1 (핵심 UX 보강)

| ID | 태스크 | 파일 | 의존성 |
|----|--------|------|--------|
| GF-06 | RedDotManager (빨간 점 시스템) | UI/RedDotManager.cs | RecommendationManager |
| GF-07 | ContextRecommendBanner (추천 배너) | UI/ContextRecommendBanner.cs | RecommendationManager |
| GF-08 | 강화 후 StatCompareView 자동 호출 | UI/EnhanceResultEffect.cs 확장 | StatCompareView |
| GF-09 | 전직 후 스킬 시연 팝업 | UI/JobAdvanceSkillPreview.cs | JobAdvanceEffect |
| GF-10 | 접속 팝업 우선순위 큐 | UI/LoginPopupQueue.cs | OfflineRewardPopup |

### P2 (완성도 향상)

| ID | 태스크 | 파일 | 의존성 |
|----|--------|------|--------|
| GF-11 | 잠금 해제 예고 UI | UI/UnlockPreviewWidget.cs | 각 패널 |
| GF-12 | 도감 진행도 바 + 보상 예고 | UI/CollectionProgressWidget.cs | CollectionBookManager |
| GF-13 | 아레나 CP 갭 표시 | UI/ArenaRankWidget.cs | ArenaManager |
| GF-14 | 주간/시즌 사이클 배너 | UI/SeasonBanner.cs | SeasonManager |
| GF-15 | DailyChecklistWidget 자동 펼침 로직 | UI/DailyChecklistWidget.cs 확장 | 기존 |

---

## 7. 핵심 설계 원칙 요약

### 원칙 1: "모든 획득은 즉시 적용으로 이어져야 한다"
- 가챠 → 비교 → 장착 → CP 변화 → 전투 체감. 중간에 끊기면 안 된다.
- **원터치 장착** 버튼이 핵심. 유저가 패널을 찾아다니게 하지 않는다.

### 원칙 2: "숫자만 올리지 말고, 체감을 만들어라"
- CP가 올라가면 반드시 전투에서 "데미지가 올라갔다"를 눈으로 확인시킨다.
- PostGrowthHint 배너 + 데미지 텍스트 크기 오버라이드가 핵심.

### 원칙 3: "항상 다음 행동이 보여야 한다"
- GoalGuideWidget (메인 목표) + ContextRecommendBanner (즉시 실행 가능) + RedDot (패널별 알림).
- 세 층의 "다음 행동 유도"가 항상 화면에 존재해야 한다.

### 원칙 4: "3분 안에 일일 할 일을 끝낼 수 있어야 한다"
- 방치형의 핵심 가치: "짧은 시간, 큰 보상, 명확한 성장".
- 추천 시스템 + 원터치 버튼으로 의사결정 시간 최소화.

### 원칙 5: "빠져나갈 때도 다음 접속 이유를 만들어라"
- 오프라인 보상 축적 표시, 던전 입장권 충전 카운트다운, 일일 미션 리셋 타이머.
- "돌아오면 보상이 기다리고 있다"는 약속.

---

## 참고: 최신 방치형 RPG UX 트렌드 (2025~2026)

1. **하이브리드 루프**: 순수 방치가 아닌, 짧은 능동 플레이 구간(3~5분)을 섞어 참여감을 높이는 추세.
2. **CP 게이팅**: 숫자가 아닌 "이 CP로 뭘 할 수 있는가"가 중요. 콘텐츠 해금과 연동.
3. **시각적 성장 = 감정적 투자**: 캐릭터 외형 변화, 이펙트 변화를 통해 성장을 "보여주는" UX.
4. **원터치 최적화**: "추천 장착", "추천 편성", "자동 분배" 등 1탭으로 최적 상태를 만드는 편의 기능.
5. **오프라인 보상 캡**: 의도적으로 오프라인 보상에 상한을 두어 "접속해서 수령 → 다시 쌓기" 사이클 유도.
6. **소셜 비교**: 랭킹, 길드, 친구 CP 비교가 성장 동기의 핵심 드라이버.

Sources:
- [Mobile Game Genre Breakdown 2026](https://www.game-developers.org/mobile-game-genre-breakdown-2026)
- [How to Make an Idle Game — GameAnalytics](https://www.gameanalytics.com/blog/how-to-make-an-idle-game-adjust)
- [Idle Game Design Principles — Eric Guan](https://ericguan.substack.com/p/idle-game-design-principles)
- ['키우기'가 달구는 게임 시장 — 방치형 RPG 인기 지속](https://v.daum.net/v/20260306090840275)
- [2023 방치형 RPG 트렌드 — Airbridge](https://www.airbridge.io/blog-ko/idle-rpg-trend-2023)
- [How To Increase Engagement and Monetization in Idle Games 2025](https://www.gamigion.com/idle/)
- [Idle Clicker Games: Best Practices — The Mind Studios](https://games.themindstudios.com/post/idle-clicker-game-design-and-monetization/)
- [Daily Login Rewards — MAF](https://maf.ad/en/blog/daily-login-rewards-engagement-retention/)
- [Mobile Game Onboarding UX — Medium](https://medium.com/@amol346bhalerao/mobile-game-onboarding-top-ux-strategies-that-boost-retention-6ef266f433cb)
- [Crafting Compelling Idle Games — Design the Game](https://www.designthegame.com/learning/tutorial/crafting-compelling-idle-games)
