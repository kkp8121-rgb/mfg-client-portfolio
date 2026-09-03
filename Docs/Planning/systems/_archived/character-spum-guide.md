# SPUM 캐릭터 리소스 가이드 (디자이너용)

> 이 문서는 디자이너 에이전트가 SPUM 에셋을 활용하여 캐릭터를 제작할 때 참조하는 가이드입니다.

## 1. SPUM 개요

**SPUM (Soonsoon Pixel Unit Maker)** 은 2D 픽셀 캐릭터를 파츠 조합으로 생성하는 Unity 에셋이다.
각 파츠(머리, 눈, 의상, 갑옷, 무기 등)를 조합하여 다양한 캐릭터를 만들 수 있다.

### 위치
- 에셋 루트: `Assets/SPUM/`
- 에디터 씬: `Assets/SPUM/Scene/SPUM_Scene`
- 저장된 유닛: `Assets/Resources/SPUM/SPUM_Units/`

## 2. SPUM 제공 애니메이션

| 애니메이션 클립 | 파일명 | 용도 |
|----------------|--------|------|
| Idle | `0_idle.anim` | 대기 |
| Run | `1_Run.anim` | 이동 |
| Attack (Normal) | `2_Attack_Normal.anim` | 근접 공격 (전사) |
| Attack (Bow) | `2_Attack_Bow.anim` | 원거리 공격 (궁수) |
| Attack (Magic) | `2_Attack_Magic.anim` | 마법 공격 (마법사) |
| Debuff/Stun | `3_Debuff_Stun.anim` | 피격/스턴 |
| Death | `4_Death.anim` | 사망 |
| Skill (Normal) | `5_Skill_Normal.anim` | 근접 스킬 |
| Skill (Bow) | `5_Skill_Bow.anim` | 궁수 스킬 |
| Skill (Magic) | `5_Skill_Magic.anim` | 마법 스킬 |

> 직업별로 Attack/Skill 애니메이션이 이미 분리되어 있어, 전사/궁수/마법사 3직업에 바로 대응 가능하다.

## 3. SPUM 파츠 구조

### 커스터마이즈 가능한 파츠
| 카테고리 | 폴더 | 설명 |
|----------|------|------|
| Hair | `Items/0_Hair/` | 헤어스타일 |
| FaceHair | `Items/1_FaceHair/` | 수염 |
| Cloth | `Items/2_Cloth/` | 의상 (상의) |
| Pant | `Items/3_Pant/` | 하의 |
| Helmet | `Items/4_Helmet/` | 투구/모자 |
| Armor | `Items/5_Armor/` | 갑옷 |
| Weapons | `Items/6_Weapons/` | 무기 |
| Back | `Items/7_Back/` | 망토/날개 |

### 추가 패키지 (확장 파츠)
- `Packages/Ver121/` — 추가 Hair, Helmet, Weapons (WoodShield 등)
- `Packages/Ver300/` — 추가 Hair, Cloth, Weapons
- `Packages/F_SR/` — SR 등급 Helmet

### 종족 (Body)
- `BodySource/Species/0_Human/` — 인간
- `BodySource/Species/1_Elf/` — 엘프
- `BodySource/Species/2_Devil/` — 악마

### 무기 목록 (기본)
| 파일명 | 무기 종류 |
|--------|-----------|
| `Sword_1~6.png` | 검 (전사용) |
| `Bow_1.png` | 활 (궁수용) |
| `Ward_1.png` | 지팡이 (마법사용) |
| `Axe_1.png` | 도끼 |
| `Spear_1.png` | 창 |
| `Shield_1.png` | 방패 |

## 4. 샘플 프리팹 (이미 제공됨)

SPUM은 3개의 샘플 프리팹을 제공한다:

| 프리팹 | 경로 | 직업 매핑 | 전직 단계 |
|--------|------|-----------|-----------|
| `SwordMan.prefab` | `Prefab/AnimationSample/` | **전사** | 1차 전직 |
| `BowMan.prefab` | `Prefab/AnimationSample/` | **궁수** | 1차 전직 |
| `MagicianMan.prefab` | `Prefab/AnimationSample/` | **마법사** | 1차 전직 |

> 이 3개 프리팹은 1차 전직 캐릭터로 사용한다. 초기(0차) 및 2차·3차 전직 캐릭터는 SPUM 에디터에서 추가 제작 필요.

## 5. 핵심 스크립트 구조

### SPUM_Prefabs (캐릭터 프리팹의 루트 컴포넌트)
```
SPUM_Prefabs
  ├── _spriteOBj: SPUM_SpriteList  ← 파츠 스프라이트 참조
  ├── _anim: Animator               ← 애니메이터
  ├── _horse: bool                   ← 말 탑승 여부
  └── PlayAnimation(string name)    ← 이름으로 애니메이션 재생
```

### SPUM_SpriteList (파츠 스프라이트 관리)
```
SPUM_SpriteList
  ├── _hairList: List<SpriteRenderer>
  ├── _clothList: List<SpriteRenderer>
  ├── _armorList: List<SpriteRenderer>
  ├── _pantList: List<SpriteRenderer>
  ├── _weaponList: List<SpriteRenderer>
  ├── _backList: List<SpriteRenderer>
  ├── _eyeList: List<SpriteRenderer>
  └── _bodyList: List<SpriteRenderer>
```

### PlayerObj (샘플 캐릭터 컨트롤러)
```
PlayerObj
  ├── _prefabs: SPUM_Prefabs
  ├── _charMS: float (이동 속도)
  ├── CurrentState: PlayerState (idle/run/attack/death)
  └── SetMovePos(Vector2 pos)  ← 목표 위치로 이동
```

### 애니메이션 재생 방법
```csharp
// SPUM_Prefabs.PlayAnimation()은 클립 이름에 포함된 문자열로 매칭
spumPrefabs.PlayAnimation("idle");      // 0_idle
spumPrefabs.PlayAnimation("run");       // 1_Run
spumPrefabs.PlayAnimation("attack");    // 2_Attack_Normal
spumPrefabs.PlayAnimation("bow");       // 2_Attack_Bow
spumPrefabs.PlayAnimation("magic");     // 2_Attack_Magic
spumPrefabs.PlayAnimation("death");     // 4_Death
spumPrefabs.PlayAnimation("skill");     // 5_Skill_Normal
```

### 방향 전환
```csharp
// 오른쪽 이동 시 localScale.x = -1, 왼쪽 이동 시 localScale.x = 1
_prefabs.transform.localScale = new Vector3(-1, 1, 1); // 오른쪽
_prefabs.transform.localScale = new Vector3(1, 1, 1);  // 왼쪽 (기본)
```

## 6. 캐릭터 제작 워크플로우

### 방법 A: SPUM 에디터로 제작 (권장)
1. Unity에서 `Assets/SPUM/Scene/SPUM_Scene` 열기
2. Play 모드 진입
3. 파츠를 선택하여 캐릭터 커스터마이징
4. Save 버튼 → `Assets/Resources/SPUM/SPUM_Units/`에 프리팹 저장
5. 저장된 프리팹을 게임 씬에서 사용

### 방법 B: 기존 샘플 프리팹 활용
1. `Prefab/AnimationSample/SwordMan.prefab` 등을 복사
2. `SPUM_SpriteList`의 스프라이트를 교체하여 외형 변경
3. 게임 프리팹으로 사용

## 7. 직업별 캐릭터 제작 스펙

### 전사 (Warrior)
| 항목 | 설정 |
|------|------|
| 베이스 프리팹 | `SwordMan.prefab` 참조 |
| 무기 | Sword 계열 (`Sword_1~6.png`) + Shield 선택 |
| 공격 애니메이션 | `2_Attack_Normal`, `5_Skill_Normal` |
| 갑옷 | Armor 계열 (무거운 외형) |
| 색상 톤 | 붉은/갈색 계열 (힘, 용기 이미지) |

### 궁수 (Archer)
| 항목 | 설정 |
|------|------|
| 베이스 프리팹 | `BowMan.prefab` 참조 |
| 무기 | Bow 계열 (`Bow_1.png`) |
| 공격 애니메이션 | `2_Attack_Bow`, `5_Skill_Bow` |
| 갑옷 | Cloth 계열 (가벼운 외형) |
| 색상 톤 | 녹색/갈색 계열 (자연, 민첩 이미지) |

### 마법사 (Mage)
| 항목 | 설정 |
|------|------|
| 베이스 프리팹 | `MagicianMan.prefab` 참조 |
| 무기 | Ward 계열 (`Ward_1.png`, 지팡이) |
| 공격 애니메이션 | `2_Attack_Magic`, `5_Skill_Magic` |
| 갑옷 | Cloth 계열 (로브 외형) |
| 색상 톤 | 파란/보라 계열 (신비, 지성 이미지) |

## 8. 게임 연동 시 주의사항

1. **프리팹 구조**: SPUM 프리팹은 자식 오브젝트로 배치해야 한다
   ```
   [PlayerCharacter]         ← 게임 로직 (컨트롤러, Rigidbody2D, Collider2D)
     └── [SPUM_Unit]         ← SPUM_Prefabs 컴포넌트 (비주얼만 담당)
   ```

2. **Resources 폴더**: 저장된 유닛은 `Resources/SPUM/SPUM_Units/`에 있으므로 `Resources.Load`로 로드 가능

3. **애니메이션 호출**: `PlayAnimation(string)`은 부분 매칭이므로, 직업별로 정확한 키워드 사용 필요
   - 전사: `"attack"` → `2_Attack_Normal` 매칭
   - 궁수: `"bow"` → `2_Attack_Bow` 매칭
   - 마법사: `"magic"` → `2_Attack_Magic` 매칭

4. **몬스터에도 활용 가능**: SPUM으로 몬스터 유닛도 제작 가능 (악마 종족 등)

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|-----------|------|
| 2026-03-06 | 초안 작성 | SPUM 에셋 분석 완료, 디자이너 가이드 |
