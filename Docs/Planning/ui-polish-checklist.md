---
read_count: 3
last_read: "2026-04-03"
status: active
---

# UI 폴리싱 체크리스트 (R4)

> **기준**: audit_20260324_120535 스크린샷 18장 + NULL sprite 545개
> **원칙**: 라운드별 순차 진행, 각 항목 완료 후 체크

---

## R4-1: 공통 구조 (겹침 해소 + 배경 + 타이틀)

- [x] A-1. 패널 열릴 때 HUD 요소 숨김 — UIState.OnPanelStateChanged + HudPanel.SetHudElementsVisible()
- [x] A-2. "캐릭터"/"던전" 타이틀 겹침 제거 — ContentPanel TitleText/CloseButton 비활성화, TabRow 위로 이동
- [x] A-3. 패널 배경 불투명 보장 — A-1 HUD 숨김으로 비침 해소, EnsureOpaqueBackground 기존 동작 확인
- [x] A-4. 서브탭 전환 시 alpha 즉시 1 — CharacterPanel/DungeonPanel/ShopPanel 페이드인 제거

---

## R4-2: CharacterPanel — 스펫 탭

- [x] C-1. 스탯 행 8개 — FrameBackground 스프라이트 + 행 높이 48px + 황금색 이름
- [x] C-2. 버튼 크기 50/54px 확대 + "최대" 레이블 + 특수스탯은 버튼 없음
- [x] C-3. 일반/특수 능력치 구분선 "특수 능력치" 텍스트 세퍼레이터 추가
- [x] C-4. StatHeader/PointsRow에 FrameBackground 스프라이트 + 패딩 개선
- [x] C-5. 서브탭 "등반자의 힘"→"동반자" 축약 + 폰트 13px + spacing 2px

---

## R4-3: CharacterPanel — 장비 탭

- [x] D-1. 장비 슬롯 — 기존 ApplyFrameBackground 확인 (이미 적용됨)
- [x] D-2. 장비 팝업 임베디드 모드 — CharacterPanel 자식일 때 Canvas/오버레이 비활성화
- [x] D-3. TitleText/CloseButton 비활성화, ContentPanel 전체 영역 사용
- [x] D-4. BonusStats/ActionRow/InventoryScroll에 FrameBackground 스프라이트 적용

---

## R4-4: DungeonPanel

- [x] F-1. 던전 행 5개 — FrameBackground 스프라이트 + 교차 색상
- [x] F-2. 월드보스/보스레이드 — CreateSectionRow에 FrameBackground 자동 적용
- [x] F-3. 탑/챌린지 — 동일하게 CreateSectionRow 개선으로 자동 적용
- [x] F-4~6. ContentPanel TitleText/CloseBtn 비활성화, TabBar 위로 이동 (R4-1에서 처리)

---

## R4-5: ShopPanel

- [x] G-1. Panel_Shop 배경 → ApplyPanelBackground 런타임 적용
- [x] G-2. 서브탭 콘텐츠 배경 → ApplyFrameBackground 적용
- [x] G-3. 뽑기 버튼은 기존 ApplyButtonFull 확인 (이미 적용됨)
- [x] G-4. 하단 빈 공간 — 천장 진행도 텍스트(pityText) 이미 구현됨

---

## R4-6: HUD (전투 화면)

- [x] B-1. 전투 피드 엔트리 — FrameBackground 스프라이트 + 반투명 0.6 적용
- [x] B-2. 스킬 슬롯 — frame_silver 이미 적용됨 (빈 슬롯은 미학습 상태 정상)
- [x] B-3. TopSection/BottomBarBackground — FrameBackground 스프라이트 적용
- [x] B-4. 챌린지 알림 260x120으로 축소

---

## R4-7: 나머지 서브탭

- [x] E-1. 스킬 탭 — SkillListRoot VLG 확인 (데이터 없으면 빈 상태 정상)
- [x] E-2. 전직 탭 — JobCurrentText/NextLevel에 FrameBackground, 직업 버튼에 ApplyButtonFull
- [x] E-3. 유물 탭 — RelicEquipSlots 이미 ApplyFrameBackground 적용됨
- [x] E-4. 동반자의 힘 — ClimberSection FrameBackground 적용
- [x] E-5. 어빌리티 — 리롤 버튼 ApplyButtonFull 이미 적용됨

---

## R4-8: 팝업

- [x] H-1. CurrencyShortagePopup — 이벤트 기반 팝업이라 audit 캡처 불가 (정상)
- [x] H-2. ReturneeGuidePopup — ApplyPanelBackground 이미 적용, 콘텐츠는 텍스트 기반
- [x] H-3. BasePopup 전체 — ApplyPopupTheme()에서 자동 테마 적용 확인

---

## 진행 현황

| 라운드 | 항목 수 | 완료 | 상태 |
|--------|---------|------|------|
| R4-1 공통 구조 | 4 | 4 | 완료 |
| R4-2 스펫 탭 | 5 | 5 | 완료 |
| R4-3 장비 탭 | 4 | 4 | 완료 |
| R4-4 던전 | 6 | 6 | 완료 |
| R4-5 상점 | 4 | 4 | 완료 |
| R4-6 HUD | 4 | 4 | 완료 |
| R4-7 서브탭 | 5 | 5 | 완료 |
| R4-8 팝업 | 3 | 3 | 완료 |
| **합계** | **35** | **35** | **완료** |
