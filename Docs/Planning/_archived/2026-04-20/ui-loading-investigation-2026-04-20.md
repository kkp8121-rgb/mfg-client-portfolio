---
read_count: 0
last_read: "never"
status: active
---

# UI Intermittent Loading Bug — Investigation 2026-04-20

## Summary
UI가 무작위로 로드되지 않는 제보의 근본 원인으로 **두 가지 상호 보강하는 레이스 컨디션**을 특정.

## Root Cause #1 — LoadCompletedEvent late-subscriber drop (Critical)
`SaveManager` has `[DefaultExecutionOrder(-10000)]` and publishes `LoadCompletedEvent` directly inside `Awake()` (SaveManager.cs:58). `EventBus` is pure fire-and-forget (no cache/replay, EventBus.cs:24). UI 구독자 대부분이 `OnEnable`에서 구독 → SaveManager Awake 이후 실행되므로 **이벤트를 놓침**.

영향받는 UI 구독자:
- `TabBarUI.cs:107` — 탭 잠금 갱신 이벤트 놓침 → 모든 탭이 잠금 상태로 남음 (유일한 캐릭터 탭만 열려보임, 나머지 클릭 시 "가이드 퀘스트를 진행하세요")
- 시스템 측: `LevelSystem`, `SkillSystem`, `QuestManager`, `CurrencyManager`, `BoosterManager` 모두 동일 위험. `SkillSystem`은 `Start()`에서 `LoadState()` 직접 호출로 방어 (SkillSystem.cs:147 주석).

재현 조건: 세이브 파일이 존재하는 런치(첫 실행 제외). 실행 순서가 Unity의 오브젝트 초기화 순서에 의존해 **간헐적**.

## Root Cause #2 — rootVisualElement null unchecked (Major)
30+ UITK 패널이 `OnEnable`에서 `_root = _doc.rootVisualElement` 후 곧바로 `.Q<>()`/`.pickingMode`에 접근. `rootVisualElement`가 null이면 NullReferenceException으로 OnEnable이 중도 탈락 → 콜백/바인딩 미설정 → "버튼 눌러도 반응 없음". 전수 조사: 30개 파일 중 null 체크 있음 2개 (`CostumePanelUI`, `UIToolkitSoundBridge`). 나머지 28개 취약.

특히 `TabBarUI.OnEnable:61`에서 `_root.pickingMode = PickingMode.Ignore;` 직접 호출 → null이면 탭 바 전체 사망.

재현 조건: VisualTreeAsset가 asset import 순서로 한 프레임 지연 빌드되는 경우, 또는 UIDocument.visualTreeAsset이 누락/참조 끊김. 씬 대규모 정리(2026-04-20) 후 참조가 끊어진 잔재가 있을 가능성.

## Investigated & Excluded
- UIManager 싱글턴 순서: Awake에서 다른 UI가 `UIManager.Instance` 접근 시 null일 수 있으나, 모든 사용처가 `OnShow`/`OnClick` 콜백 단계라 Awake race와 무관.
- PoolManager/Addressables: UITK는 Resources/Addressables를 안 씀 (UIDocument asset 참조).
- 중복 제거 후 잔재 참조: TabBarUI는 `GameObject.Find`로 재탐색하므로 직접 참조 끊겨도 복구됨. 그러나 오브젝트 이름이 변경된 경우는 조용히 실패 (`Debug.LogWarning`만, TabBarUI.cs:87).

## Fixes Applied
1. `TabBarUI.cs:60-67` — `rootVisualElement == null` 가드 추가 + 경고 로그.
2. `TabBarUI.cs:Start` 추가 — `LoadCompletedEvent` 놓쳤을 경우 `UpdateTabVisuals`로 탭 잠금 재계산 (SkillSystem의 `Start→LoadState` 패턴 차용).

## Recommended Follow-ups (main에 위임)
1. **EventBus에 "sticky event" 지원 추가** — `LoadCompletedEvent`처럼 "최초 1회 상태 알림" 성격 이벤트는 late-subscriber에게 즉시 재전달. `EventBus<T>.SubscribeSticky(handler)` API 추가 권장.
   ```csharp
   // Utils/EventBus.cs (개념)
   private static T _lastEvent;
   private static bool _hasLastEvent;
   public static void Publish(T evt) { _lastEvent = evt; _hasLastEvent = true; /* ...기존 로직 */ }
   public static void SubscribeSticky(Action<T> h) { Subscribe(h); if (_hasLastEvent) h(_lastEvent); }
   ```
2. **모든 UITK 패널에 `if (_root == null) return;` 가드 일괄 추가** — 28개 파일 검사 권장 (간단한 sed/regex 마이그레이션).
3. **SaveManager Initialize를 Awake에서 분리** — `Start` 또는 명시적 Bootstrap 시점으로 이동하여 UI가 이미 OnEnable 완료한 상태에서 이벤트 발행.

## Repro Scenario Requested
위 두 가설은 정적 분석 기반이므로 사용자의 실제 재현 패턴을 확인하고 싶음:
- 증상이 **탭 버튼이 잠금(🔒)으로만 보이는 현상**인지 (→ Root Cause #1 유력)
- 또는 **특정 팝업이 까만 화면/무반응**인지 (→ Root Cause #2 유력)
- Play 모드 진입 후 Console에 "TabBarUI 패널 못 찾음" / "요소를 찾을 수 없음" / NullReferenceException 로그가 찍히는지 확인 요청.

## Files Changed
- `client/Assets/Scripts/UI/TabBarUI.cs` (+14 lines, 방어 가드 + Start 추가)

## Compile
가드 코드만 추가했으므로 컴파일 영향 없음. Unity 리로드 후 Console 확인 권장.
