# 등반 조합 시스템 (Climbing Party System)

> 작성일: 2026-03-16
> 근거: next-content-design.md §2.2.1, CompanionManager/CompanionCombat/CompanionEffectApplier 분석

---

## 1. 개요

기존 동료 시스템("메인 동료 30초 소환 + 서브 동료 패시브 4슬롯")을 **최대 4인 파티 편성** 시스템으로 확장한다. 플레이어를 포함한 4명이 각자 고유한 역할(선봉/정찰/지원/비장)을 수행하며, 직업 조합에 따라 **조합 보너스**가 적용된다.

### 핵심 목표
- 동료 수집의 목적성 강화 (단순 패시브 → 역할별 전투 참여)
- 조합 최적화라는 전략적 요소 추가
- 기존 CompanionManager의 메인/서브 슬롯을 파티 슬롯으로 자연스럽게 확장
- CombatStats modifier 시스템과 통합 (`ModifierSource.Party`)

---

## 2. 파티 슬롯 구조

### 2.1 PartySlot enum

```csharp
public enum PartySlot
{
    /// <summary>선봉 — 플레이어 캐릭터 (항상 고정)</summary>
    Vanguard,
    /// <summary>정찰 — 자동 소환 동료, 원거리 견제 + 아이템 자동 수집</summary>
    Scout,
    /// <summary>지원 — 패시브 동료, 힐/버프/디버프</summary>
    Support,
    /// <summary>비장 — 궁극기 동료, 60초 쿨타임 강력한 단일 스킬</summary>
    Ace
}
```

### 2.2 슬롯별 역할 상세

| 슬롯 | 역할 | 행동 방식 | 기존 시스템 대응 |
|------|------|-----------|-----------------|
| **선봉 (Vanguard)** | 메인 딜러/탱커 | 플레이어 직접 조작 | PlayerCharacter (변경 없음) |
| **정찰 (Scout)** | 원거리 견제 + 아이템 수집 | 자동 소환, 플레이어 추적, 몬스터 공격 + 드롭 자동 수집 | 기존 CompanionCombat 확장 |
| **지원 (Support)** | 힐/버프/디버프 | 패시브 효과 (장착만으로 발동) | 기존 CompanionEffectApplier 확장 |
| **비장 (Ace)** | 궁극기 발동 | 60초 쿨타임, 강력한 단일 스킬 (자동 발동) | 신규 — CompanionUltimate |

### 2.3 슬롯 해금 조건

| 슬롯 | 해금 조건 | 근거 |
|------|-----------|------|
| Vanguard | 시작 시 | 플레이어 본인 |
| Scout | 챕터 2 (11층) 클리어 | 기존 메인 동료 해금 시점과 동일 |
| Support | 챕터 4 (31층) 클리어 | 기존 서브 슬롯 1 해금 시점 |
| Ace | 챕터 7 (61층) 클리어 | 중반부 전략 확장 시점 |

---

## 3. 조합 보너스 시스템

### 3.1 PartyBonusType enum

```csharp
public enum PartyBonusType
{
    None,
    /// <summary>같은 직업 2명 → 해당 직업 주 스탯 +10%</summary>
    SameJobx2,
    /// <summary>같은 직업 3명 → 해당 직업 주 스탯 +20%</summary>
    SameJobx3,
    /// <summary>같은 직업 4명 → 해당 직업 주 스탯 +30%</summary>
    SameJobx4,
    /// <summary>전사+궁수+마법사 혼합 → 방어력+공격력+쿨감 각 5%</summary>
    BalancedMix
}
```

### 3.2 동직업 조합 보너스 테이블

플레이어 직업 + 동료 직업을 합산하여 같은 직업 수를 계산한다.

| 조건 | 보너스 타입 | 효과 |
|------|-------------|------|
| 같은 직업 2명 | SameJobx2 | 해당 직업 주 스탯 +10% |
| 같은 직업 3명 | SameJobx3 | 해당 직업 주 스탯 +20% |
| 같은 직업 4명 | SameJobx4 | 해당 직업 주 스탯 +30% |

#### 직업별 "주 스탯" 정의

| 직업 | 주 스탯 | StatType |
|------|---------|----------|
| 전사 (Warrior) | 방어력 + 최대HP | Def, MaxHp |
| 궁수 (Archer) | 공격속도 + 치명타율 | AttackSpeed, CritRate |
| 마법사 (Mage) | 공격력 + 치명타피해 | Atk, CritDamage |

> 예시: 플레이어(전사) + Scout(전사) + Support(전사) = 전사 3명 → 방어력 +20%, 최대HP +20%

### 3.3 혼합 조합 보너스 (BalancedMix)

파티에 전사/궁수/마법사가 **모두 1명 이상** 포함되면 발동:

| 효과 | 수치 | StatType |
|------|------|----------|
| 방어력 증가 | +5% | Def |
| 공격력 증가 | +5% | Atk |
| 공격속도 증가 (쿨감) | +5% | AttackSpeed |

> 동직업 보너스와 혼합 보너스는 **중복 불가** — 가장 높은 보너스 1개만 적용.
> (같은 직업 4명 = 30% vs 혼합 = 5%씩 3종 → 유저가 전략적으로 선택)

### 3.4 조합 보너스 우선순위

```
1. 파티 내 직업 분포 계산 (Vanguard 포함)
2. 동직업 최대 수 확인 → SameJobx2/x3/x4 중 해당
3. 전사+궁수+마법사 모두 존재 → BalancedMix 후보
4. 두 조건 모두 충족 시 → 더 높은 총 보너스를 적용
   (SameJobx2 = 10% x 2스탯 vs BalancedMix = 5% x 3스탯 → SameJobx2 승)
5. 최종 결정된 보너스를 CombatStats modifier로 적용
```

### 3.5 전체 조합 보너스 시나리오

| 편성 예시 | 직업 분포 | 적용 보너스 | 효과 |
|-----------|-----------|-------------|------|
| 전사/전사/전사/전사 | W4 | SameJobx4 | Def +30%, MaxHp +30% |
| 전사/전사/궁수/마법사 | W2 A1 M1 | SameJobx2 | Def +10%, MaxHp +10% |
| 전사/궁수/마법사/궁수 | W1 A2 M1 | SameJobx2 + BalancedMix 중 택1 | A2: AtkSpd +10%, CritRate +10% |
| 전사/궁수/마법사/마법사 | W1 A1 M2 | SameJobx2 + BalancedMix 중 택1 | M2: Atk +10%, CritDmg +10% |
| 전사/궁수/마법사/— | W1 A1 M1 | BalancedMix | Def +5%, Atk +5%, AtkSpd +5% |

---

## 4. CompanionDataSO 확장

### 4.1 필요한 필드 추가

현재 `CompanionDataSO`에는 직업 타입이 없다. 파티 조합 보너스 계산을 위해 추가 필요:

```csharp
[Header("파티 시스템")]
public JobType companionJob;              // 동료의 직업 (조합 보너스 계산용)
public PartySlot preferredSlot;           // 권장 슬롯 (UI 힌트용, 강제 아님)

[Header("비장 스킬 (Ace 슬롯 전용)")]
public string ultimateSkillId;            // 궁극기 ID
public float ultimateCooldown = 60f;      // 궁극기 쿨타임
public float ultimateDamageRatio = 5.0f;  // 플레이어 ATK 대비 배율
public float ultimateRange = 5f;          // 궁극기 범위
```

### 4.2 기존 필드 호환

- `atkRatio`, `hpRatio`, `attackSpeed`, `moveSpeed` → Scout 슬롯 소환 시 그대로 사용 (기존 CompanionCombat 로직 호환)
- `equipEffect`, `equipEffectValue` → Support 슬롯 장착 효과 그대로 사용 (기존 CompanionEffectApplier 호환)
- 새로운 궁극기 필드 → Ace 슬롯 전용

---

## 5. ClimbingPartyManager API

### 5.1 클래스 설계

```
네임스페이스: MkLike.Companion
파일: Assets/Scripts/Companion/ClimbingPartyManager.cs
싱글톤: ClimbingPartyManager.Instance
```

### 5.2 주요 API

```csharp
public class ClimbingPartyManager : MonoBehaviour
{
    public static ClimbingPartyManager Instance { get; private set; }

    // ── 슬롯 관리 ──

    /// <summary>
    /// 파티 슬롯에 동료를 배치한다.
    /// Vanguard는 항상 플레이어이므로 설정 불가.
    /// </summary>
    public bool SetSlot(PartySlot slot, string companionId);

    /// <summary>
    /// 파티 슬롯에서 동료를 해제한다.
    /// </summary>
    public bool RemoveSlot(PartySlot slot);

    /// <summary>
    /// 특정 슬롯에 배치된 동료 ID를 반환한다.
    /// </summary>
    public string GetSlotCompanionId(PartySlot slot);

    /// <summary>
    /// 특정 슬롯이 해금되었는지 확인한다.
    /// </summary>
    public bool IsSlotUnlocked(PartySlot slot);

    // ── 조합 보너스 ──

    /// <summary>
    /// 현재 파티 편성에 따른 활성 조합 보너스를 반환한다.
    /// </summary>
    public PartyBonusType GetPartyBonus();

    /// <summary>
    /// 현재 파티 편성의 직업 분포를 반환한다.
    /// key = JobType, value = 해당 직업 인원 수 (플레이어 포함)
    /// </summary>
    public Dictionary<JobType, int> GetFormation();

    /// <summary>
    /// 현재 조합 보너스의 상세 수치를 반환한다.
    /// </summary>
    public List<(StatType stat, float percentBonus)> GetBonusDetails();

    // ── CombatStats 연동 ──

    /// <summary>
    /// 파티 보너스를 플레이어 CombatStats에 modifier로 적용한다.
    /// 편성 변경 시 자동 호출된다.
    /// </summary>
    private void ApplyPartyModifiers();

    /// <summary>
    /// 기존 파티 modifier를 제거한다.
    /// </summary>
    private void ClearPartyModifiers();

    // ── 세이브 ──

    public void SyncToSaveData(SaveData saveData);
    public void SyncFromSaveData(SaveData saveData);
}
```

### 5.3 CombatStats modifier 연동

파티 보너스는 `ModifierSource.Party`를 통해 플레이어 CombatStats에 적용한다.

**ModifierSource enum 확장:**
```csharp
public enum ModifierSource
{
    Equipment,
    Companion,
    Relic,
    Inscription,
    TowerGimmick,
    Buff,
    Job,
    HunterRank,
    Prestige,
    Blessing,
    Party          // ← 신규 추가
}
```

**modifier key 규칙:**
```
"party_bonus_{StatType}" — 예: "party_bonus_Atk", "party_bonus_Def"
```

**적용 예시 (SameJobx3 전사):**
```csharp
playerStats.AddModifier("party_bonus_Def", new StatModifier(
    source: ModifierSource.Party,
    sourceId: "SameJobx3_Warrior",
    statType: StatType.Def,
    flatBonus: 0f,
    percentBonus: 0.2f   // +20%
));
playerStats.AddModifier("party_bonus_MaxHp", new StatModifier(
    source: ModifierSource.Party,
    sourceId: "SameJobx3_Warrior",
    statType: StatType.MaxHp,
    flatBonus: 0f,
    percentBonus: 0.2f   // +20%
));
```

---

## 6. CompanionManager 연동 방식

### 6.1 기존 → 신규 전환 전략

기존 CompanionManager의 메인/서브 슬롯을 파티 슬롯으로 **점진적 확장**한다. 기존 API는 유지하되, 내부적으로 ClimbingPartyManager에 위임한다.

```
기존 구조:
  CompanionManager._mainCompanionId → 메인 동료 (30초 소환)
  CompanionManager._subSlots[0~3]  → 서브 동료 (패시브)

신규 구조:
  ClimbingPartyManager.Vanguard → 플레이어 (고정)
  ClimbingPartyManager.Scout   → 기존 _mainCompanionId 대체
  ClimbingPartyManager.Support → 기존 _subSlots[0] 대체
  ClimbingPartyManager.Ace     → 신규 궁극기 슬롯
```

### 6.2 마이그레이션

1. `ClimbingPartyManager`가 초기화 시 기존 세이브 데이터 확인
2. `_mainCompanionId`가 있으면 → Scout 슬롯으로 자동 이관
3. `_subSlots[0]`이 있으면 → Support 슬롯으로 자동 이관
4. 나머지 서브 슬롯은 "추가 패시브"로 유지 (기존 CompanionEffectApplier가 처리)

### 6.3 CompanionCombat 확장

- Scout 슬롯: 기존 CompanionCombat 로직 그대로 사용 (자동 소환 전투)
- **추가 기능**: Scout에 아이템 자동 수집 AI 추가
  - 전투 타겟이 없을 때 근처 드롭 아이템을 감지하여 이동 → 수집
  - 수집 범위: 반경 3 (하드코딩 대신 SO 필드로 노출)

### 6.4 CompanionEffectApplier 확장

- Support 슬롯: 기존 패시브 효과 로직 그대로 사용
- **변경점 없음** — Support 슬롯의 동료 ID를 CompanionManager.SubSlots에서 읽는 대신, ClimbingPartyManager에서 읽도록 참조만 변경

### 6.5 CompanionUltimate (신규)

Ace 슬롯 전용 궁극기 시스템:

```csharp
public class CompanionUltimate : MonoBehaviour
{
    // 60초 쿨타임 자동 발동
    // 발동 시:
    //   1. 화면 연출 (DOTween 줌인 + 이펙트)
    //   2. 범위 내 몬스터에게 (플레이어ATK * ultimateDamageRatio) 데미지
    //   3. 쿨다운 시작
    // CancellationToken 연동 필수
}
```

---

## 7. UI 플로우

### 7.1 파티 편성 화면 진입

```
하단 탭 [캐릭터] → 서브탭 [동료] → 파티 편성 버튼
또는
하단 탭 [캐릭터] → 서브탭 [파티] (신규 서브탭 추가 가능)
```

### 7.2 파티 편성 UI 구조

```
┌──────────────────────────────────────────┐
│  ◀ 파티 편성                         ✕  │
├──────────────────────────────────────────┤
│                                          │
│  ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐    │
│  │ 선봉 │  │ 정찰 │  │ 지원 │  │ 비장 │    │
│  │[플레 │  │[동료 │  │[동료 │  │[동료 │    │
│  │이어] │  │아이콘]│  │아이콘]│  │아이콘]│    │
│  │ 전사 │  │ 궁수 │  │ 마법 │  │ 전사 │    │
│  └──┬──┘  └──┬──┘  └──┬──┘  └──┬──┘    │
│     │        │        │        │        │
│  ┌──┴────────┴────────┴────────┴──┐     │
│  │     ⚔ 조합 보너스: 밸런스 조합     │     │
│  │     방어력 +5% / 공격력 +5%      │     │
│  │     공격속도 +5%                 │     │
│  └────────────────────────────────┘     │
│                                          │
│  ─ 보유 동료 목록 ─────────────────────  │
│  [필터: 전체 | 전사 | 궁수 | 마법사]       │
│  ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐   │
│  │동료1│ │동료2│ │동료3│ │동료4│ │동료5│   │
│  │ Lv3│ │ Lv1│ │ Lv2│ │ Lv1│ │ Lv4│   │
│  └────┘ └────┘ └────┘ └────┘ └────┘   │
│                                          │
└──────────────────────────────────────────┘
```

### 7.3 조작 플로우

1. **슬롯 선택**: 빈 슬롯(또는 교체할 슬롯) 터치
2. **동료 목록 필터링**: 슬롯 역할에 적합한 동료 하이라이트 (권장 표시)
3. **동료 선택**: 동료 터치 → 슬롯에 배치
4. **조합 보너스 실시간 프리뷰**: 편성 변경 시 조합 보너스가 즉시 갱신
5. **확인**: 자동 저장 (편성 변경 즉시 반영, 별도 확인 버튼 불필요)

### 7.4 잠금 슬롯 표시

해금 조건 미충족 슬롯은 자물쇠 아이콘 + "XX층 클리어 시 해금" 텍스트 표시.

### 7.5 Ace 슬롯 쿨타임 HUD

전투 중 화면 우측에 Ace 동료 아이콘 + 원형 쿨타임 게이지 표시. 궁극기 발동 시 아이콘이 빛나는 연출.

---

## 8. 세이브 데이터

### 8.1 PartySlotSaveData

```csharp
[Serializable]
public class PartySaveData
{
    public string scoutCompanionId = "";
    public string supportCompanionId = "";
    public string aceCompanionId = "";
}
```

### 8.2 SaveData 확장

```csharp
public class SaveData
{
    // ... 기존 필드 ...
    public PartySaveData party;
}
```

---

## 9. 이벤트

```csharp
/// <summary>파티 슬롯 변경 시 발행</summary>
public struct PartySlotChangedEvent
{
    public PartySlot Slot;
    public string PreviousCompanionId;
    public string NewCompanionId;
}

/// <summary>조합 보너스 변경 시 발행</summary>
public struct PartyBonusChangedEvent
{
    public PartyBonusType BonusType;
    public List<(StatType stat, float percentBonus)> Details;
}
```

---

## 10. 구현 태스크 분해

### Phase: 등반 조합 시스템

| ID | 태스크 | 의존성 | 난이도 | 담당 |
|----|--------|--------|--------|------|
| CP-01 | `PartySlot`, `PartyBonusType` enum 정의 | 없음 | 하 | 시스템 |
| CP-02 | `CompanionDataSO`에 `companionJob`, `preferredSlot`, 궁극기 필드 추가 | 없음 | 하 | 시스템 |
| CP-03 | `ModifierSource.Party` 추가 | 없음 | 하 | 시스템 |
| CP-04 | `ClimbingPartyManager` 코어 구현 (SetSlot/RemoveSlot/GetPartyBonus/GetFormation) | CP-01, CP-02, CP-03 | 중 | 전투 |
| CP-05 | 조합 보너스 계산 로직 + CombatStats modifier 적용 | CP-04 | 중 | 전투 |
| CP-06 | `PartySaveData` + SaveManager 연동 | CP-04 | 하 | 시스템 |
| CP-07 | 기존 CompanionManager → ClimbingPartyManager 마이그레이션 | CP-04, CP-06 | 중 | 전투 |
| CP-08 | CompanionCombat 확장: Scout 아이템 자동 수집 AI | CP-07 | 중 | 전투 |
| CP-09 | `CompanionUltimate` 신규 구현 (Ace 슬롯 궁극기) | CP-04 | 중상 | 전투 |
| CP-10 | 파티 편성 UI (PartyFormationPanel) | CP-04, CP-05 | 중상 | 디자이너 |
| CP-11 | Ace 쿨타임 HUD + 궁극기 발동 연출 | CP-09 | 중 | 디자이너 |
| CP-12 | 기존 CompanionDataSO 에셋에 `companionJob` 값 세팅 (에디터 스크립트) | CP-02 | 하 | 시스템 |
| CP-13 | 파티 시스템 통합 테스트 + 밸런스 검증 | CP-01~CP-12 | 중 | QA |

### 추천 병렬 그룹

```
[라운드 1] CP-01 + CP-02 + CP-03 (독립, enum/SO 확장)
[라운드 2] CP-04 + CP-06 (코어 + 세이브)
[라운드 3] CP-05 + CP-07 + CP-12 (보너스 로직 + 마이그레이션 + 에셋)
[라운드 4] CP-08 + CP-09 + CP-10 (Scout AI + 궁극기 + UI) — 병렬 가능
[라운드 5] CP-11 + CP-13 (HUD + 통합 테스트)
```

### 예상 작업량
- 총 13태스크, 추정 작업량: 에이전트 라운드 3~4회

---

## 11. 밸런스 참고 사항

### 11.1 조합 보너스 파워 수준

- SameJobx4 (+30% 주스탯 2종)는 최대 보너스이지만, **동료 풀에서 같은 직업 4종을 모두 보유**해야 하므로 후반 콘텐츠
- BalancedMix (+5% 3종)은 초보자도 쉽게 달성 가능하여 진입 장벽 낮음
- 조합 보너스는 **기존 장비/유물/각인/프레스티지 보너스와 곱연산** (가산이 아님)

### 11.2 궁극기 밸런스

| 등급 | ultimateDamageRatio | ultimateCooldown | ultimateRange |
|------|---------------------|------------------|---------------|
| Rare | 3.0x | 60초 | 4 |
| Epic | 4.0x | 55초 | 5 |
| Unique | 5.0x | 50초 | 5 |
| Legendary | 6.5x | 45초 | 6 |
| Mythic | 8.0x | 40초 | 7 |

### 11.3 Scout 자동 수집

- 수집 범위: 반경 3 (펫 시스템과 독립, 중첩 가능)
- 수집 속도: 0.5초/아이템
- 펫 시스템 구현 시 Scout 수집과 펫 수집은 **동시 작동** (범위 합산 아님, 각자 독립 수집)
