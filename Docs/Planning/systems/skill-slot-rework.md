---
read_count: 0
last_read: "never"
status: active
priority: P0
supersedes: systems/skill-design.md
---

# 스킬 슬롯 시스템 재설계 (2026-04-23)

## 문제

**현재 (ID 기반)**:
- `SkillSystem._skillLevels: Dict<string,int>` — 스킬 ID별 레벨 기록
- 전직해도 이전 직업 스킬 유지 (Warrior Lv700 `warrior_strike` + Knight Lv40 `knight_charge` 공존)
- 봇의 `learned[0]` 로직으로 첫 스킬만 끝없이 강화
- 유저 관점 혼란: **"전직하면 스킬이 바뀌는데 왜 이전 직업 스킬을 계속 강화하지?"**

**의도 (사용자 지시)**:
> "스킬 슬롯 자체를 강화하는 방식으로 기획부터 다시해야 할 것 같음"

## 새 모델: 4개 슬롯 고정 + 슬롯 레벨 강화

### 핵심 원칙

1. **액티브 스킬 슬롯 4개 고정** — UI/전투/데이터 모두 "슬롯 1/2/3/4" 기준
2. **슬롯별 실효 스킬은 직업/전직에 따라 교체**:
   - 슬롯 1 = 직업 기본 공격 (warrior_strike / archer_aimshot / mage_arcane)
   - 슬롯 2~4 = 직업별 고유 액티브 (warcry/arrowrain/fireburst 등)
3. **유저 강화 대상 = 슬롯 레벨** (1~20 정도). 스킬 ID가 바뀌어도 슬롯 레벨 유지
4. **스킬 특성**(쿨타임/범위/베이스 배율)은 해당 슬롯의 현재 실효 스킬 SO에서 참조
5. **실효 데미지 = `SO.damageMultiplier × (1 + slotLevel × slotGrowthRate)`**
   - 예: slotGrowthRate = 0.1 → Lv10 슬롯은 기본 배율의 2배 (1 + 10×0.1)

### 슬롯 → 실효 스킬 매핑 예시

| 슬롯 | Warrior T0 | Knight T1 | Paladin T2 | Guardian T3 | DragonKnight T4 |
|:----:|------------|-----------|-----------|-------------|-----------------|
| 1 (기본 공격) | warrior_strike | knight_charge | paladin_smite | guardian_shieldstrike | dragonknight_dragonstrike |
| 2 | warrior_training | knight_ironwall | paladin_holyward | guardian_titanwall | dragonknight_dragonscale |
| 3 | warrior_warcry | knight_rally | paladin_consecration | guardian_divineprotection | dragonknight_dragonroar |
| 4 | warrior_fury | knight_judgment | paladin_holynova | guardian_judgmentofkings | dragonknight_apocalypse |

- jobTier 0 = Warrior 기본, 각 전직 시 해당 tier의 스킬이 해당 슬롯 실효 스킬로 교체
- 스킬 SO `slotIndex` 필드 신규 (0/1/2/3)

## 데이터 모델 변경

### SaveData 마이그레이션
```csharp
// Before
[SerializeField] public List<SkillProgress> skills; // {id, level}

// After
[SerializeField] public int[] slotLevels = new int[4]; // 슬롯별 레벨
[SerializeField] public int saveVersion = 3;          // 마이그레이션 버전
```

### 마이그레이션 로직 (SaveManager.Load 시 1회)
```csharp
if (data.saveVersion < 3)
{
    // 기존 skills 리스트에서 슬롯별 최고 레벨 추출
    for (int i = 0; i < 4; i++)
        data.slotLevels[i] = ComputeSlotLevelFromLegacy(data.skills, slotIndex: i);
    data.skills = null; // legacy field 폐기
    data.saveVersion = 3;
}
```
- 기존 `warrior_strike Lv700` 데이터는 슬롯 0의 레벨로 산입 (상한 20 적용 시 Lv20로 캡)

### SkillDataSO 추가
```csharp
[Tooltip("슬롯 인덱스 (0:기본공격, 1~3:액티브)")]
[SerializeField] private int _slotIndex;
public int SlotIndex => _slotIndex;
```

## SkillSystem 구조 변경

### 제거
- `_skillLevels: Dict<string,int>` → **폐기**
- `_learnedSkills: List<SkillDataSO>` 동적 관리 → **폐기** (jobTier 전환으로 자동)

### 신규
```csharp
private int[] _slotLevels = new int[4];
private SkillDataSO[] _slotSkills = new SkillDataSO[4];

public void RefreshSlotSkillsForJob(JobType job, int jobTier)
{
    // 현재 직업/전직에 맞는 4개 스킬 자동 세팅
    // (SO 필터: slotIndex + jobTier 조건 일치)
    for (int i = 0; i < 4; i++)
        _slotSkills[i] = FindSkillForSlot(job, jobTier, i);
}

public void UpgradeSlot(int slotIndex) // 유저/봇 강화 진입점
{
    _slotLevels[slotIndex]++;
    SaveManager.Instance.CurrentData.slotLevels[slotIndex] = _slotLevels[slotIndex];
}

public float GetEffectiveDamage(int slotIndex)
{
    var skill = _slotSkills[slotIndex];
    return skill.damageMultiplier * (1f + _slotLevels[slotIndex] * SLOT_GROWTH_RATE);
}
```

### JobChangedEvent 처리
```csharp
private void OnJobChanged(JobChangedEvent evt)
{
    RefreshSlotSkillsForJob(evt.NewJob, evt.NewTier);
    // 슬롯 레벨은 유지. 스킬만 교체됨.
}
```

## UI 변경 (SkillTabUI)

### 전
- 동적 스킬 리스트 (learned 배열 기반)
- 각 row에 "레벨업" 버튼

### 후
- **고정 슬롯 4개** UXML (슬롯1~4 카드)
- 각 슬롯 카드 표시:
  - 슬롯 인덱스 ("슬롯 1")
  - 현재 실효 스킬 아이콘/이름
  - 슬롯 레벨 (Lv N / Max)
  - 강화 비용 (골드)
  - 강화 버튼
- 슬롯 레벨 시각 피드백: 슬롯 4개의 레벨 바

## 봇 AutoPlayBot 변경

### 전
```csharp
var learned = ss.LearnedSkills;
var target = learned[0]; // 항상 첫 번째만
ss.LevelUpSkill(target.id, cost);
```

### 후
```csharp
// 4개 슬롯 중 최저 레벨 슬롯 우선 (라운드 로빈 효과)
int lowestSlot = FindLowestLevelSlot(ss.SlotLevels);
ss.UpgradeSlot(lowestSlot);
```

## 기존 문서 영향

- `systems/skill-design.md` → 이 문서로 **대체(supersedes)**. 기존 문서는 노트만 유지 또는 `_archived/`
- `SkillMasteryManager`는 별도 시스템으로 유지 (특정 스킬 사용 횟수 보너스)

## 구현 범위 (Phase B 기획 + Phase C 구현)

### Phase B (기획 확정)
- 이 문서 작성 ✅
- 슬롯별 실효 스킬 매핑 테이블 (jobTier × slotIndex) 확정
- 슬롯 레벨 상한 / 강화 비용 공식 결정

### Phase C (구현)
1. `SkillDataSO._slotIndex` 필드 추가 + 기존 스킬 SO 17개에 값 설정
2. `SaveData.slotLevels[4]` + 마이그레이션 로직
3. `SkillSystem` 재구성 (Dict 제거 → 배열 기반)
4. `SkillTabUI` 재작성 (고정 4슬롯 UXML)
5. `AutoPlayBot.ExecuteSkillLevelUpNow` 라운드 로빈
6. `SkillMasteryManager` 슬롯 기반 연동

## 리스크

- 기존 세이브 마이그레이션 시 **스킬 레벨 손실 가능** (Lv700 → 슬롯 Lv20으로 캡)
  - 완화: Lv700 같은 극단 케이스는 신규 시스템에선 의도적 축소. 마이그레이션 보너스 지급
- 봇 완주 경로에 영향 — Marathon skip에서 `SkillLevelUp` 퀘스트 조건 재매핑 필요

## 성공 기준

- [ ] 전직 시 슬롯 실효 스킬이 시각적으로 교체되며 슬롯 레벨은 유지
- [ ] 봇이 4개 슬롯을 라운드 로빈 강화 (로그 `learned[0]` 반복 없음)
- [ ] `warrior_strike Lv700` 같은 비정상 레벨 안 나옴 (상한)
- [ ] 유저가 SkillTab에서 "어느 슬롯을 강화해야 할지" 명확
