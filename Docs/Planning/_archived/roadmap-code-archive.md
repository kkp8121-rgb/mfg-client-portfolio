# mkLike 개발 체크리스트

> **Phase 번호는 카테고리 분류일 뿐, 순서 강제가 아니다.**
> 의존성만 충족되면 어떤 태스크든 자유롭게 착수 가능.
> 독립적인 태스크는 병렬 서브에이전트로 동시 진행한다.
>
> 각 태스크의 상태: `[대기]` `[진행중]` `[완료]` `[블로커]` `[폐기]`

---

## 카테고리 1: 코어 파운데이션 ✅
> 모든 시스템의 기반 — 전체 완료

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| C1-01 | 프로젝트 폴더 구조 생성 | [완료] | 31개 폴더 + .gitkeep |
| C1-02 | Assembly Definition 파일 생성 | [완료] | 12개 .asmdef |
| C1-03 | GameManager 싱글톤 | [완료] | GameState enum + 이벤트 발행 |
| C1-04 | 데이터 매니저 + SO 기반 데이터 로드 | [완료] | 4종 SO + DataManager |
| C1-05 | 세이브/로드 시스템 (JSON + ISaveProvider) | [완료] | ISaveProvider + LocalSave + 60초 자동저장 |
| C1-06 | 재화 시스템 (CurrencyManager) | [완료] | 13종 재화 |
| C1-07 | UI 프레임워크 (UIManager, BasePopup, TabController) | [완료] | 팝업 스택 + 탭 전환 |
| C1-08 | 오브젝트 풀링 시스템 | [완료] | ObjectPool<T> + PoolManager |
| C1-09 | 이벤트 시스템 (게임 이벤트 버스) | [완료] | EventBus + GameEvents |

---

## 카테고리 2: 인게임 전투 루프 ✅
> 탑다운 아레나 자동 전투 — 전체 완료

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| C2-01 | 플레이어 컨트롤러 (탑다운 자동 이동) | [완료] | 8방향 이동, 자동 추적 AI |
| C2-02 | 탑다운 아레나 맵 | [완료] | ArenaMap + ArenaDataSO |
| C2-03 | 카메라 시스템 (탑다운 추적) | [완료] | ArenaMap 경계 클램핑 |
| C2-04 | 몬스터 시스템 (웨이브 스폰, 추적 AI) | [완료] | 가장자리 스폰, separation force |
| C2-05 | 전투 공식 (데미지 계산) | [완료] | CombatFormula/DamageCalculator/CombatStats |
| C2-06 | 전리품 드롭 시스템 | [완료] | LootManager + 이벤트 |
| C2-07 | 챕터-스테이지 매니저 | [완료] | UniTask 웨이브 기반 |
| C2-08 | 미니보스 + 챕터 보스 | [완료] | 100마리→미니보스, 타임아웃 |
| C2-09 | 경험치/레벨업 시스템 | [완료] | LevelSystem |
| C2-10 | 상단 HUD | [완료] | HudPanel + 스킬슬롯 4개 |
| C2-11 | 몬스터 웨이브 시스템 | [완료] | UniTask 웨이브 루프 |
| C2-12 | 대량 몬스터 최적화 | [완료] | 동시 40, 풀링 50 |
| C2-13 | StageDataSO 재설계 | [완료] | ArenaDataSO 전환 |

---

## 카테고리 3: 캐릭터 성장 + 직업
> 의존성: C1(코어), C2(전투) 완료 필요

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| C3-01 | 능력치 분배 시스템 (레벨당 5포인트) | [완료] | C2-09 | StatAllocationSystem, 5스탯, 헌터등급 |
| C3-02 | 헌터 등급 시스템 (25포인트마다 등급 상승) | [완료] | C3-01 | HunterRankSystem, 등급별 보너스 |
| C3-03 | 스킬 데이터 (SO) + 스킬 슬롯 UI | [완료] | C2-10 | Phase2에서 구현 (SkillDataSO+SkillSlotUI) |
| C3-04 | 스킬 자동 시전 + 쿨다운 관리 | [완료] | C3-03 | Phase2에서 구현 (SkillSystem) |
| C3-05 | 스킬 레벨업 시스템 | [완료] | C3-03 | SkillSystem 확장, Lv1~10 |
| C3-06 | 직업 시스템 (전사/궁수/마법사) | [완료] | C2-05 | JobSystem + JobType, 자동 전직 |
| C3-07 | 전직 시스템 (1~3차, Lv.40/80/120) | [완료] | C3-06 | JobSystem 자동 전직 포함 |
| C3-08 | 등반자의 힘 (등반의 증표로 능력치 상승) | [완료] | C3-01 | ClimberPowerSystem |
| C3-09 | 어빌리티 시스템 (탑의 휘장으로 재롤) | [완료] | C3-08 | AbilitySystem, 11종 옵션 |
| C3-10 | 캐릭터 탭 UI 통합 | [완료] | C3-01~09 | CharacterPanel 5서브탭 연동 |

---

## 카테고리 4: 장비 + 무기
> 의존성: C1(코어), C2-05(전투공식) 완료 필요. C3과 병렬 진행 가능.

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| C4-01 | 장비 데이터 SO (11부위, 등급, 옵션) | [완료] | C1-04 | 12슬롯, 특수옵션, 주문서/성급 공식 |
| C4-02 | 장비 인벤토리 시스템 | [완료] | C4-01 | Phase2에서 구현 (EquipmentManager) |
| C4-03 | 장비 장착/해제 + UI | [완료] | C4-02 | EquipmentPanel 12슬롯+무기+강화 |
| C4-04 | 정예 몬스터 소환 (장비 획득) | [완료] | C2-04, C4-01 | EquipmentEnhanceSystem |
| C4-05 | 주문서 강화 (10회, 확률 기반) | [완료] | C4-03 | EquipmentEnhanceSystem |
| C4-06 | 성급 강화 (확률, 하락, 파괴) | [완료] | C4-03 | EquipmentEnhanceSystem |
| C4-07 | 잠재능력 + 잠재의 수정 (천장) | [완료] | C4-03 | PotentialSystem |
| C4-08 | 보조 잠재 + 상급 잠재의 수정 (12성 개방) | [완료] | C4-06, C4-07 | PotentialSystem |
| C4-09 | 무기 소환 (뽑기) 시스템 | [완료] | C1-06 | WeaponManager + WeaponDataSO |
| C4-10 | 무기 레벨업 + 각성 (5성) | [완료] | C4-09 | WeaponManager 확장 |
| C4-11 | 무기 승급 (5성x5개 → 다음 등급) | [완료] | C4-10 | WeaponManager 확장 |
| C4-12 | 무기 보유/장착 효과 | [완료] | C4-10 | WeaponManager 패시브 보너스 |
| C4-13 | 장비 탭 UI 통합 | [완료] | C4-01~12 | EquipmentPanel 3서브탭 |

---

## 카테고리 5: 동료 + 유물
> 의존성: C1(코어) 완료 필요. C3, C4와 병렬 진행 가능.

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| C5-01 | 동료 소환 (뽑기) 시스템 | [완료] | C1-06 | CompanionManager + 가챠 연동 |
| C5-02 | 동료 뽑기 연출 | [완료] | C5-01 | 데이터 레이어 준비, UI는 통합 태스크 |
| C5-03 | 메인 동료 (소환 전투, 30초) | [완료] | C5-01, C2-05 | CompanionCombat 자동소환 |
| C5-04 | 서브 동료 (장착효과만) | [완료] | C5-01 | CompanionEffectApplier |
| C5-05 | 동료 레벨업 (중복 합성) | [완료] | C5-01 | CompanionManager 중복합성 포함 |
| C5-06 | 직업별 동료 장착효과 | [완료] | C5-04, C3-06 | CompanionEffectApplier 직업배율 |
| C5-07 | 유물 데이터 SO (에픽/유니크/레전드리) | [완료] | C1-04 | RelicDataSO, 패시브+액티브 효과 |
| C5-08 | 유물 장착 + 각성 | [완료] | C5-07 | RelicManager |
| C5-09 | 동료 탭 UI 통합 | [완료] | C5-01~08 | CompanionPanel |

---

## 카테고리 6: 성장 던전 + 보스
> 의존성: C2(전투) 완료 필요. 대부분 C3~C5와 병렬 진행 가능.

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| C6-01 | 성장 던전 프레임워크 (열쇠 시스템) | [완료] | C2-07 | DungeonManager + DungeonDataSO |
| C6-02 | 무기 던전 (무기소환권 + 강화석) | [완료] | C6-01, C4-09 | DungeonContentHandler |
| C6-03 | 경험치 던전 (시간 내 처치) | [완료] | C6-01 | DungeonContentHandler |
| C6-04 | 장비 던전 (사냥 포인트) | [완료] | C6-01 | DungeonContentHandler |
| C6-05 | 등반자의 시련 (등반의 증표) | [완료] | C6-01, C3-08 | DungeonContentHandler |
| C6-06 | 강화 던전 (룬 조각 + 주문서) | [완료] | C6-01 | DungeonContentHandler |
| C6-07 | 월드보스 (20단계, 랭킹) | [완료] | C2-08 | WorldBossSystem |
| C6-08 | 보스 레이드 (주 3회, 난이도별) | [완료] | C2-08 | BossRaidSystem |
| C6-09 | 오프라인 보상 (분당 처치 수) | [완료] | C2-06 | OfflineRewardSystem |
| C6-10 | 던전 탭 UI 통합 | [완료] | C6-01~09 | DungeonPanel 3서브탭 |

---

## 카테고리 7: 미션/경제/편의
> 의존성: C1(코어) 완료 필요. 대부분 독립 진행 가능.

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| C7-01 | 퀘스트/미션 시스템 (메인/일일/주간) | [완료] | C1-09 | QuestManager + QuestDataSO |
| C7-02 | 업적/도감 | [완료] | C1-09 | AchievementSystem |
| C7-03 | 출석 보상 | [완료] | C1-06 | AttendanceSystem |
| C7-04 | 상점 UI (일반/주간/이벤트) | [완료] | C1-06 | ShopSystem |
| C7-05 | 우편함 | [완료] | C1-06 | MailboxSystem |
| C7-06 | 설정 (사운드, 알림) | [완료] | 없음 | SettingsManager |
| C7-07 | 기타 탭 UI 통합 | [완료] | C7-01~06 | MiscPanel 5서브탭 |

---

## 카테고리 12: 코스튬 시스템
> 의존성: C1(코어) 완료 필요. 기획서: `systems/costume-system.md`

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| CC-01 | CostumeSlot, CostumeGrade enum 정의 | [완료] | C1-04 | CostumeTypes.cs |
| CC-02 | CostumeDataSO + CostumeSetDataSO 생성 | [완료] | CC-01 | SO 필드 설계, CreateAssetMenu |
| CC-03 | CostumeManager 싱글톤 구현 | [완료] | CC-02, C1-05 | Equip/Unequip/HasFullSet/Save/Load |
| CC-04 | 이벤트 정의 (CostumeObtainedEvent 등) | [완료] | CC-01 | EventBus 연동 |
| CC-05 | PlayerCharacter 코스튬 스프라이트 적용 | [대기] | CC-03, C2-01 | 장착 시 외형 변경 로직 |
| CC-06 | 세트 이펙트 시스템 | [대기] | CC-03, CC-02 | HasFullSet → 이펙트 프리팹 활성화 |
| CC-07 | 코스튬 패널 UI | [완료] | CC-03 | 코스튬 목록/필터/장착/미리보기 |
| CC-08 | 세트 목록 UI | [대기] | CC-06 | 세트 진행도/이펙트 설명 표시 |
| CC-09 | 코스튬 SO 에셋 생성 (초기 데이터) | [대기] | CC-02 | 최소 2세트(8개) + 개별 4개 = 12개 |
| CC-10 | 기존 시스템 연동 (배틀패스/상점/업적) | [대기] | CC-03 | 보상 지급 연동 |

---

## 카테고리 13: 챌린지 모드
> 의존성: C2(전투), C6(던전) 완료 필요. 기획서: `systems/challenge-mode.md`

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| CH-01 | ChallengeType enum + ChallengeResult 구조체 정의 | [완료] | - | ChallengeType.cs |
| CH-02 | ChallengeDataSO + StarMilestone SO 데이터 | [완료] | CH-01 | ChallengeDataSO.cs |
| CH-03 | ChallengeManager 싱글톤 구현 | [완료] | CH-02, C1-05 | Start/Complete/WeeklyReset/Save/Load |
| CH-04 | 주간 리셋 로직 (시드 기반 랜덤 선택) | [대기] | CH-03 | 월요일 00:00 리셋, 시드 = 연도*100+주차 |
| CH-05 | 이벤트 정의 (ChallengeStartedEvent 등) | [대기] | CH-01 | EventBus 연동 |
| CH-06 | 챌린지 전투 조건 시스템 | [대기] | CH-03, C2 | SpeedRun 타이머, NoDamage 카운터, LowLevel 디버프 등 |
| CH-07 | 별 평가 + 마일스톤 보상 시스템 | [대기] | CH-03 | 누적 별, 마일스톤 테이블 |
| CH-08 | 챌린지 메인 패널 UI | [완료] | CH-03 | ChallengePanel.cs |
| CH-09 | 챌린지 진행 HUD UI | [대기] | CH-06 | 타이머, 카운터, 포기 버튼 |
| CH-10 | 결과/마일스톤 팝업 UI | [대기] | CH-07 | 별 애니메이션, 보상 표시 |
| CH-11 | ChallengeDataSO 에셋 생성 (초기 데이터) | [대기] | CH-02 | 5종 x 3~5개 = 15~25개 |
| CH-12 | DungeonPanel 챌린지 탭 연동 | [대기] | CH-08 | 던전 탭에 챌린지 서브탭 추가 |

---

## 카테고리 14: 등반 조합
> 의존성: C5(동료), C3-06(직업) 완료 필요. 기획서: `systems/climbing-party.md`

| ID | 태스크 | 상태 | 의존성 | 비고 |
|----|--------|------|--------|------|
| CP-01 | PartySlot, PartyBonusType enum 정의 | [완료] | - | PartyTypes.cs |
| CP-02 | CompanionDataSO에 companionJob, preferredSlot, 궁극기 필드 추가 | [완료] | - | CompanionDataSO 확장 |
| CP-03 | ModifierSource.Party 추가 | [대기] | - | CombatStats enum 확장 |
| CP-04 | ClimbingPartyManager 코어 구현 | [완료] | CP-01, CP-02, CP-03 | SetSlot/RemoveSlot/GetPartyBonus/GetFormation |
| CP-05 | 조합 보너스 계산 로직 + CombatStats modifier 적용 | [대기] | CP-04 | 동직업/혼합 보너스 로직 |
| CP-06 | PartySaveData + SaveManager 연동 | [대기] | CP-04 | 세이브/로드 |
| CP-07 | CompanionManager → ClimbingPartyManager 마이그레이션 | [대기] | CP-04, CP-06 | 메인/서브 → Scout/Support 전환 |
| CP-08 | CompanionCombat 확장: Scout 아이템 자동 수집 AI | [대기] | CP-07 | 전투 타겟 없을 때 드롭 수집 |
| CP-09 | CompanionUltimate 신규 구현 (Ace 슬롯 궁극기) | [대기] | CP-04 | 60초 쿨타임 자동 발동, DOTween 연출 |
| CP-10 | 파티 편성 UI (PartyFormationPanel) | [완료] | CP-04, CP-05 | PartyPanel.cs |
| CP-11 | Ace 쿨타임 HUD + 궁극기 발동 연출 | [대기] | CP-09 | 원형 쿨타임 게이지, 발동 연출 |
| CP-12 | CompanionDataSO 에셋에 companionJob 값 세팅 | [대기] | CP-02 | 에디터 스크립트 |
| CP-13 | 파티 시스템 통합 테스트 + 밸런스 검증 | [대기] | CP-01~CP-12 | QA |

---

## 병렬 진행 가이드

아래 태스크 그룹은 **동시 병렬 진행 가능**:

```
[병렬 그룹 A] — C3(성장) + C4(장비) + C5(동료): 서로 독립, C1/C2만 의존
[병렬 그룹 B] — C6(던전) + C7(미션): C1/C2만 의존, A그룹과도 대부분 병렬 가능
[병렬 그룹 C] — C12(코스튬) + C13(챌린지) + C14(등반 조합): 서로 독립, 병렬 진행 가능
[순차 필요]   — C6-02(무기던전)은 C4-09(무기뽑기) 완료 필요
               C5-06(동료 장착효과)은 C3-06(직업 시스템) 완료 필요
               C6-05(등반자의 시련)은 C3-08(등반자의 힘) 완료 필요
               CP-07(마이그레이션)은 CP-06(세이브) 완료 필요
               CH-06(전투 조건)은 C2(전투) 전체 완료 필요
```

---

## 진행 요약

| 카테고리 | 태스크 수 | 완료 | 진행중 | 대기 | 블로커 |
|----------|-----------|------|--------|------|--------|
| 1. 코어 | 9 | 9 | 0 | 0 | 0 |
| 2. 전투 | 13 | 13 | 0 | 0 | 0 |
| 3. 성장+직업 | 10 | 10 | 0 | 0 | 0 |
| 4. 장비+무기 | 13 | 13 | 0 | 0 | 0 |
| 5. 동료+유물 | 9 | 9 | 0 | 0 | 0 |
| 6. 던전+보스 | 10 | 10 | 0 | 0 | 0 |
| 7. 미션/편의 | 7 | 7 | 0 | 0 | 0 |
| 8. 시즌/배틀패스 | 5 | 5 | 0 | 0 | 0 |
| 9. PvP 아레나 | 6 | 6 | 0 | 0 | 0 |
| 10. 펫 시스템 | 5 | 5 | 0 | 0 | 0 |
| 11. 길드 시스템 | 6 | 6 | 0 | 0 | 0 |
| 12. 코스튬 | 10 | 5 | 0 | 5 | 0 |
| 13. 챌린지 모드 | 12 | 4 | 0 | 8 | 0 |
| 14. 등반 조합 | 13 | 4 | 0 | 9 | 0 |
| **합계** | **128** | **106** | **0** | **22** | **0** |

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|-----------|------|
| 2026-03-06 | 초안 작성 | 전체 로드맵 수립 |
| 2026-03-07 | 카테고리 1 전체 완료 | 9/9 태스크 완료 |
| 2026-03-07 | 카테고리 2 기획서 작성 | combat-formula, character-state-machine, monster-system |
| 2026-03-07 | 카테고리 2 전체 완료 | 13/13 태스크 완료 |
| 2026-03-07 | 로드맵 전면 개편 | 메이플 키우기 시스템 구조 반영, 6→7 카테고리 |
| 2026-03-07 | 명칭 전면 교체 | IP 충돌 방지 |
| 2026-03-08 | 카테고리 2 전면 재설계: 사이드뷰 → 탑다운 | 에셋 호환성, 뱀서라이크 전투 방식 |
| 2026-03-15 | **Phase 순서제 → 체크리스트 전환** | 순서 강제 제거, 의존성 기반 병렬 진행 허용 |
| 2026-03-15 | **49개 태스크 완료** (22→71/71, **100%**) | 8라운드 병렬 에이전트 — 전체 로드맵 완료! |
| 2026-03-16 | **카테고리 12~14 추가** (코스튬/챌린지/등반 조합) | 차기 콘텐츠 3종 태스크 35개 추가, 13개 구현 완료 |
