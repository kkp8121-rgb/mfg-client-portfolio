---
read_count: 2
last_read: "2026-04-07"
status: reference
---

# SPUM 애니메이션 연결 가이드

> 애니메이션이 끊기거나 idle만 재생될 때 참조하는 트러블슈팅 가이드.

## 1. SPUM 애니메이션 아키텍처

### 두 가지 AnimatorController

| 컨트롤러 | 파라미터 | 전이 | 제어 방식 | 주 사용처 |
|----------|---------|------|----------|----------|
| `NormalAnimator` | 9개 (RunState, Attack, Die 등) | 있음 (블렌드 트리) | `SetFloat` / `SetTrigger` | 플레이어 |
| `AnimationNewController` | 0개 | 없음 (독립 스테이트) | `Animator.Play()` | 몬스터 |

### CharacterAnimBridge 자동 감지

```
TryInitialize() 에서:
  HasParam("RunState") → true  → PlayParam()  (NormalAnimator)
  HasParam("RunState") → false → PlayDirect() (AnimationNewController)
```

### NormalAnimator 파라미터 매핑

| 게임 상태 | RunState | AttackState | NormalState | SkillState | 트리거 |
|----------|----------|-------------|-------------|------------|--------|
| Idle | 0 | - | - | - | - |
| Run | 0.5 | - | - | - | - |
| Hit/Stun | 1.0 | - | - | - | - |
| Attack Normal | 0 | 0 | 0 | - | Attack |
| Attack Bow | 0 | 0 | 0.5 | - | Attack |
| Attack Magic | 0 | 0 | 1.0 | - | Attack |
| Skill Normal | 0 | 1 | - | 0 | Attack |
| Skill Bow | 0 | 1 | - | 0.5 | Attack |
| Skill Magic | 0 | 1 | - | 1.0 | Attack |
| Die | 0 | - | - | - | Die |

### AnimationNewController 스테이트명

| 게임 상태 | Animator.Play() 스테이트 |
|----------|------------------------|
| Idle | `0_idle` |
| Run/Chase | `1_Run` |
| Attack Normal | `2_Attack_Normal` |
| Attack Bow | `2_Attack_Bow` |
| Attack Magic | `2_Attack_Magic` |
| Hit/Stun | `3_Debuff_Stun` |
| Die | `4_Death` |
| Skill Normal | `5_Skill_Normal` |
| Skill Bow | `5_Skill_Bow` |
| Skill Magic | `5_Skill_Magic` |

---

## 2. 프리팹 구조 요구사항

### 올바른 구조
```
Player (또는 Monster_XXX)
  ├── CharacterAnimBridge (컴포넌트)
  └── SPUM_XXX (SPUM 프리팹 인스턴스, 1개만)
       ├── SPUM_Prefabs (컴포넌트, _anim 필드 → Animator 참조)
       └── UnitRoot
            ├── Animator (NormalAnimator 또는 AnimationNewController)
            ├── SortingGroup
            └── (스프라이트 계층)
```

### 핵심 규칙
1. **SPUM 자식은 반드시 1개만** — 2개 이상이면 브릿지가 잘못된 Animator를 잡음
2. **Animator가 비활성(disabled)이어도 OK** — 브릿지가 자동 활성화
3. **SPUM_Prefabs._anim 필드가 Animator를 참조**해야 함

---

## 3. 자주 발생하는 문제와 해결

### 3-1. idle만 재생됨 (이동/공격 안 됨)

**진단 체크리스트:**

| 확인 항목 | 확인 방법 | 해결 |
|----------|----------|------|
| SPUM 자식 2개 이상? | Hierarchy에서 Player 하위 확인 | 중복 제거 (SPUMVisual 등) |
| 잘못된 Controller? | Animator 인스펙터 → Controller 확인 | 올바른 컨트롤러 할당 |
| Animator 비활성? | Animator 체크박스 확인 | 활성화 (브릿지가 자동 처리) |
| Bridge 미초기화? | `CharacterAnimBridge.IsInitialized` | `ForceReinitialize()` 호출 |

**가장 흔한 원인: SPUM 인스턴스 중복**

씬에 원래 있던 `SPUMVisual` + 런타임 생성 `SPUM_Warrior` → 브릿지가 먼저 발견되는 잘못된 인스턴스를 잡음.

**수정 포인트** — `SpumCharacterManager.ReplaceSpumInstance()`:
```csharp
// SPUM_Prefabs 컴포넌트를 가진 자식을 모두 비활성화+Destroy
for (int i = root.childCount - 1; i >= 0; i--)
{
    var child = root.GetChild(i);
    foreach (var comp in child.GetComponents<Component>())
    {
        if (comp != null && comp.GetType().Name == "SPUM_Prefabs")
        {
            child.gameObject.SetActive(false); // 즉시 검색에서 제외
            Destroy(child.gameObject);
            break;
        }
    }
}
```

### 3-2. Destroy 타이밍 문제

`Destroy()`는 프레임 끝에 실행 → `ForceReinitialize()` 시점에 파괴 예정 오브젝트가 아직 존재.

**해결:**
1. Destroy 전에 `SetActive(false)` 호출
2. `GetComponentsInChildren<Component>(false)` — 비활성 제외 검색

### 3-3. 몬스터 프리팹 Controller 오버라이드 확인

몬스터 프리팹에 내장된 SwordMan이 `AnimationNewController`로 오버라이드되어 있을 수 있음.

**확인:** Monster_XXX 프리팹 → SPUMVisual → UnitRoot → Animator → Controller 필드
- `NormalAnimator` → 파라미터 방식 (PlayParam)
- `AnimationNewController` → 직접 재생 방식 (PlayDirect)

**둘 다 정상 지원됨** — `CharacterAnimBridge`가 자동 감지.

### 3-4. SPUM 애드온 업데이트 후 컴파일 에러

SPUM Ultimate 등 애드온이 구버전 `SPUM_Prefabs` 필드를 참조할 수 있음.

**필수 필드 (SPUM_Prefabs.cs에 존재해야 함):**
```csharp
public UnityEvent UnitTypeChanged = new UnityEvent();
public SPUM_SpriteList _spriteOBj;
public bool _horse;
public string _horseString;
public bool isRideHorse;
```

---

## 4. 새 몬스터/캐릭터 추가 시

### 방법 A: SPUM 에디터에서 생성 (권장)
1. `Assets/Folder_Assets/SPUM/Scene/SPUM_Scene` 열기
2. 캐릭터 커스터마이징 → SAVE UNIT
3. 저장된 프리팹: `Assets/Resources/SPUM/SPUM_Units/`
4. 몬스터 프리팹에 자식으로 배치

### 방법 B: 기존 샘플 프리팹 사용
- `Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab` (전사)
- `Assets/Folder_Assets/SPUM/Prefab/AnimationSample/BowMan.prefab` (궁수)
- `Assets/Folder_Assets/SPUM/Prefab/AnimationSample/MagicianMan.prefab` (마법사)

### MonsterDataSO 설정
```yaml
spumPrefabPath: Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab
attackAnimType: 0  # 0=Normal, 1=Bow, 2=Magic
visualTint: {r: 1, g: 1, b: 1, a: 1}
visualScale: 1.0
```

---

## 5. 디버깅 런타임 확인

### Inspector에서 확인할 값
- `CharacterAnimBridge.IsInitialized` → true여야 함
- `CharacterAnimBridge.CurrentClipKey` → 현재 상태 (Idle/Run/Attack 등)
- Animator 창 → 현재 활성 스테이트 + 파라미터 값

### 에디터 메뉴 진단 도구
`SPUM/Diagnostics/DumpSPUMSpriteRenderers` — 프리팹 Animator/클립 상태 덤프
`SPUM/Diagnostics/DiagnoseScenePlayer` — 씬 내 플레이어 SPUM 상태 진단

---

## 6. 장비 비주얼 (SPUM_SpriteList) 끊김

### 구조

`EquipmentVisualMapper`가 `SPUM_SpriteList`의 렌더러 리스트를 리플렉션으로 캐싱:

```
SPUM_SpriteList
  ├── _hairList    [0]=Hair, [1]=FaceAccessory, [3]=Helmet
  ├── _armorList   [0~2]=상체 갑옷 (Body/Left/Right)
  ├── _clothList   [1,2]=좌우 팔 (장갑)
  ├── _pantList    [0,1]=좌우 다리 (부츠)
  ├── _weaponList  [0]=우측 무기
  ├── _backList    []=망토/날개
  ├── _bodyList    []=신체
  └── _eyeList     []=눈
```

### 끊기는 원인

1. **SPUM 인스턴스 교체 후 캐시 무효화** — 직업 변경 시 `SpumCharacterManager`가 SPUM 인스턴스를 Destroy+재생성 → `EquipmentVisualMapper`의 캐싱된 `SpriteRenderer` 참조가 null로 변함
2. **초기화 타이밍** — `DelayedInit()`이 0.5초 후 실행되지만 SPUM 인스턴스 생성이 더 늦을 수 있음
3. **비활성 오브젝트 검색** — `GetComponentsInChildren(true)`가 Destroy 예정 오브젝트의 SpriteList를 잡을 수 있음

### 수정 포인트

**재캐싱 트리거 추가** — SPUM 인스턴스 교체 시 `EquipmentVisualMapper`에 알림:

```csharp
// SpumCharacterManager.ApplyJobVisual() 끝에:
var visualMapper = playerRoot.GetComponent<EquipmentVisualMapper>();
if (visualMapper != null)
    visualMapper.ForceRecache();

// EquipmentVisualMapper에 추가:
public void ForceRecache()
{
    _initialized = false;
    CacheSpumRenderers();
    RefreshAllSlots();
}
```

**검색에서 비활성 제외:**
```csharp
// FindSpumSpriteList()에서:
GetComponentsInChildren<Component>(false)  // true → false
```

### SPUM_SpriteList 필드 인덱스 참조표

| 장비 슬롯 | SpriteList 필드 | 인덱스 |
|-----------|----------------|--------|
| Helmet | `_hairList` | [3] |
| FaceAccessory | `_hairList` | [1] |
| Top (상의) | `_armorList` | [0] Body, [1] Left, [2] Right |
| Gloves (장갑) | `_clothList` | [1] Left, [2] Right |
| Boots (부츠) | `_pantList` | [0] Left, [1] Right |
| Weapon (무기) | `_weaponList` | [0] |
| Back (망토) | `_backList` | 전체 |
| Ring/Necklace | 비주얼 없음 | - |

### SPUM 에디터에서 장비 파츠 교체 시

1. `SPUM_Scene` 열기 → 캐릭터 로드
2. 해당 파츠 카테고리 선택 (모자/갑옷/무기 등)
3. 원하는 아이템 클릭 → EDIT UNIT으로 저장
4. `SPUM_SpriteList`의 String 필드(`_armorListString` 등)에 경로가 기록됨
5. 런타임에 `ResyncData()`로 경로 기반 스프라이트 복원 가능

---

## 7. 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/Combat/CharacterAnimBridge.cs` | 애니메이션 브릿지 (컨트롤러 자동 감지) |
| `Assets/Scripts/Combat/SpumCharacterManager.cs` | SPUM 인스턴스 생성/교체 관리 |
| `Assets/Scripts/Combat/MonsterController.cs` | 몬스터 상태 → 애니메이션 연결 |
| `Assets/Scripts/Combat/PlayerCharacter.cs` | 플레이어 상태 → 애니메이션 연결 |
| `Assets/Folder_Assets/SPUM/Res/Animation/NormalAnimator.controller` | 파라미터 기반 컨트롤러 |
| `Assets/Folder_Assets/SPUM/Res/Animation/AnimationNewController.controller` | 직접 Play 컨트롤러 |
| `Assets/Scripts/Combat/EquipmentVisualMapper.cs` | 장비 → SPUM 파츠 색상 매핑 |
| `Assets/Folder_Assets/SPUM/Script/SPUM_Prefabs.cs` | SPUM 프리팹 핵심 클래스 |
| `Assets/Folder_Assets/SPUM/Script/SPUM_SpriteList.cs` | 스프라이트 렌더러 리스트 관리 |
