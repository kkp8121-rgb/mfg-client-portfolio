---
read_count: 2
last_read: "2026-04-03"
status: reference
---

# 코스튬/스킨 시스템 기획서

> 작성일: 2026-03-16
> 참조: next-content-design.md §3.6

---

## 1. 개요

코스튬 시스템은 **순수 외형 변경** 시스템이다. 스탯에 영향을 주지 않으며, 과금 공정성을 유지한다. 전신 세트 완성 시 특수 이펙트(파티클 변경 등)가 발동되지만 스탯 보너스는 없다.

### 핵심 원칙
- **스탯 영향 없음**: 코스튬은 100% 외형 전용. 전투력에 영향 없음
- **과시 + 수집 욕구**: 등급별 희소성, 한정 코스튬으로 수집 동기 부여
- **다양한 획득 경로**: 무과금/과금 모두 코스튬 획득 가능

---

## 2. 데이터 설계

### 2.1 CostumeSlot (enum)

```csharp
public enum CostumeSlot
{
    Head,       // 머리
    Top,        // 상의
    Bottom,     // 하의
    Weapon      // 무기 스킨
}
```

### 2.2 CostumeGrade (enum)

```csharp
public enum CostumeGrade
{
    Normal,     // 일반 — 기본 색상 변경
    Rare,       // 희귀 — 디자인 변경
    Legendary,  // 전설 — 특수 이펙트 포함
    Limited     // 한정 — 시즌 한정, 재판매 없음
}
```

### 2.3 CostumeDataSO (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "MkLike/Costume/CostumeData")]
public class CostumeDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string _id;                // 고유 ID (예: "costume_head_forest_01")
    [SerializeField] private string _displayName;       // 표시명 (예: "숲의 관")
    [SerializeField] private CostumeSlot _slot;         // 장착 부위
    [SerializeField] private CostumeGrade _grade;       // 등급
    [SerializeField] private Sprite _icon;              // 아이콘 (UI 표시용)

    [Header("세트 정보")]
    [SerializeField] private string _setId;             // 세트 ID (빈 값이면 세트 미소속)

    [Header("획득 정보")]
    [SerializeField] private bool _isLimited;           // 한정 여부 (시즌 종료 후 획득 불가)
    [SerializeField] private string _obtainDescription; // 획득 경로 설명 (UI 표시용)

    [Header("비주얼")]
    [SerializeField] private Sprite _equipSprite;       // 장착 시 캐릭터 스프라이트
    [SerializeField] private GameObject _effectPrefab;  // 전설 등급 이상 특수 이펙트 프리팹 (nullable)
}
```

### 2.4 CostumeSetDataSO (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "MkLike/Costume/CostumeSetData")]
public class CostumeSetDataSO : ScriptableObject
{
    [SerializeField] private string _setId;                 // 세트 ID (예: "set_forest")
    [SerializeField] private string _setName;               // 세트 이름 (예: "숲의 수호자")
    [SerializeField] private CostumeDataSO[] _requiredParts;// 세트 구성 코스튬 목록 (4부위)
    [SerializeField] private string _setEffectDescription;  // 세트 이펙트 설명
    [SerializeField] private GameObject _setEffectPrefab;   // 세트 완성 시 특수 이펙트 프리팹
    [SerializeField] private RuntimeAnimatorController _setAnimOverride; // 세트 전용 애니메이션 오버라이드 (nullable)
}
```

---

## 3. CostumeManager API

```csharp
public class CostumeManager : SingletonBase<CostumeManager>, ISaveProvider
{
    // === 핵심 API ===

    /// 코스튬 장착 (이전 장착 자동 해제)
    public bool Equip(string costumeId);

    /// 코스튬 해제
    public bool Unequip(CostumeSlot slot);

    /// 전신 세트 완성 여부 확인
    public bool HasFullSet(string setId);

    /// 현재 장착 중인 코스튬 딕셔너리 반환 (slot → costumeId)
    public Dictionary<CostumeSlot, string> GetEquippedCostumes();

    /// 특정 슬롯 장착 코스튬 반환 (미장착 시 null)
    public CostumeDataSO GetEquipped(CostumeSlot slot);

    // === 보유 관리 ===

    /// 코스튬 획득 (이벤트 발행: CostumeObtainedEvent)
    public void ObtainCostume(string costumeId);

    /// 코스튬 보유 여부
    public bool HasCostume(string costumeId);

    /// 보유 코스튬 목록
    public List<CostumeDataSO> GetOwnedCostumes(CostumeSlot? slotFilter = null);

    // === 세트 관련 ===

    /// 현재 활성화된 세트 이펙트 (전신 완성된 세트)
    public CostumeSetDataSO GetActiveSetEffect();

    /// 특정 세트의 완성 진행도 (0~4)
    public int GetSetProgress(string setId);

    // === 저장/로드 (ISaveProvider) ===
    public string SaveKey { get; }
    public string ToJson();
    public void FromJson(string json);
}
```

### 이벤트

```csharp
// 코스튬 획득 시
public struct CostumeObtainedEvent
{
    public string CostumeId;
    public CostumeGrade Grade;
}

// 코스튬 장착/해제 시
public struct CostumeEquipChangedEvent
{
    public CostumeSlot Slot;
    public string PreviousCostumeId; // null이면 빈 슬롯이었음
    public string NewCostumeId;      // null이면 해제
}

// 세트 완성 시
public struct CostumeSetCompletedEvent
{
    public string SetId;
}
```

---

## 4. 코스튬 등급별 획득 경로

| 등급 | 획득 방법 | 세부 |
|------|-----------|------|
| **일반 (Normal)** | 층 마일스톤 | 매 50층마다 코스튬 1개 해금 |
| **희귀 (Rare)** | 이벤트/길드 상점 | 이벤트 재화 교환, 길드 포인트 교환 |
| **전설 (Legendary)** | 배틀패스 Lv.40+ / PvP 시즌 보상 | 시즌별 1~2종, PvP 마스터 등급 보상 |
| **한정 (Limited)** | 시즌 한정 | 해당 시즌 종료 후 획득 불가, 재판매 없음 |

### 추가 획득처
- **루비 상점**: 상시 판매 코스튬 (일반/희귀 등급, 루비 300~1,000)
- **업적 보상**: 특정 업적 달성 시 코스튬 해금 (예: "1,000층 돌파" → 전설 코스튬)
- **코스튬 조각**: PvP 아레나 상점에서 코스튬 조각 구매, 조각 100개 → 코스튬 1개 교환

---

## 5. 전신 세트 시스템

### 5.1 세트 구성
- 1개 세트 = 4부위 (머리 + 상의 + 하의 + 무기)
- 4부위 모두 장착 시 **세트 이펙트** 발동

### 5.2 세트 이펙트 (스탯 보너스 없음)
세트 이펙트는 **시각적 효과만** 제공한다:

| 세트 예시 | 세트 이펙트 |
|-----------|-------------|
| 숲의 수호자 | 공격 시 나뭇잎 파티클 발생 |
| 화산의 전사 | 캐릭터 주변 화염 오라 |
| 심연의 그림자 | 이동 시 그림자 잔상 |
| 별의 탐험가 | 캐릭터 주변 별빛 파티클 |
| 시즌1 한정 | 전용 공격 이펙트 + 등장 연출 |

### 5.3 세트 진행도 UI
- 세트 목록에서 각 세트의 보유/미보유 부위를 시각적으로 표시
- 미보유 부위는 실루엣 + 획득 경로 안내 표시

---

## 6. UI 플로우

### 6.1 진입 경로
```
캐릭터 탭 > 장비 서브탭 > [코스튬] 버튼
또는
상단 캐릭터 프로필 터치 > 코스튬
```

### 6.2 코스튬 패널 구성

```
┌────────────────────────────────────────────┐
│  [코스튬]                           [X 닫기] │
├────────────────────────────────────────────┤
│                                            │
│   ┌──────────┐    ┌──────────────────┐     │
│   │          │    │ [머리] [상의]     │     │
│   │ 캐릭터   │    │ [하의] [무기]     │     │
│   │ 미리보기  │    │                  │     │
│   │          │    │ 세트: 숲의 수호자  │     │
│   │          │    │ 진행: 3/4        │     │
│   └──────────┘    └──────────────────┘     │
│                                            │
├────────────────────────────────────────────┤
│ 필터: [전체] [머리] [상의] [하의] [무기]     │
│ 정렬: [등급순] [최신순]                      │
├────────────────────────────────────────────┤
│                                            │
│  ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐       │
│  │코스│ │코스│ │코스│ │코스│ │코스│       │
│  │튬1 │ │튬2 │ │튬3 │ │튬4 │ │튬5 │       │
│  │★★★│ │★★ │ │★  │ │????│ │????│       │
│  └────┘ └────┘ └────┘ └────┘ └────┘       │
│                                            │
│  보유: 12/30                               │
└────────────────────────────────────────────┘
```

### 6.3 코스튬 상세 팝업
```
┌──────────────────────────────┐
│  [숲의 관]          ★★★ 전설  │
├──────────────────────────────┤
│  ┌──────┐                    │
│  │ 아이콘│  부위: 머리        │
│  │      │  세트: 숲의 수호자  │
│  └──────┘  획득: 배틀패스 Lv.40│
│                              │
│  [장착하기]  [미리보기]        │
└──────────────────────────────┘
```

### 6.4 세트 목록 탭
```
┌────────────────────────────────────────────┐
│  [세트 목록]                                │
├────────────────────────────────────────────┤
│  ┌────────────────────────────────────┐    │
│  │ 숲의 수호자  [3/4]                 │    │
│  │ ✓머리  ✓상의  ✓하의  ✗무기         │    │
│  │ 세트 효과: 공격 시 나뭇잎 파티클     │    │
│  └────────────────────────────────────┘    │
│  ┌────────────────────────────────────┐    │
│  │ 화산의 전사  [1/4]                 │    │
│  │ ✗머리  ✓상의  ✗하의  ✗무기         │    │
│  │ 세트 효과: ???                     │    │
│  └────────────────────────────────────┘    │
└────────────────────────────────────────────┘
```

---

## 7. 저장 데이터

```csharp
[System.Serializable]
public class CostumeSaveData
{
    public List<string> OwnedCostumeIds;                    // 보유 코스튬 ID 목록
    public Dictionary<CostumeSlot, string> EquippedCostumes;// 슬롯별 장착 코스튬 ID
}
```

- `SaveManager`의 `ISaveProvider`로 등록
- 코스튬 획득/장착/해제 시 자동 저장 트리거

---

## 8. 기존 시스템 연동

| 시스템 | 연동 방식 |
|--------|-----------|
| **SaveManager** | ISaveProvider 구현, JSON 직렬화 |
| **EventBus** | CostumeObtainedEvent, CostumeEquipChangedEvent, CostumeSetCompletedEvent |
| **BattlePassSystem** | 배틀패스 보상으로 ObtainCostume() 호출 |
| **ArenaRewardSystem** | PvP 시즌 보상으로 ObtainCostume() 호출 |
| **AchievementSystem** | 업적 보상으로 ObtainCostume() 호출 |
| **ShopSystem** | 루비 상점에서 코스튬 구매 |
| **TowerManager** | 층 마일스톤 보상으로 일반 코스튬 해금 |
| **PlayerCharacter** | 장착 코스튬에 따라 스프라이트/이펙트 변경 |

---

## 9. 구현 태스크 분해

| ID | 태스크 | 의존성 | 비고 |
|----|--------|--------|------|
| CC-01 | CostumeSlot, CostumeGrade enum 정의 | - | Core/Enums/ |
| CC-02 | CostumeDataSO + CostumeSetDataSO 생성 | CC-01 | SO 필드 설계, CreateAssetMenu |
| CC-03 | CostumeManager 싱글톤 구현 | CC-02, C1-05 | Equip/Unequip/HasFullSet/Save/Load |
| CC-04 | 이벤트 정의 (CostumeObtainedEvent 등) | CC-01 | EventBus 연동 |
| CC-05 | PlayerCharacter 코스튬 스프라이트 적용 | CC-03, C2-01 | 장착 시 외형 변경 로직 |
| CC-06 | 세트 이펙트 시스템 | CC-03, CC-02 | HasFullSet → 이펙트 프리팹 활성화 |
| CC-07 | 코스튬 패널 UI | CC-03 | 코스튬 목록/필터/장착/미리보기 |
| CC-08 | 세트 목록 UI | CC-06 | 세트 진행도/이펙트 설명 표시 |
| CC-09 | 코스튬 SO 에셋 생성 (초기 데이터) | CC-02 | 최소 2세트(8개) + 개별 4개 = 12개 |
| CC-10 | 기존 시스템 연동 (배틀패스/상점/업적) | CC-03 | 보상 지급 연동 |

### 추천 병렬 그룹
```
[그룹 1] CC-01 + CC-02 + CC-04 (데이터/enum/이벤트 — 독립)
[그룹 2] CC-03 (매니저 — 그룹 1 완료 후)
[그룹 3] CC-05 + CC-06 + CC-07 + CC-08 (비주얼/UI — CC-03 완료 후, 병렬 가능)
[그룹 4] CC-09 + CC-10 (에셋 + 연동 — 그룹 3 완료 후)
```
