---
read_count: 0
last_read: "never"
status: active
priority: P0
---

# 액션 → 인게임 피드백 표준 (2026-04-23)

## 문제

런틱 + 사용자 실 플레이에서 확인된 패턴:
- "소환 눌렀는데 연출 없음" (이슈 2)
- "소탕했는지 안 했는지 모르겠음" (이슈 10)
- "정예 소환 버튼 눌러 봐도 뭐가 달라지는지 모름" (이슈 14)
- "부스터 쓰면 뭔가 부스팅 되는 것처럼 보여야 함" (이슈 15)
- 전체적으로 **"시스템은 많은데 제대로 되는 건 없음"**

### 근본 원인
- `Toast`, `DamageTextManager`, `ScreenShakeManager`, `GachaEffect` 등 **개별 피드백 시스템은 존재**
- 하지만 **액션(버튼 클릭 → 시스템 반응)을 일관된 피드백으로 연결하는 공통 패턴이 없음**
- 각 시스템마다 알아서 연출하거나 안 하거나

## 표준: 3-Tier 필수 피드백

**모든 유저 액션(버튼 클릭 or 자동 시스템 트리거)** 후 아래 3가지를 반드시 발생시킨다:

### Tier 1 — **즉시 Toast** (0~300ms)
- 액션 인식 확인용 단문 메시지
- "소환 1회 실행" / "소탕권 3장 사용" / "부스터 발동"
- 위치: 화면 상단 또는 액션 버튼 위
- 지속: 1.5~2초

### Tier 2 — **효과 VFX** (액션 시점 ~ 연출 종료)
- 해당 액션의 영속적 시각 확인
- 예: 가챠 카드 플립 연출, 소탕 보상 펼침, 부스터 HUD 뱃지 1분 표시, 강화 성공 스파크
- 화면 중앙 또는 관련 UI 요소에서 발화

### Tier 3 — **변화 수치 표시** (액션 직후 ~ 3초)
- 실제 변경된 데이터를 명확히 표시
- "루비 9700 → 9400 (−300)" / "골드 +1620" / "경험치 +256380"
- DamageText/Toast/HUD 플래시 조합

### 예시 매핑

| 액션 | Tier 1 (Toast) | Tier 2 (VFX) | Tier 3 (수치) |
|------|----------------|--------------|----------------|
| 가챠 1회 | "소환 1회 실행" | 카드 플립 오버레이 | "루비 9700 → 9400" + 획득 아이템 |
| 소탕 3장 | "소탕 3회 실행" | 보상 리스트 펼침 | "골드 +5000 경험치 +12000 장비 3개" |
| 장비 강화 | "강화 시도 (+4 → +5)" | 스파크 + 성공/실패 색 | "성공! ATK +120" / "실패, 단계 유지" |
| 스탯 +1 ATK | "ATK +12" | HUD ATK 숫자 플래시 | 전투력 업데이트 |
| 부스터 활성 | "경험치 2배 부스터 시작" | HUD 부스터 뱃지 + 타이머 | "1시간 동안 적용" |
| 정예 소환 | "정예 몬스터 소환" | 맵에 정예 출현 + 보스 VFX | "정예 HP 10000 / 리워드 X" |

## 구현 방향

### FeedbackBus 공통 헬퍼

```csharp
namespace MkLike.Feedback
{
    public enum FeedbackKind {
        Currency, Gacha, Enhance, Stat, Booster, Arena,
        Dungeon, Elite, Skill, Boss, Quest
    }

    public struct FeedbackEvent : IEvent {
        public FeedbackKind Kind;
        public string Summary;         // Tier 1 Toast 문구
        public Action VfxHook;         // Tier 2 VFX 콜백
        public string ValueDiff;       // Tier 3 수치 (optional)
        public Vector3? WorldPos;      // 화면 위치 힌트
    }

    public static class FeedbackBus {
        public static void Emit(FeedbackKind kind, string summary,
                                Action vfx = null, string diff = null,
                                Vector3? worldPos = null) {
            EventBus.Publish(new FeedbackEvent {
                Kind = kind, Summary = summary, VfxHook = vfx,
                ValueDiff = diff, WorldPos = worldPos
            });
        }
    }
}
```

### FeedbackDisplayManager (수신자)
- `EventBus<FeedbackEvent>.Subscribe(OnFeedback)`
- 매 이벤트:
  - Tier 1: `ToastUI.Show(evt.Summary)`
  - Tier 2: `evt.VfxHook?.Invoke()`
  - Tier 3: 지정 위치에 `DamageTextManager.SpawnCurrencyChange(evt.ValueDiff)`

### 기존 이벤트와 공존
- `CurrencyChangedEvent`, `EquipmentEnhancedEvent` 등 기존 이벤트는 유지
- 각 매니저가 기존 이벤트 발행 **직후** `FeedbackBus.Emit()` 추가

## 이슈별 적용 매핑

| 이슈 | 액션 | Feedback 적용 지점 |
|------|------|---------------------|
| 2 | 가챠 결과 | `GachaManager.Pull` 성공 직후 `FeedbackBus.Emit(Gacha, ...)` |
| 10 | 소탕 실행 | `QuickHuntManager.Execute` 성공 후 |
| 11 | 정예 처치수 | `EliteSummonManager` Kill 시 `FeedbackBus.Emit(Elite, ...)` |
| 13 | 스탯 분배 | `StatAllocationSystem.Allocate` 후 |
| 14 | 정예 소환 버튼 | `EliteSummonManager.Summon` 후 |
| 15 | 전반 | 모든 Manager에 `FeedbackBus.Emit` 의무화 |

## 런틱 테스트 통합 (이슈 16 연동)

`BotInvariants.Check_ActionFeedbackUI`:
- 봇이 액션 실행 후 N tick(3 tick, real ~4.5s) 내 `FeedbackEvent` 1건 이상 발행 확인
- 누락 감지 시 해당 Manager의 피드백 호출 누락으로 판정

## 구현 범위

### Phase B (이 문서)
- FeedbackBus 설계 확정

### Phase C
1. `client/Assets/Scripts/Core/Feedback/FeedbackBus.cs` 신규
2. `FeedbackEvent` 타입 정의
3. `FeedbackDisplayManager` 싱글턴 (씬에 배치)
4. 주요 매니저(Gacha/QuickHunt/EliteSummon/Booster/Equipment/StatAllocation)에 `FeedbackBus.Emit` 호출 주입
5. ToastUI / DamageTextManager 기존 API 재사용

### Phase D
- BotInvariants 신규 체크 활성
- Marathon tick에서 피드백 누락 자동 감지

## 성공 기준

- [ ] 모든 유저 액션 후 Toast 문구 표시
- [ ] 액션별 VFX 연출 확인 (가챠/소탕/강화/정예/부스터)
- [ ] 수치 변동 시 확실한 화면 피드백
- [ ] 런틱에서 피드백 누락 시 Invariant 실패로 리포트됨
