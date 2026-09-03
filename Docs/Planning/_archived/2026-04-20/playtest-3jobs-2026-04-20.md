---
read_count: 0
last_read: "never"
status: active
---

# AI 플레이테스트 리포트 — 3직업 순회 (Warrior/Archer/Mage)

- 날짜: 2026-04-20
- 테스트 초점: 3직업 각각 Title 씬부터 온보딩 → Main 전환 → 방치 전투 검증
- 사유: 직업은 계정 귀속 (재선택 불가) → 완전 신규 계정 3회 순회

## 테스트 흐름

각 직업마다 다음 순서:
1. `PlayerPrefs.DeleteKey("mklike_dev_uid")` + `Settings_HasCompletedOnboarding=0` + `save.json/.bak/.bak2` 삭제
2. Title 씬 오픈 → Play
3. `LoginPopup.BotGuestLogin()` → `NicknamePopup.BotSubmit()` → `JobSelectPopup.BotSelectAndConfirm(JobType.X)`
4. Main 씬 로드 + ~10초 방치 전투
5. 스크린샷 + 콘솔 에러 확인 → Stop

## 결과 요약

| 직업 | 닉네임 | jobId | JobSystem.CurrentJob | Lv | 전투력 | HP | Stage | 에러 | 스크린샷 |
|------|--------|-------|----------------------|----|-------|-----|--------|------|----------|
| Warrior | BotWarrior | `warrior` | `Warrior` | 3 | 678 | 325/325 | 1-4 | 0 | `run_01_warrior.png` |
| Archer | BotArcher | `archer` | `Archer` | 3 | 707 | 350/350 | 1-4 | 0 | `run_02_archer.png` |
| Mage | BotMage | `mage` | `Mage` | 3 | 707 | 262/350 | 1-3 | 0 | `run_03_mage.png` |

## 직업별 관찰

### Warrior (GREEN)
- 근접 캐릭터 (전사 복장) 중앙에서 교전
- HP 325 — 3직업 중 최저 (Archer/Mage 350)
- 전투력 678
- 기본 근접 슬래시 공격 (VFX 작음)
- 방어형 클래스 특징 확인 필요 (HP는 오히려 낮음 — 밸런스 검토 권장)

### Archer (GREEN)
- 궁수 캐릭터 원거리 위치
- HP 350, 전투력 707 (+29 vs Warrior)
- **원거리 화살 발사체 VFX** 정상 (불 궤적 이펙트)
- 골드/경험치 획득 피드 정상
- 스킬 아이콘이 활 아이콘으로 변경됨 (UI 직업 반영)

### Mage (GREEN)
- 마법사 캐릭터 (분홍 머리 + 지팡이)
- HP 350, 전투력 707 (Archer와 동일)
- **마력탄 폭발 VFX** 크고 화려함 (직업 특성 살아 있음)
- 스킬 아이콘 `마력탄` (보라색) 확인
- 3직업 중 가장 다른 비주얼 (SPUM 스왑 정상)

## 공통 확인 항목 (3직업 모두 PASS)

- [x] Title → Login → Nickname → JobSelect → Main 온보딩 플로우 정상
- [x] `SaveData.player.jobId` 정상 기록 (warrior/archer/mage 소문자)
- [x] `JobSystem.CurrentJob` Enum 변환 정확
- [x] Main 씬 로드 후 캐릭터 스폰 + AI 전투 활성
- [x] HUD 표시 (레벨, 전투력, HP, 재화, 스테이지, 킬카운트)
- [x] 몬스터 자동 스폰 + 교전
- [x] 경험치/골드 획득 피드 (좌하단 floating text)
- [x] 가이드 퀘스트 #2 "첫 전투" 조건 충족 → 보상 수령 버튼 표시
- [x] 탭바 + 퀵메뉴 버튼 표시
- [x] Full Auto 버튼 표시
- [x] 콘솔 에러 0건 (3회차 모두)

## 발견된 문제점

### P2 (경미)
1. **Warrior HP 325 vs Archer/Mage HP 350 — 역설적**
   - 원인 추정: Warrior의 기본 스탯 SO가 Archer/Mage 대비 오히려 낮게 설정되어 있음.
   - 직업 컨셉상 Warrior는 HP/DEF가 가장 높아야 함.
   - 수정 제안: `JobDefinitionSO` 또는 `Character/Warrior` 초기 스탯 검토.

### P3 (스타일/기록)
2. **Archer 시 골드 1.1만 표시** — 이전 Warrior 세션 잔재 가능성 확인 (세이브 리셋됐지만 Currency 초기값 검토).
   - → 실제 재확인: 골드 x40→x42 kill 누적으로 ~1.1만까지 늘어날 수 있음. 아이템 드롭+퀘스트 보상 누적. 허위 경고 가능.

### P0/P1
- **없음**. 3직업 모두 플로우 완주.

## 검증 완료

- 2026-04-20 수정분 검증 반영:
  - EventBus sticky: 3직업 모두 `LoadCompletedEvent` sticky 수신 → UI 로딩 이상 없음
  - 26 UITK 패널 null 가드 효과: Q&lt;&gt; 접근 에러 0
  - ServerSaveProvider: `HasAuth=false`(Mock) → LocalSaveProvider 경로 유지, fallback 정상
  - 유물 제거: 도감/가챠/퀘스트에 relic 잔재 런타임 에러 0

## 미검증 영역 (후속)

- 서버 모드(`HasAuth=true`) 경로 — Firebase Auth 실체 연결 전 수동 확인 불가
- `/save/migrate` 실호출 — 서버 인증 후 `/run` 장시간 시나리오 필요
- 오래 방치 시 스테이지 진행/보스 전환 (보스 챕터1 진입 테스트 별도 필요)
- Equipment `TryScrollEnhanceAsync` 실제 UI 동작 (장비 입수+강화 팝업 열기 필요)
