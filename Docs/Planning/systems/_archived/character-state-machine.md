# 캐릭터 상태 머신 기획서 (P2-01)

## 개요
- **목적**: 자동 전투 캐릭터의 행동 로직을 상태 머신으로 관리
- **핵심 경험**: 캐릭터가 자연스럽게 움직이고 전투하는 모습을 보는 것

## 상태 정의

| 상태 | 설명 | SPUM 애니메이션 |
|------|------|-----------------|
| Idle | 대기. 적을 탐색 중 | `idle` |
| Run | 오른쪽으로 자동 이동 | `run` |
| Attack | 일반 공격 | 전사: `attack`, 궁수: `bow`, 마법사: `magic` |
| Skill | 스킬 사용 | 전사: `skill`, 궁수: `skill_bow`*, 마법사: `skill_magic`* |
| Hit | 피격 리액션 (0.3초) | `stun` |
| Die | 사망 | `death` |

> *Skill 애니메이션은 SPUM에서 `5_Skill_Normal`, `5_Skill_Bow`, `5_Skill_Magic` 제공

## 상태 전이도

```
[Idle]
  ├─ 적 발견 & 사거리 내 → [Attack]
  ├─ 적 발견 & 사거리 밖 → [Run]
  └─ 적 없음 & 맵 끝 아님 → [Run]

[Run]
  ├─ 적 사거리 내 진입 → [Attack]
  ├─ 맵 끝 도달 → [Idle]
  └─ 피격 → [Hit]

[Attack]
  ├─ 공격 애니메이션 완료 → [Idle] (재탐색)
  ├─ 스킬 쿨다운 완료 → [Skill]
  └─ 피격 → [Hit] (피격 모션 우선)

[Skill]
  └─ 스킬 애니메이션 완료 → [Idle]

[Hit]
  └─ 히트 지속시간(0.3초) 완료 → [Idle]

[Die]
  └─ 부활 타이머(3초) → [Idle] (시작 지점 리스폰)

모든 상태에서:
  └─ HP ≤ 0 → [Die]
```

## 컴포넌트 구조

```
[PlayerCharacter] (GameObject)
  ├── Rigidbody2D (Dynamic, Freeze Rotation Z)
  ├── CapsuleCollider2D (물리 충돌)
  ├── CharacterController (로직 - 상태 머신)
  ├── CharacterCombat (전투 - 데미지 처리)
  ├── CharacterStats (스탯 - HP/ATK/DEF 등)
  └── [SPUM_Unit] (자식 오브젝트)
       └── SPUM_Prefabs (비주얼)
```

### CharacterController 책임
- 상태 머신 관리 (현재 상태, 전이 조건)
- 이동 로직 (Rigidbody2D.velocity 제어)
- 적 탐색 (Physics2D.OverlapCircle 사거리 체크)
- SPUM 애니메이션 호출

### CharacterCombat 책임
- 데미지 계산 (combat-formula.md 참조)
- 공격 타이밍 (공속 기반 쿨다운)
- 스킬 슬롯 관리 및 자동 시전
- 데미지 텍스트 이벤트 발행

### CharacterStats 책임
- 현재 HP/ATK/DEF/CritRate 관리
- CharacterDataSO + 레벨 보정 + 장비 보정 합산
- HP 변동 이벤트 (HUD 갱신용)

## 이동 로직 상세

### 자동 이동
- 기본 방향: 오른쪽 (+x)
- 이동 속도: CharacterDataSO.moveSpeed (기본 3.0 유닛/초)
- 점프: Phase 2에서는 단일 바닥 맵만 사용 → 점프 미구현
  - 멀티 플랫폼은 Phase 3 이후 확장

### 적 탐색
```
detectionRange = 8.0f  // 적 감지 범위
attackRange    = 직업별 사거리 (1.5 / 6.0 / 5.0)
```
- Physics2D.OverlapCircleAll로 감지
- 가장 가까운 적을 타겟으로 설정
- 타겟이 사거리 밖이면 Run, 안이면 Attack

### 방향 전환
- 타겟 위치에 따라 localScale.x 반전
  - 타겟이 오른쪽: `localScale.x = 1` (SPUM 기본 방향이 왼쪽)
  - 타겟이 왼쪽: `localScale.x = -1`

## 엣지 케이스

| 상황 | 처리 |
|------|------|
| 동시에 여러 적 감지 | 가장 가까운 적 우선 타겟 |
| 타겟이 사망 | 즉시 Idle로 전이 → 재탐색 |
| 공격 중 타겟 사망 | 현재 공격 애니메이션 완료 후 Idle |
| 맵 경계 충돌 | Rigidbody2D가 Tilemap Collider로 자동 처리 |
| 부활 중 적 공격 | Die 상태에서는 무적 (Collider 비활성) |

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|-----------|------|
| 2026-03-07 | 초안 작성 | Phase 2 캐릭터 컨트롤러 기획 |
