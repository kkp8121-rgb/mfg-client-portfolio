# 성장 중심 시스템 연결 설계서

> 작성일: 2026-03-17
> 목적: 코드베이스를 분석하여 모든 성장 경로를 맵핑하고, 끊긴 고리를 파악하며, "성장 벽" 구조를 설계한다.
> 핵심 전제: 방치형 키우기 RPG에서 모든 시스템은 "성장"을 위해 존재한다.

---

## 1. 현재 성장 경로 맵 (코드 기반)

### 1.1 ModifierSource 기반 성장 출처 (13종)

`CombatStats.cs` (line 16~32)에서 `ModifierSource` enum으로 정의된 성장 출처:

| Source | 시스템 | CombatStats 적용 방식 | 코드 위치 |
|--------|--------|----------------------|-----------|
| **Equipment** | EquipmentManager | `AddBonus()` (레거시) | `EquipmentManager.cs:215~249` |
| **Companion** | CompanionEffectApplier | `AddBonus()` (레거시) | `CompanionEffectApplier.cs:129~137` |
| **Relic** | RelicManager | AddModifier | (구현 확인 필요) |
| **Inscription** | InscriptionManager | `AddModifier()` | `InscriptionManager.cs` |
| **TowerGimmick** | TowerGimmickSystem | `AddModifier()` | `TowerGimmickSystem.cs` |
| **Buff** | DailyBlessingSystem | `AddModifier()` | `DailyBlessingSystem.cs` |
| **Job** | JobSystem | `AddModifier()` (4차 전직만) | `JobSystem.cs:259~264` |
| **HunterRank** | (미확인) | 미사용 | - |
| **Prestige** | PrestigeSystem | `AddModifier()` | `PrestigeSystem.cs` |
| **Blessing** | DailyBlessingSystem | `AddModifier()` | `DailyBlessingSystem.cs` |
| **Party** | ClimbingPartyManager | `AddModifier()` | `ClimbingPartyManager.cs` |
| **Collection** | CollectionBookManager | `AddModifier()` | `CollectionBookManager.cs` |
| **Pet** | PetCombatSystem | `AddModifier()` | `PetCombatSystem.cs` |
| **Mastery** | MasteryManager | `AddModifier()` | `MasteryManager.cs` |

### 1.2 성장 경로별 CP 기여 흐름

#### 경로 A: 레벨업 → 스탯 직접 성장
```
LevelSystem → CombatStats.InitFromCharacterData(data, level)
  → _bonusHp = data.hpPerLevel * (level - 1)
  → _bonusAtk = data.atkPerLevel * (level - 1)
  → OnStatsChanged → PublishCpChangeIfNeeded → CpChangedEvent
```
**상태**: 완전 연결됨. 레벨업 → 스탯 증가 → CP 변화 → CpCounterWidget 애니메이션.

#### 경로 B: 장비 장착 → CP 상승
```
GachaManager.Pull() → GachaResultEvent 발행
  → EquipmentManager.OnGachaResult() → AddToInventory()
    → EquipmentInventoryChangedEvent 발행
  유저 수동 장착 → EquipmentManager.Equip()
    → RecalculateAndApplyBonuses() → CombatStats.AddBonus()
    → EquipmentChangedEvent 발행
    → CpCounterWidget.OnStatChanged() → 애니메이션
```
**상태**: 가챠→인벤토리는 자동. 장착은 수동. **"자동 장착" 미구현**.

#### 경로 C: 동료 장착 → CP 상승
```
GachaManager.PullCompanion() → GachaResultEvent 발행
  → CompanionManager.OnGachaResult() → AddOwned()
    → CompanionObtainedEvent 발행
  유저 수동 장착 → CompanionManager.SetMainCompanion/SetSubSlot()
    → CompanionEquippedEvent 발행
    → CompanionEffectApplier.RecalculateAndApplyBonuses()
      → CombatStats.AddBonus() → CpChangedEvent
```
**상태**: 가챠→보유목록 자동. 편성은 수동. 직업 특화 배율 적용됨(+20%).

#### 경로 D: 전직 → CP 상승
```
LevelUpEvent → JobSystem.OnLevelUp()
  → Advance() → JobChangedEvent 발행
  → 4차 전직 시 ApplyTier4Modifiers()
    → CombatStats.AddModifier(ModifierSource.Job, Atk/Def/MaxHp +15%)
```
**상태**: 1~3차 전직은 **스탯 보너스 없음** (직업명 변경만). 4차만 +15% modifier 적용.

#### 경로 E: 장비 강화 → CP 상승
```
EquipmentEnhanceSystem → 주문서 강화/성급 강화
  → EquipmentManager.RecalculateAndApplyBonuses()
  → CombatStats.AddBonus() → CpChangedEvent
```
**상태**: 강화 비용 공식 존재 (`CombatFormula.cs:44~60`). UI 연동 확인 필요.

#### 경로 F: 코스튬 → CP 상승
```
CostumeManager → 코스튬 장착
  → CostumeEquipChangedEvent 발행
  → CostumeSetVfxHandler (비주얼만)
```
**상태**: 세트 효과 스탯 적용 확인 필요. VFX는 연결됨.

#### 경로 G: 각인 → CP 상승
```
InscriptionManager → 각인 장착
  → CombatStats.AddModifier(ModifierSource.Inscription)
  → InscriptionChangedEvent 발행
```
**상태**: modifier 시스템으로 정상 연결.

#### 경로 H: 마스터리 → CP 상승
```
MasteryManager → 노드 해금
  → CombatStats.AddModifier(ModifierSource.Mastery)
  → MasteryUnlockedEvent 발행
```
**상태**: modifier 시스템으로 정상 연결.

#### 경로 I: 환생 → CP 상승
```
PrestigeSystem → 환생 실행
  → CombatStats.AddModifier(ModifierSource.Prestige)
  → PrestigeExecutedEvent 발행
```
**상태**: modifier 시스템으로 정상 연결. 영구 보너스.

### 1.3 CP 계산 공식 (CombatFormula.cs:141~151)

```
CP = ATK * 3 + MaxHp * 0.5 + DEF * 2 + CritRate * 500 + AttackSpeed * 100
```

**가중치 분석:**
- ATK +1 = CP +3 (가장 높은 가중치)
- DEF +1 = CP +2
- MaxHp +1 = CP +0.5
- CritRate +1% = CP +5
- AttackSpeed +0.1 = CP +10

이 가중치에서 **공격력이 가장 CP 효율이 높다**. 궁수/마법사가 전사보다 CP가 높게 나오는 경향.

---

## 2. 끊긴 고리 분석 (코드 레벨)

### 2.1 심각 (성장 체감 직접 차단)

#### [GAP-01] 가챠 → 자동 장착 미연결
- **현상**: `GachaManager.Pull()` → `GachaResultEvent` → `EquipmentManager.AddToInventory()` 까지는 자동이지만, **장착은 유저가 수동으로 해야 함**
- **영향**: 가챠 후 "뭐가 바뀌었는지 모르겠다" 문제의 핵심 원인
- **GachaCompareSystem** (`GachaCompareSystem.cs:86~130`): 에픽 이상 아이템의 CP 증감을 **추정치로만** 계산 (하드코딩: Mythic=500, Legendary=300). **실제 장비 데이터를 조회하지 않음**.
- **해결**: `EstimateCpDelta()`를 실제 `EquipmentManager.GetData()` + 현재 장착 비교로 교체 필요

#### [GAP-02] ~~1~3차 전직에 스탯 보너스 없음~~ [해결됨 — R6]
- **해결 완료**: `ApplyJobTierModifiers()` + `TierBonusPercents = {0.05f, 0.08f, 0.12f, 0.15f}` 적용. 1~4차 누적 보너스 ATK/DEF/HP에 AddModifier(ModifierSource.Job) 사용.

#### [GAP-03] ~~CpChangedEvent의 Reason 필드가 항상 빈 문자열~~ [해결됨 — R6]
- **해결 완료**: `SetCpReason()` 메서드 추가. 각 시스템에서 modifier 적용 전 reason 설정 ("전직", "장비 장착" 등). `_pendingCpReason` 필드로 추적 후 CpChangedEvent에 전달.

### 2.2 중요 (성장 가시성 저하)

#### [GAP-04] ~~EquipmentManager가 레거시 AddBonus 사용~~ [해결됨 — R5]
- **해결 완료**: `AddModifier(ModifierSource.Equipment)` + `ClearModifiers(ModifierSource.Equipment)` 사용. 슬롯별 키로 개별 추적.

#### [GAP-05] ~~CompanionEffectApplier도 레거시 AddBonus 사용~~ [해당 없음 — 세션20]
- **해당 없음**: 동반자 시스템 전체 삭제 (세션20 범위 축소). CompanionEffectApplier.cs 파일 부재.

#### [GAP-06] RecommendationManager.AutoEquipRecommended()가 호출되지 않음
- **현상**: `RecommendationManager.cs:291~307`에 "추천 장비 자동 장착" 기능이 구현되어 있지만, **어떤 UI에서도 호출하는 코드가 없음**
- **영향**: 추천 시스템이 있지만 유저에게 노출되지 않음
- **해결**: EquipmentPanel에 "추천 장착" 버튼 + GachaCompareSystem에서 "자동 장착" 버튼 연결

#### [GAP-07] RecommendationManager.AutoFormParty()도 미호출
- **현상**: `RecommendationManager.cs:312~331`의 "추천 편성" 기능이 UI 미연결
- **해결**: PartyPanel/CompanionPanel에 "추천 편성" 버튼 추가

#### [GAP-08] GachaCompareSystem이 EquipComparePopup에 "전투력" 단일 스탯만 전달
- **현상**: `GachaCompareSystem.cs:112~125`에서 `StatDelta[]`에 "전투력" 1개만 넣어 `ShowComparison()` 호출
- **영향**: 장비 비교 팝업이 개별 스탯(ATK/DEF/HP) 비교를 보여주지 않음
- **해결**: 실제 장비 데이터 조회 후 ATK/DEF/HP/CritRate 등 개별 스탯 delta 배열 생성

### 2.3 보통 (UX 개선 필요)

#### [GAP-09] AcquisitionShortcutPopup의 "장착하기" 버튼이 패널만 열어줌
- **현상**: `AcquisitionShortcutPopup.ExecuteAction()` (line 266~284)이 `UIManager.OpenPopup<BasePopup>("EquipmentPanel")`로 패널만 오픈
- **영향**: 유저가 패널 열고 → 아이템 찾고 → 장착 버튼 눌러야 함 (3단계)
- **해결**: 획득한 아이템을 선택된 상태로 패널 오픈 + "바로 장착" 원클릭 지원

#### [GAP-10] ~~CurrencyShortagePopup의 NavigateToSource()에서 일부 패널 미연결~~ [해결됨 — 세션22]
- **해결 완료**: shop→TabBarUI.SelectTab(3), quest/achievement→HudPanel.ShowToast() 안내, costume→토스트. SelectTab 인덱스도 5→3 수정.

#### [GAP-11] ~~GachaEffect 연출 종료 후 GachaCompareSystem 타이밍 불확실~~ [해결됨 — R6]
- **해결 완료**: `GachaEffect.IsPlaying` 프로퍼티 폴링 방식으로 전환. 100ms 간격 체크, 최대 10초 대기 후 300ms 추가 지연. 하드코딩 3500ms 제거.

---

## 3. 성장 파이프라인 재설계

### 3.1 "가챠 → 비교 → 장착 → CP 변화 → 전투 체감" 파이프라인

#### 현재 (끊긴 흐름)
```
가챠 → [GachaResultEvent] → 인벤토리 추가 → [InventoryChangedEvent]
                                              ↓
                         GachaEffect 연출 → GachaCompareSystem (추정치만)
                                              ↓
                         AcquisitionShortcutPopup → 패널 열기만
                                              ↓
                         (유저가 알아서 장착) → EquipmentChangedEvent → CP 변화
```

#### 목표 (완전 연결 흐름)
```
가챠 → [GachaResultEvent]
  ├→ EquipmentManager.AddToInventory() → [EquipmentInventoryChangedEvent]
  ├→ GachaEffect 연출 (등급별 카드)
  └→ [GachaEffectCompleteEvent] ← 신규 이벤트
       ↓
  GachaCompareSystem (실제 데이터 비교)
       ↓
  ┌─ CP 향상 아이템 있음? ──────────────────────────┐
  │ YES                                              │
  │  EquipComparePopup.ShowComparison(               │
  │    실제 스탯 delta: ATK, DEF, HP, CritRate,      │
  │    CP before/after)                              │
  │    + "자동 장착" 버튼                             │
  │    + "나중에" 버튼                                │
  │         ↓                                        │
  │  [자동 장착 클릭]                                 │
  │    RecommendationManager.AutoEquipRecommended()  │
  │    → EquipmentManager.Equip()                    │
  │    → CombatStats.AddBonus() → CpChangedEvent     │
  │    → CpCounterWidget 카운트업 + Reason 표시       │
  │                                                  │
  │ NO                                               │
  │  AcquisitionShortcutPopup만 표시                  │
  └──────────────────────────────────────────────────┘
```

### 3.2 필요한 변경 사항 (구체적)

#### A. 신규 이벤트

```csharp
// GameEvents.cs에 추가
public struct GachaEffectCompleteEvent : IEvent
{
    public int BatchCount;
    public bool HasHighGrade; // 에픽 이상 포함 여부
}
```

#### B. GachaEffect.cs 수정
- `ProcessQueue()` 완료 시점(line 171 부근)에서 `GachaEffectCompleteEvent` 발행
- 현재: `_isPlaying = false;` 직후
- 추가: `EventBus.Publish(new GachaEffectCompleteEvent { ... });`

#### C. GachaCompareSystem.cs 수정

1. **GachaEffectCompleteEvent 구독으로 타이밍 변경** (하드코딩 3500ms 제거)
2. **EstimateCpDelta() → 실제 데이터 비교로 교체:**

```csharp
private long CalculateRealCpDelta(GachaResultEvent result)
{
    if (result.PoolName == "Equipment" && EquipmentManager.Instance != null)
    {
        var data = EquipmentManager.Instance.GetData(result.ItemId);
        if (data == null) return 0;

        // 현재 해당 슬롯에 장착된 장비의 CP 기여
        var equipped = EquipmentManager.Instance.GetEquipped(data.slot);
        int equippedCp = 0;
        if (equipped != null)
        {
            var eqData = EquipmentManager.Instance.GetData(equipped.equipmentId);
            if (eqData != null)
                equippedCp = eqData.GetAtk(equipped.grade) * 3 + eqData.GetDef(equipped.grade) * 2 + (int)(eqData.GetHp(equipped.grade) * 0.5f);
        }

        // 새 아이템의 CP 기여
        int newCp = data.GetAtk(result.Grade) * 3 + data.GetDef(result.Grade) * 2 + (int)(data.GetHp(result.Grade) * 0.5f);
        return newCp - equippedCp;
    }

    // 동료 풀: 동료 효과 기반 추정
    if (result.PoolName == "Companion" && CompanionManager.Instance != null)
    {
        var data = CompanionManager.Instance.GetData(result.ItemId);
        if (data == null) return 0;
        // equipEffect 값을 CP 공식에 맞게 변환
        return (long)(data.equipEffectValue * 10); // 대략적 환산
    }

    return 0;
}
```

3. **ShowComparison()에 개별 스탯 delta 전달:**

```csharp
var deltas = new StatDelta[]
{
    new StatDelta { StatName = "공격력", Before = currentAtk, After = currentAtk + newAtk },
    new StatDelta { StatName = "방어력", Before = currentDef, After = currentDef + newDef },
    new StatDelta { StatName = "체력", Before = currentHp, After = currentHp + newHp },
    new StatDelta { StatName = "전투력", Before = (int)currentCp, After = (int)(currentCp + cpDelta) }
};
```

#### D. CpChangedEvent Reason 채우기

**방법 1 (간단)**: CombatStats에 `_lastModifyReason` 필드 추가

```csharp
private string _lastModifyReason = "";

public void AddModifier(string key, StatModifier modifier, string reason = "")
{
    _lastModifyReason = reason;
    _modifiers[key] = modifier;
    RecalculateModifiers();
}

// PublishCpChangeIfNeeded에서:
EventBus.Publish(new CpChangedEvent
{
    PreviousCp = _previousPowerScore,
    CurrentCp = currentCp,
    Delta = delta,
    Reason = _lastModifyReason
});
_lastModifyReason = "";
```

**방법 2 (정교)**: 각 시스템에서 reason을 직접 포함

```csharp
// EquipmentManager에서:
_playerStats.AddBonus(atk: ..., reason: "장비 장착");

// JobSystem에서:
_playerStats.AddModifier("job_tier4_atk", modifier, reason: "4차 전직");

// CompanionEffectApplier에서:
_playerStats.AddBonus(atk: ..., reason: "동료 효과");
```

#### E. EquipmentManager/CompanionEffectApplier modifier 마이그레이션

EquipmentManager.RecalculateAndApplyBonuses()를 modifier 기반으로 전환:

```csharp
private void RecalculateAndApplyBonuses()
{
    FindPlayerStats();
    if (_playerStats == null) return;

    // 기존 장비 modifier 전체 제거
    _playerStats.ClearModifiers(ModifierSource.Equipment);

    // 각 슬롯별 개별 modifier 추가
    foreach (var kv in _equipped)
    {
        var data = GetData(kv.Value.equipmentId);
        if (data == null) continue;

        string key = $"equip_{kv.Key}";
        // ATK
        if (data.GetAtk(kv.Value.grade) > 0)
            _playerStats.AddModifier($"{key}_atk", new StatModifier(
                ModifierSource.Equipment, kv.Value.instanceId, StatType.Atk,
                data.GetAtk(kv.Value.grade), 0f));
        // DEF, HP, CritRate, AtkSpd 동일 패턴
    }
}
```

### 3.3 "자동 장착" 원클릭 흐름 구현

GachaCompareSystem + EquipComparePopup 연동:

```csharp
// EquipComparePopup에 "자동 장착" 버튼 콜백 추가
public void ShowComparisonWithAutoEquip(string title, StatDelta[] deltas,
    long cpBefore, long cpAfter, System.Action onAutoEquip)
{
    ShowComparison(title, deltas, cpBefore, cpAfter);

    // "자동 장착" 버튼 활성화 + 콜백 연결
    _autoEquipButton.gameObject.SetActive(true);
    _autoEquipButton.onClick.RemoveAllListeners();
    _autoEquipButton.onClick.AddListener(() =>
    {
        onAutoEquip?.Invoke();
        Hide();
    });
}
```

---

## 4. "성장 벽" 설계

### 4.1 벽 구조 개요

| 벽 | 트리거 조건 | 체감 | 해결 경로 (시간) | 해결 경로 (과금) |
|----|------------|------|------------------|-----------------|
| 전직 벽 (Lv.40/80/120/160) | 레벨업 속도 급감 | "레벨이 안 오른다" | 던전 EXP 소진 + 오프라인 누적 | 루비로 EXP 부스트 구매 |
| 장비 강화 벽 (+5/+10/+15) | 강화 성공률 하락 | "강화가 안 된다" | 룬 조각 파밍 + 재시도 | 루비로 룬 조각/보호권 구매 |
| 던전 열쇠 벽 | 일일 열쇠 소진 | "더 할 게 없다" | 다음날 열쇠 리셋 대기 | 루비로 열쇠 추가 구매 |
| 탑 진입 벽 (50/100/200/300층) | CP 미달 | "몬스터가 안 죽는다" | 장비/동료 강화 후 재도전 | 가챠로 상위 장비 획득 |
| 가챠 천장 벽 | 소환권/루비 소진 | "뽑을 수가 없다" | 일일 퀘스트/배틀패스로 누적 | 루비 직접 구매 |

### 4.2 전직 벽 상세 설계

#### Lv.40 (1차 전직): 초보자 졸업 벽
```
[유저 상태] Lv.35~39, CP 약 800~1200
[벽 체감] EXP 필요량 급증 (RequiredExp(40) = 20 + 40^2 * 5 = 8,020)
[현재 문제] 전직해도 스탯 변화 없음 (GAP-02)

[해결 설계]
  1. JobSystem에 1차 전직 보너스 추가: Atk/Def/MaxHp +5% (ModifierSource.Job)
  2. 전직 시 신규 스킬 2개 자동 해금 (이미 SkillSystem에서 처리)
  3. 전직 전후 비교 팝업:
     "견습 전사 → 나이트"
     "공격력 +5%, 방어력 +5%, 체력 +5%"
     "CP 1,200 → 1,420 (+18.3%)"
  4. 전직 축하 연출: JobAdvanceEffect + 화면 전환
  5. 전직 축하 보상: 루비 500 + 장비 상자 1개 + 던전 열쇠 x3

[유도 흐름]
  GoalGuideWidget: "Lv.40 달성 → 1차 전직!"
  → 레벨 부족 시: "경험치 던전 추천!" (DungeonRecommendation)
  → 던전 열쇠 부족 시: CurrencyShortagePopup → "열쇠 구매" or "내일 리셋 대기"
```

#### Lv.80 (2차 전직): 중급자 벽
```
[유저 상태] Lv.70~79, CP 약 3,000~5,000
[벽 체감] EXP 32,020 필요. 일반 파밍으로 1~2시간.

[해결 설계]
  1. 2차 전직 보너스: Atk/Def/MaxHp +8%
  2. 전직 시 스킬 트리 확장 + 패시브 스킬 3개 추가
  3. "장비 에픽 등급 확보" 서브 목표 제시
  4. 전직 보상: 루비 1,500 + 10연차 소환권 + 코스튬 상자

[유도 흐름]
  GoalGuideWidget: "Lv.80 → 2차 전직! 새 스킬이 기다립니다!"
  → CurrencyShortagePopup 연동: EXP 부족 → 던전/오프라인 보상 활용 안내
```

#### Lv.120 (3차 전직): 고급자 벽
```
[유저 상태] Lv.110~119, CP 약 10,000~15,000
[벽 체감] EXP 72,020 필요. 파밍만으로 3~5시간.

[해결 설계]
  1. 3차 전직 보너스: Atk/Def/MaxHp +12%
  2. 궁극기 스킬 해금 (SkillUltimateVfx 연출)
  3. 마스터리 트리 해금 (MasteryManager)
  4. 전직 보상: 루비 5,000 + 선택 장비 상자 + 코스튬 세트 1개

[유도 흐름]
  GoalGuideWidget: "3차 전직 후 마스터리가 해금됩니다!"
  → 마스터리 = 새로운 성장 축 → 환생까지의 브릿지 콘텐츠
```

#### Lv.160 (4차 전직): 엔드게임 입장 벽
```
[유저 상태] Lv.150~159, CP 약 25,000~40,000
[벽 체감] EXP 128,020 + 탑 300층 조건 (이중 벽)
[현재 구현] 탑 300층 미달 시 전직 불가 (JobSystem.cs:205~211)

[해결 설계]
  1. 4차 전직 보너스: Atk/Def/MaxHp +15% (이미 구현)
  2. 탑 300층 조건 → "탑 클리어" 서브 목표로 전환
  3. 전직 보상: 루비 10,000 + 미스틱 장비 확정 상자
  4. 전직 후 환생(Prestige) 시스템 본격 소개

[유도 흐름]
  GoalGuideWidget: "탑 300층 돌파가 필요합니다! (현재: 280/300)"
  → 탑 공략 어려움 → 장비/동료 강화 → 던전 → 재화 소비 루프 유도
```

### 4.3 장비 강화 벽 설계

```
[강화 단계별 성공률]
  +1~+5:  100% (안전 구간 — 초보자 학습)
  +6~+10: 80% → 60% → 50% ... (실패 경험 시작)
  +11~+15: 40% → 30% ... (실패 누적 → 천장 필요)
  +16~+20: 20% → 15% ... (엔드게임 최적화)

[실패 보상 설계] — "실패해도 진전이 있다"
  1. 연속 실패 보너스: 실패할수록 다음 성공률 +5% (천장)
  2. 실패 시 "강화 경험치" 누적 → 10회 실패 시 무료 1회 확정 강화
  3. 실패 연출: 빨간 이펙트 + 화면 흔들림 + BUT "다음 시도: 성공률 +5%!" 텍스트
  4. 보호권 아이템: 실패해도 강화 수치 하락 방지 (과금 or 이벤트 획득)

[CurrencyShortagePopup 연동]
  룬 조각 부족 → "강화 던전으로" 바로가기
  별의 결정 부족 → "스테이지 보스 도전" + "상점에서 구매"
```

### 4.4 던전 열쇠 벽 설계

```
[일일 열쇠 배분]
  기본 열쇠: 5개/일
  출석 보너스: +1개
  배틀패스: +2개 (프리미엄)
  이벤트: +1~3개

[벽 체감 시점] 일일 루틴 15분 완료 후
  → "더 할 게 없다" 감정

[해결 흐름]
  1. 열쇠 소진 시 GoalGuideWidget 자동 전환:
     "오늘의 던전 완료! 다음 목표: 탑 도전"
  2. 탑은 열쇠 불필요 → 무한 콘텐츠로 유도
  3. 광고 시청으로 열쇠 1개 추가 (AdRewardManager 연동)
  4. 열쇠 쿨타임 표시: "다음 열쇠까지 2시간 30분"
  5. CurrencyShortagePopup:
     "던전 열쇠가 부족합니다!"
     [광고 시청 (+1)] [루비로 구매 (+3, 300루비)] [내일 리셋 대기]
```

### 4.5 탑 진입 벽 (CP 게이팅) 설계

```
[층수별 권장 CP]
  1~50층:    CP 500~3,000   (신규 유저)
  51~100층:  CP 3,000~10,000 (1차 전직 후)
  101~200층: CP 10,000~25,000 (2~3차 전직)
  201~300층: CP 25,000~50,000 (3차 전직 + 장비 강화)
  301~500층: CP 50,000+       (4차 전직 + 마스터리)

[벽 체감] "이전 층은 쉽게 클리어했는데 갑자기 안 된다"

[해결 흐름]
  1. TowerManager에 권장 CP 표시:
     "현재 CP: 8,500 | 권장 CP: 10,000 (-15%)"
  2. CP 미달 시 RecommendationManager 자동 호출:
     "장비 강화로 CP +800 가능!"
     "추천 동료 편성으로 CP +500 가능!"
  3. GoalGuideWidget: "탑 100층 돌파! (현재 CP: 8,500, 권장: 10,000)"
  4. 실패 시 팝업:
     "몬스터가 너무 강합니다!"
     "추천: 장비 강화 (+800 CP) → [강화 화면으로]"
     "추천: 동료 편성 변경 (+500 CP) → [편성 화면으로]"
     "추천: 던전에서 재료 수급 → [던전으로]"
```

---

## 5. 구현 우선순위 (영향도 순)

### Tier 1 — 성장 체감 직접 개선 (즉각 효과)

| ID | 작업 | 관련 GAP | 예상 공수 |
|----|------|---------|----------|
| GP-01 | 1~3차 전직 스탯 보너스 추가 (JobSystem) | GAP-02 | 소 |
| GP-02 | CpChangedEvent.Reason 채우기 (CombatStats) | GAP-03 | 소 |
| GP-03 | GachaCompareSystem 실제 데이터 비교 교체 | GAP-01, GAP-08 | 중 |
| GP-04 | EquipComparePopup에 "자동 장착" 버튼 추가 | GAP-01 | 소 |

### Tier 2 — 시스템 정합성 (안정성)

| ID | 작업 | 관련 GAP | 예상 공수 |
|----|------|---------|----------|
| GP-05 | EquipmentManager modifier 마이그레이션 | GAP-04 | 중 |
| GP-06 | CompanionEffectApplier modifier 마이그레이션 | GAP-05 | 중 |
| GP-07 | GachaEffectCompleteEvent 추가 + 타이밍 정비 | GAP-11 | 소 |

### Tier 3 — UX 연결 완성

| ID | 작업 | 관련 GAP | 예상 공수 |
|----|------|---------|----------|
| GP-08 | RecommendationManager UI 버튼 연결 | GAP-06, GAP-07 | 중 |
| GP-09 | AcquisitionShortcutPopup "바로 장착" 기능 | GAP-09 | 중 |
| GP-10 | CurrencyShortagePopup 미연결 패널 완성 | GAP-10 | 소 |

### Tier 4 — 성장 벽 시스템

| ID | 작업 | 예상 공수 |
|----|------|----------|
| GP-11 | 강화 실패 천장 시스템 (연속 실패 보너스) | 중 |
| GP-12 | 탑 권장 CP 표시 + 실패 시 추천 팝업 | 중 |
| GP-13 | 던전 열쇠 쿨타임/광고 추가 획득 | 소 |

---

## 6. 성장 순환 완성도 평가

### 현재 순환 상태 (10점 만점)

| 순환 | 연결도 | 체감도 | 종합 | 비고 |
|------|--------|--------|------|------|
| 레벨업 → 스탯 성장 | 10/10 | 8/10 | 9 | CP 변화 연출 양호, Reason 미표시 |
| 가챠 → 장비 획득 → 장착 | 7/10 | 4/10 | 5.5 | 자동 장착 미연결, 비교가 추정치 |
| 가챠 → 동료 획득 → 편성 | 7/10 | 5/10 | 6 | 편성 수동, 추천 미노출 |
| 장비 강화 → CP 상승 | 8/10 | 6/10 | 7 | 강화 실패 보상 부재 |
| 전직 → 성장 체감 | 3/10 | 2/10 | 2.5 | 1~3차 보너스 없음 (심각) |
| 던전 → 재화 → 강화 | 9/10 | 7/10 | 8 | 재화 부족 팝업 잘 연결 |
| 탑 → 랭킹 → 보상 | 8/10 | 7/10 | 7.5 | 권장 CP 미표시 |
| 환생 → 영구 보너스 | 9/10 | 8/10 | 8.5 | modifier 정상 연결 |

**전체 평균: 6.75/10**

가장 시급한 개선점: **전직 보너스 (2.5점)** → **가챠-장착 연결 (5.5점)** → **CP Reason 표시 (8점→10점)**

---

## 변경 이력

| 날짜 | 변경 내용 |
|------|-----------|
| 2026-03-17 | 초안 작성 — 13종 성장 출처 맵핑, 11개 GAP 파악, 파이프라인 재설계, 성장 벽 5종 설계 |
