---
read_count: 0
last_read: "never"
status: active
priority: P0
---

# 봇 피드백 검증 Invariant 확장 (2026-04-23)

## 문제

사용자 관찰:
> "런틱 테스트가 이런 거 확인하려는 건데 잘 안 되는 것 같음. 뭐 정예를 누르면 정예가 나오는지, 소탕을 하면 소탕만큼 경험치가 오르는지, 부스터를 쓰면 뭔가 부스팅 되는 것처럼 보이는지."

**근본 원인**: `BotInvariants.cs`의 13개 invariant는 전부 **데이터/상태 검증**:
- UniqueUitkPanels, PlayerCoreComponents, MonsterPrefabsUnique,
  SpawnedMonsterStats, LevelSystemRange, QuestManagerIndex, UitkDocumentUnique,
  CharacterVisualJobMatch, ActiveSkillTierMatch, BattlePassRewards,
  CollectionCategoryTotals, QuickMenuButtons, UpdateManagerInstance,
  NoRuntimeMonsterResidue, NoLegacyCanvasWidgets

**누락**: **"봇이 액션을 실행한 후 UI/VFX 피드백이 실제로 발화했는가"** 확인 없음.

## 제안: 피드백 검증 invariant 3종

### Check_ActionFeedbackUI
봇 행동 직후 `FeedbackEvent` 또는 관련 UI 활성화 여부 자동 감지.

```csharp
// BotInvariants.cs 신규
static InvariantResult Check_ActionFeedbackUI()
{
    if (!EditorApplication.isPlaying)
        return Skip("ActionFeedbackUI", "edit mode");

    var botType = ...AutoPlayBot;
    var window = Resources.FindObjectsOfTypeAll(botType)[0];
    float lastActionTime = GetLastActionTime(window);   // bot reflection
    string lastAction = GetLastActionLabel(window);

    if (Time.time - lastActionTime > 3.0f)
        return Skip("ActionFeedbackUI", "no recent action");

    // 봇 행동 직후 3초 내 FeedbackEvent 발행 여부
    int feedbackCount = FeedbackBus.GetRecentEventCount(3.0f);
    if (feedbackCount == 0)
        return Fail("ActionFeedbackUI",
            $"action={lastAction} but no FeedbackEvent within 3s",
            InvariantResult.Severity.Warning);

    return Pass("ActionFeedbackUI", $"{feedbackCount} feedback events after {lastAction}");
}
```

**전제**:
- `FeedbackBus`에 최근 이벤트 기록 버퍼 + `GetRecentEventCount()` 추가
- AutoPlayBot에 `_lastActionTime` / `_lastActionLabel` 노출

### Check_VFXState
전투 활성 중 DamageText 또는 ScreenShake 발화 감지.

```csharp
static InvariantResult Check_VFXState()
{
    if (!EditorApplication.isPlaying) return Skip("VFXState", "edit mode");

    // 전투 활성 + 몬스터 존재 + 플레이어 공격 중
    var monsters = Object.FindObjectsByType<MonsterController>(...);
    if (monsters.Length == 0) return Skip("VFXState", "no monsters");

    // DamageText 최근 5초 내 발화 (Manager 내부 카운터)
    var dtm = DamageTextManager.Instance;
    if (dtm == null) return Skip("VFXState", "no DamageTextManager");
    if (dtm.SpawnedInLast(5.0f) == 0)
        return Fail("VFXState",
            "combat active but no DamageText in last 5s",
            InvariantResult.Severity.Warning);

    return Pass("VFXState", $"DamageText spawned {dtm.SpawnedInLast(5.0f)} in 5s");
}
```

**전제**:
- `DamageTextManager`에 `SpawnedInLast(seconds)` API 추가

### Check_BoosterActiveBadge
부스터 사용 시 HUD 뱃지 활성 확인 (Feedback Tier 2 검증).

```csharp
static InvariantResult Check_BoosterActiveBadge()
{
    if (!EditorApplication.isPlaying) return Skip("BoosterActiveBadge", "edit mode");

    var bm = BoosterManager.Instance;
    if (bm == null || !bm.HasActive) return Skip("BoosterActiveBadge", "no active booster");

    var hud = Object.FindFirstObjectByType<HudUI>();
    var doc = hud?.GetComponent<UIDocument>();
    var badge = doc?.rootVisualElement?.Q("booster-badge");
    if (badge == null || badge.style.display == DisplayStyle.None)
        return Fail("BoosterActiveBadge",
            "booster active but HUD badge not visible",
            InvariantResult.Severity.Warning);

    return Pass("BoosterActiveBadge", "badge visible");
}
```

**전제**:
- `HUD.uxml`에 `name="booster-badge"` VisualElement 추가 (Phase C에서)
- `BoosterManager`가 active 상태 발화 시 badge.style.display 전환

## 추가 제안 (Phase D 이후 확장)

### Check_GachaResultOverlayShown
- 봇 가챠 실행 직후 `result-overlay`가 1초 이상 Display=Flex 유지

### Check_QuestRewardPopupShown
- 봇 `ClaimGuideRewardAndAdvance` 직후 보상 Toast/Popup 표시

### Check_StageTransitionVFX
- 챕터 변경 시 배경 전환 + 페이드 VFX 감지 (Tier 2 피드백)

## 구현 범위

### Phase B (이 문서)
- 설계 확정 ✅

### Phase C
1. `FeedbackBus.GetRecentEventCount` + 순환 버퍼 추가
2. `DamageTextManager.SpawnedInLast` API
3. `AutoPlayBot`에 `_lastActionTime` / `_lastActionLabel` 필드 + ExecutePendingAction 직후 기록
4. `BotInvariants.cs`에 3개 신규 invariant 추가 → `RunAll()` 리스트 확장

### Phase D
- Marathon 재검증 시 신규 invariant 자동 감지
- 누락 시 리포트

## 성공 기준

- [ ] 봇 가챠 실행인데 FeedbackEvent 없음 → Invariant Warning 발생
- [ ] 전투 중인데 DamageText 없음 → Warning
- [ ] 부스터 active인데 HUD 뱃지 없음 → Warning
- [ ] 정상 상태에서는 15/15 pass 유지

## 기존 invariant와의 관계

- 기존 13개: **상태/데이터 검증** (유지)
- 신규 3개(+추가): **피드백/연출 검증**
- 총 16개 → 런틱 테스트가 "시스템 작동" + "유저에게 보이는 것" 모두 커버
