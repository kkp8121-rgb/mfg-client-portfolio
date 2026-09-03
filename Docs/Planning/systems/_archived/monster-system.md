# 몬스터 시스템 기획서 (P2-04)

## 개요
- **목적**: 자동 전투의 적 역할. 처치 시 보상을 제공하여 성장 루프를 구동
- **핵심 경험**: 몬스터가 끊임없이 스폰되어 "사냥하는 맛"을 제공

## 몬스터 행동

### 상태 머신
```
[Idle] → 플레이어 감지 → [Chase] → 사거리 내 → [Attack]
                                                   ↓
                                              [Hit] ← 피격
                                                   ↓
                                              HP≤0 → [Die] → 풀 반환
```

| 상태 | 설명 | 애니메이션 |
|------|------|-----------|
| Idle | 스폰 후 대기, 플레이어 감지 대기 | idle |
| Chase | 플레이어 방향으로 이동 | run |
| Attack | 근접 공격 | attack |
| Hit | 피격 리액션 (0.2초) | stun |
| Die | 사망 → 보상 드롭 → 풀 반환 | death |

### 몬스터 AI 규칙
- 감지 범위: 6.0 유닛
- 공격 사거리: 1.5 유닛 (모든 일반 몬스터 근접)
- 이동 속도: MonsterDataSO.moveSpeed (기본 2.0)
- 공격 속도: MonsterDataSO.attackSpeed (기본 1.0)
- 감지 범위 밖이면 Idle 유지 (레일 위 왔다갔다 X)

## 스폰 시스템

### 스폰 규칙
- 스테이지 시작 시 몬스터 3마리 동시 스폰
- 몬스터 1마리 사망 → 1.5초 후 새 몬스터 1마리 스폰
- 최대 동시 존재: 5마리
- 스폰 위치: 플레이어 기준 오른쪽 5~10 유닛 범위 랜덤

### 스폰 위치 규칙
```
spawnX = playerX + Random(5, 10)
spawnY = groundLevel  // 바닥 높이
```

### 풀링 연동
- PoolManager.CreatePool("monster_{id}", prefab, 5)
- 스폰: PoolManager.Spawn("monster_{id}", pos, rot)
- 사망: 사망 애니메이션 완료 후 PoolManager.Despawn("monster_{id}", obj, 0.5f)

## 보스 몬스터

### 등장 조건
- 5층마다 보스 스테이지 (5, 10, 15, 20...)
- 일반 몬스터 10마리 처치 후 보스 출현
- 보스 등장 시 일반 몬스터 스폰 중지

### 보스 특성
| 항목 | 일반 몬스터 대비 |
|------|-----------------|
| HP | 8배 |
| ATK | 1.5배 |
| DEF | 1.2배 |
| 크기 | 1.5배 (localScale) |
| 보상 | 10배 |

### 보스 처치 시
1. 대량 보상 드롭 (골드 뿌리기 연출)
2. "STAGE CLEAR" 텍스트 표시
3. 2초 후 다음 층으로 자동 이동

### 보스 처치 실패 시
- 플레이어 사망 → 부활 → 같은 층 처음부터 재시작
- 보스 HP 리셋

## 컴포넌트 구조

```
[Monster] (GameObject, 풀링 대상)
  ├── Rigidbody2D (Dynamic, Freeze Rotation Z)
  ├── CapsuleCollider2D
  ├── MonsterController (AI 상태 머신)
  ├── MonsterCombat (데미지 처리)
  ├── MonsterStats (HP/ATK/DEF, MonsterDataSO 참조)
  ├── LootDropper (사망 시 보상 드롭)
  └── [Visual] (자식 오브젝트)
       └── SpriteRenderer 또는 SPUM 유닛
```

## 프로토타입 비주얼

Phase 2 프로토타입에서는 단색 사각형 + 이름 텍스트로 몬스터를 표현한다.
- 일반: 2x2 유닛 빨간 사각형
- 보스: 3x3 유닛 어두운 빨간 사각형
- 추후 스프라이트 교체

## 엣지 케이스

| 상황 | 처리 |
|------|------|
| 스폰 위치에 다른 몬스터 있음 | 약간 오프셋 (±1 유닛) |
| 몬스터가 맵 밖으로 밀림 | Rigidbody2D + Collider로 방지 |
| 플레이어 사망 중 몬스터 행동 | 타겟 잃음 → Idle |
| 동시에 여러 보스 존재 | 불가. 보스는 1마리만 스폰 |

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|-----------|------|
| 2026-03-07 | 초안 작성 | Phase 2 몬스터 시스템 기획 |
