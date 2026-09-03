---
read_count: 5
last_read: "2026-04-03"
status: reference
---

# mkLike 개발 가이드라인

## 1. 프로젝트 컨벤션

### 1.1 폴더 구조 (Assets/)
```
Assets/
  Scenes/
    Main.unity              <- 메인 씬 (UI + 전투 통합)
    Loading.unity           <- 로딩/스플래시
  Scripts/
    Core/                   <- 코어 시스템 (GameManager, SaveManager 등)
      Core.asmdef
    Data/                   <- ScriptableObject 정의
      Data.asmdef
    Combat/                 <- 전투 관련 (캐릭터, 몬스터, 데미지)
      Combat.asmdef
    Growth/                 <- 성장 시스템 (레벨, 스탯, 전직, 스킬)
      Growth.asmdef
    Equipment/              <- 장비 (장착, 강화, 스타포스)
      Equipment.asmdef
    Companion/              <- 동료 (소환, 장착, 강화)
      Companion.asmdef
    Gacha/                  <- 가챠 시스템
      Gacha.asmdef
    Dungeon/                <- 던전/부가 컨텐츠
      Dungeon.asmdef
    Economy/                <- 재화 관리
      Economy.asmdef
    Quest/                  <- 퀘스트/미션/업적
      Quest.asmdef
    UI/                     <- UI 공통 프레임워크
      UI.asmdef
    Utils/                  <- 유틸리티 (ObjectPool, Extensions 등)
      Utils.asmdef
  ScriptableObjects/        <- SO 인스턴스 (데이터 에셋)
    Characters/
    Monsters/
    Equipment/
    Companions/
    Skills/
    Stages/
    Relics/
  Prefabs/
    Characters/
    Monsters/
    Effects/
    UI/
  Art/
    Sprites/
      Characters/
      Monsters/
      Tilesets/
      UI/
    Animations/
  Audio/
    BGM/
    SFX/
```

### 1.2 네이밍 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| C# 클래스 | PascalCase | `GameManager`, `MonsterSpawner` |
| C# 메서드 | PascalCase | `TakeDamage()`, `OnLevelUp()` |
| C# 필드 (private) | _camelCase | `_currentHp`, `_attackPower` |
| C# 필드 (public/SerializeField) | camelCase | `maxHp`, `moveSpeed` |
| C# 상수/enum | UPPER_SNAKE / PascalCase | `MAX_LEVEL`, `ItemGrade.Epic` |
| ScriptableObject 파일 | PascalCase + 접미사 | `WarriorData.asset`, `Stage001Data.asset` |
| Prefab | PascalCase | `Slime.prefab`, `DamageText.prefab` |
| 씬 | PascalCase | `Main.unity`, `Loading.unity` |

### 1.3 코드 규칙

- **싱글톤**: 코어 매니저만 싱글톤 허용 (`GameManager`, `SaveManager`, `UIManager`)
- **이벤트 기반**: 시스템 간 통신은 C# event 또는 UnityEvent 사용, 직접 참조 최소화
- **ScriptableObject**: 게임 데이터는 SO로 정의, 하드코딩 금지
- **Input System**: `UnityEngine.InputSystem` API만 사용
- **주석**: 한국어 허용, UTF-8 인코딩 준수
- **Assembly Definition**: 각 시스템별 .asmdef로 분리하여 컴파일 속도 확보

### 1.4 Git 규칙

- `.meta` 파일은 항상 함께 커밋
- `Library/`, `Temp/`, `Logs/` 폴더는 .gitignore
- 커밋 메시지 형식: `<type>(<scope>): <subject>`
  - type: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`
  - scope: `core`, `combat`, `ui`, `equipment`, `companion`, `gacha`, `dungeon`, `economy`, `quest`

## 2. 탑다운 아레나 디자인 가이드

### 2.1 카메라 설정
- **2D 탑다운 카메라**: 캐릭터 추적, Damping으로 부드러운 이동
- **Confiner**: 아레나 경계 제한
- **화면 비율**: 모바일 세로 9:16 우선, 가로 16:9 대응
- **줌 레벨**: 고정 (다수 몬스터 가시성 확보)

### 2.2 이동/물리
- **키네마틱 이동**: transform.position 직접 변경 (물리 엔진 배제)
- **8방향 자유 이동**: 플레이어는 탑다운 평면에서 자유 이동
- **Rigidbody2D**: Kinematic (OverlapCircle 탐색용 콜라이더만)
- **자동 이동**: 방치형이므로 캐릭터가 자동으로 몬스터 추적/이동
- **몬스터 이동**: 스폰 후 플레이어를 향해 지속 접근
- **몬스터 분리**: 겹침 방지를 위한 separation force 적용

### 2.3 아레나 구조
```
+--------------------------------------------------+
|                                                    |
|  [스폰 영역 - 아레나 가장자리 360도]                 |
|                                                    |
|              [전투 영역]                            |
|         몬스터 ← → 플레이어                         |
|              [전투 영역]                            |
|                                                    |
|  [경계벽 - 보이지 않는 콜라이더]                     |
+--------------------------------------------------+
```

- 아레나는 직사각형, 챕터/스테이지마다 크기 조정 가능
- 몬스터는 아레나 가장자리(화면 밖)에서 스폰
- 플레이어 시작 위치: 아레나 중앙

### 2.4 레이어 구성
| 레이어 | Sorting Order | 용도 |
|--------|---------------|------|
| Background | -100 | 아레나 바닥/배경 |
| Entity | 10 | 캐릭터, 몬스터 |
| Effect | 20 | 이펙트, 파티클 |
| UI World | 30 | HP바, 데미지 텍스트 |
| UI Screen | 100 | 메인 UI (Canvas) |

### 2.5 애니메이션 상태
```
[Idle] <-> [Run] -> [Attack] -> [Idle]
                       ↓
                    [Skill] -> [Idle]
                       ↓
                     [Hit] -> [Idle]
                       ↓
                     [Die]
```
- 탑다운이므로 Jump/Fall/Land 상태 없음
- 이동 방향에 따라 스프라이트 Flip (좌우)

## 3. UI 설계 원칙

### 3.0 최우선 원칙: 시각적 피드백
> **버튼을 누르면 확실히 피드백이 와야 한다.**
> 키우기 게임의 핵심은 시각적 쾌감으로 유저를 끌어들이는 것.
> 시스템이 아무리 정교해도 눈에 보이지 않으면 없는 것이다.

**모든 유저 액션에 즉시 피드백**:
- **터치/클릭**: 버튼 scale punch (0.95→1.0) + SFX
- **구매/획득**: 아이템 아이콘 팝 + 파티클 + 숫자 카운트업 애니메이션
- **장착/강화**: CP 변화량 표시 (↑초록/↓빨강) + 화면 플래시 + 장비 글로우
- **레벨업/전직**: 전체 화면 이펙트 + 카메라 쉐이크 + 축하 파티클
- **가챠**: 카드 뒤집기 연출 + 등급별 배경 글로우 + 연출 길이 차등 (고등급일수록 긴 연출)
- **스킬 시전**: VFX + 화면 멈춤(히트스톱) + 카메라 줌
- **보스 처치**: 슬로우 모션 + 대형 데미지 텍스트 + 전리품 폭발

**피드백 없는 기능은 미완성으로 간주**:
- 시스템 코드 구현 ≠ 완료. 대응 UI + 이펙트 + SFX가 있어야 완료.
- 모든 태스크에 "시각적 검증" 항목 포함

**시각 요소 우선순위**:
1. 캐릭터 모션/이펙트 (유저가 가장 오래 보는 것)
2. 전투 VFX (스킬, 피격, 사망)
3. UI 인터랙션 피드백 (버튼, 팝업, 트랜지션)
4. 연출 (레벨업, 전직, 가챠, 보스 등장)
5. 배경/맵 (가장 후순위지만 없으면 허전함)

### 3.1 화면 구성
- **상단 HUD**: 층수, 전투력(CP), 레벨/EXP바, 재화(골드·루비)
- **중앙**: 전투 필드 (탑다운 아레나)
- **하단 탭바**: 주요 메뉴 (캐릭터/장비/던전/상점 — 4탭)
- **팝업**: 모든 상세 UI는 팝업 형태로 열림

### 3.2 UI 아키텍처
- **UIManager**: 팝업 스택 관리 (Open/Close/CloseAll)
- **BasePopup**: 모든 팝업의 부모 클래스 (Show/Hide 애니메이션)
- **TabController**: 하단 탭 전환 로직
- **바인딩**: 데이터 변경 → UI 자동 갱신 (Observer 패턴)

### 3.3 빨간 점 (알림 뱃지)
- 새로운 장비, 강화 가능, 미수령 보상 등에 빨간 점 표시
- 각 시스템이 자체적으로 알림 상태를 관리하고 UI에 전파

## 4. 데이터 설계 원칙

### 4.1 ScriptableObject 활용
- **정적 데이터**: 몬스터 스탯, 스킬 정보, 스테이지 구성, 드롭 테이블 → SO
- **동적 데이터**: 플레이어 진행도, 인벤토리, 재화 → JSON 직렬화 저장
- **밸런스 수치**: SO에 정의하여 Unity Inspector에서 직접 조정 가능

### 4.2 세이브 구조 (JSON)
```json
{
  "player": {
    "level": 1,
    "exp": 0,
    "statPoints": { "atk": 0, "def": 0, "hp": 0, "crit": 0 },
    "jobId": "warrior",
    "jobTier": 0
  },
  "inventory": {
    "equipment": [],
    "materials": {}
  },
  "currency": {
    "gold": 0,
    "diamond": 0,
    "stamina": 100
  },
  "progress": {
    "currentFloor": 1,
    "maxFloor": 1,
    "lastLoginTime": "2026-03-06T00:00:00Z"
  },
  "companions": [],
  "relics": [],
  "quests": {},
  "settings": {}
}
```

### 4.3 서버 연동 대비
- 세이브/로드를 인터페이스(`ISaveProvider`)로 추상화
- 프로토타입: `LocalSaveProvider` (JSON 파일)
- 추후: `ServerSaveProvider` (REST API)

## 5. 씬 셋업 자동화 원칙

### 5.1 핵심 규칙
- **씬에 배치해야 하는 오브젝트(매니저, UI, 프리팹 구조 등)는 반드시 에디터 스크립트로 자동 생성한다**
- 수동 배치는 휴먼 에러(잘못된 참조, 누락된 컴포넌트, 잘못된 설정값)를 유발하므로 금지
- 에디터 스크립트는 `Assets/Scripts/Editor/` 폴더에 배치한다

### 5.2 에디터 스크립트 규칙
- **메뉴 경로**: `MkLike > {카테고리} > {기능명}` (예: `MkLike > Scene Setup > Create All`)
- **멱등성**: 이미 존재하는 오브젝트는 건너뛰고, 중복 생성하지 않는다
- **SerializedObject**: Inspector 필드 연결은 `SerializedObject`/`FindProperty`로 자동 세팅
- **Undo 지원**: `Undo.RegisterCreatedObjectUndo`로 실행 취소 가능하게 한다
- **로그 출력**: 생성 결과를 Debug.Log로 알린다

### 5.3 대상 작업
| 작업 | 에디터 스크립트 | 수동 |
|------|:-:|:-:|
| 매니저 GameObject 배치 | O | X |
| UI Canvas/Panel/Button 구조 | O | X |
| 프리팹 계층 구조 조립 | O | X |
| SO 에셋 생성 (CreateAssetMenu) | - | O (Inspector) |
| 밸런스 수치 입력 | - | O (Inspector) |
| 아트 에셋 임포트 | - | O (드래그앤드롭) |

### 5.4 현재 에디터 스크립트 목록
| 파일 | 메뉴 | 기능 |
|------|------|------|
| `SceneClearEditor.cs` | MkLike > Clear Scene | 씬 오브젝트 전체 자동 삭제 |
| `FontSetupEditor.cs` | (내부) | 한글 TMP 폰트 에셋 생성 + TMP Fallback 등록 |
| `SceneSetupEditor.cs` | MkLike > Full Setup | 매니저 + UI + 아레나 + 전투 루프 전체 자동 생성 |
| `CameraSetupEditor.cs` | (내부) | 2D 추적 카메라 |
| `Phase2SetupEditor.cs` | (내부) | 전투 루프 (플레이어+몬스터+매니저+HUD) |

> Phase가 진행될 때마다 새로운 에디터 스크립트가 추가되며, 이 테이블을 갱신한다.

### 5.5 씬 재구성 절차
1. `MkLike > Clear Scene` (기존 오브젝트 삭제)
2. `MkLike > Full Setup` (매니저 + UI + 아레나 + 전투 루프)
3. **Play**

## 6. 성능 가이드

- **오브젝트 풀링**: 몬스터, 이펙트, 데미지 텍스트, 드롭 아이템
- **스프라이트 아틀라스**: UI 스프라이트, 캐릭터 스프라이트를 아틀라스로 묶기
- **GC 최소화**: Update()에서 할당 피하기, StringBuilder 활용
- **비동기 로딩**: Addressables 또는 Resources.LoadAsync 활용
- **대량 몬스터 최적화**: 동시 활성 몬스터 수 제한 (30~50), 화면 밖 몬스터 비활성화

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|-----------|------|
| 2026-03-06 | 초안 작성 | 프로젝트 시작 |
| 2026-03-07 | 에디터 스크립트 목록 갱신 | Phase 2 완료, Clear/Font 추가 |
| 2026-03-07 | 씬 재구성 절차 추가 | Clear → Setup 워크플로우 문서화 |
| 2026-03-08 | **사이드뷰 → 탑다운 전면 전환** | 플랫폼 제거, 아레나 구조, 8방향 이동, 에디터 메뉴 2개로 통합 |
