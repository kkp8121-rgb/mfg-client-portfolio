# MFG Client

> ⚠️ **중단된 개인 프로토타입 (2026-03 ~ 2026-04, 단독 개발).** 학습·포트폴리오 목적으로 공개합니다. 상용 서비스나 완성작이 아니며, 아래 "현재 상태 / 완성도" 절에 무엇이 동작하고 무엇이 미완인지 정리했습니다.

Unity 6000.3.10f1 기반 탑다운 방치형(idle) RPG 클라이언트. 모바일(세로 9:16) 타깃. 2026-03-06~04-24(약 7주) 동안 **173커밋 전량 단독 개발(solo)**.

- 자체 작성 C# 스크립트 **343개** (Assets/Scripts, 서드파티 에셋 제외)
- 서버 연동: [`mfg-server`](../mfg-server) (ASP.NET Core 10, 별도 레포)

---

## 스크린샷

아래는 캡처 대상 화면 목록입니다. 실제 이미지는 아직 첨부되지 않았습니다 — `docs/screenshots/` 에 파일을 넣고 경로를 채워주세요.

| # | 화면 | 캡처 상태 | 미리보기 |
|---|------|:---:|---|
| 1 | 메인 전투 화면 (HUD + 오토전투) | [ ] 미캡처 | `![](docs/screenshots/01-main-combat.png)` |
| 2 | 소환(가챠) 결과 연출 | [ ] 미캡처 | `![](docs/screenshots/02-gacha-result.png)` |
| 3 | 장비 강화 / 인벤토리 패널 | [ ] 미캡처 | `![](docs/screenshots/03-equipment.png)` |
| 4 | 보스전 패턴 UI (경고 → 강공격 → 무력화 타이머) | [ ] 미캡처 | `![](docs/screenshots/04-boss-pattern.png)` |
| 5 | 스테이지/던전 진입 화면 | [ ] 미캡처 | `![](docs/screenshots/05-stage-dungeon.png)` |
| 6 | 캐릭터 성장 (전직/스탯 분배) 패널 | [ ] 미캡처 | `![](docs/screenshots/06-growth.png)` |

---

## 현재 상태 / 완성도

내부 마라톤 플레이테스트 리포트(`Assets/playtest_marathon_*.md`, `Docs/Planning/issues-2026-04-23.md`)에 근거한 실측 상태입니다. 최종 커밋(2026-04-24) 시점 기준.

**작동 확인됨 (실 플레이 실측)**
- 3직업 중 Mage 잡 마라톤 350/350 완주, FeedbackBus 토스트 16개 지점 실제 발화 확인
- 던전 탭 진입/전투, 정예 소환, 조이스틱 기반 수동 이동, 스탯 분배 UI 동작 확인 (스크린샷 증거 존재)
- EventBus 기반 상태 이벤트(로그인/세이브 완료 등) 전파, 세이브 자동 마이그레이션(Local→Server) 동작 확인

**부분 동작 / 워크어라운드로 임시 봉합**
- Archer 116/350, Warrior 76/350에서 마라톤 중단 (시간 제약, 핵심 로직은 검증됨)
- timeScale 드리프트, Die 애니메이션 압축 등 P1급 이슈가 워크어라운드 상태로 남음

**미해결 버그 (개발 중단 시점 기준)**
- "침묵 버그 #5": Char_Skill / Char_Equip 탭 UI(UIDocument)가 `worldBound NaN`으로 렌더링되지 않음. 6가지 수정 시도 모두 실패, Unity 6 UIDocument 런타임 Panel attach 관련 근본 문제로 잠정 결론 — 미해결

**미착수 / 확인 필요**
- 실제 빌드 타깃(Android/WebGL 등)으로 최종 빌드·구동까지 검증됐는지 — `[소유자 확인]`
- 로드맵(`Docs/Planning/roadmap.md`) 기준 Phase 1~7은 "완료" 표기, **Phase 8(가이드 퀘스트 기반 컨텐츠 확장)** 진행 중 상태에서 개발 중단 — Phase 8 이후 항목 착수 여부 `[소유자 확인]`

---

## 핵심 시스템

각 시스템은 코드가 존재하고 위 "현재 상태" 절의 실측 범위 내에서 동작이 확인된 **프로토타입 수준** 구현입니다.

### 가챠 (Economy/GachaManager.cs, 743줄)
뽑기 누적 → "소환 레벨"업 → 고등급 확률 증가 방식의 자체 가챠 로직. 10연차 뽑기는 `tenPullGuaranteeGrade` 이상 등급을 마지막 1회에 보장. `GachaPoolSO` 데이터에 등급/가중치를 정의하고, 과거 존재했던 유물(Relic) 풀은 2026-04-20 커밋에서 완전 제거된 이력이 코드 주석에 남아 있음.

### 전투 (Combat/)
`StageManager`(648줄)가 챕터-스테이지 진행을 관리 — 웨이브당 100마리 처치 후 미니보스, 실패 시 무한 파밍 모드로 전환, 진행이 멈추면 스턱 복구 워치독이 개입. `MonsterController`(698줄)는 탑다운 추적 AI + separation force로 몬스터 간 겹침을 방지하며 `Lean.Pool` 기반 오브젝트 풀링(`IPoolable`)으로 동작. `BossPatternController`(1,118줄)는 Normal→Warning→StrongAttack→Stunned 4단계 패턴 사이클과 HP바/경고 텍스트/타이머 UI를 코드로 동적 생성.

### 스테이지 (Combat/StageManager.cs)
위 전투 시스템에 포함. 챕터당 9스테이지 + 보스 스테이지 구조, 챕터 진행에 따라 초반 킬 요구치가 점진적으로 증가하도록 설계.

### 장비 (Equipment/, 3개 스크립트 — 프로토타입 수준)
`EquipmentManager`/`WeaponManager`/`PotentialSystem`이 `EquipmentDataSO`/`WeaponDataSO` 데이터를 참조. 다른 시스템(Combat 66개, UI 93개) 대비 스크립트 수가 적어 상대적으로 얕게 구현된 영역으로 판단됨.

### 세이브 (Core/Save/)
`ISaveProvider` 인터페이스로 저장소를 추상화 — `LocalSaveProvider`(로컬 파일)와 `ServerSaveProvider`(서버 동기화)를 교체 가능. `SaveManager`는 `DefaultExecutionOrder(-10000)`으로 다른 시스템보다 먼저 초기화되며, 60초 주기 자동 저장 + 로그인 완료 시 Local→Server 전환과 1회 마이그레이션을 수행.

---

## 아키텍처

### 폴더 → 역할 (Assets/Scripts, 스크립트 수 기준 상위 10개 — 실제 clone 실측)

| 폴더 | 스크립트 수 | 역할 |
|---|---:|---|
| `Scripts/UI` | 93 | 화면·패널·팝업 UI 로직 (UI Toolkit 기반) |
| `Scripts/Combat` | 66 | 전투 루프 — 몬스터 AI, 스테이지 진행, 보스 패턴, 데미지/VFX |
| `Scripts/Editor` | 63 | 에디터 확장 도구 — 씬/패널 셋업 자동화, 데이터 생성기, 플레이테스트 봇 (런타임 미포함) |
| `Scripts/Core` | 43 | 게임 매니저, 세이브(`Save/`), 네트워크(`Net/`), 튜토리얼, 오디오 등 전역 시스템 |
| `Scripts/Data` | 35 | ScriptableObject 데이터 정의(`SO/`) + `DataManager` 로더 |
| `Scripts/Utils` | 8 | `EventBus`, 업데이트 루프, `BigNumber`(BreakInfinity) 등 공용 유틸 |
| `Scripts/Economy` | 8 | 가챠·재화·상점·IAP·이벤트 콘텐츠 |
| `Scripts/Quest` | 7 | 퀘스트·업적·출석·배틀패스·시즌샵 |
| `Scripts/Growth` | 6 | 전직(Job)·숙련도·스탯 분배·등반력 등 캐릭터 성장 |
| `Scripts/Dungeon` | 6 | 던전 전투·층별 랭킹·프레스티지 |

나머지: `Equipment`(3) / `Guild`(2) / `Arena`(2) / `Costume`(1). `Scripts/Gacha/` 폴더는 존재하나 `.cs` 파일이 0개(실제 가챠 로직은 `Economy/GachaManager.cs`에 있음) — 레거시로 남은 빈 폴더로 추정, `[소유자 확인]`.

`Scripts/Editor`(63개)는 런타임 빌드에 포함되지 않는 개발용 툴체인입니다. `AutoPlayBot.cs`/`BotInvariants.cs`(자동 플레이 봇 + 24종 불변조건 검증), 다수의 `*SetupEditor.cs`(씬/패널/캐릭터 자동 배치), `HierarchyDumper.cs`/`ScreenshotCapture.cs`(진단) 등으로 구성.

### EventBus · ISaveProvider · SO 데이터 주도 설계

```
┌──────────────┐   Publish<T>(evt)   ┌────────────────────┐
│ GameManager   │────────────────────▶│  EventBus<T>        │  (제네릭 정적 클래스,
│ StageManager  │◀────────────────────│  타입별 독립 구독자   │   Sticky 캐시 지원)
│ GachaManager  │  Subscribe<T>(fn)   │  목록 관리)           │
│ ... (다수)    │                     └────────────────────┘
└──────────────┘

┌───────────────┐    ISaveProvider (interface)     ┌───────────────────────┐
│ SaveManager    │───────────────────────────────▶ │ LocalSaveProvider       │ (기본, 로컬 파일)
│ (60초 자동저장, │  로그인 완료 시 1회 전환 +        │ ServerSaveProvider      │ (인증 성공 시 전환)
│  Execution     │  MigrateAsync()                  └───────────────────────┘
│  Order -10000) │
└───────────────┘

┌────────────────────┐   읽기 전용 참조   ┌─────────────────────────────────┐
│ GachaManager         │───────────────▶ │ GachaPoolSO / EquipmentDataSO /   │
│ StageManager         │                 │ MonsterDataSO / DungeonDataSO ... │  (Scripts/Data/SO, 약 30종
│ MonsterSpawner 등     │                 │ 데이터 인스턴스(.asset) 734개      │   Assets/Data 하위 실측)
└────────────────────┘                 └─────────────────────────────────┘
```

---

## 기술 스택 · 패키지 버전

| 항목 | 버전 |
|---|---|
| Unity Editor | 6000.3.10f1 |
| UniTask (Cysharp) | git 참조 (`Cysharp/UniTask`) |
| Addressables | 2.3.16 |
| Cinemachine | 3.1.6 |
| Input System | 1.18.0 |
| Universal Render Pipeline (URP) | 17.3.0 |
| 2D Animation | 13.0.4 |
| Timeline | 1.8.10 |
| Visual Effect Graph | 17.3.0 |

기타: DOTween/DOTweenPro(Demigiant), Lean.Pool(CW), TextMesh Pro — `Plugins/`, `Assets/Folder_Assets/` 하위 서드파티 에셋으로 포함.

---

## 빌드 · 실행 방법

1. Unity Hub에서 **6000.3.10f1** 설치
2. 리포지토리를 clone 후 Unity Hub에 프로젝트로 추가 → 최초 오픈 시 Addressables/패키지 자동 리졸브 대기
3. `Assets/Scenes/Main.unity`(또는 `Title.unity`) 열어 Play

> **서드파티 에셋 제거 안내** — 공개 전 보안·라이선스 검수(`mfg-publish-review.md`) 결과에 따라 라이선스상 재배포가 불가능한 아래 에셋 폴더를 이 미러에서 제거했습니다(유료 구매 전용 또는 재배포/재게시 금지 조항 확인):
>
> - `Assets/Folder_Assets/DAX`(유료 VFX 추정) · `Assets/Prefabs/VFX/Skills/DAX_*.prefab` 47개(동일 팩의 프리팹 정의)
> - `Assets/Folder_Assets/BGM`·`SFX`(AlkaKrab/PGS/TomMusic — 재배포 금지·허가 문구 부재)
> - `Assets/SPUM`, `Assets/Folder_Assets/SPUM`(SPUM Asset Store 유료 Standard License)
> - `Assets/Folder_Assets/500_skill_icons`(라이선스 파일 없음)
> - `Assets/Folder_Assets/Admurin's Pixel Items`(유료 추정, 라이선스 미동봉)
> - `Assets/GUI Kit The Stone`, `Assets/Folder_Assets/GUI Kit - Dark Geo`(유료 추정 시리즈)
> - `Assets/Folder_Assets/craftpix-net-*`(CraftPix Free 라이선스 — 원본 파일 재배포 명시적 금지)
> - `Assets/Folder_Assets/VFX_Packs`(Frostwindz/Gothicvania — 상업 제한 추정, CodeManu 하위 제외)
> - `Assets/Fonts/KoreanFont.ttf`(출처 미상 폰트 2건)
> - 그 외 위 자산에 종속된 `Assets/Resources/**` 하위 리소스, `Assets/Scenes/SpumBrowser.unity` 등
>
> 전체 목록과 사유는 `THIRD_PARTY_NOTICES.md`를 참고하세요. **이 저장소는 코드 열람용이며, 제거된 에셋을 재구매·재배치하지 않으면 그대로 빌드되지 않습니다.**

빌드 타깃(Android/WebGL 등) 및 실제 빌드 성공 여부는 `[소유자 확인]`.

---

## 직접 작성 범위 vs 서드파티 범위

**직접 작성**
- `Assets/Scripts/` 전체 (자체 C# 스크립트 343개)
- `Assets/Data/SO/` 데이터 정의 클래스 + `Assets/Data` 하위 데이터 인스턴스(.asset) 734개
- `Assets/Scenes/`의 자체 씬 5개(`Main`, `Main_backup_2026-04-20`, `SampleScene`, `SpumBrowser`, `Title`)
- `Docs/Planning/` 기획 문서 일체

**서드파티 (에셋 스토어/무료 에셋)**
- `Assets/Folder_Assets/`(BGM/SFX/아이콘/이펙트 팩 다수), `Assets/SPUM/`(캐릭터 스프라이트/애니메이션), `Assets/GUI Kit The Stone/`, `Assets/Joystick Pack/`, `Assets/TextMesh Pro/`, `Assets/Plugins/`(DOTween, Lean.Pool 등)
- 위 폴더들의 라이선스 및 공개 저장소 포함 가능 여부는 `mfg-publish-review.md` 검토 결과로 확정

---

## 서버 연동

`Core/Net/ApiClient.cs`가 UniTask 기반 HTTP 클라이언트로 서버와 통신 — Firebase ID Token을 `Authorization: Bearer` 헤더로 첨부하거나, 개발 환경에서는 `X-Dev-Uid` 헤더로 인증을 우회. 서버 쪽 구현은 별도 레포 [`mfg-server`](../mfg-server) 참고 (ASP.NET Core 10 + EF Core + MySQL 8).

---

## 로드맵 · 미완 항목

`Docs/Planning/roadmap.md` 기준:
- Phase 1~7 (전투 화면, 장비/무기/소환 탭, 캐릭터/스킬, 보스전, 가이드 퀘스트 기반 해금) — 문서상 "완료" 표기
- **Phase 8(가이드 퀘스트 기반 컨텐츠 확장)** 진행 중 상태에서 최종 커밋(2026-04-24) 발생 — 이후 진행 여부 `[소유자 확인]`
- "침묵 버그 #5"(Char_Skill/Char_Equip 탭 렌더링 실패)는 미해결 상태로 남음
- 위 "현재 상태 / 완성도" 절의 미착수·확인 필요 항목 참고

---

## 중단 사유 · 배운 점

`[소유자 작성]`

---

## 라이선스

리포지토리에 `LICENSE` 파일 없음. `[라이선스 미정 — 소유자 확인 필요 (예: All Rights Reserved / MIT 등)]`

---

## English Summary

**MFG Client** — a discontinued solo prototype (Mar–Apr 2026, ~7 weeks, 173 commits) of a top-down idle RPG client built in Unity 6000.3.10f1. Published for portfolio/learning purposes only; it is not a finished or production product. 343 self-written C# scripts across Combat, UI, Editor tooling, Core, and Data layers. Notable systems: a static generic `EventBus<T>` for decoupled cross-system messaging, an `ISaveProvider` abstraction swapping between local-file and server-backed saves (with a one-time migration on login), a gacha system (`GachaManager`, 743 lines) with a pull-accumulation "summon level" mechanic and a guaranteed-grade 10-pull, and a boss encounter controller cycling through Normal → Warning → StrongAttack → Stunned phases. Stack: UniTask, Addressables 2.3.16, Cinemachine 3.1.6, Input System 1.18.0, URP 17.3.0. Internal playtest logs show one job class (Mage) completed a full 350-stage marathon while two others stopped partway, and one UI rendering bug (Unity 6 UIDocument panel attach) remained unresolved at the time development stopped. Third-party art/audio/animation assets are excluded from the "self-authored" scope — see the section above.
