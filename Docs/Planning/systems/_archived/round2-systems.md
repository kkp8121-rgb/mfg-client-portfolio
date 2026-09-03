# 라운드 2 기획서 — 탑 비밀 방 + 아레나 실전투 + 길드 실전투

> 작성일: 2026-03-17
> 작성자: Planner 에이전트
> 상태: 최종 (디자이너 + brainstormer 피드백 반영)

---

## 1. 탑 비밀 방 (카테고리 20, HR-01~HR-05)

### 1.1 개요

- **목적**: 탑 등반에 탐험 요소를 추가하여 "다음 층에 뭔가 있을지도"라는 기대감을 유발
- **핵심 가치**: 발견의 즐거움, 희귀 보상, 도감 수집 욕구
- **타겟 유저**: 중~후반 유저 (100층+)
- **세계관**: "탑의 벽 사이에 숨겨진 균열 — 특별한 자만이 감지할 수 있는 공간"

### 1.2 기존 코드 분석

| 파일 | 상태 | 내용 |
|------|------|------|
| `SecretRoomManager.cs` | 기본 골격 | 속클리어(30초)/무피격 2조건만 구현. 보상은 유물/코스튬 하드코딩. HashSet 기반 발견 기록. |
| `TowerGimmickSystem.cs` | 완성 | 10종 기믹, 층별 활성화, CombatStats modifier 연동 |
| `SecretRoomPopup.cs` | 완성 | 발견 알림 팝업 (DOTween 연출) |
| `TowerManager.cs` | 완성 | 층수 관리, 스탯 스케일링, 마일스톤 보상 |

**빠진 것:**
- HiddenRoomDataSO (비밀 방 데이터 정의)
- 비밀 방 10종 조건/보상 테이블
- 비밀 방 도감 UI
- 비밀 방 내부 콘텐츠 로직 (상인, 대장장이, 시련 등)
- 발견 연출 (글리치 + 포탈)
- 1일 제한 로직

### 1.3 핵심 메커니즘

#### 트리거 시스템 (확률 아닌 조건 기반 확정)

비밀 방은 **확률이 아닌 확정 조건**으로 발동한다. 조건을 충족하면 100% 발견.

| # | 조건 ID | 조건 설명 | 난이도 | 해금 최소 층 |
|---|---------|-----------|--------|------------|
| 1 | `speed_clear` | 보스 30초 내 클리어 | 하 | 1층 |
| 2 | `no_damage` | 현재 층 무피격 클리어 | 중 | 50층 |
| 3 | `streak_5` | 5층 연속 무피격 클리어 | 상 | 100층 |
| 4 | `set_equip` | 동일 세트 장비 4부위 이상 착용 | 중 | 100층 |
| 5 | `full_party` | 4인 파티 + 전원 Epic 이상 동료 | 중상 | 150층 |
| 6 | `prestige_floor` | 윤회 후 특정 층 도달 (50/100/150) | 중 | 50층 |
| 7 | `gimmick_clear` | 활성 기믹 3개 이상인 층 클리어 | 상 | 200층 |
| 8 | `costume_set` | 코스튬 세트 효과 발동 중 | 중 | 100층 |
| 9 | `collection_50` | 도감 50종 이상 수집 상태 | 중 | - |
| 10 | `boss_oneshot` | 보스 HP 50% 이상을 한 번의 스킬로 깎기 | 상 | 150층 |

**조건 판정 시점:**
- 보스 클리어 직후 (`StageManager.OnBossKilled` → `SecretRoomManager.CheckTriggers`)
- 일반 스테이지 클리어 시 (`StageChangedEvent`)

**1일 제한:** 최대 2회 발견. 자정(00:00) 초기화.

#### 비밀 방 등급 체계 (RoomRarity)

| 등급 | 방 유형 | 연출 색상 | 발견 연출 |
|------|---------|-----------|-----------|
| **Common** (일반) | 보물방, 치유의샘, 방랑상인 | 흰색/은색 글로우 | 글리치 0.3초 → 작은 포탈 → 팝업 |
| **Rare** (희귀) | 대장장이, 룬의방, 도감몬스터, 기억의방 | 파란색/보라색 글로우 | 글리치 0.5초 + 화면 흔들림 → 중간 포탈(파란빛) → 팝업 + 사운드 |
| **Epic** (에픽) | 시련의방, 유물의방, 비밀상점 | 금색/오렌지 글로우 | 글리치 0.8초 + 화면 흔들림 + 색상 왜곡 → 큰 포탈(금색, 파티클) → 팝업 + 화면 플래시 |

#### 비밀 방 10종

| # | 방 유형 | 등급 | 콘텐츠 | 보상 | 체류 시간 | 권장 VFX |
|---|---------|------|--------|------|-----------|----------|
| 1 | **보물방** | Common | 자동 루팅 (골드/소환권/재화 상자 5개) | 골드 x5, 소환권 1~2장 | 즉시 | coin_shower (금화 비) |
| 2 | **대장장이** | Rare | 장비 1개 무료 강화 (성공률 100%) or 장비 1개 승급 | 강화/승급 1회 | 선택 후 즉시 | anvil_spark (모루 불꽃) |
| 3 | **방랑 상인** | Common | 할인 상점 (정가 50% 할인, 3품목) | 할인 구매 기회 | 선택 후 즉시 | sparkle_soft (반짝임) |
| 4 | **도감 몬스터** | Rare | 희귀 도감 전용 몬스터 등장 (전투) | 도감 등록 + 유물 조각 | 30초 전투 | portal_purple (보라 포탈) |
| 5 | **시련의 방** | Epic | 강화된 엘리트 몬스터 3웨이브 | 유물 확정 1개 + 루비 50 | 60초 전투 | fire_circle (화염 원형) |
| 6 | **기억의 방** | Rare | 이전에 클리어한 보스 재전투 (강화) | 전용 코스튬 조각 | 60초 전투 | ghost_mist (유령 안개) |
| 7 | **치유의 샘** | Common | HP 전회복 + 30초간 스탯 20% 증가 버프 | 버프 (30초) | 즉시 | heal_fountain (치유 분수) |
| 8 | **룬의 방** | Rare | 랜덤 각인 1개 획득 | 각인 1개 | 즉시 | rune_glow (룬 발광) |
| 9 | **유물의 방** | Epic | 유물 2개 중 택 1 선택 | 유물 1개 (Epic+) | 선택 후 즉시 | artifact_beam (유물 광선) |
| 10 | **비밀 상점** | Epic | 프리미엄 품목 (루비 전용, 정가) | 한정판 코스튬/펫/유물 | 선택 후 즉시 | portal_gold (금색 포탈) |

#### 리스크-리워드 선택 (brainstormer 제안 반영)

비밀 방 진입 시 **2가지 모드** 중 선택:

| 모드 | 내용 | 보상 배율 | 실패 시 |
|------|------|-----------|---------|
| **안전 모드** | 일반 난이도 콘텐츠 | x1.0 | 없음 (보상 확정) |
| **도전 모드** | 강화 몬스터/가격 상승/제한 시간 절반 | x2.0 | 전투형: 실패 시 보상 없음. 선택형: 가격 1.5배 |

**적용 방식 (유형별):**
- **전투형** (도감몬스터, 시련, 기억): 도전 모드 = 몬스터 HP/ATK x1.5, 보상 x2
- **선택형** (대장장이, 상인, 유물, 비밀상점): 도전 모드 = 상위 등급 품목 추가, 가격 x1.5
- **즉시형** (보물방, 치유의샘, 룬의방): 도전 모드 = 미니 퀴즈/QTE 성공 시 보상 x2, 실패 시 x0.5

**UI:** 진입 팝업에 "안전 진입" (녹색) / "도전 진입" (빨간색, 해골 아이콘) 2버튼

**방 선택 로직:** 조건마다 출현 가능한 방 유형이 다르다 (가중치 테이블).

```
speed_clear  → 보물방(40%) / 치유의샘(30%) / 방랑상인(30%)
no_damage    → 시련의방(30%) / 유물의방(30%) / 룬의방(40%)
streak_5     → 유물의방(40%) / 비밀상점(30%) / 기억의방(30%)
set_equip    → 대장장이(50%) / 룬의방(30%) / 보물방(20%)
full_party   → 시련의방(40%) / 유물의방(40%) / 기억의방(20%)
prestige     → 기억의방(40%) / 비밀상점(30%) / 보물방(30%)
gimmick      → 시련의방(50%) / 유물의방(30%) / 룬의방(20%)
costume_set  → 비밀상점(40%) / 대장장이(30%) / 보물방(30%)
collection   → 도감몬스터(50%) / 유물의방(30%) / 보물방(20%)
boss_oneshot → 유물의방(40%) / 시련의방(30%) / 비밀상점(30%)
```

### 1.4 데이터 설계

#### HiddenRoomDataSO

```csharp
[CreateAssetMenu(menuName = "MkLike/Data/HiddenRoomData")]
public class HiddenRoomDataSO : ScriptableObject
{
    [System.Serializable]
    public class RoomTypeData
    {
        public string roomTypeId;        // "treasure", "blacksmith", etc.
        public string displayName;       // "보물방"
        public string description;       // "금화와 소환권이 가득한 방"
        public Sprite icon;
        public RoomRarity rarity;        // Common, Rare, Epic
        public RoomContentType contentType; // Instant, Combat, Selection
        public float combatDuration;     // 전투형 방의 제한 시간
        public float glitchDuration;     // 등급별 글리치 연출 시간 (0.3/0.5/0.8)
        public Color portalColor;        // 등급별 포탈 색상
        public RewardEntry[] rewards;
    }

    [System.Serializable]
    public class TriggerCondition
    {
        public string conditionId;       // "speed_clear"
        public string displayName;       // "속클리어"
        public string hintTextPartial;   // 1단계 힌트: "보스를 빠르게..."
        public string hintTextFull;      // 2단계 힌트: "보스를 30초 내에 처치하라"
        public int hintThreshold1;       // 1단계 해금 관련 행동 횟수 (3)
        public int hintThreshold2;       // 2단계 해금 관련 행동 횟수 (10)
        public int minFloor;             // 해금 최소 층
        public RoomWeight[] roomWeights; // 방 유형별 가중치
    }

    // 도전 모드 설정
    [System.Serializable]
    public class ChallengeConfig
    {
        public float monsterStatMultiplier = 1.5f;  // 전투형: 몬스터 스탯 배율
        public float rewardMultiplier = 2.0f;       // 보상 배율
        public float priceMultiplier = 1.5f;        // 선택형: 가격 배율
        public float timeLimitMultiplier = 0.5f;    // 전투형: 제한 시간 배율
    }

    [System.Serializable]
    public class RoomWeight
    {
        public string roomTypeId;
        public float weight; // 0~1
    }

    [System.Serializable]
    public class RewardEntry
    {
        public CurrencyType currencyType;
        public long amount;
        public string itemId; // 비재화 아이템
    }

    public RoomTypeData[] roomTypes;
    public TriggerCondition[] triggerConditions;
    public int maxDailyDiscoveries = 2;
}

public enum RoomContentType
{
    Instant,    // 즉시 보상 (보물방, 치유의샘, 룬의방)
    Combat,     // 전투 (도감몬스터, 시련, 기억)
    Selection   // 선택 (대장장이, 상인, 유물택1, 비밀상점)
}

public enum RoomRarity
{
    Common,     // 보물방, 치유의샘, 방랑상인 — 은색 글로우
    Rare,       // 대장장이, 룬의방, 도감몬스터, 기억의방 — 보라색 글로우
    Epic        // 시련의방, 유물의방, 비밀상점 — 금색 글로우
}
```

#### SecretRoomManager 확장 필드

```csharp
// 추가 필드
[SerializeField] private HiddenRoomDataSO _roomData;

private int _dailyDiscoveries;
private string _lastDailyReset;

// 무피격 연속 층수 트래킹
private int _consecutiveNoDamageFloors;

// 현재 활성 비밀 방 (진입 대기 중)
private string _pendingRoomTypeId;
private int _pendingFloor;
```

#### 비밀 방 도감 이벤트

```csharp
public struct SecretRoomEnteredEvent : IEvent
{
    public string RoomTypeId;
    public string ConditionId;
    public int Floor;
}

public struct SecretRoomCompletedEvent : IEvent
{
    public string RoomTypeId;
    public int Floor;
    public bool IsSuccess; // 전투형: 클리어 여부
}
```

### 1.5 UI/UX 플로우

```
[전투 중] 조건 충족
    ↓
[화면 글리치 이펙트] (등급별: Common 0.3초 / Rare 0.5초+흔들림 / Epic 0.8초+흔들림+색상왜곡)
    ↓
[포탈 오브젝트 생성] (등급별 색상: 은색/보라/금색, 크기 차등)
    ↓
[SecretRoomPopup] "비밀 방을 발견했습니다!" + 등급 색상 배경
    ├── [안전 진입] (녹색 버튼) — 일반 보상
    ├── [도전 진입] (빨간 버튼, 해골 아이콘) — 보상 x2, 난이도 UP
    └── [무시] (회색)
    ↓ (진입 선택)
[화면 전환] (페이드 or 줌인)
    ↓
[비밀 방 오버레이 UI] — 유형별 콘텐츠
    ├── Instant: 보상 표시 → 수령 → 복귀
    ├── Combat: 전투 시작 → 결과 → 보상 → 복귀
    └── Selection: 선택지 표시 → 선택 → 결과 → 복귀
    ↓
[복귀] 일반 전투 재개
```

**비밀 방 도감:** CollectionBookPanel에 "비밀" 탭 추가
- 발견한 방: 아이콘 + 이름 + 발견 층 + 발견 일시 + 등급 색상 테두리
- 미발견 방: "???" + 잠금 아이콘
- 조건별 진행도: "속클리어: 3/10회 발견"

#### 힌트 시스템 (brainstormer 제안 반영)

미발견 비밀 방의 조건을 **단계적으로 공개**하여 도전 동기를 부여한다:

| 힌트 단계 | 조건 | 표시 내용 |
|-----------|------|-----------|
| **0단계** (미발견) | 기본 상태 | "???" + 잠금 아이콘 |
| **1단계** (조건 힌트) | 해당 조건 관련 행동 3회 이상 | 흐릿한 텍스트: "보스를 빠르게..." (조건의 앞 부분만) |
| **2단계** (상세 힌트) | 해당 조건 관련 행동 10회 이상 | 선명한 텍스트: "보스를 30초 내에 처치하라" (전체 조건) |
| **3단계** (발견) | 조건 달성 | 완전 공개 + 등급 색상 + 보상 정보 |

**"관련 행동"이란:**
- `speed_clear`: 보스 전투 횟수
- `no_damage`: 무피격 클리어 시도 횟수
- `streak_5`: 연속 무피격 최대 기록
- `set_equip`: 세트 장비 착용 횟수
- etc.

**데이터:** `TriggerCondition.hintText` (기존)를 `hintTextPartial`(1단계)과 `hintTextFull`(2단계)로 분리.

**UI:** 미발견 방 카드의 텍스트 alpha를 단계별로 조절 (0단계: 0, 1단계: 0.3, 2단계: 0.7, 3단계: 1.0)

### 1.6 밸런스 수치

| 항목 | 수치 | 근거 |
|------|------|------|
| 1일 최대 발견 | 2회 | 희소성 유지. 일간 사이클에서 "오늘 비밀 방 찾았나?" 체크 동기 |
| 속클리어 기준 | 30초 | 기존 코드 유지. 중반 유저 기준 보스 평균 처치 40~50초 |
| 무피격 연속 | 5층 | 10층은 너무 어려움. 5층이면 집중 플레이 2~3분에 달성 가능 |
| 시련의 방 보상 | 유물 1개 + 루비 50 | 유물 가챠 1회분(루비 100) 대비 50% 할인 느낌 |
| 보물방 골드 | 현재 층 x 100 | 일반 사냥 5분치 수준 |
| 대장장이 강화 | 성공률 100% 1회 | 후반 강화 성공률 30%일 때 기대값 = 3회분 재화 절약 |

### 1.7 구현 태스크

| ID | 태스크 | 의존성 | 파일 스코프 |
|----|--------|--------|------------|
| HR-01 | HiddenRoomDataSO + RoomContentType enum | - | `Data/HiddenRoomDataSO.cs` |
| HR-02 | SecretRoomManager 확장 (10종 조건, SO 기반, 일일 제한) | HR-01 | `Dungeon/SecretRoomManager.cs` |
| HR-03 | 비밀 방 콘텐츠 로직 (Instant/Combat/Selection) | HR-02 | `Dungeon/SecretRoomContent.cs` |
| HR-04 | 비밀 방 도감 (CollectionBookPanel 탭 추가) | HR-02, CB-03 | `UI/CollectionBookPanel.cs` 확장 |
| HR-05 | 발견 연출 (글리치 + 포탈 + 진입) | HR-02 | `Combat/SecretRoomEffect.cs`, `UI/SecretRoomPopup.cs` 확장 |

### 1.8 의존성

- `TowerManager` (층수, 보스 클리어 이벤트)
- `TowerGimmickSystem` (기믹 3개 조건)
- `CollectionBookManager` (도감 50종 조건, 도감 등록)
- `CombatStats` (무피격 트래킹)
- `CostumeManager` (코스튬 세트 조건)
- `PrestigeSystem` (윤회 조건)

---

## 2. 아레나 실전투 (카테고리 21, AP-01~AP-05)

### 2.1 개요

- **목적**: 오프라인 PvP에 "실전투 체감"을 부여하여 경쟁 동기를 극대화
- **핵심 가치**: 경쟁, 전략적 편성, 성장 검증
- **타겟 유저**: 중반 이후 (100층+, 아레나 해금)
- **세계관**: "탑의 투기장 — 등반자들이 실력을 겨루는 전투의 전당"

### 2.2 기존 코드 분석

| 파일 | 상태 | 내용 |
|------|------|------|
| `ArenaManager.cs` | 기본 골격 | ELO(K=32), 일일 5회, 시즌 보상(등급별), 전투 호출 |
| `ArenaBattleSimulator.cs` | 완성 | 0.5초 턴제, 직업상성 +10%, 크리/회피, 60초 타임아웃 |
| `ArenaAIClone.cs` | 기본 | ELO 기반 더미 스탯 생성, 더미 이름 풀 16개 |
| `ArenaResultPopup.cs` | 완성 | 승/패, ELO 변동, 상대 정보 표시 |

**빠진 것:**
- 플레이어 스냅샷 시스템 (현재 ±20% 랜덤 상대)
- AI 전투 시각화 (현재 즉시 결과만)
- 전적 기록 시스템
- 직업별 AI 패턴 (현재 단순 턴제)
- 동료 포함 전투 옵션
- 매칭 연출

### 2.3 핵심 메커니즘

#### 스냅샷 시스템

서버가 없으므로 **로컬 스냅샷 풀** 방식을 사용한다:

1. **자기 스냅샷 자동 저장**: 플레이어 스탯/장비/스킬/동료가 변경될 때마다 로컬에 스냅샷 저장
2. **NPC 클론 풀 확장**: ELO 기반 더미 대신 **사전 정의된 "네임드 라이벌" 30명**을 생성
   - 각 라이벌은 고유 이름, 직업, 장비 세트, 전투 스타일이 있음
   - ELO 구간별 10명씩 배치 (브론즈~골드: 10명, 플래티넘~다이아: 10명, 마스터: 10명)
3. **스케일링**: 라이벌의 기본 스탯에 플레이어 스탯의 0.85~1.15 배율을 적용하여 적정 난이도 유지

```csharp
[System.Serializable]
public class ArenaRival
{
    public string rivalId;
    public string displayName;
    public string title;        // "불꽃의 검성", "얼음의 마도사"
    public string jobId;        // "warrior", "archer", "mage"
    public int jobTier;         // 1~4
    public int eloMin;          // 이 라이벌이 출현하는 최소 ELO
    public int eloMax;          // 최대 ELO
    public float statScale;     // 플레이어 대비 스탯 배율 (0.9~1.1)

    // AI 행동 패턴
    public AIBehavior behavior;  // Aggressive, Defensive, Balanced, Berserk

    // 동료 정보 (1+3 전투 시)
    public string[] companionIds; // 최대 3명
}

public enum AIBehavior
{
    Aggressive,   // 공격 우선, 크리 특화
    Defensive,    // 방어/회피 특화, 지구전
    Balanced,     // 균형
    Berserk       // HP 40% 이하 시 공격력 50% 증가
}
```

#### 전투 규칙

| 항목 | 수치 | 비고 |
|------|------|------|
| 시간 제한 | 60초 | 기존 유지 |
| 턴 간격 | 0.5초 | 기존 유지 (120턴) |
| 직업 상성 | +10% 데미지 | 기존 유지 |
| 선공 판정 | 공격 속도 높은 쪽 | 기존 유지 |
| 타임아웃 | HP 비율 높은 쪽 승리 | 기존 유지 |
| **신규: AI 행동** | 4종 패턴 | Aggressive/Defensive/Balanced/Berserk |
| **신규: 배속** | 1x/2x/스킵 | 전투 시각화 시 배속 옵션 |
| **신규: 연승 보너스** | 3연승마다 보너스 코인 +50% | 연속 도전 동기 |
| **신규: 난이도 선택** | 3명 제시 (쉬움/보통/어려움) | 리스크-리워드 전략 선택 |

#### 매치메이킹 난이도 선택 (디자이너 제안 반영)

매칭 시 3명의 라이벌을 난이도별로 제시하여 유저가 선택한다:

| 난이도 | ELO 차이 | 승률 기대값 | 보상 배율 | 스탯 배율 |
|--------|----------|------------|-----------|-----------|
| **쉬움** | 내 ELO - 150~200 | 70~80% | x0.8 | 0.85~0.95 |
| **보통** | 내 ELO ± 50 | 45~55% | x1.0 | 0.95~1.05 |
| **어려움** | 내 ELO + 150~200 | 20~30% | x1.5 | 1.05~1.15 |

**구현:** `ArenaManager.GetMatchCandidates()` → 3명의 `ArenaRival` 반환 → UI에서 선택 → `Fight(selectedRival)`

#### AI 행동 패턴 (ArenaBattleSimulator 확장)

```
Aggressive:
  - CritRate +5%, CritDmg +0.2
  - Def -10%
  - 매 3턴마다 "강타" (데미지 x1.5, 다음 턴 쉼)

Defensive:
  - DodgeRate +10%, Def +15%
  - Atk -10%
  - HP 50% 이하 시 "방어 자세" (3턴간 받는 데미지 -30%)

Balanced:
  - 변동 없음 (기본 스탯 그대로)

Berserk:
  - HP 40% 이하 시 Atk +50%, Def -20%
  - 매 턴 HP 1% 자해 데미지
```

#### 전적 기록

```csharp
[System.Serializable]
public struct ArenaRecord
{
    public string opponentName;
    public string opponentJob;
    public int opponentElo;
    public bool isVictory;
    public int eloChange;
    public int playerDamage;
    public int opponentDamage;
    public string timestamp; // ISO 8601
}

// ArenaManager 내부
private List<ArenaRecord> _records = new(); // 최근 50전 보관
```

### 2.4 데이터 설계

#### ArenaRivalDataSO

```csharp
[CreateAssetMenu(menuName = "MkLike/Data/ArenaRivalData")]
public class ArenaRivalDataSO : ScriptableObject
{
    public ArenaRival[] rivals; // 30명

    public ArenaRival GetRandomRival(int playerElo)
    {
        // playerElo ±200 범위 내 라이벌 중 랜덤 선택
    }
}
```

### 2.5 UI/UX 플로우

```
[아레나 패널]
    ├── [매칭] 버튼 클릭
    │   ↓
    │   [매칭 연출] (1~2초) — "상대를 찾고 있습니다..." 스피너
    │   ↓
    │   [상대 선택 화면] — 3명 라이벌 카드 (쉬움/보통/어려움)
    │   ├── 각 카드: 초상화 + 이름 + 직업 + ELO + 보상배율
    │   ├── 쉬움: 녹색 테두리, "보상 x0.8"
    │   ├── 보통: 파란 테두리, "보상 x1.0"
    │   └── 어려움: 빨간 테두리, "보상 x1.5"
    │   ↓ (카드 선택)
    │   [VS 화면] 플레이어 vs 선택한 라이벌 (이름, 직업, ELO, AI유형)
    │   ↓
    │   [전투 시작] 버튼
    │   ↓
    │   [전투 시각화] — 좌우 캐릭터 + HP바 + 턴 로그
    │   ├── 1x / 2x / 스킵 버튼
    │   └── 60초 타이머
    │   ↓
    │   [ArenaResultPopup] 확장 — 승/패 + ELO + 보상(난이도 배율 적용) + 전적 추가
    │
    ├── [전적] 탭 — 최근 50전 목록
    │   └── 승률, 연승 기록, 등급별 전적
    │
    └── [시즌] 탭 — 현재 시즌 정보 + 보상 미리보기
```

#### 전투 시각화 (간소화 방식)

실시간 캐릭터 전투가 아닌 **"전투 로그 + HP바 애니메이션"** 방식:

```
┌─────────────────────────────────────┐
│  [플레이어 초상화]   VS   [라이벌 초상화]  │
│  HP ████████░░ 75%    HP ██████░░░░ 55%  │
│  ATK 150  DEF 80      ATK 140  DEF 90    │
│                                           │
│  ──────── 전투 로그 ────────               │
│  ▶ 플레이어의 공격! 45 데미지               │
│  ▶ 라이벌의 방어 자세!                     │
│  ▶ 플레이어의 크리티컬! 78 데미지          │
│  ▶ 라이벌의 반격! 32 데미지                │
│                                           │
│  [1x] [2x] [스킵]     ⏱ 42초 남음        │
└─────────────────────────────────────┘
```

- HP바는 DOTween으로 부드럽게 감소
- 크리티컬 시 텍스트 크기 확대 + 색상 변경
- 스킵 시 즉시 결과 산출

### 2.6 밸런스 수치

| 항목 | 수치 | 근거 |
|------|------|------|
| 일일 도전 횟수 | 5회 | 기존 유지. 방치형 적정 수준 |
| ELO K-factor | 32 | 기존 유지. 변동성 적당 |
| 연승 보너스 | 3연승: +50% 코인 | 연속 도전 동기. 5연승 이상은 운 의존이므로 3연승이 적절 |
| 라이벌 스탯 배율 | 0.85~1.15 | 절대 못 이기는 상대 없음. 실력(편성)으로 뒤집기 가능 |
| 시즌 기간 | 2주 | 기존 SeasonManager 연동 |
| 전적 보관 | 최근 50전 | 메모리 부담 없는 수준 |

### 2.7 구현 태스크

| ID | 태스크 | 의존성 | 파일 스코프 |
|----|--------|--------|------------|
| AP-01 | ArenaRivalDataSO + ArenaRival 구조 + 라이벌 30명 데이터 | - | `Data/ArenaRivalDataSO.cs` |
| AP-02 | ArenaBattleSimulator 확장 (4종 AI 패턴, 강타/방어자세/버서크) | AP-01 | `Core/ArenaBattleSimulator.cs` |
| AP-03 | ArenaManager 확장 (라이벌 매칭, 전적 기록, 연승 보너스) | AP-01, AP-02 | `Combat/ArenaManager.cs` |
| AP-04 | 아레나 전투 시각화 UI (HP바 + 로그 + 배속) | AP-02 | `UI/ArenaBattlePanel.cs` |
| AP-05 | 전적/시즌 UI (전적 탭, 시즌 보상 미리보기) | AP-03 | `UI/ArenaPanel.cs` 확장 |

### 2.8 의존성

- `ArenaManager` (ELO, 도전 횟수, 시즌 보상)
- `ArenaBattleSimulator` (턴 기반 전투)
- `CombatStats` (플레이어 스탯 스냅샷)
- `JobSystem` (직업 정보)
- `SeasonManager` (시즌 기간)

---

## 3. 길드 실전투 (카테고리 22, GB-01~GB-05)

### 3.1 개요

- **목적**: 길드 콘텐츠에 실제 전투 경험을 추가하여 길드 참여율과 소속감을 높임
- **핵심 가치**: 협동, 기여도 경쟁, 보스 패턴 공략
- **타겟 유저**: 길드 가입 유저 전체
- **세계관**: "탑의 심층에 봉인된 고대 수호자 — 길드원들이 힘을 합쳐 봉인을 해제한다"

### 3.2 기존 코드 분석

| 파일 | 상태 | 내용 |
|------|------|------|
| `GuildBossSystem.cs` | 기본 골격 | 주 2회, 60초 DPS 시뮬레이션, 누적 데미지, 보상 수령 |
| `GuildBossBattleSimulator.cs` | 기본 | ATK/크리/공속 기반 단순 DPS 계산 (for루프) |
| `GuildBossResultPopup.cs` | 완성 | 데미지 카운트업, 클리어/실패, 기여도 표시 |

**빠진 것:**
- 3페이즈 보스 패턴 (현재 단일 DPS 계산)
- 보스 전용 스킬/패턴 시스템
- 토벌전 (웨이브 처치) 모드
- 기여도 기반 보상 분배 (현재 길드 경험치만)
- 전투 시각화
- 개인 순위 UI

### 3.3 핵심 메커니즘

#### 길드 보스 3페이즈

| 페이즈 | HP 구간 | 보스 행동 | 유저 대응 |
|--------|---------|-----------|-----------|
| **1: 각성** | 100%~70% | 일반 공격 + 방어력 보통 | DPS 체크. 기본 전투 |
| **2: 분노** | 70%~40% | 공격력 +30%, 3초마다 전체 공격 | 회피/방어 스탯이 중요해짐 |
| **3: 폭주** | 40%~0% | 방어력 -50%, 공격력 +80%, 자폭 카운트다운(30초) | 화력 집중 타임. 시간 내 처치 못하면 강제 종료 |

**페이즈 전환 시:**
- 1→2: "보스가 분노합니다!" + 화면 붉은색 플래시
- 2→3: "보스가 폭주합니다!" + 화면 흔들림 + 카운트다운 시작

#### 분노 게이지 시스템 (brainstormer 제안 반영)

보스에게 HP 외에 **분노 게이지(Rage Gauge)** 를 추가한다. 전투에 전략적 긴장감을 부여하는 서브 메커니즘.

| 항목 | 수치 | 설명 |
|------|------|------|
| 최대치 | 100 | 게이지 풀 차면 전멸기 발동 |
| 피격 시 증가 | +2/턴 | 플레이어가 보스를 때릴 때마다 축적 |
| 크리티컬 피격 시 | +5/턴 | 크리티컬은 보스를 더 자극 |
| 비공격 턴 감소 | -3/턴 | 플레이어가 스턴/회피 등으로 공격 안 하면 감소 |
| MAX 도달 시 | **전멸기** | ATK x 5 전체 공격 → 사실상 즉사. 게이지 0으로 리셋 |
| 페이즈 2 보너스 | 증가량 x1.3 | 2페이즈에서 분노가 더 빨리 찬다 |
| 페이즈 3 보너스 | 증가량 x1.6 | 3페이즈는 더 위험 |

**전략적 의미:**
- 무조건 DPS만 올리면 분노 게이지가 빨리 차서 전멸당함
- 가끔 "공격을 멈추는" 타이밍 판단이 필요 (시뮬레이션에서는 턴 스킵 확률로 반영)
- 크리 특화 빌드는 리스크가 높아짐 → 빌드 다양성 유발

**UI:** 보스 HP바 위에 주황색 분노 게이지 바. MAX 접근 시 빨간색으로 변하며 깜빡임.

#### 길드 보스 전용 스킬 6종

| # | 스킬 | 페이즈 | 효과 | 쿨타임 | 권장 VFX |
|---|------|--------|------|--------|----------|
| 1 | **일격** | 1,2,3 | 단일 대상 ATK x 2 데미지 | 5초 | slash_heavy (빨간 참격) |
| 2 | **포효** | 2,3 | 전체 공격 (ATK x 0.5) + 3초 스턴 | 8초 | shockwave_ring (확산 링) |
| 3 | **방어 강화** | 1,2 | 5초간 받는 데미지 -50% | 15초 | shield_bubble (방어막) |
| 4 | **광역 파동** | 2,3 | 화면 전체 ATK x 1.5 + 넉백 | 12초 | explosion_radial (방사형 폭발) |
| 5 | **자폭 준비** | 3 only | 30초 카운트다운 시작. 종료 시 전멸 | 1회 | charge_aura (점점 커지는 오라) |
| 6 | **전멸기** | 분노MAX | ATK x 5 전체, 게이지 리셋 | 분노 게이지 | screen_flash_red + screen_shake |

#### 시뮬레이션 방식 (강화된 GuildBossBattleSimulator)

실시간 전투가 아닌 **강화된 턴제 시뮬레이션**:

```csharp
public static class GuildBossBattleSimulator
{
    public struct PhaseConfig
    {
        public float HpThreshold;       // 0.7, 0.4, 0
        public float AtkMultiplier;
        public float DefMultiplier;
        public float SkillFrequency;    // 스킬 사용 빈도 (0~1)
        public float RageGainMultiplier; // 분노 게이지 증가 배율 (1.0, 1.3, 1.6)
    }

    public struct RageGaugeState
    {
        public float Current;           // 0~100
        public float Max;               // 100
        public float GainPerHit;        // +2
        public float GainPerCrit;       // +5
        public float DecayPerIdleTurn;  // -3
    }

    public struct EnhancedRaidResult
    {
        public long TotalDamage;
        public bool IsBossDefeated;
        public float DamagePercent;
        public float Duration;
        public int PhaseReached;        // 1, 2, or 3
        public int BossSkillsUsed;      // 보스가 사용한 스킬 수
        public int PlayerHitsReceived;  // 플레이어가 받은 피격 수
        public int RageUltiUsed;        // 전멸기 발동 횟수
        public float MaxRageReached;    // 최대 분노 게이지 도달치
        public List<BattleLogEntry> BattleLog; // 전투 로그 (시각화용)
    }

    public struct BattleLogEntry
    {
        public float Timestamp;
        public string ActorName;  // "Player" or "Boss"
        public string ActionName;
        public long Damage;
        public bool IsCritical;
        public int PhaseAtTime;
        public float RageAtTime;  // 해당 시점 분노 게이지
    }
}
```

**시뮬레이션 흐름:**
1. 0.5초 간격으로 턴 진행
2. 플레이어: ATK/크리/공속 기반 공격
3. 보스: 페이즈별 스킬 테이블에서 쿨타임 기반 스킬 선택
4. 스킬 피격 시 플레이어 HP 감소 (HP 0 = 강제 종료)
5. 페이즈 전환 시 보스 스탯 변경
6. 3페이즈 자폭 카운트다운 종료 시 강제 종료

#### 토벌전 (길드 웨이브 모드)

- **주기**: 주 1회 (보스와 별도)
- **시간**: 제한 시간 180초
- **규칙**: 웨이브별 몬스터 N마리 등장, 처치 수 카운트
- **웨이브 구성**: 5웨이브 (일반 → 엘리트 → 일반 → 보스급 → 최종 보스)
- **결과**: 개인 처치 수 → 길드 총 처치 수 합산
- **보상**: 길드 총 처치 수 마일스톤별 보상

**시뮬레이션 방식:** 기존 StageManager 전투 루프와 유사하되, 시간 제한 + 처치 카운트 모드.

```csharp
public struct RaidWaveResult
{
    public int TotalKills;
    public int WavesCleared;
    public float Duration;
    public long DamageDealt;
}
```

#### 보상 분배 시스템

**기여도 = 개인 데미지 / 보스 최대 HP**

| 기여도 순위 | 보상 배율 | 추가 보상 |
|-------------|-----------|-----------|
| 1위 | x1.5 | MVP 칭호 (1주간) |
| 2~3위 | x1.2 | - |
| 4~10위 | x1.0 | - |
| 11위~ | x0.8 | - |
| **전원** | +기본 보상 | 보스 클리어 시 전원 기본 보상 |

**공정성 장치:**
- **기여도 상한**: 1인당 최대 기여도 40% 제한 (고레벨 독식 방지)
- **참여 보상**: 도전만 해도 기본 보상의 30% 지급
- **약자 보호**: 길드 평균 레벨의 50% 이하 유저는 데미지 x1.5 보정

**보상 내용:**

| 보상 | 보스 클리어 기본 | 기여도 보너스 |
|------|-----------------|--------------|
| 골드 | 10,000 x 길드레벨 | 기본 x 배율 |
| 루비 | 50 | 기본 x 배율 |
| 유물 조각 | 3개 | 1위: +2개 |
| 길드 경험치 | 500 x 길드레벨 | - (전원 동일) |
| 길드 코인 | 100 | 기본 x 배율 |

### 3.4 데이터 설계

#### GuildBossDataSO

```csharp
[CreateAssetMenu(menuName = "MkLike/Data/GuildBossData")]
public class GuildBossDataSO : ScriptableObject
{
    [System.Serializable]
    public class BossPhaseData
    {
        public float hpThreshold; // 0.7, 0.4
        public float atkMultiplier;
        public float defMultiplier;
        public string phaseName; // "각성", "분노", "폭주"
        public BossSkillEntry[] phaseSkills;
    }

    [System.Serializable]
    public class BossSkillEntry
    {
        public string skillId;
        public string displayName;
        public float damageMultiplier;
        public float cooldown;
        public bool isAoe;          // 전체 공격 여부
        public float stunDuration;  // 스턴 시간 (0이면 없음)
        public float defReduction;  // 방어력 감소 (0이면 없음)
    }

    [System.Serializable]
    public class ContributionReward
    {
        public int rankMin;  // 1
        public int rankMax;  // 1
        public float rewardMultiplier; // 1.5
        public string bonusTitle; // "MVP"
    }

    public string bossName;
    public string bossDescription;
    public long baseHp;           // 길드 레벨과 곱해짐
    public int baseAtk;
    public int baseDef;
    public BossPhaseData[] phases; // 3개
    public ContributionReward[] contributionRewards;
    public float maxContributionPercent = 0.4f;
    public float weakPlayerDamageBonus = 1.5f;
    public int weakPlayerLevelThresholdPercent = 50;
}
```

#### GuildRaidDataSO (토벌전)

```csharp
[CreateAssetMenu(menuName = "MkLike/Data/GuildRaidData")]
public class GuildRaidDataSO : ScriptableObject
{
    [System.Serializable]
    public class WaveData
    {
        public int monsterCount;
        public float monsterHpMultiplier;
        public float monsterAtkMultiplier;
        public bool hasBoss;
    }

    public WaveData[] waves; // 5개
    public float timeLimit = 180f;
    public int[] killMilestones; // 50, 100, 200, 500
    public RewardEntry[] milestoneRewards;
}
```

### 3.5 UI/UX 플로우

#### 길드 보스

```
[길드 패널] → [보스 레이드] 탭
    ↓
[보스 정보] — 이름, HP(누적), 현재 페이즈, 남은 도전 횟수
    ↓
[도전] 버튼
    ↓
[전투 시각화] — 보스 초상화 + HP바(3색 페이즈) + 전투 로그
    ├── 페이즈 전환 연출 (붉은 플래시, "분노!")
    ├── 보스 스킬 사용 시 화면 효과
    └── 60초 타이머
    ↓
[GuildBossResultPopup 확장]
    ├── 데미지 카운트업
    ├── 페이즈 도달 표시 (도달한 페이즈 아이콘 + 색상)
    ├── 기여도 순위 표시:
    │   ├── 결과 팝업: 상위 3명 + 내 순위 (총 4줄)
    │   ├── 1위: MVP 아이콘 + 금색 하이라이트
    │   └── 길드 패널 상세: 전원 (스크롤)
    └── 보상 표시 (난이도 배율 적용)
```

#### 토벌전

```
[길드 패널] → [토벌전] 탭
    ↓
[토벌전 정보] — 남은 시간, 길드 총 처치수, 내 처치수, 마일스톤
    ↓
[참전] 버튼
    ↓
[전투 화면] — 기존 전투 UI + 오버레이(타이머, 처치 카운트, 웨이브 표시)
    ↓
[결과 팝업] — 개인 처치수, 길드 순위
```

#### 보스 HP바 3색 디자인

```
┌──────────────────────────────────────────┐
│ ████████████████████████████████████████ │  100%
│ ██████████████████████████│█░░░░░░░░░░░ │   70% — 페이즈 1→2 경계 (밝은 세로줄)
│ █████████████████│░░░░░░░░░░░░░░░░░░░░░ │   40% — 페이즈 2→3 경계 (밝은 세로줄)
│ 초록(1:각성)    │주황(2:분노)│빨강(3:폭주) │
└──────────────────────────────────────────┘
```

**페이즈 경계 70%/40% 설계 근거:**
- 1페이즈를 짧게(30%분) → "빠르게 진행되고 있다"는 체감
- 3페이즈를 길게(40%분) → 긴장감 있는 DPS 레이스
- 33/33/33 균등 분배보다 앞부분이 빠르고 뒷부분이 긴 비대칭이 재미있다

### 3.6 밸런스 수치

| 항목 | 수치 | 근거 |
|------|------|------|
| 보스 기본 HP | 100,000 x 길드레벨 | 기존 GuildDataSO.BaseBossHpPerLevel 유지 |
| 페이즈 1→2 경계 | HP 70% | 초반 DPS 체크 구간. 너무 빠르면 2페이즈 체감 부족 |
| 페이즈 2→3 경계 | HP 40% | 후반 화력 집중 구간 |
| 3페이즈 자폭 | 30초 카운트다운 | 긴장감 + DPS 레이스. 못 잡으면 다음 도전에 이어서 |
| 보스 ATK (1페이즈) | 길드레벨 x 10 | 플레이어 HP의 약 5~10%를 턴당 깎는 수준 |
| 보스 ATK (2페이즈) | x 1.3 | 회복/방어 없으면 60초 내 사망 가능 |
| 보스 ATK (3페이즈) | x 1.8 | 공격에 집중해야 하는 구간 |
| 주간 보스 도전 | 2회 | 기존 유지 |
| 토벌전 시간 | 180초 | 3분 집중 플레이 |
| 토벌전 주기 | 주 1회 | 보스(주2)와 합치면 주간 길드 콘텐츠 3회 |
| 기여도 상한 | 40% | 5인 길드 기준 고레벨 1명이 40% 넘게 기여하면 나머지 동기 저하 |

### 3.7 구현 태스크

| ID | 태스크 | 의존성 | 파일 스코프 |
|----|--------|--------|------------|
| GB-01 | GuildBossDataSO + 3페이즈 + 보스 스킬 5종 | - | `Data/GuildBossDataSO.cs` |
| GB-02 | GuildBossBattleSimulator 확장 (3페이즈, 스킬, 전투 로그) | GB-01 | `Core/GuildBossBattleSimulator.cs` |
| GB-03 | GuildBossSystem 확장 (기여도 순위, 보상 분배, 약자 보호) | GB-01, GB-02 | `Quest/GuildBossSystem.cs` |
| GB-04 | GuildRaidDataSO + 토벌전 시뮬레이션 (5웨이브, 처치 카운트) | - | `Data/GuildRaidDataSO.cs`, `Quest/GuildRaidSystem.cs` |
| GB-05 | 길드 전투 UI (보스 HP바 3색, 전투 로그, 기여도 순위, 토벌전 오버레이) | GB-01~04 | `UI/GuildBossPanel.cs`, `UI/GuildRaidPanel.cs` |

### 3.8 의존성

- `GuildBossSystem` (기존 레이드 로직 확장)
- `GuildManager` (길드 레벨, 멤버 정보)
- `CombatStats` (플레이어 스탯)
- `CurrencyManager` (보상 지급)
- `StageManager` (토벌전 웨이브 패턴 참고)

---

## 4. 공통 사항

### 4.1 이벤트 설계

라운드 2 시스템에 필요한 신규 이벤트:

```csharp
// 비밀 방
public struct SecretRoomEnteredEvent : IEvent { ... }
public struct SecretRoomCompletedEvent : IEvent { ... }

// 아레나 (기존 ArenaMatchEvent 유지 + 확장)
public struct ArenaRivalMatchedEvent : IEvent
{
    public string RivalId;
    public string RivalName;
    public int RivalElo;
}

// 길드 (기존 GuildBossRaidEvent 유지 + 확장)
public struct GuildBossPhaseChangedEvent : IEvent
{
    public int NewPhase;
    public float BossHpPercent;
}

public struct GuildRaidCompletedEvent : IEvent
{
    public int PersonalKills;
    public int GuildTotalKills;
    public int WavesCleared;
}

public struct GuildContributionUpdatedEvent : IEvent
{
    public int Rank;
    public float ContributionPercent;
}
```

### 4.2 공통 UI 패턴

3개 시스템 모두 "전투 시각화 + 결과 팝업" 패턴을 사용한다. 공통 컴포넌트:

1. **BattleVisualizerBase**: HP바 + 타이머 + 전투 로그 + 배속 컨트롤
2. **ResultPopupBase**: BasePopup 상속, 카운트업 연출 + 보상 표시
3. **RankListUI**: 순위 표시 (아레나 전적, 길드 기여도)

### 4.3 병렬 구현 가능성

| 태스크 그룹 | 파일 스코프 | 병렬 가능 |
|-------------|------------|-----------|
| HR-01~05 (비밀 방) | Dungeon/, Data/, UI/ | O (독립) |
| AP-01~05 (아레나) | Combat/, Core/, UI/ | O (독립) |
| GB-01~05 (길드) | Quest/, Core/, Data/, UI/ | O (독립) |

**3그룹 완전 병렬 가능.** 파일 충돌 없음 (GameEvents.cs에 이벤트 추가만 동기화 필요).

### 4.4 총 태스크 요약

| 카테고리 | 태스크 수 | SO 파일 | 매니저 파일 | UI 파일 |
|----------|-----------|---------|------------|---------|
| 20. 탑 비밀 방 | 5 | HiddenRoomDataSO | SecretRoomManager 확장 | CollectionBookPanel 확장, SecretRoomPopup 확장 |
| 21. 아레나 실전투 | 5 | ArenaRivalDataSO | ArenaManager 확장, ArenaBattleSimulator 확장 | ArenaBattlePanel(신규), ArenaPanel 확장 |
| 22. 길드 실전투 | 5 | GuildBossDataSO, GuildRaidDataSO | GuildBossSystem 확장, GuildRaidSystem(신규) | GuildBossPanel(신규), GuildRaidPanel(신규) |
| **합계** | **15** | **4** | **5** | **6** |

---

## 5. 기획 품질 체크리스트

- [x] 핵심 메커니즘이 모호함 없이 기술되었는가
- [x] 데이터 구조 (SO 필드, enum)가 구현 가능한 수준으로 정의되었는가
- [x] 수치에 근거가 있는가 (공식, 레퍼런스, 시뮬레이션)
- [x] UI 플로우가 사용자 동선을 고려하는가
- [x] 기존 시스템과의 의존성/연동이 명확한가
- [x] roadmap.md에 추가할 태스크가 분해되었는가
- [x] mkLike 세계관과 자연스럽게 연결되는가
