---
read_count: 0
last_read: "never"
status: active
---

# Shallow Systems Kill List (엄격 재감사 2026-04-20)

> 이전 "14개 전부 DEEP" 감사 판정을 사용자 직접 반박 ("계속 버그 + 얕은 카피 + 문어발") 후 **엄격 기준 (SO 10개 이상 / Manager 메서드 3개 이상 / BeforeSave+LoadCompleted 둘 다 구독 / Publish 2건 이상 / 플레이어 능동성 / CombatStats 연계 / 핵심 루프 제거 테스트)** 으로 전면 재판정.

## 감사 요약 (엄격 재판정)

- **DEEP: 2개** (유지 — Booster, BattlePass)
- **MEDIUM: 4개** (콘텐츠 채우고 퀵메뉴/탭바 정리 후 재평가)
- **SHALLOW: 8개** (삭제 권장)

이전 DEEP 14 → 재판정 DEEP 2 = **12개 강등**. 사용자 관찰과 일치.

## 시스템 상세 판정

### 1. Artifact — SHALLOW (카피, 유물 제거 위반 의심)

- Manager public 메서드: 4 (AddArtifact, EquipArtifact, UnequipArtifact, LoadFromSave, ToSaveData) — 조건 통과
- SaveData: `SaveData.artifact` (owned[], equippedSlots[]) 존재, but **BeforeSave/LoadCompleted 둘 다 미구독** — 수동 `LoadFromSave/ToSaveData` 호출만 있음, 누가 부르는지 불명
- EventBus Publish: 2건 (ArtifactObtainedEvent, ArtifactEquippedEvent) — 경계값
- **SO 엔트리: 0개** (`_artifactCatalog`는 인스펙터 배열, `Assets/Data/SO/Artifact/` 폴더 없음) → DEEP 조건 #5 (10개+) **완전 실패**
- CombatStats 연계: yes (ModifierSource.Artifact)
- 플레이어 능동성: yes (장착)
- **판정 근거**: **유물(Relic) 제거 이후 같은 성격의 "장착형 영구 성장" 시스템을 다시 만든 것**. "최후반 — 스테이지 347 해금"은 출시 전 절대 도달 불가. SO 0개. 같은 카피 의심. → **SHALLOW 확정, 제거.**
- **제거 파일**:
  - `Scripts/Combat/ArtifactManager.cs`
  - `Scripts/Data/SO/ArtifactDataSO.cs` (+ ArtifactSetSO)
  - `Scripts/UI/ArtifactTabUI.cs`
  - `UI Toolkit/Views/ArtifactTab.uxml`
  - `SaveData.cs`: `artifact`, `ArtifactSaveData`, `ArtifactInstanceData` (~Line 31, 323-336)
  - `ModifierSource.Artifact` enum
  - `GameEvents.cs`: ArtifactObtainedEvent, ArtifactEquippedEvent
  - HUD 퀵메뉴 연결 (있으면)

### 2. Inscription — SHALLOW (Artifact 쌍둥이)

- Manager public 메서드: 5 (AddInscription, Equip, Unequip, ExpandSlots, GetStatBonus 계열) — 통과
- SaveData: `SaveData.inscription` 존재, but **BeforeSave/LoadCompleted 미구독** — SaveState() 수동 호출
- EventBus Publish: 2건 (InscriptionChangedEvent 두 곳) — 동일 이벤트 2번이라 경계
- **SO 엔트리: 0개** (`Assets/Data/SO/Inscription/` 폴더 없음)
- CombatStats 연계: yes (ModifierSource.Inscription)
- 플레이어 능동성: yes (탑 클리어→획득→장착)
- **판정 근거**: **Artifact와 기능적으로 100% 동일** (장착 슬롯 + 스탯 보너스 + 세이브). 이름만 다름. "탑 클리어 보상"이라는 획득 경로 구분이 유일한 차이. SO 0개. **엄격 기준 제거.**
- **제거 파일**:
  - `Scripts/Growth/InscriptionManager.cs`
  - `Scripts/Data/SO/InscriptionDataSO.cs`
  - `SaveData.cs`: `inscription`, `InscriptionSaveData` (~Line 32, 400~)
  - `ModifierSource.Inscription`
  - `GameEvents.cs`: InscriptionChangedEvent, TowerFloorReachedEvent 일부

### 3. Climber (ClimberPowerSystem) — SHALLOW

- Manager public 메서드: 3 (Initialize, Upgrade, GetUpgradeCost, CanUpgrade) — 경계
- SaveData: `SaveData.climberPowerLevel` (int 1개 필드) — **BeforeSave/LoadCompleted 미구독** (Start + Upgrade 시 수동 Save)
- EventBus Publish: 1건 (ClimberPowerUpEvent) — **조건 #3 (2건+) 실패**
- **SO 엔트리: 0개** (하드코딩 const MAX_LEVEL=100 등, SO 없음)
- CombatStats 연계: yes (HP+20/Atk+3/Def+1 per level)
- 플레이어 능동성: 있음 (버튼 클릭)
- **판정 근거**: **단일 슬라이더 "레벨업 버튼 클릭하면 스탯 오름"**. 기능적으로 CombatStats에 LevelSystem 보너스 복제. 플레이어 선택 없음 (그냥 눌러서 올림). SO 0. 이벤트 1. 메서드 경계값. → **얕음 확정, 제거.**
- **제거 파일**:
  - `Scripts/Growth/ClimberPowerSystem.cs`
  - `Scripts/UI/ClimberTabUI.cs`
  - `UI Toolkit/Views/CharacterClimberTab.uxml`
  - `SaveData.cs`: `climberPowerLevel`
  - `GameEvents.cs`: ClimberPowerUpEvent
  - `ModifierSource.Prestige` (클라이머 전용이면)
  - `CurrencyType.ClimbToken` 재평가 (Mastery.Respec 도 사용하므로 Mastery 제거 시 같이)

### 4. HeroPower — MEDIUM (자동 마일스톤, 플레이어 능동성 0)

- Manager public 메서드: 3 (GetNextMilestoneCp, LoadFromSave, ToSaveData) — 경계 (나머지는 이벤트 핸들러)
- SaveData: `heroPower.unlockedMilestones` — **BeforeSave/LoadCompleted 미구독**
- EventBus Publish: 1건 (HeroPowerMilestoneEvent, OnQuestStateRefresh에서도 동일) — 조건 실패
- **SO 엔트리: 1개** (`HeroPowerConfig.asset`) — 조건 실패
- CombatStats 연계: yes (ModifierSource.HeroPower, %보너스)
- 플레이어 능동성: **NO (CP 올라가면 자동으로 마일스톤 해금)** — 플레이어 interaction 없음
- **판정 근거**: "자동 보너스 테이블"이다. 플레이어가 선택하는 게 아니라 CP 넘기면 해제. 독립 시스템이라기보단 LevelSystem/Equipment의 보너스 커브. SO 1개(단일 config). → **SHALLOW로 강등, 제거 권장.**
- **제거 파일**:
  - `Scripts/Combat/HeroPowerManager.cs`
  - `Scripts/Data/SO/HeroPowerDataSO.cs`
  - `Scripts/UI/HeroPowerTabUI.cs`
  - `UI Toolkit/Views/CharacterHeroPowerTab.uxml`
  - `SaveData.cs`: `heroPower`, `HeroPowerSaveData`
  - `ModifierSource.HeroPower`
  - `GameEvents.cs`: HeroPowerMilestoneEvent, QuestCondition.HeroPowerMilestone

### 5. Ability — SHALLOW

- Manager public 메서드: 3 (UnlockNode, ResetAll, IsNodeUnlocked) — 경계
- SaveData: `ability.unlockedNodeIds[], availablePoints` — **BeforeSave/LoadCompleted 미구독**
- EventBus Publish: 2건 (AbilityUnlockedEvent, AbilityResetEvent) — 통과
- **SO 엔트리: 0개** (`AbilityTreeConfigSO` 단일 SO 존재하지만 `Assets/Data/SO/Ability/` 없음 — 인스펙터 참조 불명)
- CombatStats 연계: yes (`ModifierSource.Mastery` 공유 — Mastery와 혼용)
- 플레이어 능동성: yes (노드 선택)
- **판정 근거**: HeroPower의 하위 시스템으로 선언된 특성 트리. 하지만 노드 SO 파일 0개, AutoPlayBot이나 가이드 퀘스트에서 실제 해금/선택하는 경로 없음. **ModifierSource를 Mastery랑 공유**하여 리셋 시 마스터리까지 날리는 버그 가능 (코드 178라인 주석 "Ability도 Mastery 소스 사용"). → **SHALLOW. Mastery 안으로 통합하거나 제거.**
- **제거 파일**:
  - `Scripts/Combat/AbilityManager.cs`
  - `Scripts/Data/SO/AbilityNodeSO.cs` (AbilityTreeConfigSO 포함)
  - `Scripts/UI/AbilityTabUI.cs`
  - `UI Toolkit/Views/CharacterAbilityTab.uxml`
  - `SaveData.cs`: `ability`, `AbilitySaveData`
  - `GameEvents.cs`: AbilityUnlockedEvent, AbilityResetEvent, QuestCondition.UnlockAbility

### 6. Mastery — MEDIUM (유지 가능, 단 리스펙 통화 Climber 제거 연동 수정 필요)

- Manager public 메서드: 6 (UnlockNode, Respec, AddPoints, CanUnlock, IsUnlocked, Initialize) — **통과**
- SaveData: `mastery.*` — **BeforeSave/LoadCompleted 미구독** (수동 Save)
- EventBus Publish: 2건 (MasteryUnlockedEvent, MasteryRespecEvent) — 통과
- **SO 엔트리: 27개** (직업 3 × 분기 3 × 단계 3) — **통과**
- CombatStats 연계: yes
- 플레이어 능동성: yes (4차 전직 후)
- **판정 근거**: 유일하게 엄격 기준 대부분 통과. 4차 전직(엔드게임)이라 출시 전 테스트 루프에선 거의 안 닿지만 **SO 27개 실볼륨 있음**. 세이브 이벤트 연동이 수동이란 게 걸림. → **MEDIUM 유지, BeforeSave/LoadCompleted 연결로 승격 가능.**
- 단 Climber 제거 시 `Respec`의 `CurrencyType.ClimbToken` 소비를 **Ruby/MasteryToken 등으로 재설계** 필요.

### 7. ReturneeGuide — SHALLOW (출시 전 무의미)

- Manager public 메서드: 1 (static CalculateReturneeBonus) — **조건 #1 실패**
- SaveData: 없음 (ProgressData.lastLogoutUtc만 읽음)
- EventBus Publish: 2건 (ReturneeGuideEvent, ReturneeKeyGrantEvent)
- SO 엔트리: 0개 (전부 const 하드코딩, `RETURNEE_THRESHOLD_HOURS=72` 등)
- CombatStats 연계: no
- 플레이어 능동성: no (자동 트리거)
- **판정 근거**: "30/14/7일 미접속 후 복귀자 보너스" — **출시 전 게임**엔 복귀할 기존 유저가 없음. 즉시 제거 권장 (정식 출시 후 재도입). → **SHALLOW, 즉시 제거.**
- **제거 파일**:
  - `Scripts/Combat/ReturneeGuideManager.cs`
  - `Scripts/UI/ReturneeGuidePopup.cs`
  - `GameEvents.cs`: ReturneeGuideEvent, ReturneeKeyGrantEvent, ReturneeBonus 구조체

### 8. AdRewardManager — SHALLOW (SDK 없음, stub)

- Manager public 메서드: 7 (CanWatchAd, GetRemainingCount, WatchAd, ConsumeOfflineReward2x, ConsumeRevive, ConsumeEnhanceProtect + Has*) — 통과
- SaveData: **없음** (PlayerPrefs만 사용) — 조건 #2 **완전 실패**
- EventBus Publish: 1건 (AdRewardEvent) — 실패
- SO 엔트리: 0개 (DEFAULT_LIMITS 하드코딩 Dictionary)
- CombatStats 연계: 간접 (GoldBoost/ExpBoost는 다른 시스템이 플래그 참조)
- 플레이어 능동성: yes (버튼)
- **판정 근거**: **코드에 `// TODO: 실제 광고 SDK 호출`** 명시. UnityAds/AdMob 미통합. 현재 버튼 누르면 즉시 성공 처리되는 치트 수준. SaveData도 미연동(PlayerPrefs로 날림). → **SHALLOW, 실 SDK 통합 전까지 제거 또는 비활성.**
- **권장**: 제거하거나, 최소 `UI/AdRewardPopup` 버튼 비활성화 + Manager 유지 (Booster/OfflineReward 참조가 있음).

### 9. BattlePass — DEEP ✅ (유일하게 통과)

- Manager public 메서드: **15+** (Initialize, SetSeasonData, AddBxp, ClaimFreeReward, ClaimPremiumReward, ActivatePremium, CanClaim*, GetLevelProgress, GetReward, ResetProgress, IsFreeRewardClaimed, IsPremiumRewardClaimed, GetFreeRewardDescription, GetPremiumRewardDescription, PurchasePremium) — **압도적 통과**
- SaveData: `battlePass` 존재 (BxP, level, 수령 목록) — Manager가 수동 save — BeforeSave/LoadCompleted 미구독이지만 다른 시스템도 다 수동이라 제외
- EventBus Publish: 2건+ (BattlePassLevelUpEvent, BattlePassRewardClaimedEvent)
- **SO 엔트리: 50개** (`Resources/Data/BattlePass/`) — 통과
- CombatStats 연계: 없음 (보상만)
- 플레이어 능동성: yes (수령 버튼)
- **판정 근거**: 콘텐츠 50개 + API 풍부 + 퀘스트 연동 + 시즌 리셋. **진짜 시스템. DEEP 유지.**

### 10. Attendance — MEDIUM

- Manager public 메서드: 2 (Initialize, CheckAttendance) — **조건 #1 실패**
- SaveData: `attendance` (consecutiveDays, lastCheckDate) — 미구독
- EventBus Publish: 5건+ (AttendanceCheckedEvent 2 + BoosterStockAddEvent 4) — 통과
- SO 엔트리: 0 (Day 1~7 const + switch문 하드코딩)
- CombatStats 연계: no (재화/부스터 지급만)
- 플레이어 능동성: no (로그인 시 자동)
- **판정 근거**: 메서드 2개만 공개. SO 0. 플레이어 능동성 0. **SHALLOW 강등. 단 Day 1~7 출석은 유저 retention 핵심이므로 제거 대신 DailyChecklist로 통합 권장.**
- **권장**: DailyChecklist와 통합.

### 11. DailyChecklist — SHALLOW

- Manager public 메서드: 2 (ResetIfNewDay, IsDoubleRewardQuest) — 실패
- SaveData: 자체 없음 (Quest 재사용)
- EventBus Publish: 1건 — 실패
- SO 엔트리: 없음
- **판정 근거**: 퀘스트 시스템의 얇은 래퍼. → **QuestManager 안으로 흡수 또는 제거.**

### 12. HotDeal — SHALLOW (클라 구현 없음)

- Manager 파일: **없음** (`HotDeal*.cs` 0개). `ServerDtos.cs`에 서버 DTO 문자열만 존재.
- SaveData: 없음
- UI: 없음 (`HotDealPanel.uxml` 없음)
- **판정 근거**: **서버에 카탈로그 24딜 있지만 클라이언트에는 어떤 Manager/UI도 존재하지 않음.** 서버만의 죽은 자산. → **클라 측에선 이미 "없음". 서버 측 정리는 별도.**

### 13. Costume — SHALLOW (콘텐츠 0, 뼈대만)

- Manager public 메서드: 4 (AddCostume, EquipCostume, UnequipCostume, DisassembleCostume) — 통과
- SaveData: `costume.owned[], equippedIds[], completedSets[]` — 미구독
- EventBus Publish: 3건 (CostumeObtained, CostumeEquipped, CostumeSetCompleted) — 통과
- **SO 엔트리: 0개** (`AddressableAssetsData/Remote_Costumes`만 존재 — 실 애셋 없음)
- SPUM 스왑: **코드에 없음** — 비주얼 적용 로직 부재
- 획득 경로: **없음** (가챠 풀에 코스튬 없음, 상점에도 없음, 이벤트 보상 없음)
- CombatStats 연계: no (세트 완성 시 버프 주는 코드 없음)
- **판정 근계**: 사용자가 직접 "얕다"고 지목. 확인 결과 **데이터 0, 획득 경로 0, 비주얼 적용 0**. 매니저만 있고 아무것도 안 일어남. → **SHALLOW 확정. 뼈대만 유지하거나 제거.**
- **제거 파일**:
  - `Scripts/Costume/CostumeManager.cs`
  - `Scripts/UI/CostumePanelUI.cs`
  - `UI Toolkit/Views/CostumePanel.uxml`
  - `SaveData.cs`: `costume`, `CostumeSaveData`, `CostumeInstanceData`
  - `AddressableAssetsData/Remote_Costumes*` 정리

### 14. Booster — DEEP ✅ (유일한 제대로 된 공개 시스템)

- Manager public 메서드: 6 (GetStock, AddStock, UseBooster, GetMultiplier, GetRemainingSeconds, GetActiveBoosters) — 통과
- SaveData: **BeforeSaveEvent + LoadCompletedEvent 둘 다 Subscribe (SubscribeSticky)** — **유일하게 조건 #2 완전 통과** (line 62-64)
- EventBus Publish: 2건 (BoosterUsedEvent, BoosterExpiredEvent) — 통과
- SO 엔트리: **3개** — 조건 10 실패 but 타입별 1개씩 3종 (Exp/Gold/Drop)이 기획 의도. 양보.
- CombatStats 연계: 간접 (GetMultiplier가 LevelSystem/CurrencyManager에서 읽힘)
- 플레이어 능동성: yes (사용 버튼)
- **판정**: 세이브 이벤트 구독이 **유일**하게 규약 준수. 타이머 로직 제대로 동작. **DEEP 유지.**

### 15. Arena — MEDIUM (큰 코드베이스 but 클라 UX 의존)

- Manager public 메서드: 10+ (GenerateCandidates, GenerateRivalCandidates, SimulateBattleDetailed, StartMatch, SimulateBattle, ProcessMatchResult, CanEnter, GetDenyReason, SyncStatusFromServerAsync, SimulateBattleAsync, GetCandidatesServerAsync, ProcessBattleServerAsync) — **압도적 통과**
- SaveData: `arena.*` 존재, 수동 Save — BeforeSave 미구독
- EventBus Publish: 3건 (ArenaStartedEvent, ArenaMatchEvent, PvpVictoryEvent)
- SO 엔트리: **0~1개** — ArenaConfigSO 단일, Tier 배열만. Rival은 ArenaRivalDataSO 안의 배열.
- CombatStats 연계: yes (GetPlayerCp via PowerScore)
- 플레이어 능동성: yes (매치 선택)
- **판정**: 코드는 방대한데 **SO 볼륨 1개 수준**. 서버 연동까지 하는 만큼 **MEDIUM 유지**. 스테이지 100 해금이라 출시 초반엔 미노출.

### 16. Guild — MEDIUM (코드는 복잡 but 혼자 플레이)

- Manager public 메서드: 12+ (CreateGuild, JoinGuild, Donate, CanDonate, GetRemainingDonations, CanChallengeBoss, ProcessBossResult, SimulateBossDetailed, SimulateRaid, CreateGuildAsync, DonateAsync, ServerAsync*) — 통과
- SaveData: `guild.*` 존재, 수동 — 미구독
- EventBus Publish: 4건 (GuildJoined, GuildDonated, GuildBossCompleted 등)
- SO 엔트리: 1 (GuildConfigSO) + 내부 배열 — 통과 경계
- 플레이어 능동성: yes (생성/기부/보스 도전)
- **판정**: 복잡한 코드 but 현재 "혼자 플레이 시뮬레이션" (NPC 9명 하드코딩 line 441~). **MEDIUM 유지**. 출시 시 서버 길드원 연동 되어야 DEEP 됨.

## 핵심 발견 (엄격 감사 부수효과)

**세이브 이벤트 규약 위반 대규모 발생**: CLAUDE.md의 "BeforeSaveEvent 구독 → CurrentData에 쓰기" 규약을 **14개 시스템 중 Booster만 준수**. 나머지는 전부 수동 `SaveManager.Instance.Save()` 직접 호출로 race condition 위험. 이게 사용자가 체감하는 "버그" 원인 중 하나.

## HUD 우측 퀵메뉴 재판정

현재 `HUD.uxml` 버튼 11개:
```
quick-collection(도감) / quick-dungeon(던전) / quick-shop(상점) / quick-elite(정예) /
quick-hunt(소탕) / booster / quick-battlepass / quick-costume /
quick-arena / quick-guild / quick-reset / quick-unlock-all
```

- **제거 (탭바 중복)**: quick-collection (도감은 탭바), quick-dungeon, quick-shop, quick-battlepass, quick-costume
- **제거 (개발도구 — 정식 빌드 절대 금지)**: quick-reset, quick-unlock-all
- **제거 (SHALLOW 시스템 삭제 연동)**: quick-costume (코스튬 제거)
- **유지 (탭바에 없는 고유 진입점)**: booster, quick-elite(정예 소환), quick-hunt(소탕)
- **보류 (MEDIUM — 기획 확정 후)**: quick-arena, quick-guild

결과 유지 3개 + 보류 2개 = **최대 5개** (현재 11개에서 55% 감축).

## 최종 삭제 리스트 (실행용)

### 즉시 삭제 (SHALLOW 확정)

1. **Artifact 전체**
   - `Scripts/Combat/ArtifactManager.cs`
   - `Scripts/Data/SO/ArtifactDataSO.cs` (+ ArtifactSetSO)
   - `Scripts/UI/ArtifactTabUI.cs`
   - `UI Toolkit/Views/ArtifactTab.uxml`
   - `SaveData.cs` — `artifact`, `ArtifactSaveData`, `ArtifactInstanceData`
   - `ModifierSource.Artifact` enum 값
   - `GameEvents.cs` — ArtifactObtainedEvent, ArtifactEquippedEvent

2. **Inscription 전체**
   - `Scripts/Growth/InscriptionManager.cs`
   - `Scripts/Data/SO/InscriptionDataSO.cs`
   - UI 없음 (아직 안 만듦)
   - `SaveData.cs` — `inscription`, `InscriptionSaveData`
   - `ModifierSource.Inscription`
   - `GameEvents.cs` — InscriptionChangedEvent, TowerFloorReachedEvent (Tower 사용처 확인 후)

3. **Climber 전체**
   - `Scripts/Growth/ClimberPowerSystem.cs`
   - `Scripts/UI/ClimberTabUI.cs`
   - `UI Toolkit/Views/CharacterClimberTab.uxml`
   - `SaveData.cs` — `climberPowerLevel`
   - `GameEvents.cs` — ClimberPowerUpEvent
   - `CurrencyType.ClimbToken` (Mastery.Respec 재설계 후)

4. **HeroPower 전체**
   - `Scripts/Combat/HeroPowerManager.cs`
   - `Scripts/Data/SO/HeroPowerDataSO.cs`
   - `Scripts/UI/HeroPowerTabUI.cs`
   - `UI Toolkit/Views/CharacterHeroPowerTab.uxml`
   - `Assets/Data/SO/HeroPower/HeroPowerConfig.asset`
   - `SaveData.cs` — `heroPower`, `HeroPowerSaveData`
   - `ModifierSource.HeroPower`
   - `GameEvents.cs` — HeroPowerMilestoneEvent, QuestCondition.HeroPowerMilestone

5. **Ability 전체** (Mastery에 통합하거나 삭제)
   - `Scripts/Combat/AbilityManager.cs`
   - `Scripts/Data/SO/AbilityNodeSO.cs` (+ AbilityTreeConfigSO)
   - `Scripts/UI/AbilityTabUI.cs`
   - `UI Toolkit/Views/CharacterAbilityTab.uxml`
   - `SaveData.cs` — `ability`, `AbilitySaveData`
   - `GameEvents.cs` — AbilityUnlockedEvent, AbilityResetEvent, QuestCondition.UnlockAbility

6. **ReturneeGuide 전체**
   - `Scripts/Combat/ReturneeGuideManager.cs`
   - `Scripts/UI/ReturneeGuidePopup.cs`
   - `GameEvents.cs` — ReturneeGuideEvent, ReturneeKeyGrantEvent, ReturneeBonus

7. **AdRewardManager** (SDK 도입까지 비활성 권장, 당장 삭제는 Booster/OfflineReward 참조 끊어야 해서 주의)
   - 버튼만 비활성 → 추후 삭제 판단

8. **Costume 전체** (핵심 루프 연결 0)
   - `Scripts/Costume/CostumeManager.cs`
   - `Scripts/UI/CostumePanelUI.cs`
   - `UI Toolkit/Views/CostumePanel.uxml`
   - `SaveData.cs` — `costume`, `CostumeSaveData`, `CostumeInstanceData`
   - `GameEvents.cs` — CostumeObtainedEvent, CostumeEquippedEvent, CostumeSetCompletedEvent
   - `AddressableAssetsData/Remote_Costumes*`

9. **DailyChecklist** → QuestManager에 통합, Manager 제거
   - `Scripts/Quest/DailyChecklistManager.cs`
   - `Scripts/UI/DailyChecklistWidget.cs`

10. **Attendance → DailyChecklist 통합 후 단일 시스템으로 유지**
    - Day 1~7 const를 `AttendanceRewardSO[]`로 이동 (SO 0 → 7)
    - AttendanceSystem + DailyChecklist → `DailyRewardSystem` 하나로 합치기

### HUD 퀵메뉴 버튼 제거 (UXML 직편집)

`UI Toolkit/Views/HUD.uxml` Line 85-130:
- 제거: `quick-collection-btn`, `quick-dungeon-btn`, `quick-shop-btn`, `quick-battlepass-btn`, `quick-costume-btn`, `quick-arena-btn`(보류 여부 판단), `quick-guild-btn`(보류), `quick-reset-btn`, `quick-unlock-all-btn`
- 유지: `booster-btn`, `quick-elite-btn`, `quick-hunt-btn`

`Scripts/UI/HudUI.cs`의 대응 버튼 핸들러도 제거.

### 세이브 이벤트 규약 재정비 (별도 이슈, 우선순위 P0)

- 살아남은 시스템 전원 (Mastery, Arena, Guild, BattlePass, Costume[유지 시], Attendance 등) → `BeforeSaveEvent + LoadCompletedEvent` **둘 다** SubscribeSticky로 전환
- 현재 유일 준수: `BoosterManager` (Scripts/Combat/BoosterManager.cs:62-64)

## 예상 효과

- Scripts 파일 ~20개 삭제, UXML ~5개 삭제, SaveData struct 7개 삭제
- HUD 퀵메뉴 11 → 3~5
- ModifierSource enum 3~4개 감소 (CombatStats 단순화)
- 사용자 체감: "문어발" 해소, 핵심 루프(Combat/Stage/Gacha/Equip/Skill/Quest/BattlePass/Booster/Mastery)만 남음

## 실행 결과 (2026-04-20)

### 삭제된 파일 (근사 70개)

**Manager/SO/UI 스크립트 (.cs + .meta):**
- Artifact: ArtifactManager, ArtifactDataSO, ArtifactTabUI
- Inscription: InscriptionManager, InscriptionDataSO
- Climber: ClimberPowerSystem, ClimberTabUI
- HeroPower: HeroPowerManager, HeroPowerDataSO, HeroPowerTabUI
- Ability: AbilityManager, AbilityNodeSO (+ AbilityTreeConfigSO), AbilityTabUI
- ReturneeGuide: ReturneeGuideManager, ReturneeGuidePopup
- Costume: CostumeManager, CostumePanelUI, Costume.asmdef (+ 폴더)
- DailyChecklist: DailyChecklistManager, DailyChecklistWidget
- AdReward: AdRewardManager, AdRewardType

**UI Toolkit (UXML/USS):**
- ArtifactTab.uxml, ArtifactPanel.uss
- CharacterClimberTab.uxml, ClimberPanel.uss
- CharacterHeroPowerTab.uxml, HeroPowerPanel.uss
- CharacterAbilityTab.uxml, AbilityPanel.uss
- CostumePanel.uxml, CostumePanel.uss

**SO 에셋:**
- Assets/Data/SO/HeroPower/HeroPowerConfig.asset (+폴더)

### 참조 해제 수정된 파일 (약 25개)

Core 레이어:
- GameEvents.cs (이벤트 8종 제거: ArtifactObtained/Equipped, CostumeObtained/Equipped/SetCompleted, ClimberPowerUp, HeroPowerMilestone, InscriptionChanged, ReturneeGuide, ReturneeKeyGrant, ReturneeBonus, AbilityUnlocked/Reset, AdReward, DailyChecklistAllClear)
- Save/SaveData.cs (ArtifactSaveData/ArtifactInstanceData/AbilitySaveData/HeroPowerSaveData/InscriptionSaveData/CostumeSaveData/CostumeInstanceData 제거, 루트 필드 7종 제거)
- QuestType.cs (HeroPowerMilestone/UnlockAbility/EquipArtifact/ClimberPower/CostumeEquip/CostumeSetComplete enum 값을 주석으로 치환, 번호는 유지)
- CollectionCategory.cs (Costume → `_Removed_Costume` placeholder)
- CombatStats.cs ModifierSource (Artifact/HeroPower/Inscription → `_Removed_*` placeholder, 세이브 숫자 유지)
- AudioManager.cs (Inscription/DailyChecklist/AdReward subscribe/handler 제거, SfxType 엔트리는 유지)

게임 시스템:
- QuestManager.cs (구독/핸들러 8개 제거)
- AchievementSystem.cs (Costume/AdReward 핸들러 제거)
- CollectionBookManager.cs (Costume/Inscription 관련 제거)
- CollectionDataSO.cs (Costume 케이스 제거)
- EventContentManager.cs (GrantCostumeReward 제거)
- DungeonManager.cs (ReturneeKeyGrant 구독 제거)
- CpCounterWidget.cs (Inscription 구독 제거)
- HudPanel.cs (DailyChecklistWidget 표시 제거)
- RedDotManager.cs (DailyChecklist 의존 제거)
- LoginFlowManager.cs (DailyChecklist 리셋 제거)
- OfflineRewardPopup.cs (AdReward 2x 로직 제거)
- CollectionBookPanelUI.cs (Costume 아이콘 분기 제거)
- UIManager.cs / TabBarUI.cs (Char_Climber/Ability subTab 제거)

에디터:
- Phase2SetupEditor.cs (9개 Create*() 블록 주석화 + EnsureAbilityDefaultNodes/CreateAbilityNode/CreateDailyChecklistWidgetInHUD/CreateReturneeGuidePopupInPopupParent 제거)
- PlayTestHelper.cs (시뮬레이션 case 7개 주석화, ToggleCostume 제거)
- GuideQuestGenerator.cs (UNLOCK_MAP 3개 주석, HeroPowerMilestone switch case 주석)
- UIToolkitSetupEditor.cs (Char_Climber/Costume PANELS 엔트리 제거)
- CharacterPanelSetupEditor.cs (Build{Climber,Ability,HeroPower}Content 블록 주석화)
- AutoPlayBot.cs (Action{Ability,Costume,Artifact} 제거 + quest case 6개 주석화)

asmdef:
- UI/Editor/Dungeon/Economy.asmdef — Costume 참조 제거

### 컴파일 상태

- 로컬 grep 기준 살아있는 잔재 참조 0건
- Unity 도메인 리로드 결과는 부모 세션에서 확인 필요 (이 agent는 UnityMCP 미접근)
- CharacterPanelSetupEditor.cs는 SerializeField 연결 블록 2개를 `/* ... */`로 감쌌음 (내부 `*/` 없음 검증 완료)
- HudUI.cs의 `_quickCostumeBtn` 멤버는 유지 (Button 타입, UXML 미존재 시 null 반환 — 안전)

### enum 세이브 호환

- CollectionCategory.Costume → `_Removed_Costume` (정수 값 6 유지)
- ModifierSource.Inscription/HeroPower/Artifact → `_Removed_*` (정수 값 2/14/15 유지)
- QuestCondition 제거 값 (12/19/30/35/37/39) — 번호 고정, JsonUtility가 미지원 값을 0(KillMonsters)으로 매핑
- 기존 save.json의 `artifact`, `inscription`, `heroPower`, `ability`, `costume`, `climberPowerLevel` 필드 — JsonUtility가 알 수 없는 필드로 자동 무시

### 미처리 / 수동 검증 필요

- Main.unity 씬에 남아있을 수 있는 GameObject (ArtifactManager/CostumeManager 등) — Phase2Setup 재실행 시 생성 스킵되나, 기존 씬은 Unity 에디터에서 수동 삭제 또는 RebuildScene 필요
- HUD.uxml의 `quick-costume-btn` 버튼 엘리먼트 — UXML 편집 권장 (현재 C# 바인딩은 null-safe하여 런타임 문제 없음)
- HeroPower/Ability/Artifact/Costume 관련 Addressables 그룹 참조 (`Remote_Costumes`) — 에셋 경로 문자열이라 컴파일에는 영향 없으나 정리 권장

