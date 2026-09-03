# 오리지널 시스템 아카이브

> 작성일: 2026-04-07
> 사유: 메이플 키우기 범위 내 압축 + 완성도 우선 방침으로 제거
> 향후 확장 시 참고용으로 기록 보존

---

## 제거 배경

mkLike는 메이플 키우기를 탑뷰로 재해석한 게임.
인력/시간/비용 한계로 메이플 키우기보다 큰 게임은 비현실적.
메이플 키우기에 없는 오리지널 시스템은 범위 초과로 판단하여 제거.
기획서는 `Docs/Planning/systems/`에 보존.

---

## 제거된 시스템 7종

### 1. 등반 조합(파티) 시스템
- **기획서**: `systems/climbing-party.md`
- **구현 상태**: 미구현 (기획서만 존재)
- **내용**: 4인 파티 편성 (선봉/정찰/지원/비장), 직업 조합 보너스 (SameJobx2~x4, BalancedMix), CompanionManager 확장, ClimbingPartyManager, CompanionUltimate (Ace 궁극기)
- **코드 영향**: 없음

### 2. 탑 기믹 시스템 (TowerGimmickSystem)
- **기획서**: `systems/tower-system.md`
- **구현 Phase**: P15 (Sprint 15-1)
- **내용**: 10종 환경 기믹 (독안개/중력변동/심연침식 등), 층별 자동 적용/해제, CombatStats modifier 연동, 엘리트(101~200F)/심연(201~300F)/무한(301F+) 구간별 기믹 배정
- **주요 코드**: `Combat/TowerGimmickSystem.cs`, `SecretRoomManager.CheckGimmickCondition()`
- **로드맵 태스크**: S151-01 ~ S151-06

### 3. 특수 몬스터 6종
- **구현 Phase**: P16 (Sprint 16-1)
- **내용**: SpecialMonsterType enum (Shield/Berserk/Split/Healer/Stealth/Elite), SpecialMonsterBehavior, MonsterSpawner 확률 스폰 (엘리트 구간 101F+ 에서 3~15%)
- **주요 코드**: `SpecialMonsterBehavior.cs`, MonsterSpawner 확률 스폰 통합
- **로드맵 태스크**: S161-01 ~ S161-04

### 4. 비밀 방 (SecretRoom)
- **기획서**: `systems/round2-systems.md` §1
- **구현 Phase**: P14 (Sprint 14-1)
- **내용**: 4종 조건 트리거 (set_equip/full_party/costume_set/gimmick_clear), 조건 충족 시 숨겨진 던전 해금
- **주요 코드**: `SecretRoomManager.cs`, 각 트리거 조건 체크 로직
- **로드맵 태스크**: S141-01 ~ S141-06

### 5. 스킬 마스터리
- **구현 Phase**: P8 (Sprint 8-3)
- **내용**: 스킬별 사용 횟수 추적, 마스터리 5단계, 단계별 보너스, SkillMasteryManager, SkillMasteryDataSO
- **주요 코드**: `SkillMasteryManager.cs`, `SkillMasteryDataSO.cs`, 스킬 탭 UI 마스터리 게이지
- **로드맵 태스크**: S83-01 ~ S83-05

### 6. 챌린지 모드
- **기획서**: `systems/challenge-mode.md`
- **구현 Phase**: P10 (Sprint 10-2)
- **내용**: 특수 조건 스테이지 (시간제한/HP제한 등), 별(Stars) 평가, ChallengeManager, ChallengeCompletedEvent
- **주요 코드**: `ChallengeManager.cs`, DungeonPanel 챌린지 통합
- **로드맵 태스크**: S102-01 ~ S102-04

### 7. 탑 마일스톤
- **구현 Phase**: P16 (Sprint 16-2)
- **내용**: 20개 마일스톤 (10~200F), 층 도달 시 자동 수령 (골드/루비/소환권 + 특수 보상), TowerMilestoneManager, TowerMilestoneDataSO
- **주요 코드**: `TowerMilestoneManager.cs`, `TowerMilestoneDataSO.cs`, `TowerMilestoneClaimedEvent.cs`
- **로드맵 태스크**: S162-01 ~ S162-04

---

## 크게 확장했던 시스템 (축소 검토 대상)

아래는 메이플 키우기에 있지만 mkLike에서 과도하게 확장한 부분.
메이플 키우기 수준으로 축소하거나, 확장분만 비활성화 검토 필요.

| 시스템 | mkLike 확장분 | 메이플 키우기 수준 |
|--------|-------------|-----------------|
| 아레나 | 라이벌 AI 4종 + 턴제 시뮬 + 전투 로그 시각화 | 단순 CP 비교 매칭 |
| 길드 보스 | 3페이즈 + 분노 게이지 + 스킬 6종 + 토벌전 | 단순 데미지 기여 |
| 어빌리티 | 3갈래 선택형 트리 + 선행 조건 | 랜덤 패시브 슬롯 |
| 아티팩트 | 4슬롯 + 2세트/4세트 세트 효과 | 유물 수집 + 보유효과 |
| 몬스터 스케일링 | 엘리트/심연/무한 구간 배율 | 단순 레벨 스케일링 |
