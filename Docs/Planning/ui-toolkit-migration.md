---
read_count: 5
last_read: "2026-04-03"
status: active
---

# UI Toolkit 전환 체크리스트

> **목표**: 모든 게임 UI를 UI Toolkit (UXML/USS)으로 전환
> **원칙**: 화면 단위로 전환, 각 화면마다 UXML + USS + C# 컨트롤러 3종 세트
> **참조**: CharacterStatTab이 첫 번째 완성 사례

---

## Phase 1: 캐릭터 패널 서브탭 (스탯 완료 → 나머지 6개)

- [x] P1-01. 스탯 탭 (CharacterStatTab) — 완료, 검증됨
- [x] P1-02. 장비 탭 — 12슬롯 그리드 + 인벤토리 + 보너스 스탯 (EquipmentTabUI.cs)
- [x] P1-03. 스킬 탭 — 학습된 스킬 목록 + 슬롯 4개 (SkillTabUI.cs)
- [x] P1-04. 전직 탭 — 현재 직업 + 전직 경로 + 직업 선택 카드 3개 (JobTabUI.cs)
- [x] P1-05. 유물 탭 — 장착 슬롯 3개 + 보유 유물 리스트 (RelicTabUI.cs)
- [x] P1-06. 동반자의 힘 탭 — 레벨/비용/슬롯 + 강화 버튼 (ClimberTabUI.cs)
- [x] P1-07. 어빌리티 탭 — 슬롯 리스트 + 리롤 버튼 (AbilityTabUI.cs)

---

## Phase 2: 던전 패널 서브탭 (5개)

- [x] P2-01. 성장 던전 탭 — 열쇠 + 던전 행 6개 (DungeonPanelUI.cs)
- [x] P2-02. 월드보스 탭 — 보스 정보 + 도전 + 결과 (DungeonPanelUI.cs)
- [x] P2-03. 보스 레이드 탭 — 난이도 3개 + 레이드 시작 (DungeonPanelUI.cs)
- [x] P2-04. 탑 탭 — 현재 층수 + 보상 + 도전/프레스티지 (TowerTabUI.cs)
- [x] P2-05. 챌린지 탭 — 챌린지 목록 + 진행도 (ChallengeTabUI.cs)

---

## Phase 3: 상점 패널 서브탭 (3개)

- [x] P3-01. 동료 소환 탭 — 천장 + 1회/10연차 + 결과 (ShopPanelUI.cs)
- [x] P3-02. 장비 소환 탭 — 동일 구조 (ShopPanelUI.cs)
- [x] P3-03. 무기 소환 탭 — 동일 구조 (ShopPanelUI.cs)

---

## Phase 4: HUD (전투 화면)

- [x] P4-01. 상단 바 — 킬카운트 + 스테이지 + HP바 + 골드/루비 (HudUI.cs)
- [x] P4-02. 하단 EXP 바 + 스킬 슬롯 4개 (HudUI.cs)
- [x] P4-03. 전투 피드 (HudUI.cs)
- [x] P4-04. 챌린지 알림 (HudUI.cs)
- [x] P4-05. 콤보 카운터 (HudUI.cs)

---

## Phase 5: 팝업/오버레이

- [x] P5-01. 오프라인 보상 팝업 (OfflineRewardPopupUI.cs)
- [x] P5-02. 재화 부족 팝업 (CurrencyShortagePopupUI.cs)
- [x] P5-03. 가챠 결과 팝업 (GachaResultPopupUI.cs)
- [x] P5-04. 장비 비교 팝업 (EquipComparePopupUI.cs)
- [x] P5-05. 아레나 결과 팝업 (ArenaResultPopupUI.cs)
- [x] P5-06. 길드 보스 결과 팝업 (GuildBossResultPopupUI.cs)
- [x] P5-07. 설정 패널 (SettingsPopupUI.cs)
- [x] P5-08. 비밀 방 팝업 (SecretRoomPopupUI.cs)

---

## Phase 6: 하단 탭 바 + 공용 컴포넌트

- [x] P6-01. 하단 탭 바 (TabBarUI.cs)
- [x] P6-02. 공용 팝업 베이스 — Common.uss (popup-dim/panel/title/close/confirm + grade 색상)
- [x] P6-03. 공용 토스트/알림 — Toast.uxml + ToastUI.cs (UniTask 페이드)

---

## Phase 7: 정리 + 기존 uGUI 제거

- [x] P7-01. uGUI 비활성화 — UIToolkitBootstrap + SceneTransitionManager 추가
- [x] P7-02. TabBarUI 서브탭 전환 — CharacterStatTabUI.SwitchTab() 구현
- [x] P7-03. HUD 레이아웃 — bottom-area 60px, combat-feed 투명 배경, 탭 바 아이콘/폰트 확대
- [x] P7-04. 전체 UI audit — UIAuditRunner UI Toolkit 모드 업데이트 완료

---

## 진행 현황

| Phase | 항목 수 | 완료 | 상태 |
|-------|---------|------|------|
| P1 캐릭터 패널 | 7 | 7 | 완료 |
| P2 던전 패널 | 5 | 5 | 완료 |
| P3 상점 패널 | 3 | 3 | 완료 |
| P4 HUD | 5 | 5 | 완료 |
| P5 팝업 | 8 | 8 | 완료 |
| P6 공용 | 3 | 3 | 완료 |
| P7 정리 | 4 | 4 | 완료 |
| **합계** | **35** | **35** | **완료** |

---

## 파일 구조

```
Assets/UI Toolkit/
├── Styles/
│   ├── CharacterPanel.uss       ✅
│   ├── DungeonPanel.uss
│   ├── ShopPanel.uss
│   ├── HUD.uss
│   ├── Popup.uss
│   └── Common.uss               (공용 변수/컴포넌트)
├── Views/
│   ├── CharacterStatTab.uxml    ✅
│   ├── CharacterEquipTab.uxml
│   ├── CharacterSkillTab.uxml
│   ├── CharacterJobTab.uxml
│   ├── CharacterRelicTab.uxml
│   ├── CharacterClimberTab.uxml
│   ├── CharacterAbilityTab.uxml
│   ├── DungeonGrowthTab.uxml
│   ├── DungeonBossTab.uxml
│   ├── DungeonRaidTab.uxml
│   ├── DungeonTowerTab.uxml
│   ├── DungeonChallengeTab.uxml
│   ├── ShopGachaTab.uxml
│   ├── HUD.uxml
│   ├── PopupOfflineReward.uxml
│   └── ...
└── GamePanelSettings.asset      ✅
```

## 작업 규칙

1. 각 화면마다 **UXML(구조) + USS(스타일) + C#(컨트롤러)** 3종 세트
2. USS는 가능하면 **공용 스타일(Common.uss)을 import**하여 일관성 유지
3. C# 컨트롤러는 기존 게임 시스템(EventBus, Manager)과 동일하게 연동
4. uGUI 코드는 전환 완료 후 Phase 7에서 일괄 제거
5. 각 항목 완료 후 Play 모드에서 시각적 확인
6. **아이콘이 필요하면** Iconify API에서 SVG 다운로드 → `Assets/Resources/Icons/` 저장 → USS에서 `background-image: resource("Icons/...")` 사용
