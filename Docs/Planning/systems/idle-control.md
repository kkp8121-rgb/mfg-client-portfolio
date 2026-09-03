---
read_count: 0
last_read: "never"
status: active
priority: P0
---

# 아이들 컨트롤 정책 (2026-04-23)

## 핵심 원칙

> **MFG는 항상 오토 플레이다.** 유저의 수동 조작은 전투 AI를 대체하는 것이 아니라 **보조**한다.

이 정책은 사용자(게임 기획자)가 확정:
> "어차피 무조건 오토고, 수동일 때는 유저가 컨트롤로 조작해서 움직일 때뿐 — 보스 회피 용도"

## 두 가지 정책 결정

### 결정 1: Full Auto 토글 버튼 **삭제** (이슈 7)

**근거**:
- "Full Auto"라는 토글 자체가 "수동 플레이 모드가 존재한다"는 오해 유발
- 실제로는 토글이 기능 없이 UXML에만 남아 있던 dead UI (`HUD.uxml:153-154`)
- 설계 철학과 불일치

**조치**:
- `HUD.uxml`에서 `full-auto-btn` Button 요소 완전 삭제
- HudUI.cs에 관련 바인딩 코드 없음 확인 (현재 dead)
- 저장 데이터에 "fullAutoEnabled" 같은 플래그 있으면 제거 (마이그레이션 불필요 — 항상 true 동작)

### 결정 2: 수동 이동 = **보스 회피 전용** (이슈 6)

**현황**:
- `PlayerCharacter.cs:55-101` `_isManualMoving` + `JoystickBridge.HasInput` 정상 작동
- `JoystickBridge` 정적 클래스가 Direction/HasInput 공급
- Input System `InputSystem_Actions.inputactions`에 "Move" 액션 매핑(Gamepad/Keyboard)
- **문제**: 조이스틱 UI 컴포넌트가 현재 씬에 배치되지 않음 (터치용 Joystick UIDocument 누락)

**정책**:
- 일반 전투: 오토 (PlayerCharacter AI가 몬스터 추적/공격)
- **보스전 한정 수동 조작 UX**:
  - 보스 등장 시 HUD에 조이스틱(또는 WASD 힌트) 활성
  - 일반 전투 시 조이스틱 UI 비활성 (화면 방해 안 함)
- 모바일: 가상 조이스틱 (UI Toolkit + Touch)
- PC: WASD + 화살표 키 (Input System)
- 게임패드: 왼쪽 스틱 (Input System 자동)

**조작 범위**:
- **이동만** — 공격/스킬은 오토 유지 (유저가 "어디로 이동" 할지만 결정)
- 이동 중에도 자동 타겟팅/공격 계속 동작

## 구현 (Phase C)

### C-a. HUD.uxml 수정
```xml
<!-- 제거 -->
<ui:Button name="full-auto-btn" text="Full&#10;Auto" class="hud__btn hud__btn--fullauto"/>

<!-- (이 라인 통째로 삭제. 이미지/버튼 없음) -->
```

### C-b. 조이스틱 UIDocument 배치
- `Assets/UI Toolkit/Views/VirtualJoystick.uxml` 신규 또는 기존 복구
- `Main.unity` 씬에 `[UITK] VirtualJoystick` GameObject + UIDocument 컴포넌트
- 활성화 조건: 보스전 진입 시 (StageManager.IsBossPhase)
- 터치 이벤트 → `JoystickBridge.SetInput(direction, hasInput)` 호출

### C-c. PlayerCharacter 검증
- 현재 코드는 유지. `JoystickBridge.HasInput` true면 수동 이동 우선.
- 보스전 종료 시 `JoystickBridge.Clear()` (입력 해제)

### C-d. InputSystem 바인딩
- `InputSystem_Actions.inputactions`의 "Move" 액션이 이미 Gamepad 스틱 + Keyboard WASD 바인딩됨 확인
- PlayerCharacter에서 Input System 리딩 또는 JoystickBridge 중계 — 기존 구조 유지

## 런틱 테스트 통합 (이슈 16 연동)

- BotInvariants 신규 체크: `Check_FullAutoButtonRemoved`
  - HUD 루트에서 "full-auto-btn" 이름 Button 검색 → 존재 시 Fail
- 보스전 진입 시 조이스틱 UI 활성 검증 (Phase D)

## 성공 기준

- [ ] HUD에서 Full Auto 버튼 사라짐
- [ ] 일반 전투 시 유저가 가만히 있어도 캐릭터가 몬스터 추격 + 공격
- [ ] 보스전 진입 시 조이스틱(또는 힌트) 활성
- [ ] WASD / 게임패드 / 터치 조이스틱 어느 입력이든 캐릭터 이동 가능
- [ ] 이동 중에도 자동 공격/타겟팅 유지
