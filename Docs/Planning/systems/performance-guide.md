---
read_count: 5
last_read: "2026-04-11"
status: active
---

# mkLike 성능 최적화 가이드

> 모바일 타겟 (Android/iOS) 최적화 전략. 30fps 절전 / 60fps 성능 모드 지원.

---

## 1. 현재 성능 위험 요소 분석

### 1.1 OverlapCircle 매 프레임 호출 (심각도: 높음)

**현황**: `FindTarget()`이 매 프레임 `Physics2D.OverlapCircleAll()`을 호출한다.

| 파일 | 호출 빈도 | 문제 |
|------|-----------|------|
| `PlayerCharacter.cs` L215 | 매 프레임 (UpdateIdle, UpdateRun) | 40마리 콜라이더 탐색 |
| `MonsterController.cs` L312 | 매 프레임 × 40마리 | **O(N^2)** — 40마리가 각각 매 프레임 탐색 |
| `MonsterController.cs` L244 | 매 프레임 × 40마리 | separation force용 NonAlloc |
| `CharacterCombat.cs` L174 | 공격 시 | 범위 공격 판정 (빈도 낮음) |
| `SkillSystem.cs` L430 | 각성기 시전 시 | 범위 데미지 (빈도 낮음) |
| `CompanionCombat.cs` L237 | 0.2초 간격 | **이미 최적화됨** (TARGET_SEARCH_INTERVAL) |

**핵심 문제**: 몬스터 40마리가 각각 매 프레임 `FindTarget()` + `CalculateSeparation()`을 호출.
- `FindTarget()`: `OverlapCircleAll` (할당 발생) — 프레임당 40회
- `CalculateSeparation()`: `OverlapCircleNonAlloc` (할당 없음, 상대적으로 양호) — 프레임당 40회
- 총: 프레임당 **80회** 물리 쿼리

**개선안**:
```csharp
// MonsterController.cs — 탐색 주기 분산
private float _targetSearchTimer;
private const float TARGET_SEARCH_INTERVAL = 0.15f; // 150ms

// Update() 내부
_targetSearchTimer -= Time.deltaTime;
if (_targetSearchTimer <= 0f)
{
    _targetSearchTimer = TARGET_SEARCH_INTERVAL + Random.Range(0f, 0.05f); // 지터로 프레임 분산
    FindTarget();
}
```

```csharp
// PlayerCharacter.cs — 동일 패턴 적용
private float _targetSearchTimer;
private const float TARGET_SEARCH_INTERVAL = 0.1f;

private void UpdateIdle()
{
    _targetSearchTimer -= Time.deltaTime;
    if (_targetSearchTimer <= 0f)
    {
        _targetSearchTimer = TARGET_SEARCH_INTERVAL;
        FindTarget();
    }
    // ... 나머지 로직
}
```

**예상 효과**: 물리 쿼리 80회/프레임 → 약 6회/프레임 (93% 감소)

### 1.2 MonsterController.FindTarget() — OverlapCircleAll 할당 (심각도: 중간)

**현황**: `Physics2D.OverlapCircleAll()`은 매 호출마다 `Collider2D[]`를 새로 할당한다.

**개선안**: `OverlapCircleNonAlloc` + static 버퍼 사용 (separation에서 이미 사용 중인 패턴)
```csharp
// MonsterController.cs — FindTarget 최적화
private static readonly Collider2D[] _targetBuffer = new Collider2D[5]; // 플레이어는 1~2명

private void FindTarget()
{
    int count = Physics2D.OverlapCircleNonAlloc(
        transform.position, detectionRange, _targetBuffer, playerLayer);

    if (count == 0)
    {
        _currentTarget = null;
        return;
    }

    float closestDist = float.MaxValue;
    CombatStats closestTarget = null;

    for (int i = 0; i < count; i++)
    {
        CombatStats targetStats = _targetBuffer[i].GetComponent<CombatStats>();
        if (targetStats == null || targetStats.IsDead) continue;

        float dist = Vector2.SqrMagnitude(
            (Vector2)transform.position - (Vector2)_targetBuffer[i].transform.position);
        if (dist < closestDist)
        {
            closestDist = dist;
            closestTarget = targetStats;
        }
    }

    _currentTarget = closestTarget;
}
```

**추가**: `Vector2.Distance` → `Vector2.SqrMagnitude` 변환 (sqrt 제거).
- `PlayerCharacter.FindTarget()`, `CompanionCombat.FindTarget()`에도 동일 적용.

### 1.3 MonsterController.FindTarget() — 불필요한 플레이어 탐색 (심각도: 중간)

**현황**: 몬스터가 플레이어를 찾기 위해 매번 `OverlapCircleAll(playerLayer)`을 호출하지만, 플레이어는 1명뿐이다.

**개선안**: 플레이어 Transform을 직접 캐싱하여 물리 탐색 완전 제거.
```csharp
// MonsterController.cs
private Transform _playerTransform;
private CombatStats _playerStats;

public void SetPlayer(Transform player, CombatStats stats)
{
    _playerTransform = player;
    _playerStats = stats;
}

private void FindTarget()
{
    if (_playerStats == null || _playerStats.IsDead)
    {
        _currentTarget = null;
        return;
    }

    float sqrDist = ((Vector2)transform.position - (Vector2)_playerTransform.position).sqrMagnitude;
    _currentTarget = sqrDist <= detectionRange * detectionRange ? _playerStats : null;
}
```
**예상 효과**: 몬스터 FindTarget에서 물리 쿼리 **완전 제거**. 프레임당 40회 → 0회.

### 1.4 DOTween 인스턴스 누적 (심각도: 중간)

**현황 점검**:
| 파일 | DOTween 사용 | Kill 처리 | 평가 |
|------|-------------|-----------|------|
| `DamageText.cs` | Sequence (풀링) | OnReturnToPool + OnDestroy + SetLink | 양호 |
| `LootDrop.cs` | DOMove (풀링) | OnDisable + Complete | 양호 |
| `MonsterController.cs` | DOTween.To (페이드) + Sequence (빙결) | OnDestroy + SetLink | 양호 |
| `HudPanel.cs` | DOPunchScale × 다수 + DOTween.To | DOKill 선행 + SetUpdate | **주의** |
| `SkillVfx.cs` | 대량 Sequence (매 공격/스킬) | SetLink + OnComplete(Destroy) | **위험** |
| `FloatingTextManager.cs` | Sequence (풀링) | ReturnToPool에서 DOTween.Kill | 양호 |

**주요 문제점**:

1. **SkillVfx.cs — VFX 오브젝트 풀링 미적용**: `CreateVfxObj()`가 매번 `new GameObject`로 생성하고, `OnComplete(() => Object.Destroy(go))`로 파괴. 전투 중 초당 수십 개 생성/파괴 발생.
   - `SpawnCircle`, `SpawnRing`, `SpawnSlash`, `SpawnHit`, `SpawnDotTick`, `SpawnFreezeEffect` 등
   - 각각 `DOTween.Sequence()` 생성 → GC pressure + DOTween 인스턴스 누적

2. **HudPanel.cs — DOPunchScale 빈번 호출**: 몬스터 사망마다 killCount 펀치, 골드 획득마다 펀치. DOKill 선행은 되어있으나 빈번한 tween 생성/파괴.

3. **SpriteSheetVfx.cs — Debug.Log가 매 스폰마다 출력** (L83): 성능 저하 원인.

**개선안**:
```csharp
// SkillVfx.cs — VFX 오브젝트 풀링 전환
// PoolManager에 "vfx_circle", "vfx_ring", "vfx_slash" 풀 등록
// Destroy(go) → PoolManager.Despawn() 전환
// DOTween.Sequence의 OnComplete에서 풀 반환

// SpriteSheetVfx.cs — Debug.Log 제거
// L83: Debug.Log 제거 또는 #if UNITY_EDITOR 래핑
```

### 1.5 SkillSystem.UpdateCooldowns() — 매 프레임 List 할당 (심각도: 중간)

**현황** (`SkillSystem.cs` L258-265):
```csharp
private void UpdateCooldowns()
{
    var keys = new List<string>(_cooldowns.Keys); // ← 매 프레임 List 할당!
    foreach (var key in keys)
    {
        if (_cooldowns[key] > 0f)
            _cooldowns[key] -= Time.deltaTime;
    }
}
```

`UpdateBuffs()`도 동일 패턴 (L284-295): 매 프레임 `new List<string>` 2회.

**개선안**:
```csharp
// 캐시된 키 리스트 재사용
private readonly List<string> _cdKeyCache = new();
private readonly List<string> _expiredBuffs = new();

private void UpdateCooldowns()
{
    // Dictionary 순회 중 수정하지 않으므로 직접 순회 가능
    // (값만 변경, 키 추가/삭제 없음)
    foreach (var kvp in _cooldowns)
    {
        if (kvp.Value > 0f)
            _cooldowns[kvp.Key] = kvp.Value - Time.deltaTime;
    }
}

private void UpdateBuffs()
{
    _expiredBuffs.Clear();

    foreach (var kvp in _activeBuffs)
    {
        var buff = kvp.Value;
        buff.remainingTime -= Time.deltaTime;
        _activeBuffs[kvp.Key] = buff;

        if (buff.remainingTime <= 0f)
            _expiredBuffs.Add(kvp.Key);
    }

    for (int i = 0; i < _expiredBuffs.Count; i++)
        RemoveBuff(_expiredBuffs[i]);
}
```

### 1.6 HudPanel.UpdateSkillSlots() — FindFirstObjectByType 매 프레임 (심각도: 낮음)

**현황** (`HudPanel.cs` L414-416):
```csharp
private void UpdateSkillSlots()
{
    if (_skillSystem == null)
        _skillSystem = FindFirstObjectByType<SkillSystem>(); // ← 매 프레임 가능
```

`FindFirstObjectByType`은 비싼 연산이다. null인 동안 매 프레임 호출.

**개선안**: Start() 또는 SetPlayerStats()에서 한 번만 캐싱.

### 1.7 문자열 할당 (심각도: 낮음)

**현황**: `$"..."` 인터폴레이션이 UI 갱신 메서드에서 사용되지만, 이벤트 기반으로 호출 빈도가 적어 큰 문제는 아님.

주의할 곳:
- `DamageText.Play()` L47: `damage.ToString("N0")` — 매 데미지 텍스트 스폰마다 할당
- `HudPanel.OnHpChanged()` L313: `$"{current}/{max}"` — HP 변경마다

이 부분은 현재 수준에서 허용 가능. 향후 최적화 시 StringBuilder 캐시 도입.

### 1.8 CompanionCombat.Spawn() — Instantiate/Destroy 반복 (심각도: 중간)

**현황** (`CompanionCombat.cs` L138, L179):
- 소환 시 `Instantiate(_companionPrefab)`
- 퇴장 시 `Destroy(_summonedObject)`
- 소환 쿨타임마다 반복 → GC 스파이크

**개선안**: 동료 오브젝트를 한 번 생성 후 SetActive(true/false)로 재사용.

---

## 2. 오브젝트 풀링 현황

### 2.1 풀링 적용 현황

| 시스템 | 풀링 방식 | 초기 크기 | 평가 |
|--------|-----------|-----------|------|
| 몬스터 (`MonsterSpawner`) | PoolManager | 50 (일반), 2 (보스) | 양호 |
| 데미지 텍스트 (`DamageTextManager`) | PoolManager | 20 | 양호 |
| 전리품 드롭 (`LootVisualManager`) | 자체 Queue | 40 | 양호 |
| 플로팅 텍스트 (`FloatingTextManager`) | 자체 Queue | 20 | 양호 |
| 스프라이트시트 VFX (`SpriteSheetVfx`) | static Dictionary 풀 | MAX 8/키 | 양호 |

### 2.2 풀링 미적용 (반드시 적용 필요)

| 대상 | 현재 | 생성/파괴 빈도 | 우선순위 |
|------|------|--------------|---------|
| **2D 도형 VFX** (`SkillVfx.cs` 전체) | `new GameObject` + `Destroy` | 공격당 1~5개, 초당 10~30개 | **최우선** |
| **동료 오브젝트** (`CompanionCombat`) | `Instantiate` + `Destroy` | 쿨타임당 1회 | 중간 |
| **레벨업/스킬습득 연출** (`HudPanel`) | `new GameObject` + `Destroy` | 레벨업당 1회 | 낮음 |

### 2.3 풀 크기 최적화 권고

| 풀 | 현재 크기 | 권고 크기 | 이유 |
|----|-----------|-----------|------|
| 몬스터 일반 | 50 | **60** | maxAliveCount(40) + 사망 페이드 중(~20) |
| 데미지 텍스트 | 20 | **30~40** | 다단히트(5회) × 범위공격 대상(5~10마리) |
| 2D VFX (신규) | 없음 | **Circle: 15, Ring: 10, Slash: 8, Hit: 15** | 전투 피크 기준 |
| 전리품 | 40 | 40 | 현재 적절 |

### 2.4 PoolManager 개선 사항

**GetComponents<IPoolable> 매번 호출 문제** (`PoolManager.cs` L162, L175):
```csharp
private void NotifySpawn(GameObject obj)
{
    var poolables = obj.GetComponents<IPoolable>(); // ← 매 스폰마다 할당
}
```

**개선안**: IPoolable 배열을 Dictionary에 캐싱.
```csharp
private readonly Dictionary<GameObject, IPoolable[]> _poolableCache = new();

private void NotifySpawn(GameObject obj)
{
    if (!_poolableCache.TryGetValue(obj, out var poolables))
    {
        poolables = obj.GetComponents<IPoolable>();
        _poolableCache[obj] = poolables;
    }
    for (int i = 0; i < poolables.Length; i++)
        poolables[i].OnSpawnFromPool();
}
```

---

## 3. Update() 최적화

### 3.1 현재 Update 사용 목록

| 클래스 | Update 내용 | 대체 가능 | 우선순위 |
|--------|------------|-----------|---------|
| `PlayerCharacter` | AI 상태 머신 | 부분 (탐색만 분산) | 높음 |
| `MonsterController` × 40 | AI 상태 머신 + CC | 부분 (탐색 분산) | **최우선** |
| `MonsterCombat` × 40 | 공격 쿨다운 | UpdateManager 또는 통합 | 중간 |
| `CharacterCombat` | 공격 로직 | 유지 (1개) | 낮음 |
| `SkillSystem` | 쿨다운 + 버프 + 자동시전 | 유지 (1개) | 낮음 |
| `HudPanel` | 스킬 슬롯 갱신 | 이벤트 기반 전환 | 중간 |
| `StageManager` | 사망 감지 | 이벤트 기반 전환 | 낮음 |
| `LootDrop` × N | 자석 흡수 | 유지 (간단) | 낮음 |
| `SpriteSheetVfx` × N | 프레임 진행 | 유지 (필수) | 낮음 |
| `GachaManager` | 루비 충전 타이머 | UniTask 전환 | 낮음 |
| `CompanionCombat` | 동료 AI | 유지 (1개) | 낮음 |
| `CombatDebugLogger` | 디버그 | 릴리즈에서 제거 | 낮음 |
| `ParticleVfx2DAdapter` | 어댑터 | 유지 | 낮음 |

### 3.2 UpdateManager 패턴 (권고)

40마리 몬스터의 개별 `Update()` 호출은 Unity MonoBehaviour 오버헤드가 크다. `UpdateManager`로 통합하면 단일 `Update()`에서 모든 몬스터를 순회하여 오버헤드를 줄일 수 있다.

```csharp
public class UpdateManager : MonoBehaviour
{
    public static UpdateManager Instance { get; private set; }

    private readonly List<IUpdatable> _updatables = new(64);

    public void Register(IUpdatable updatable) => _updatables.Add(updatable);
    public void Unregister(IUpdatable updatable) => _updatables.Remove(updatable);

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = _updatables.Count - 1; i >= 0; i--)
        {
            _updatables[i].OnUpdate(dt);
        }
    }
}

public interface IUpdatable
{
    void OnUpdate(float deltaTime);
}
```

**적용 대상**: `MonsterController`, `MonsterCombat` → `IUpdatable` 구현.
- `MonsterController.Update()` 제거, `OnUpdate(float dt)` 전환.
- Unity의 MonoBehaviour.Update 오버헤드 (네이티브→매니지드 브릿지) 제거.

**예상 효과**: 40개 MonoBehaviour.Update 호출 → 1개 호출 (80%+ 오버헤드 감소).

### 3.3 이벤트 기반 전환 가능 항목

**HudPanel.UpdateSkillSlots()** — 매 프레임 대신:
```csharp
// SkillSystem에서 쿨다운 변경 시 이벤트 발행
EventBus.Publish(new SkillCooldownTickEvent { SkillId = id, Remaining = cd });

// HudPanel에서 구독
// → UpdateSkillSlots를 Update()에서 제거
```

단, 방치형 게임 특성상 매 프레임 갱신이 시각적으로 자연스러울 수 있으므로, 0.1초 간격 갱신으로 타협 가능.

**StageManager.Update()** — 플레이어 사망 감지:
```csharp
// 현재: 매 프레임 stats.IsDead 체크
// 개선: CombatStats.OnDeath 이벤트 구독으로 대체
_playerStats.OnDeath += () => EnterFarmingMode();
```

**GachaManager.Update()** — 루비 충전:
```csharp
// 현재: 매 프레임 타이머
// 개선: UniTask 루프
private async UniTaskVoid RechargeLoopAsync()
{
    while (this != null && gameObject.activeInHierarchy)
    {
        float interval = 60f / _rubyPerMinute;
        await UniTask.Delay(TimeSpan.FromSeconds(interval),
            cancellationToken: destroyCancellationToken);
        AddRuby(1);
    }
}
```

---

## 4. 메모리 최적화

### 4.1 SO 에셋 캐싱

**현재**: `DataManager`가 SO를 `Resources.Load`로 로드. 이미 Unity가 캐싱하므로 추가 작업 불필요.

**주의사항**:
- `Resources.Load`는 메인 스레드에서만 호출
- 대량 SO 로딩 시 `Resources.LoadAll<T>()` 사용하여 일괄 로드
- 향후 Addressables 전환 시 비동기 로딩 필수

### 4.2 스프라이트 아틀라스 가이드

Unity 2D에서 드로우콜 최적화의 핵심. 같은 머터리얼의 스프라이트를 아틀라스로 묶으면 배칭 가능.

**아틀라스 분류 권고**:

| 아틀라스 | 포함 대상 | 최대 크기 |
|----------|-----------|-----------|
| `Atlas_UI_HUD` | HUD 아이콘, 버튼, 슬라이더 | 2048x2048 |
| `Atlas_UI_Popup` | 팝업 배경, 탭, 아이템 프레임 | 2048x2048 |
| `Atlas_VFX` | SkillVfx용 원형/링/슬래시 텍스처 | 1024x1024 |
| `Atlas_Loot` | 전리품 드롭 스프라이트 (골드, 보석 등) | 1024x1024 |
| `Atlas_Monsters` | 몬스터 스프라이트 (종류별) | 2048x2048 |

**설정**:
```
Project Settings > Editor > Sprite Packer > Mode: Always Enabled
```

**아틀라스 생성**:
- `Assets > Create > 2D > Sprite Atlas`
- Include: 해당 폴더 또는 개별 스프라이트 지정
- Packing Settings: Max Texture Size = 2048, Allow Rotation = false (SPUM 호환)

### 4.3 SkillVfx 런타임 텍스처 캐싱

**현재 양호**: `_circleSprite`와 `_ringSprite`가 static으로 캐싱되어 한 번만 생성됨.

**개선**: 텍스처를 에디터 타임에 미리 생성하여 에셋으로 저장하면 런타임 텍스처 생성 비용 제거.

### 4.4 Addressables 전환 계획

**1단계 (현재)**: Resources 폴더 기반 — 소규모 프로젝트에서 충분.

**2단계 (콘텐츠 증가 시)**:
- `CharacterDesigns/` JSON → Addressables Group으로 전환
- 몬스터/캐릭터 프리팹을 Addressables로 전환
- SO 에셋을 Addressables로 전환
- 씬 로딩을 Addressables Scene으로 전환

**전환 기준**: 앱 크기 100MB 초과 또는 챕터 10+ 시점.

---

## 5. 모바일 특화 최적화

### 5.1 프레임 레이트 제어

```csharp
public class PerformanceManager : MonoBehaviour
{
    private const int FPS_BATTERY_SAVER = 30;
    private const int FPS_PERFORMANCE = 60;

    [SerializeField] private bool _isBatterySaver;

    private void Start()
    {
        SetFrameRate(_isBatterySaver ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);
        QualitySettings.vSyncCount = 0; // vSync 비활성화 (모바일)
    }

    public void SetFrameRate(int fps)
    {
        Application.targetFrameRate = fps;
    }

    public void SetBatterySaver(bool enabled)
    {
        _isBatterySaver = enabled;
        SetFrameRate(enabled ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);

        // 30fps 모드에서 추가 최적화
        if (enabled)
        {
            // VFX 품질 낮추기
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
        else
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
```

### 5.2 배터리 소모 최소화

**렌더링**:
- URP `UniversalRP.asset` > Rendering > SRP Batcher: **ON**
- Shadow: 2D Light Shadow **OFF** (필요 없는 곳)
- Anti-Aliasing: **OFF** (모바일 2D에서 불필요)

**연산 분산**:
- `Physics2D.simulationMode = SimulationMode2D.Script` 설정 후 FixedUpdate에서만 시뮬레이션
- 또는 `Physics2D.autoSimulation = false` → `Physics2D.Simulate(fixedDeltaTime)` 수동 호출

**30fps 모드 추가 최적화**:
- 몬스터 AI 탐색 주기: 0.15초 → 0.3초
- VFX 생성량 50% 감소 (SpawnCircle 등 확률적 스킵)
- HudPanel 스킬 슬롯 갱신: 매 프레임 → 0.2초 간격

### 5.3 발열 관리

```csharp
// SettingsManager.cs에 추가
public void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus)
    {
        // 백그라운드 진입 — 최소 연산
        Application.targetFrameRate = 5;
        Time.timeScale = 0.1f;
        // 자동 저장
        SaveManager.Instance?.Save();
    }
    else
    {
        // 포그라운드 복귀
        Application.targetFrameRate = _isBatterySaver ? 30 : 60;
        Time.timeScale = 1f;
    }
}
```

**백그라운드 방치 수익 계산**:
- 앱이 백그라운드에 있는 동안의 시간을 기록
- 복귀 시 경과 시간 × 분당 수익으로 오프라인 보상 지급
- 실제 전투 시뮬레이션은 하지 않음 (배터리 절약)

### 5.4 메모리 예산

| 항목 | 예산 | 비고 |
|------|------|------|
| 텍스처 | 100MB | 아틀라스 2048x2048 × 5장 기준 |
| 메시/스프라이트 | 30MB | SPUM 캐릭터 포함 |
| 오디오 | 20MB | BGM OGG + SFX WAV |
| 스크립트/SO | 10MB | |
| 풀링 오브젝트 | 20MB | 몬스터 60 + VFX 50 + 텍스트 40 |
| **총 런타임** | **~200MB** | 저사양 Android 2GB RAM 기준 |

---

## 6. 우선순위별 최적화 로드맵

### Phase A: 즉시 적용 (코드 변경만, 아키텍처 변경 없음)

1. **MonsterController.FindTarget() — 플레이어 직접 캐싱** → 물리 쿼리 40회/프레임 제거
2. **PlayerCharacter.FindTarget() — 탐색 주기 분산 (0.1초)** → 물리 쿼리 매 프레임 → 10fps
3. **MonsterController 탐색 주기 분산 (separation 포함, 0.15초+지터)**
4. **OverlapCircleAll → NonAlloc 전환** (PlayerCharacter, CompanionCombat)
5. **SkillSystem.UpdateCooldowns/Buffs — List 할당 제거**
6. **SpriteSheetVfx.cs L83 — Debug.Log 제거**
7. **Vector2.Distance → SqrMagnitude 전환** (모든 거리 비교)

### Phase B: 중기 (아키텍처 개선)

1. **SkillVfx 2D 도형 풀링 시스템 구축** → Instantiate/Destroy 제거
2. **UpdateManager 도입** → MonsterController/MonsterCombat 통합
3. **CompanionCombat 동료 오브젝트 재사용** (Destroy → SetActive)
4. **HudPanel.UpdateSkillSlots() 갱신 주기 조절** (0.1초 간격)
5. **GachaManager.Update → UniTask 전환**
6. **StageManager.Update → 이벤트 기반 전환**

### Phase C: 장기 (콘텐츠 확장 시)

1. **스프라이트 아틀라스 구성 + 드로우콜 최적화**
2. **Addressables 전환**
3. **PerformanceManager + 30fps 절전 모드**
4. **오프라인 방치 수익 시스템**
5. **LOD 시스템** (화면 밖 몬스터 애니메이션 중지)

---

## 7. 성능 측정 방법

### Unity Profiler

```
Window > Analysis > Profiler
```

**확인 항목**:
- CPU: MonoBehaviour.Update 비용, Physics2D.OverlapCircle 비용
- Memory: GC Alloc (매 프레임 할당량), Total Allocated
- Rendering: Draw Calls, Batches, SetPass Calls

### 커스텀 프로파일링 마커

```csharp
using Unity.Profiling;

public class MonsterController : MonoBehaviour
{
    static readonly ProfilerMarker s_FindTargetMarker = new("Monster.FindTarget");
    static readonly ProfilerMarker s_SeparationMarker = new("Monster.Separation");

    private void FindTarget()
    {
        using (s_FindTargetMarker.Auto())
        {
            // ... 기존 로직
        }
    }
}
```

### 빌드 후 측정 (실기기)

```csharp
// FPS 카운터
public class FpsCounter : MonoBehaviour
{
    private float _deltaTime;

    void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        float fps = 1.0f / _deltaTime;
        GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {fps:0.0}");
    }
}
```

### 핵심 지표 (KPI)

| 지표 | 목표 (60fps) | 목표 (30fps) | 위험 |
|------|-------------|-------------|------|
| 프레임 시간 | < 16.6ms | < 33.3ms | > 20ms / > 40ms |
| GC Alloc/프레임 | < 1KB | < 1KB | > 5KB |
| Draw Calls | < 50 | < 50 | > 100 |
| 메모리 (Total) | < 200MB | < 200MB | > 300MB |
| Physics2D 쿼리/프레임 | < 10 | < 5 | > 30 |

---

## 8. 코드 변경 체크리스트

> 각 항목 구현 시 체크.

- [x] MonsterController: 플레이어 캐싱으로 FindTarget 물리 쿼리 제거 ✅ 2026-03-16
- [x] MonsterController: 탐색 주기 분산 (0.15초 + 지터) ✅ 2026-03-16
- [x] PlayerCharacter: FindTarget 탐색 주기 분산 (0.1초) ✅ 2026-03-16
- [x] PlayerCharacter: OverlapCircleAll → NonAlloc 전환 ✅ 2026-03-16
- [x] CompanionCombat: OverlapCircleAll → NonAlloc 전환 ✅ 2026-03-16
- [x] SkillSystem: UpdateCooldowns/UpdateBuffs List 할당 제거 ✅ 2026-03-16
- [x] SpriteSheetVfx: Debug.Log 제거 ✅ 2026-03-16
- [x] Vector2.Distance → SqrMagnitude (거리 비교 전체) ✅ 2026-03-16
- [x] SkillVfx: VFX 오브젝트 풀링 시스템 구축 ✅ 2026-03-16
- [x] UpdateManager 도입 + MonsterController/MonsterCombat 통합 ✅ 2026-03-16
- [x] CompanionCombat: 동료 오브젝트 SetActive 재사용 ✅ 2026-03-16
- [x] HudPanel: UpdateSkillSlots 갱신 주기 조절 ✅ 2026-03-16
- [x] GachaManager: Update → UniTask 전환 ✅ 2026-03-16
- [x] StageManager: Update → 이벤트 기반 전환 ✅ 2026-03-16
- [x] PoolManager: IPoolable 캐싱 ✅ 2026-03-16
- [x] 스프라이트 아틀라스 구성 ✅ 2026-03-16
- [x] PerformanceManager + 프레임 레이트 제어 ✅ 2026-03-16
- [x] 오프라인 방치 수익 시스템 ✅ 2026-03-16
- [x] LOD 시스템 (화면 밖 몬스터 애니메이션 중지) ✅ 2026-03-16
