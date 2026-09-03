---
read_count: 2
last_read: "2026-04-03"
status: reference
---

# 챌린지 모드 기획서

> 작성일: 2026-03-16
> 참조: next-content-design.md §3.7.2

---

## 1. 개요

챌린지 모드는 **특수 조건 하에 스테이지를 클리어**하는 엔드게임 콘텐츠이다. 기존 전투 시스템에 조건 제한을 추가하여 새로운 도전과 보상을 제공한다.

### 핵심 원칙
- **해금 조건**: 메인 스토리 50층 클리어
- **주간 리셋**: 매주 월요일 00:00에 3개 챌린지 조합 갱신
- **별(Star) 시스템**: 클리어 성적에 따라 별 1~3개, 누적 별로 마일스톤 보상
- **반복 도전 가능**: 더 높은 별을 위해 재도전 가능 (주간 리셋까지)

---

## 2. 데이터 설계

### 2.1 ChallengeType (enum)

```csharp
public enum ChallengeType
{
    SpeedRun,   // 속도전: 제한 시간 내 클리어
    NoDamage,   // 무피격: 피격 0회로 클리어
    LowLevel,   // 저레벨: 레벨 디버프 적용
    BossRush,   // 보스 러시: 보스 연속 처치
    Survival    // 생존: 제한 시간 동안 생존 (무한 웨이브)
}
```

### 2.2 ChallengeDataSO (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "MkLike/Challenge/ChallengeData")]
public class ChallengeDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string _id;                  // 고유 ID (예: "challenge_speedrun_01")
    [SerializeField] private string _displayName;         // 표시명 (예: "속도전: 번개 같은 일격")
    [SerializeField] private ChallengeType _type;         // 챌린지 유형
    [SerializeField] [TextArea] private string _description; // 챌린지 설명

    [Header("조건")]
    [SerializeField] private int _timeLimitSec;           // 시간 제한 (초). SpeedRun: 30, Survival: 300
    [SerializeField] private int _levelDebuff;            // 레벨 디버프 (LowLevel: -20, 나머지: 0)
    [SerializeField] private int _bossCount;              // 보스 수 (BossRush: 5, 나머지: 0)

    [Header("별 기준")]
    [SerializeField] private int _maxStars;               // 최대 별 수 (기본 3)
    [SerializeField] private int _star2Threshold;         // 별 2개 기준 (유형별 다름)
    [SerializeField] private int _star3Threshold;         // 별 3개 기준 (유형별 다름)

    [Header("보상 (1회 클리어)")]
    [SerializeField] private CurrencyType _rewardType;    // 보상 재화 종류
    [SerializeField] private int _rewardAmount;           // 보상 재화 수량

    [Header("난이도")]
    [SerializeField] private int _recommendedFloor;       // 추천 전투력 (층수 기준)
    [SerializeField] private ArenaDataSO _arenaData;      // 전투 맵 데이터 (기존 ArenaDataSO 활용)
}
```

### 2.3 별 평가 기준 (유형별)

| 유형 | 별 1개 | 별 2개 | 별 3개 |
|------|--------|--------|--------|
| **SpeedRun** | 클리어 | 20초 내 | 15초 내 |
| **NoDamage** | 피격 3회 이하 | 피격 1회 이하 | 피격 0회 |
| **LowLevel** | 클리어 | 60초 내 | 30초 내 |
| **BossRush** | 보스 3마리 처치 | 보스 4마리 처치 | 보스 5마리 전부 처치 |
| **Survival** | 3분 생존 | 4분 생존 | 5분 생존 |

---

## 3. ChallengeManager API

```csharp
public class ChallengeManager : SingletonBase<ChallengeManager>, ISaveProvider
{
    // === 챌린지 실행 ===

    /// 챌린지 시작 (전투 씬 전환 + 조건 적용)
    public async UniTask<bool> StartChallenge(string challengeId);

    /// 챌린지 완료 처리 (결과 판정 + 보상 지급)
    public void CompleteChallenge(string challengeId, ChallengeResult result);

    /// 현재 진행 중인 챌린지 (없으면 null)
    public ChallengeDataSO GetActiveChallenge();

    // === 주간 챌린지 ===

    /// 이번 주 챌린지 3개 반환
    public List<ChallengeDataSO> GetWeeklyChallenges();

    /// 주간 리셋까지 남은 시간
    public TimeSpan GetTimeUntilReset();

    // === 별/마일스톤 ===

    /// 특정 챌린지의 최고 별 수
    public int GetBestStars(string challengeId);

    /// 누적 별 수 (전체)
    public int GetTotalStars();

    /// 마일스톤 보상 수령
    public bool ClaimStarMilestone(int milestoneIndex);

    /// 수령 가능한 마일스톤 목록
    public List<StarMilestone> GetClaimableMilestones();

    // === 해금 ===

    /// 챌린지 모드 해금 여부 (50층 클리어 확인)
    public bool IsUnlocked();

    // === 저장/로드 (ISaveProvider) ===
    public string SaveKey { get; }
    public string ToJson();
    public void FromJson(string json);
}
```

### 보조 구조체

```csharp
[System.Serializable]
public struct ChallengeResult
{
    public string ChallengeId;
    public bool IsCleared;
    public int StarsEarned;
    public float ElapsedTime;
    public int DamageTaken;
    public int BossesKilled;
}

[System.Serializable]
public struct StarMilestone
{
    public int RequiredStars;
    public CurrencyType RewardType;
    public int RewardAmount;
    public string RewardDescription;
    public bool IsClaimed;
}
```

### 이벤트

```csharp
// 챌린지 시작 시
public struct ChallengeStartedEvent
{
    public string ChallengeId;
    public ChallengeType Type;
}

// 챌린지 완료 시
public struct ChallengeCompletedEvent
{
    public string ChallengeId;
    public ChallengeResult Result;
}

// 마일스톤 달성 시
public struct StarMilestoneReachedEvent
{
    public int MilestoneIndex;
    public int TotalStars;
}
```

---

## 4. 주간 리셋 메커니즘

### 4.1 챌린지 선택 로직
```
매주 월요일 00:00 (서버 시간):
1. 5종 ChallengeType 중 3종을 중복 없이 랜덤 선택
2. 각 타입별 ChallengeDataSO 풀에서 1개씩 랜덤 선택
3. 선택된 3개 챌린지를 주간 챌린지로 설정
4. 이전 주 기록 초기화 (누적 별은 유지)
```

### 4.2 시드 기반 랜덤
- **시드**: `연도 * 100 + 주차 번호` (예: 202612)
- 모든 유저에게 동일한 주간 챌린지 제공 (공정성)
- 같은 주에 재접속해도 동일한 챌린지 유지

### 4.3 리셋 시 처리
- 주간 별 기록 → 누적 별에 합산 (최고 기록만)
- 챌린지 클리어 상태 초기화
- 보상 수령 상태 초기화 (주간 보상)
- 마일스톤 보상은 리셋되지 않음 (영구)

---

## 5. 별 마일스톤 보상 테이블

누적 별 수에 따라 영구 보상 수령. 한 번 수령하면 리셋되지 않는다.

| 누적 별 | 보상 | 수량 |
|---------|------|------|
| 5 | 루비 | 200 |
| 10 | 무기 소환권 | 3 |
| 15 | 골드 | 100,000 |
| 20 | 동료 소환권 | 3 |
| 30 | 각인 선택 상자 | 1 |
| 40 | 루비 | 500 |
| 50 | 코스튬 조각 | 50 |
| 60 | 유물 상자 | 1 |
| 75 | 루비 | 1,000 |
| 90 | 전설 코스튬 선택권 | 1 |
| 100 | 루비 | 2,000 |
| 120 | 한정 칭호: "도전자" | 1 |
| 150 | 루비 | 3,000 + 전설 각인 선택 상자 |

---

## 6. 챌린지별 상세 설계

### 6.1 속도전 (SpeedRun)
- **목표**: 제한 시간(30초) 내 모든 몬스터 처치
- **몬스터 구성**: 일반 몬스터 20마리 (현재 층수 기준 스탯)
- **UI**: 화면 상단에 카운트다운 타이머 표시
- **실패 조건**: 시간 초과

### 6.2 무피격 (NoDamage)
- **목표**: 피격 없이 스테이지 클리어
- **몬스터 구성**: 일반 몬스터 15마리 + 미니보스 1마리
- **UI**: 피격 카운터 표시, 피격 시 화면 가장자리 붉은 경고
- **실패 조건**: 없음 (피격 횟수에 따라 별 차등)

### 6.3 저레벨 (LowLevel)
- **목표**: 레벨 -20 디버프 상태로 클리어
- **몬스터 구성**: 현재 층수 기준 일반 몬스터 20마리
- **디버프 적용**: `CombatStats.AddModifier()`로 임시 레벨 디버프
- **UI**: 디버프 아이콘 표시, 감소된 스탯 빨간색 표시
- **실패 조건**: 플레이어 사망

### 6.4 보스 러시 (BossRush)
- **목표**: 보스 5마리 연속 처치
- **보스 구성**: 현재 챕터 보스 변형 5종 (체력 80%/90%/100%/110%/120%)
- **UI**: 보스 처치 카운터 (1/5, 2/5...)
- **실패 조건**: 플레이어 사망
- **특수**: 보스 간 3초 휴식, HP 50% 회복

### 6.5 생존 (Survival)
- **목표**: 5분간 생존 (무한 웨이브)
- **몬스터 구성**: 10초마다 웨이브 발생, 시간 경과에 따라 몬스터 수/강화 증가
- **UI**: 경과 시간 타이머, 웨이브 카운터
- **실패 조건**: 플레이어 사망
- **웨이브 스케일링**:
  - 0~1분: 기본 몬스터 5마리/웨이브
  - 1~2분: 기본 몬스터 8마리 + 스탯 120%
  - 2~3분: 기본 몬스터 10마리 + 스탯 150% + 미니보스
  - 3~4분: 기본 몬스터 12마리 + 스탯 200%
  - 4~5분: 기본 몬스터 15마리 + 스탯 300% + 보스

---

## 7. UI 플로우

### 7.1 진입 경로
```
던전 탭 > [챌린지] 서브탭
(50층 미클리어 시 잠금 표시 + "50층 클리어 후 해금" 안내)
```

### 7.2 챌린지 메인 패널

```
┌────────────────────────────────────────────┐
│  [챌린지 모드]          리셋까지: 3일 14시간  │
├────────────────────────────────────────────┤
│  이번 주 챌린지                             │
│                                            │
│  ┌────────────────────────────────────┐    │
│  │ ⚡ 속도전: 번개 같은 일격           │    │
│  │ 30초 내 모든 적 처치!              │    │
│  │ ★★☆  최고 기록: 2/3              │    │
│  │ 보상: 루비 100                    │    │
│  │                [도전하기]          │    │
│  └────────────────────────────────────┘    │
│                                            │
│  ┌────────────────────────────────────┐    │
│  │ 🛡️ 무피격: 완벽한 회피             │    │
│  │ 피격 없이 스테이지 클리어!          │    │
│  │ ☆☆☆  미도전                      │    │
│  │ 보상: 각인 상자                    │    │
│  │                [도전하기]          │    │
│  └────────────────────────────────────┘    │
│                                            │
│  ┌────────────────────────────────────┐    │
│  │ 💀 보스 러시: 연속 처형             │    │
│  │ 보스 5연속 처치!                   │    │
│  │ ★☆☆  최고 기록: 1/3              │    │
│  │ 보상: 유물 상자                    │    │
│  │                [도전하기]          │    │
│  └────────────────────────────────────┘    │
│                                            │
├────────────────────────────────────────────┤
│  [별 마일스톤]  누적: 47/150 ★             │
│  ████████░░░░░░░░░  다음: 50★ 코스튬 조각   │
└────────────────────────────────────────────┘
```

### 7.3 챌린지 진행 중 HUD

```
┌────────────────────────────────────────────┐
│  [속도전] 남은 시간: 00:18     ★★☆          │
├────────────────────────────────────────────┤
│                                            │
│            (전투 화면)                      │
│                                            │
│                                            │
├────────────────────────────────────────────┤
│  남은 적: 7/20                [포기]        │
└────────────────────────────────────────────┘
```

### 7.4 결과 팝업

```
┌──────────────────────────────┐
│      챌린지 완료!             │
│                              │
│   ★ ★ ★                     │
│   속도전: 번개 같은 일격       │
│                              │
│   클리어 시간: 14.3초         │
│   신기록!                    │
│                              │
│   보상:                      │
│   💎 루비 x100               │
│                              │
│   [확인]    [재도전]          │
└──────────────────────────────┘
```

### 7.5 별 마일스톤 팝업

```
┌──────────────────────────────┐
│    별 마일스톤 달성!           │
│                              │
│    누적 ★ 50개 달성!          │
│                              │
│    보상: 코스튬 조각 x50       │
│                              │
│    [수령하기]                 │
└──────────────────────────────┘
```

---

## 8. 저장 데이터

```csharp
[System.Serializable]
public class ChallengeSaveData
{
    public int TotalStars;                              // 누적 별 수
    public List<int> ClaimedMilestoneIndices;           // 수령한 마일스톤 인덱스
    public Dictionary<string, int> BestStars;           // 챌린지ID → 최고 별 수 (주간)
    public Dictionary<string, int> AllTimeBestStars;    // 챌린지ID → 역대 최고 별 수
    public long LastResetTimestamp;                     // 마지막 리셋 시간 (Unix)
    public List<string> WeeklyChallengeIds;             // 이번 주 챌린지 ID 3개
}
```

---

## 9. 기존 시스템 연동

| 시스템 | 연동 방식 |
|--------|-----------|
| **SaveManager** | ISaveProvider 구현, JSON 직렬화 |
| **EventBus** | ChallengeStartedEvent, ChallengeCompletedEvent, StarMilestoneReachedEvent |
| **CombatStats** | LowLevel 디버프: AddModifier()로 임시 레벨 감소 적용 |
| **StageManager** | 챌린지 전투 시 기존 스테이지 로직 활용, 조건 오버라이드 |
| **CurrencyManager** | 보상 재화 지급 |
| **TowerManager** | 50층 클리어 여부로 해금 판정 |
| **DungeonPanel** | 챌린지 서브탭 추가 |

---

## 10. 구현 태스크 분해

| ID | 태스크 | 의존성 | 비고 |
|----|--------|--------|------|
| CH-01 | ChallengeType enum + ChallengeResult 구조체 정의 | - | Core/Enums/ |
| CH-02 | ChallengeDataSO + StarMilestone SO 데이터 | CH-01 | SO 필드 설계 |
| CH-03 | ChallengeManager 싱글톤 구현 | CH-02, C1-05 | Start/Complete/WeeklyReset/Save/Load |
| CH-04 | 주간 리셋 로직 (시드 기반 랜덤 선택) | CH-03 | 월요일 00:00 리셋, 시드 = 연도*100+주차 |
| CH-05 | 이벤트 정의 (ChallengeStartedEvent 등) | CH-01 | EventBus 연동 |
| CH-06 | 챌린지 전투 조건 시스템 | CH-03, C2 | SpeedRun 타이머, NoDamage 카운터, LowLevel 디버프, BossRush 연속, Survival 웨이브 |
| CH-07 | 별 평가 + 마일스톤 보상 시스템 | CH-03 | 누적 별, 마일스톤 테이블 |
| CH-08 | 챌린지 메인 패널 UI | CH-03 | 주간 챌린지 목록, 리셋 타이머 |
| CH-09 | 챌린지 진행 HUD UI | CH-06 | 타이머, 카운터, 포기 버튼 |
| CH-10 | 결과/마일스톤 팝업 UI | CH-07 | 별 애니메이션, 보상 표시 |
| CH-11 | ChallengeDataSO 에셋 생성 (초기 데이터) | CH-02 | 5종 x 3~5개 = 15~25개 |
| CH-12 | DungeonPanel 챌린지 탭 연동 | CH-08 | 던전 탭에 챌린지 서브탭 추가 |

### 추천 병렬 그룹
```
[그룹 1] CH-01 + CH-02 + CH-05 (데이터/enum/이벤트 — 독립)
[그룹 2] CH-03 + CH-04 (매니저 + 리셋 — 그룹 1 완료 후)
[그룹 3] CH-06 + CH-07 (전투 조건 + 별 시스템 — CH-03 완료 후, 병렬 가능)
[그룹 4] CH-08 + CH-09 + CH-10 (UI 3종 — 그룹 3 완료 후, 병렬 가능)
[그룹 5] CH-11 + CH-12 (에셋 + 연동 — 그룹 4 완료 후)
```
