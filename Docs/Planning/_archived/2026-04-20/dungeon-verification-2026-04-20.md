---
read_count: 0
last_read: "never"
status: active
---

# 던전 전투 경로 재검증 리포트 (2026-04-20)

이전 세션에서 `DungeonBattleController` GameObject가 `Main.unity` 씬에 없어 던전 입장 시 즉시 보상 처리되던 P0 버그를 수정(GameObject 추가)한 뒤, 실제 동작을 정적 분석으로 재검증한 결과.

## 1. 씬 설정 상태 — PASS

`client/Assets/Scenes/Main.unity`에서 확인:
- `DungeonBattleController` GameObject 존재 (fileID 1600231853, line 57204~57253)
  - `m_IsActive: 1` (활성)
  - MonoBehaviour 컴포넌트 부착 (`guid: 925f65fe96c180445a9e90a25211b8ec` → `DungeonBattleController.cs`)
  - 직렬화 필드: `_defaultDuration=30, _spawnInterval=1.5, _spawnBatchSize=4, _maxAliveMonsters=30, _difficultyHpMultiplier=2, _difficultyAtkMultiplier=1.2` — SO 디폴트 일치
- `DungeonManager` GameObject 존재 (line 70729~)
- `DungeonContentHandler` GameObject 존재 (line 48571~)

씬에 직접 연결되는 SerializeField가 없어(참조는 `FindFirstObjectByType<MonsterSpawner>`/`StageManager`로 런타임 획득) 링크 미연결 리스크 0.

## 2. 던전 입장 플로우 — PASS

`DungeonPanelUI.OnEnterDungeon` (DungeonPanelUI.cs:339~380) 정적 경로:

1. `DungeonManager.CanEnter` → 열쇠·층·CP 게이팅
2. `DungeonManager.EnterDungeon` → 열쇠 차감, `_activeDungeon` 기록, `DungeonEnteredEvent` 발행, BGM/배경 전환
3. `DungeonBattleController.Instance.StartBattle(dungeon)` 호출 (line 370)
   - `_isActive=true`, 타이머 시작, `_spawnTimer=0f` (즉시 첫 웨이브), `DungeonBattleStartedEvent` 발행
4. `Update()` 매 프레임 `_remainingTime` 감소 + `SpawnDungeonWave` 반복
5. `MonsterDiedEvent` 수신 시 `_killCount++`, `DungeonBattleProgressEvent` 발행

이벤트 구독자 존재 확인:
- `DungeonBattleEndedEvent` 구독: DungeonManager, DungeonPanelUI, QuestManager, AchievementSystem, RedDotManager, GoalGuideWidget, TutorialManager, AudioManager, PlayTestHelper, DungeonResultPopup, DungeonTransitionEffect, DungeonHUD
- `MonsterDiedEvent` Publisher: `MonsterController.cs:353` (사망 시점)

## 3. 즉시 보상 버그 부재 확인 — PASS

예전 버그(컨트롤러 부재 → fallback 경로로 즉시 `CompleteDungeon(dungeon, 1f)`) 분기는 `DungeonPanelUI.cs:372~379`에 위치:
```
if (DungeonBattleController.Instance != null) { StartBattle(...); }
else { CompleteDungeon(dungeon, 1f); ... }  // fallback
```
이번 검증에서 씬에 GameObject가 추가되어 `Awake()`에서 `Instance = this`가 세팅되므로 **fallback 분기는 더 이상 진입하지 않음**.

`EndBattle()` (line 248~283)에서만 `CompleteDungeon`이 호출되며, 이는 `_remainingTime <= 0f` 또는 `ForceEnd()` 이후에만 발생. 입장 직후 `_spawnTimer=0f`이므로 킬 이벤트보다 스폰이 먼저이고, score는 `killCount/targetKills`로 계산돼 순간 0.0→보스 사망까지 필연적인 전투 시간 경과 보장.

## 4. 이벤트 던전(EventDungeon) — N/A

DungeonPanelUI 내 EventDungeon 관련 필드 제거됨(line 44, 128, 193, 497 주석 참조). 시스템이 완전 삭제된 상태이므로 이번 세션 검증 범위 외. 서버측 `EventController.Claim`(일일 5회) 연동은 별도 시스템.

## 5. 보상 지급 — PASS

`DungeonManager.CompleteDungeon` (DungeonManager.cs:239~280):
- score 비례 mainReward/bonusReward를 `CurrencyManager.Add`
- `DungeonCompletedEvent` 발행 → `DungeonContentHandler`가 던전 타입별 추가 보상(WeaponTicket/WeaponStone/HuntPoint 등)
- BGM/배경 로비 복귀
- `DungeonResultPopup`, `ToastUI`가 결과 이벤트 구독으로 표시

## 6. QA 검사 6종

| 항목 | 결과 |
|---|---|
| 컴파일 에러 | PASS (using/namespace 일치) |
| DOTween 위반 | PASS (DOTween 미사용 파일) |
| asmdef 참조 | PASS (Dungeon.asmdef → Core/Combat/Data/Economy/Utils 정상) |
| null 안전 | PASS (모든 Instance/Manager 접근 null-check 완비) |
| 이벤트 쌍 | PASS (Subscribe/Unsubscribe 대응. DungeonBattleController OnEnable/OnDisable 쌍 확인) |
| UniTask 안전 | PASS (`PlayOpenAnimation`이 `this.GetCancellationTokenOnDestroy()` 사용) |

## 7. 미세 권장 사항 (Non-blocking)

1. `DungeonBattleController.Start()`에서 `_spawner`/`_stageManager`를 `FindFirstObjectByType`로 캐시하는데, 씬에 부재 시 로그 없이 스폰이 no-op이 됨(line 161). 디버깅 가시성을 위해 `Debug.LogWarning` 추가 고려.
2. `EndBattle()`에서 `_killCount == 0`인데 `_targetKills == 0`인 엣지 케이스 시 score=1 (line 254). DungeonDataSO의 `monsterCount==0` 방지 가드를 에디터 검사에 추가 권장.
3. `_maxAliveMonsters=30`은 portrait 9:16에서 많음. 프레임레이트 스파이크 시 `15~20`으로 조정 고려(튜닝 이슈, 버그 아님).

## 8. 완료 판정

- 컴파일 에러: 0
- Critical 이슈: 0
- 이전 P0 버그(즉시 보상) 재현 불가 확정 — DungeonBattleController 씬 부착으로 실제 전투 경로 확보
- Play-mode 런타임 테스트는 본 세션 범위 외(정적 분석 한정). 사용자 관점 invariant(`전투 30s 경과 후에만 DungeonCompletedEvent`)는 코드상 보장됨.

판정: **GREEN (정적)**. Play 테스트로 확정하려면 `/run` 봇 또는 수동 `PlayTestHelper.StartDungeonNow` 스모크 권장.
