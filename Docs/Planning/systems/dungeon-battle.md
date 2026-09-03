---
read_count: 3
last_read: "2026-04-03"
status: reference
---

# 던전 전투 시스템 기획서

> 키우기 RPG의 핵심 도전 콘텐츠.
> "버튼=보상" 자판기가 아닌, 실제 전투를 통한 성취감 제공.
> 참고: 메이플 키우기 성장던전, 매직 서바이벌의 메카닉 우선 철학.

---

## 1. 개요

던전은 제한 시간 내 목표를 달성하는 **도전 콘텐츠**다.
- 실패 가능성이 있어야 성취감이 존재한다
- 성과에 비례하여 보상이 차등 지급된다
- 성장의 결과를 체감하는 검증 무대다

---

## 2. 던전 유형 (5종)

| 유형 | 목표 | 제한 시간 | 핵심 보상 |
|------|------|----------|----------|
| 무기 던전 | 시간 내 최대 킬 수 | 30초 | WeaponTicket, WeaponStone |
| 경험치 던전 | 시간 내 최대 킬 수 | 30초 | EXP (50% 보너스) |
| 장비 던전 | 시간 내 최대 킬 수 | 30초 | HuntPoint |
| 등반자의 시련 | 웨이브 생존 | 45초 | ClimbToken |
| 강화석 던전 | 시간 내 최대 킬 수 | 30초 | RuneFragment, StarCrystal |

---

## 3. 전투 흐름

```
[입장 버튼]
  → 열쇠 소모 (1~3개)
  → 맵 전환 (던전 팔레트 + BGM)
  → 전환 연출 (플래시 + 와이프 + 텍스트)

[전투 시작]
  → HUD에 타이머 + 킬카운터 표시
  → 일반 스테이지 스폰 중단
  → 던전 몬스터 웨이브 시작 (빠른 간격, 강화 스탯)

[전투 중]
  → 플레이어 자동 전투 (기존 AI)
  → 킬 수 실시간 갱신
  → 타이머 감소

[시간 종료]
  → 잔여 몬스터 제거
  → 점수 계산: killCount / targetKills (0~1)
  → 결과 팝업: 성공/실패 + 킬수 + 점수 + 보상
  → 맵/BGM 복귀
  → 일반 스테이지 재개
```

---

## 4. 난이도 설계

### 몬스터 스탯 배율
- HP: 기본 x2.0 (던전 난이도 곱)
- ATK: 기본 x1.2
- 스폰 간격: 1.5초 (일반 3초 대비 2배 빠름)
- 배치 수: 4마리/웨이브
- 최대 동시: 30마리

### 목표 킬 수
- DungeonDataSO.monsterCount 필드 사용
- 기본 20마리 (던전별 상이)
- CP가 높으면 쉽게 달성, 낮으면 시간 부족

### 실패 조건
- 시간 내 목표 미달 → score < 1.0 → 보상 감소
- 플레이어 사망 → 즉시 종료 (score = 현재 달성률)

---

## 5. 보상 체계

### 성과 비례
```
baseReward = DungeonDataSO.baseRewardAmount
score = Clamp01(killCount / targetKills)
actualReward = Round(baseReward * score)
```

- 100% 달성: 풀 보상
- 50~99%: 비례 감소
- 50% 미만: 최소 보상 (노력 보상)
- 0%: 보상 없음

### 첫 클리어 보너스
- 각 던전 레벨 첫 클리어 시 보너스 보상 (추후)

---

## 6. 소탕 시스템 (추후)

이미 클리어한 던전(score >= 1.0)은 **소탕** 가능:
- 열쇠 소모 + 즉시 풀 보상
- 전투 없이 바로 결과
- "소탕" 버튼 별도 표시

---

## 7. 시각적 차별화

### 맵 전환
- PixelMapGenerator.TransitionToDungeon() 호출
- 던전 유형별 전용 팔레트 (추후)

### BGM
- AudioManager.PlayBgm(BgmType.Dungeon) 호출

### HUD
- 화면 상단: 타이머 바 (남은 시간 비율)
- 화면 상단: "12 / 20" 킬 카운터
- 던전 이름 표시

### 전환 연출
- DungeonTransitionEffect: 플래시 → 와이프 → 텍스트
- 종료 시: 밝은 플래시 → 결과 팝업

---

## 8. 데이터 구조

### DungeonDataSO (기존)
- timeLimit: 제한 시간 (초)
- monsterCount: 목표 킬 수
- difficultyMultiplier: 난이도 배율
- requiredKeys: 입장 열쇠 수
- requiredFloor: 최소 층수
- requiredCp: 최소 전투력

### DungeonBattleController (신규)
- 싱글톤, 씬에 배치
- StartBattle(DungeonDataSO) → 전투 시작
- Update()에서 타이머/스폰 관리
- EndBattle() → 점수 계산 + 완료

---

## 9. 이벤트

| 이벤트 | 시점 | 용도 |
|--------|------|------|
| DungeonEnteredEvent | 입장 시 | 맵/BGM 전환 |
| DungeonBattleStartedEvent | 전투 시작 | HUD 타이머 표시 |
| DungeonBattleProgressEvent | 킬 시마다 | HUD 킬카운터 갱신 |
| DungeonBattleEndedEvent | 전투 종료 | 결과 팝업 |
| DungeonCompletedEvent | 보상 지급 후 | 던전 UI 갱신 |

---

## 10. 구현 상태

- [x] DungeonBattleController 코어
- [x] MonsterSpawner.SpawnSingleMonster
- [x] DungeonPanelUI 연결
- [x] 이벤트 3종 정의
- [ ] HUD 타이머/킬카운터
- [ ] 결과 팝업
- [ ] 던전별 몬스터 차별화
- [ ] 소탕 시스템
- [ ] 던전 레벨 진행
