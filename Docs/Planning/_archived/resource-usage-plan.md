# 무료 에셋 활용 계획서

> 작성일: 2026-03-16
> 대상: `Assets/Folder_Assets/` 하위 다운로드 완료 에셋
> 목적: 직업별 스킬 VFX, 아이콘, UI, SFX, BGM을 게임 시스템에 매핑하고 구현 태스크를 분해한다.

---

## 1. VFX 매핑 테이블

### 1.1 전사 계열 (Warrior)

| 전직 단계 | 전직명 | Tier | 스킬 용도 | 에셋 소스 | 비고 |
|-----------|--------|------|-----------|-----------|------|
| 0차 | 견습 전사 | 0 | 기본 공격 슬래시 | `craftpix-net-825597/slash` + `slash2` | 단순 베기 모션 |
| 0차 | 견습 전사 | 0 | 기본 히트 이펙트 | `Warrior/Impacts/VFX1` (COLOR) | 물리 타격감 |
| 1차 | 나이트 | 1 | 강화 슬래시 | `Warrior/Warrior/VFX 1` + `VFX 2` | 검기 이펙트 |
| 1차 | 나이트 | 1 | 방어 스킬 | `Warrior/Impacts/VFX3` + `VFX4` (COLOR) | 쉴드 충격파 |
| 1차 | 나이트 | 1 | 차지 어택 | `Warrior/Warrior/VFX 3` | 돌진 이펙트 |
| 2차 | 워로드 | 2 | 워크라이 | `Warrior/Warrior/VFX 4` + `VFX 5` | 전장 버프 |
| 2차 | 워로드 | 2 | 범위 타격 | `Warrior/Paladin/VFX 1` + `VFX 2` | 빛 기둥 범위기 |
| 2차 | 워로드 | 2 | 강타 | `Warrior/Impacts/VFX5` + `VFX6` (COLOR) | 중타 이펙트 |
| 3차 | 타이탄 | 3 | 신성 강타 | `Warrior/Paladin/VFX 3` + `VFX 4` | 성스러운 빛 |
| 3차 | 타이탄 | 3 | 보호막 | `Warrior/Paladin/VFX5` | 광역 보호진 |
| 3차 | 타이탄 | 3 | 지진 | `Impacts_Explosions/Explosions/1` + `/2` | 지면 폭발 |
| 4차 | 드래곤 슬레이어 | 4 | 빙결 참격 | `Warrior/FrostKnight/VFX1` | 빙결 속성 베기 |
| 4차 | 드래곤 슬레이어 | 4 | 빙룡의 숨결 | `Warrior/FrostKnight/VFX2` + `VFX3` | 광역 빙결 브레스 |
| 4차 | 드래곤 슬레이어 | 4 | 궁극기 | `Warrior/FrostKnight/VFX3` + `Paladin/VFX 4` + `VFX5` | 빙결+신성 복합 |

### 1.2 궁수 계열 (Archer)

| 전직 단계 | 전직명 | Tier | 스킬 용도 | 에셋 소스 | 비고 |
|-----------|--------|------|-----------|-----------|------|
| 0차 | 견습 궁수 | 0 | 기본 사격 | `Archer/MagicArrows/1` | 단발 화살 궤적 |
| 0차 | 견습 궁수 | 0 | 히트 이펙트 | `Warrior/Impacts/VFX2` (COLOR) | 관통 타격 |
| 1차 | 스카우트 | 1 | 다중 사격 | `Archer/MagicArrows/2` + `/3` | 다발 화살 |
| 1차 | 스카우트 | 1 | 연막 | `Common/SmokeDust/VFX1` + `VFX2` | 회피 연막 |
| 1차 | 스카우트 | 1 | 독화살 | `Archer/Rogue/VFX1` | 독 부여 사격 |
| 2차 | 윈드워커 | 2 | 단검 난무 | `Archer/Rogue/VFX2` + `VFX3` | 근접 연타 |
| 2차 | 윈드워커 | 2 | 독안개 | `Archer/Rogue/VFX4` + `VFX5` | 독 장판 |
| 2차 | 윈드워커 | 2 | 은신 | `Common/SmokeDust/VFX3` + `VFX4` | 투명화 이펙트 |
| 3차 | 호크아이 | 3 | 번개 화살 | `Archer/Lightning/VFX1` + `VFX2` | 전기 속성 사격 |
| 3차 | 호크아이 | 3 | 체인 라이트닝 | `Archer/Lightning/VFX3` + `VFX4` | 연쇄 번개 |
| 3차 | 호크아이 | 3 | 라이트닝 스톰 | `Archer/Lightning/VFX5` + `VFX6` | 광역 전기장 |
| 4차 | 스톰브링어 | 4 | 화염 화살 | `Archer/Fireballs/FXpack13/Effect1` | 화염 투사체 |
| 4차 | 스톰브링어 | 4 | 화염 폭풍 | `Archer/Fireballs/FXpack13/Effect2` + `Effect3` | 광역 화염 |
| 4차 | 스톰브링어 | 4 | 궁극기: 폭풍의 심판 | `Archer/Lightning/VFX5` + `VFX6` + `Fireballs/Effect3` | 번개+화염 복합 |

### 1.3 마법사 계열 (Mage)

| 전직 단계 | 전직명 | Tier | 스킬 용도 | 에셋 소스 | 비고 |
|-----------|--------|------|-----------|-----------|------|
| 0차 | 견습 마법사 | 0 | 기본 매직 미사일 | `Common/Gothicvania/spritesheets` | 기본 마법 투사체 |
| 0차 | 견습 마법사 | 0 | 히트 이펙트 | `Impacts_Explosions/CodeManu/5_magickahit` | 마법 타격 |
| 1차 | 소서러 | 1 | 파이어볼 | `Mage/FireMage/VFX1` | 화염구 |
| 1차 | 소서러 | 1 | 화염 기둥 | `Mage/FireMage/VFX2` + `VFX3` | 화염 장판 |
| 1차 | 소서러 | 1 | 화염 오라 | `Impacts_Explosions/FireAura/1` + `/2` | 자기 버프 |
| 2차 | 세이지 | 2 | 별의 강타 | `Mage/Starcaller/VFX 1` | 성스러운 타격 |
| 2차 | 세이지 | 2 | 별의 비 | `Mage/Starcaller/VFX 2` + `VFX 3` | 광역 성스러운 비 |
| 2차 | 세이지 | 2 | 힐 | `Common/Priest/VFX 1` + `VFX 2` | 회복 이펙트 |
| 3차 | 룬마스터 | 3 | 마법 소용돌이 | `Impacts_Explosions/CodeManu/13_vortex` | 회전 마법진 |
| 3차 | 룬마스터 | 3 | 룬 폭발 | `Impacts_Explosions/CodeManu/1_magicspell` + `2_magic8` | 마법 폭발 |
| 3차 | 룬마스터 | 3 | 보호진 | `Impacts_Explosions/CodeManu/8_protectioncircle` | 방어 마법진 |
| 3차 | 룬마스터 | 3 | 빙결 마법 | `Impacts_Explosions/CodeManu/19_freezing` | 빙결 CC |
| 4차 | 아크메이지 | 4 | 사령술 | `Mage/Necromancer/VFX 1` + `VFX 2` | 암흑 마법 |
| 4차 | 아크메이지 | 4 | 암흑 저주 | `Mage/Warlock/VFX1` + `VFX2` + `VFX3` | 어둠 속성 |
| 4차 | 아크메이지 | 4 | 메테오 | `Mage/Necromancer/VFX 3` + `VFX 4` | 대규모 낙하 마법 |
| 4차 | 아크메이지 | 4 | 궁극기: 차원의 문 | `Mage/Starcaller/VFX 3` + `Necromancer/VFX 4` | 성+암 복합 |

### 1.4 공용 VFX

| 용도 | 에셋 소스 | 비고 |
|------|-----------|------|
| **물리 히트** | `Warrior/Impacts/VFX1~VFX7` (COLOR) | 7종 랜덤/등급별 사용 |
| **크리티컬 히트** | `Impacts_Explosions/XYEzawr_1/1` + `/2` + `/3` | 강화된 타격 이펙트 |
| **출혈 히트** | `Impacts_Explosions/Blood/NEw pack blood/1~5` | 물리 출혈 표현 |
| **소규모 출혈** | `Impacts_Explosions/MiniBlood/Polished/1~9` | 미니 히트 표현 |
| **폭발** | `Impacts_Explosions/Explosions/1` + `/2` + `/3` | 범위 폭발 |
| **캐스팅** | `Impacts_Explosions/CodeManu/4_casting` | 마법 시전 버프 |
| **화염 회전** | `Impacts_Explosions/CodeManu/7_firespin` | 회전 화염 |
| **화염 채찍** | `Impacts_Explosions/CodeManu/6_flamelash` | 화염 직선 공격 |
| **무기 히트** | `Impacts_Explosions/CodeManu/10_weaponhit` | 물리 무기 충돌 |
| **성화** | `Impacts_Explosions/CodeManu/16_sunburn` | 신성 속성 |
| **암흑 마법** | `Impacts_Explosions/CodeManu/18_midnight` | 암흑 속성 |
| **마법 버블** | `Impacts_Explosions/CodeManu/20_magicbubbles` | 물/독 속성 |
| **유령** | `Impacts_Explosions/CodeManu/14_phantom` | 소환/언데드 |
| **성운** | `Impacts_Explosions/CodeManu/12_nebula` | 우주 마법 |
| **충전** | `Impacts_Explosions/CodeManu/15_loading` | 차지 게이지 VFX |
| **저주** | `Impacts_Explosions/CodeManu/17_felspell` | 디버프 VFX |
| **푸른 화염** | `Impacts_Explosions/CodeManu/3_bluefire` | 마나 불꽃 |
| **밝은 화염** | `Impacts_Explosions/CodeManu/9_brightfire` | 버서크 화염 |
| **일반 화염** | `Impacts_Explosions/CodeManu/11_fire` | 범용 화염 |
| **슬라임 몬스터** | `Common/Slime/*` (idle/attack/damage/death/move) | 몬스터 애니메이션 |
| **화염 오라** (루프) | `Impacts_Explosions/FireAura/1~6` | 버프 오라/보스 오라 |
| **화염 갈래** | `Impacts_Explosions/ForksOfFlame/1~6` | 방사형 화염 |
| **슬래시 (카툰)** | `craftpix-net-501088/PNG/1~10` | 10종 만화풍 슬래시 |
| **슬래시 (스파인)** | `craftpix-net-825597/slash~slash10` | 10종 정교한 슬래시 |
| **범용 임팩트** | `Impacts_Explosions/XYEzawr_2a` + `XYEzawr_2b` | 다양한 히트 이펙트 |
| **미니 이펙트** | `Impacts_Explosions/MiniPack/1~9` | 소형 이펙트 9종 |
| **Fx Pack** | `Impacts_Explosions/Fx_pack` + `Fx_pack_#2` | 범용 이펙트 |

### 1.5 보스/던전 전용 VFX

| 용도 | 에셋 소스 | 비고 |
|------|-----------|------|
| **보스 사령술** | `Mage/Necromancer/VFX 1~4` | 보스 전용 암흑 공격 |
| **보스 흡혈** | `Common/Vampire/VFX1~5` | 5종 뱀파이어 스킬 |
| **보스 혈마법** | `Common/BloodMage/VFX1~3` | 3종 혈마법 (3파트 구성) |
| **보스 신성** | `Common/Priest/VFX 1~3` | 3종 성스러운 보스 패턴 |
| **보스 할로윈** | `Common/Halloween/VFX 1~7` | 7종 특수 이벤트/보스 패턴 |
| **보스 포탈** | `Common/Portal` | 보스 등장/소환 포탈 |
| **던전 포탈** | `Impacts_Explosions/GlitchPortals/V1 + V2` (128x128) | 차원문/던전 입장 이펙트 |
| **환경: 연기** | `Common/SmokeDust/VFX1~4` | 기믹 연기/안개 |
| **환경: 화염벽** | `Impacts_Explosions/FireAura/3~6` | 타워 화염 기믹 |
| **환경: 부식** | `Impacts_Explosions/CodeManu/17_felspell` | 부식 기믹 |
| **환경: 심연** | `Impacts_Explosions/CodeManu/18_midnight` | 심연 기믹 |
| **환경: 빙결** | `Impacts_Explosions/CodeManu/19_freezing` | 빙결 기믹 |
| **공용 마법 이펙트** | `Common/MagicEffects/1 Magic/*` | 범용 마법 VFX |

### 1.6 DAX 파티클 (별도 관리)

`Folder_Assets/DAX/Magic Packs Vol1/` 에 포함된 파티클 시스템 프리팹은 **3D 파티클 기반**이므로 2D 프로젝트에서 직접 사용이 어렵다. 추후 사용자 지시가 있을 때 별도로 2D 변환 작업을 진행한다.

---

## 2. 아이콘 매핑

### 2.1 500 Skill Icons (`500_skill_icons/`)

**494장** (배경 제거 버전: `Skill_nobg/`)

| 용도 | 할당 수량 | 선정 기준 |
|------|----------|-----------|
| 전사 스킬 아이콘 (5티어 x 4~5스킬) | ~25장 | 검/방패/갑옷 모티브 선별 |
| 궁수 스킬 아이콘 (5티어 x 4~5스킬) | ~25장 | 화살/활/번개/바람 모티브 |
| 마법사 스킬 아이콘 (5티어 x 4~5스킬) | ~25장 | 불/얼음/별/암흑 모티브 |
| 패시브 스킬 | ~30장 | 오라/룬/기호 모티브 |
| 버프/디버프 상태 아이콘 | ~20장 | 상태이상 표현 |
| 유물 아이콘 | ~30장 | 특수 아이템 모티브 |
| 어빌리티 아이콘 | ~20장 | 범용 능력 모티브 |
| 각인 아이콘 | ~15장 | 문양/문자 모티브 |
| 동료 스킬 | ~20장 | 동료별 고유 스킬 |
| 보너스/업그레이드 | Bonus 폴더 | 스킬 강화 표현 |

**네이밍 규칙**: `skill_{번호}_noBG.png` -> 스킬 ID와 매핑 테이블로 관리.
`SkillDataSO.icon` 필드에 직접 할당.

### 2.2 Paladin Skills (`Icons/PaladinSkills/`)

48종 32x32 아이콘 -- 전사 계열 스킬 슬롯 우선 배정.

| 용도 | 아이콘 | 비고 |
|------|--------|------|
| 전사 1차 스킬 | Icon1~Icon8 | 기본 전투 스킬 |
| 전사 2차 스킬 | Icon9~Icon16 | 중급 전투 스킬 |
| 전사 3차 스킬 | Icon17~Icon28 | 고급 전투 스킬 |
| 전사 4차 스킬 | Icon29~Icon36 | 최종 전투 스킬 |
| 전사 패시브 | Icon37~Icon48 | 패시브/버프 |

### 2.3 RPG 30 Icons (`Icons/RPG_30Icons/`)

30종 범용 아이콘 (무기/방어구/물약/보석/음식).

| 용도 | 비고 |
|------|------|
| 장비 슬롯 기본 아이콘 | 무기/방어구 |
| 소비 아이템 아이콘 | 물약/음식 |
| 재화 아이콘 보조 | 보석 |
| 가챠 결과 화면 아이템 표시 | 드롭 아이템 |

### 2.4 Skill Icon Set (`Icons/SkillIcons/`)

9종 스킬 아이콘 (여러 색상 스프라이트시트 포함).

| 용도 | 비고 |
|------|------|
| 공용 스킬 UI 테마 | 색상별 분류로 등급 표현 |
| 퀘스트/업적 아이콘 | 범용 목적 아이콘 |

### 2.5 Admurin's Pixel Items (`Admurin's Pixel Items/PixelItems/`)

| 카테고리 | 용도 |
|----------|------|
| `Armory/` (무기+방어구) | 장비 시스템 아이콘 -- EquipmentDataSO.icon |
| `Gems/` + `Gems II/` | 보석/강화석 재화 아이콘 -- CurrencyType 매핑 |
| `Potions/` | 버프/소비 아이템 아이콘 |
| `Rings/` | 악세서리 장비 아이콘 |
| `Skills/` | 추가 스킬 아이콘 (500_skill_icons 보충) |
| `General/` | 범용 UI 아이콘 |
| `Miscellaneous/` | 기타 UI 데코 |
| `Clothes/` | 코스튬/외형 시스템 (추후) |
| `Librarium/` | 도감/컬렉션 UI |
| `Emoticons/` | 캐릭터 감정 표현 |

---

## 3. UI 팩 매핑

### 3.1 RPG_UI (`UI_Packs/RPG_UI/`)

9종 테마별 UI 스킨. 게임의 메인 UI 프레임으로 사용.

| 테마 번호 | 용도 |
|-----------|------|
| `1/` | 메인 HUD 프레임 (HP/EXP 바, 미니맵 테두리) |
| `2/` | 팝업 다이얼로그 배경 |
| `3/` | 인벤토리/장비 패널 프레임 |
| `4/` (8종 서브) | 캐릭터 탭 7개 + 추가 패널 |
| `5/` | 던전 탭 UI |
| `6/` | 상점/가챠 탭 UI |
| `7/` | 설정/기타 패널 |
| `8/` | 보스/월드보스 전용 UI |
| `9/` | 결과 화면/보상 팝업 |

### 3.2 Cyberpunk_UI (`UI_Packs/Cyberpunk_UI/`)

미래풍 UI 팩. 아레나(PvP)/시즌 콘텐츠 전용 UI 테마로 활용.

### 3.3 GUI Kit - Dark Geo (`GUI Kit - Dark Geo/`)

다크 테마 UI 키트. 던전/타워 UI의 어두운 분위기 표현에 활용.

---

## 4. SFX 매핑

### 4.1 RPG_Essentials → AudioManager SfxType 매핑

#### 전투 SFX (`10_Battle_SFX/`)

| 파일 | SfxType | Resources 파일명 |
|------|---------|------------------|
| `03_Claw_03.wav` | MonsterHit | `sfx_monster_hit` |
| `08_Bite_04.wav` | MonsterHit (변형) | `sfx_monster_hit_02` |
| `15_Impact_flesh_02.wav` | SwordHit | `sfx_sword_hit` |
| `22_Slash_04.wav` | SwordSwing | `sfx_sword_swing` |
| `35_Miss_Evade_02.wav` | (신규) Miss | `sfx_miss` |
| `39_Block_03.wav` | (신규) Block | `sfx_block` |
| `51_Flee_02.wav` | (신규) Flee | `sfx_flee` |
| `55_Encounter_02.wav` | BossRoar | `sfx_boss_roar` |
| `69_Enemy_death_01.wav` | MonsterDie | `sfx_monster_die` |
| `77_flesh_02.wav` | CritHit | `sfx_crit_hit` |

#### UI/메뉴 SFX (`10_UI_Menu_SFX/`)

| 파일 | SfxType | Resources 파일명 |
|------|---------|------------------|
| `001_Hover_01.wav` | UiTap | `sfx_ui_tap` |
| `013_Confirm_03.wav` | UiConfirm | `sfx_ui_confirm` |
| `029_Decline_09.wav` | UiCancel | `sfx_ui_cancel` |
| `033_Denied_03.wav` | UiError | `sfx_ui_error` |
| `051_use_item_01.wav` | (신규) UseItem | `sfx_use_item` |
| `070_Equip_10.wav` | Equip | `sfx_equip` |
| `071_Unequip_01.wav` | Unequip | `sfx_unequip` |
| `079_Buy_sell_01.wav` | GoldPickup | `sfx_gold_pickup` |
| `092_Pause_04.wav` | UiPopupOpen | `sfx_ui_popup_open` |
| `098_Unpause_04.wav` | UiPopupClose | `sfx_ui_popup_close` |

#### 이동 SFX (`12_Player_Movement_SFX/`)

| 파일 | 용도 | Resources 파일명 |
|------|------|------------------|
| `56_Attack_03.wav` | 근접 공격 보조 | `sfx_melee_attack` |
| `61_Hit_03.wav` | 일반 히트 보조 | `sfx_hit` |
| `88_Teleport_02.wav` | 포탈/텔레포트 | `sfx_teleport` |
| `30_Jump_03.wav` | 점프/회피 | `sfx_dodge` |

#### 마법 SFX (`8_Atk_Magic_SFX/`)

| 파일 | SfxType | Resources 파일명 |
|------|---------|------------------|
| `04_Fire_explosion_04_medium.wav` | SkillFire | `sfx_skill_fire` |
| `13_Ice_explosion_01.wav` | SkillIce | `sfx_skill_ice` |
| `18_Thunder_02.wav` | SkillLightning | `sfx_skill_lightning` |
| `22_Water_02.wav` | (신규) SkillWater | `sfx_skill_water` |
| `25_Wind_01.wav` | (신규) SkillWind | `sfx_skill_wind` |
| `30_Earth_02.wav` | SkillMeteor | `sfx_skill_meteor` |
| `45_Charge_05.wav` | SkillCharge | `sfx_skill_charge` |
| `46_Poison_01.wav` | (신규) SkillPoison | `sfx_skill_poison` |

#### 버프/힐 SFX (`8_Buffs_Heals_SFX/`)

| 파일 | SfxType | Resources 파일명 |
|------|---------|------------------|
| `02_Heal_02.wav` | (신규) Heal | `sfx_heal` |
| `16_Atk_buff_04.wav` | SkillBuffActivate | `sfx_skill_buff_activate` |
| `17_Def_buff_01.wav` | (신규) DefBuff | `sfx_def_buff` |
| `21_Debuff_01.wav` | (신규) Debuff | `sfx_debuff` |
| `30_Revive_03.wav` | (신규) Revive | `sfx_revive` |
| `39_Absorb_04.wav` | (신규) Absorb | `sfx_absorb` |
| `44_Sleep_01.wav` | (신규) Sleep | `sfx_sleep` |
| `48_Speed_up_02.wav` | (신규) SpeedUp | `sfx_speed_up` |

### 4.2 Fantasy_200 → 보충 SFX

#### 전투 공격 (`SFX/Attacks/`)

| 파일 | 용도 | Resources 파일명 |
|------|------|------------------|
| `Sword Attack 1~3.ogg` | 전사 기본공격 변형 | `sfx_sword_swing_01~03` |
| `Sword Impact Hit 1~3.ogg` | 전사 히트 변형 | `sfx_sword_hit_01~03` |
| `Sword Blocked 1~3.ogg` | 방어 변형 | `sfx_block_01~03` |
| `Sword Parry 1~3.ogg` | 패리 | `sfx_parry_01~03` |
| `Sword Sheath/Unsheath.ogg` | 장비 장착 연출 | `sfx_equip_sword` |
| `Bow Attack 1~2.ogg` | 궁수 기본공격 | `sfx_bow_release_01~02` |
| `Bow Impact Hit 1~3.ogg` | 궁수 히트 | `sfx_arrow_hit_01~03` |
| `Bow Blocked 1~3.ogg` | 궁수 방어 | `sfx_bow_block_01~03` |

#### 마법 (`SFX/Spells/`)

| 파일 | 용도 | Resources 파일명 |
|------|------|------------------|
| `Fireball 1~3.ogg` | 파이어볼 변형 | `sfx_fireball_01~03` |
| `Firebuff 1~2.ogg` | 화염 버프 | `sfx_firebuff_01~02` |
| `Firespray 1~2.ogg` | 화염 방사 | `sfx_firespray_01~02` |
| `Ice Barrage 1~2.ogg` | 빙결 공격 | `sfx_ice_barrage_01~02` |
| `Ice Freeze 1~2.ogg` | 빙결 CC | `sfx_ice_freeze_01~02` |
| `Ice Throw 1~2.ogg` | 빙결 투사체 | `sfx_ice_throw_01~02` |
| `Ice Wall 1~2.ogg` | 얼음벽 | `sfx_ice_wall_01~02` |
| `Rock Meteor Swarm 1~2.ogg` | 메테오 | `sfx_meteor_01~02` |
| `Rock Meteor Throw 1~2.ogg` | 바위 투척 | `sfx_rock_throw_01~02` |
| `Rock Wall 1~2.ogg` | 바위벽 | `sfx_rock_wall_01~02` |
| `Spell Impact 1~3.ogg` | 마법 충돌 | `sfx_magic_hit_01~03` |
| `Wave Attack 1~2.ogg` | 파동 공격 | `sfx_wave_01~02` |
| `Waterspray 1~2.ogg` | 물 마법 | `sfx_water_01~02` |

#### 환경 루프 (`BGS Loops/`)

| 폴더 | 용도 |
|------|------|
| `Cave/` (3종) | 챕터 4~6 동굴 배경음 |
| `Forest Day/` (3종) | 챕터 1~3 숲 배경음 |
| `Forest Night/` (3종) | 심연 챕터 야간 분위기 |
| `Beach/Sea/` (6종) | 해변 던전/특수 스테이지 |
| `Interior Day/Night/` (6종) | 실내 던전/상점 |

#### 기타 (`SFX/Doors Gates and Chests/`)

| 파일 | 용도 | Resources 파일명 |
|------|------|------------------|
| `Chest Open 1~2.ogg` | 가챠/보상함 열기 | `sfx_chest_open_01~02` |
| `Chest Close 1~2.ogg` | 보상함 닫기 | `sfx_chest_close_01~02` |
| `Door Open/Close.ogg` | 던전 입장/퇴장 | `sfx_door_open/close` |
| `Gate Open/Close.ogg` | 보스방 게이트 | `sfx_gate_open/close` |
| `Lock Unlock.ogg` | 잠금 해제 | `sfx_unlock` |

---

## 5. BGM 매핑

### 5.1 BgmType별 에셋 할당

| BgmType | 에셋 파일 | 루프 | 비고 |
|---------|-----------|------|------|
| `Lobby` | `Feather_Falling/.../5 A Safe Space LOOP TomMusic.ogg` | O | 평화로운 로비 |
| `ChapterForest` | `Feather_Falling/.../1 Exploration LOOP TomMusic.ogg` | O | 숲 챕터 1~3 |
| `ChapterCave` | `Fantasy_RPG/.../Dungeon-Exploration Music 1.ogg` | O | 동굴 챕터 4~6 |
| `ChapterVolcano` | `Feather_Falling/.../4 Battle Track LOOP TomMusic.ogg` | O | 화산 챕터 7~9 |
| `ChapterSky` | `Feather_Falling/.../3 A Magic Forest LOOP TomMusic.ogg` | O | 하늘 챕터 10~12 |
| `ChapterAbyss` | `Dark_Dungeon/dark dungeon.mp3` | O | 심연 챕터 13+ |
| `Boss` | `Boss_Battle/Loops/Ogg/1. Abyssal Tyrant (Loop).ogg` | O | 일반 보스전 |
| `Dungeon` | `Feather_Falling/.../2 Journey LOOP TomMusic.ogg` | O | 특수 던전 |
| `Gacha` | `Fantasy_RPG/.../Event Music 1.ogg` | O | 가챠/상점 |
| `WorldBoss` | `Boss_Battle/Loops/Ogg/6. Dread Requiem (Loop).ogg` | O | 월드보스 |

### 5.2 추가 BGM 활용 (신규 BgmType 후보)

| 신규 BgmType | 에셋 파일 | 용도 |
|--------------|-----------|------|
| `BossRaid` | `Boss_Battle/Loops/Ogg/2. Soulrend Sovereign (Loop).ogg` | 보스 레이드 |
| `Arena` | `JRPG_Battle/.../Battle-Furious-Gt_loop.ogg` | 아레나 PvP |
| `Tower` | `Boss_Battle/Loops/Ogg/3. Veil of the Forsaken (Loop).ogg` | 무한의 탑 |
| `GuildBoss` | `Boss_Battle/Loops/Ogg/8. Bloodbound Fight (Loop).ogg` | 길드보스 |
| `Event` | `Fantasy_RPG/.../Event Music 2~4.ogg` | 이벤트 던전 |
| `Town` | `Fantasy_RPG/.../Town-Village Theme 1~3.ogg` | 마을/로비 변형 |
| `BattleIntense` | `JRPG_Battle/.../Battle-SAMURAI_loop.ogg` | 고난도 전투 |
| `BattleEpic` | `JRPG_Battle/.../Battle-SilverMoon_loop.ogg` | 특수 보스 |
| `BattleSad` | `JRPG_Battle/.../Battle-Grief_loop.ogg` | 슬픈 보스전 |
| `Prestige` | `JRPG_Battle/.../Battle-Dawn_loop.ogg` | 환생 연출 |

### 5.3 JRPG Battle 팩 (인트로+루프 구성)

`Assets_for_Unity/` 폴더에 **intro + loop** 분리 파일 제공. Unity AudioSource에서 인트로 재생 후 루프로 자동 전환하는 방식 적용 가능.

| 곡명 | 인트로 | 루프 | 추천 용도 |
|------|--------|------|-----------|
| Furious Battle | `Battle-Furious_intro.ogg` | `Battle-Furious-Gt_loop.ogg` | 아레나 PvP |
| Conflict | `Battle-Conflict_intrto.ogg` | `Battle-Conflict_loop.ogg` | 길드전 |
| Grief of Souls | `Battle-Grief_intro.ogg` | `Battle-Grief_loop.ogg` | 비극적 보스 |
| Queen of the White Dawn | `Battle-Dawn_intro.ogg` | `Battle-Dawn_loop.ogg` | 환생 보스 |
| Holy Rapier | `Battle-rapier_intro.ogg` | `Battle-rapier_loop.ogg` | 기사 보스 |
| Silver Moon | `Battle-SilverMoon_intro.ogg` | `Battle-SilverMoon_loop.ogg` | 최종 보스 |
| Vampire Prayer | `Battle-Vampire_intro.ogg` | `Battle-Vampire_loop.ogg` | 뱀파이어 보스 |
| SAMURAI BLADE | `Battle-SAMURAI_intro.ogg` | `Battle-SAMURAI_loop.ogg` | 사무라이 보스 |
| Forbiddens Saga | `Battle-Forbidden_SNES_intro.ogg` | `Battle-Forbidden_SNES_loop.ogg` | 레트로 이벤트 |
| 8bit Battle | `8bit-Battle01_intro.ogg` | `8bit-Battle01_loop.ogg` | 미니게임/이벤트 |

### 5.4 Boss_Battle Advanced (풀트랙 8곡)

고퀄리티 보스 BGM. 보스별 고유 BGM으로 배분.

| 곡명 | 용도 |
|------|------|
| Abyssal Tyrant | 일반 보스 (기본) |
| Soulrend Sovereign | 보스 레이드 |
| Veil of the Forsaken | 무한의 탑 보스 |
| Ruinlord Ascendant | 챕터 최종 보스 |
| The Unseen Monarch | 숨겨진 보스 |
| Dread Requiem | 월드보스 |
| Twilight of the Harbinger | 4차 전직 보스 |
| Bloodbound Fight | 길드보스 |

---

## 6. SO 생성 계획

### 6.1 SpriteSheetVfxSO 자동 생성

에디터 스크립트로 `Folder_Assets/VFX_Packs/` 하위 스프라이트 시트를 자동 감지하여 SO를 생성한다.

**자동 생성 범위**:
- `VFX_Packs/Warrior/**/*.png` (sprite-sheet 폴더 내)
- `VFX_Packs/Archer/**/*.png`
- `VFX_Packs/Mage/**/*.png`
- `VFX_Packs/Common/**/*.png` (spritesheets 폴더 내)
- `VFX_Packs/Impacts_Explosions/CodeManu/*.png` (20종 스프라이트시트)
- `craftpix-net-*/` (슬래시 이펙트)

**네이밍 규칙**:
```
VFX_{직업}_{팩이름}_{번호}.asset

예시:
VFX_Warrior_Warrior_01.asset
VFX_Warrior_Paladin_03.asset
VFX_Archer_Lightning_02.asset
VFX_Mage_Necromancer_04.asset
VFX_Common_SmokeDust_01.asset
VFX_Impact_CodeManu_05_MagickaHit.asset
VFX_Slash_Craftpix_01.asset
```

**SO 저장 경로**: `Assets/Resources/VFX/` (하위 폴더별)
```
Assets/Resources/VFX/Warrior/
Assets/Resources/VFX/Archer/
Assets/Resources/VFX/Mage/
Assets/Resources/VFX/Common/
Assets/Resources/VFX/Impact/
Assets/Resources/VFX/Slash/
Assets/Resources/VFX/Boss/
Assets/Resources/VFX/Environment/
```

### 6.2 자동 생성 에디터 스크립트 사양

```
에디터 메뉴: MkLike > Asset Setup > Generate VFX SOs
```

처리 로직:
1. `Folder_Assets/VFX_Packs/` 하위 모든 `sprite-sheet/`, `spritesheets/`, `Sprite-sheet/` 폴더 스캔
2. 각 PNG 파일에 대해:
   - 이미 Texture Import Settings에서 Multiple 모드로 슬라이스되었는지 확인
   - 미슬라이스 시: Auto Slice 적용 (Grid by Cell Count 또는 Automatic)
   - 슬라이스된 Sprite[] 추출
3. `SpriteSheetVfxSO` 생성:
   - `frames` = 슬라이스된 Sprite 배열 (순서 정렬)
   - `fps` = 12 (기본) / 히트 이펙트는 24
   - `loop` = false (기본) / 오라/버프는 true
   - `additive` = true (발광 이펙트) / false (일반)
   - `defaultScale` = 1.0
4. SO를 `Assets/Resources/VFX/` 하위에 저장

### 6.3 오디오 클립 복사 스크립트 사양

```
에디터 메뉴: MkLike > Asset Setup > Setup Audio Resources
```

처리 로직:
1. `Folder_Assets/SFX/` 및 `Folder_Assets/BGM/` 스캔
2. 매핑 테이블(4장)에 따라 파일을 `Assets/Resources/Audio/` 하위로 **복사** (원본 보존)
3. 복사 시 파일명을 Resources 파일명 규칙에 맞게 변환
4. 폴더 구조:
```
Assets/Resources/Audio/
  BGM/
    bgm_lobby.ogg
    bgm_chapter_forest.ogg
    bgm_chapter_cave.ogg
    bgm_boss.ogg
    ...
  SFX/
    Attack/
      sfx_sword_swing.wav
      sfx_sword_hit.wav
      ...
    Skill/
      sfx_skill_fire.wav
      sfx_skill_ice.wav
      ...
    Monster/
      sfx_monster_hit.wav
      sfx_monster_die.wav
      ...
    System/
      sfx_levelup.wav
      sfx_equip.wav
      ...
    UI/
      sfx_ui_tap.wav
      sfx_ui_confirm.wav
      ...
    Gacha/
      sfx_gacha_spin.wav
      ...
```

---

## 7. 코드 연동 계획

### 7.1 SkillDataSO <-> SpriteSheetVfxSO 연동

**현재 상태**: `SkillDataSO`에 `vfxSheet` / `buffVfxSheet` 필드 이미 존재.

**할 일**:
1. 에디터 스크립트에서 스킬 SO 생성 시 VFX SO를 자동 할당하는 매핑 테이블 적용
2. `SpriteSheetVfx.Play()` 호출 시 SO의 frames 배열을 순회하며 SpriteRenderer에 적용
3. 이미 구현된 `SkillVfx.cs`가 SpriteSheetVfxSO를 사용 -- 추가 코드 변경 최소화

### 7.2 AudioManager 확장

**현재 상태**: `SfxType` enum + `SFX_FILE_MAP` 딕셔너리 매핑 방식.

**할 일**:
1. `SfxType`에 신규 enum 값 추가:
   ```
   Miss, Block, Flee, UseItem, Heal, DefBuff, Debuff,
   Revive, Absorb, Sleep, SpeedUp, SkillWater, SkillWind, SkillPoison
   ```
2. `SFX_FILE_MAP`에 신규 매핑 추가
3. `BgmType`에 신규 enum 값 추가:
   ```
   BossRaid, Arena, Tower, GuildBoss, Event, Town, Prestige
   ```
4. `BGM_FILE_MAP`에 신규 매핑 추가
5. 인트로+루프 BGM 지원을 위한 `PlayBgmWithIntro()` 메서드 추가:
   ```csharp
   public async UniTaskVoid PlayBgmWithIntro(string introName, string loopName)
   ```

### 7.3 SFX 변형 시스템

동일 SfxType에 여러 변형(01~03)이 있을 때 랜덤 선택하는 로직:

```csharp
// AudioManager에 추가
private static readonly Dictionary<SfxType, string[]> SFX_VARIANTS = new()
{
    { SfxType.SwordSwing, new[] { "sfx_sword_swing_01", "sfx_sword_swing_02", "sfx_sword_swing_03" } },
    { SfxType.SwordHit, new[] { "sfx_sword_hit_01", "sfx_sword_hit_02", "sfx_sword_hit_03" } },
    // ...
};

public void PlaySfxVariant(SfxType type, float pitchVariation = 0.05f)
{
    if (SFX_VARIANTS.TryGetValue(type, out var variants))
    {
        PlaySfx(variants[Random.Range(0, variants.Length)], pitchVariation);
    }
    else
    {
        PlaySfx(type, pitchVariation);
    }
}
```

### 7.4 환경 BGS(Background Sound) 시스템

`Fantasy_200/BGS Loops/`의 환경음 루프를 챕터별 배경 사운드로 사용.

```csharp
// AudioManager에 추가
[Header("BGS (Background Sound)")]
[SerializeField] private AudioSource _bgsSource;

public void PlayBgs(string bgsName, float fadeDuration = 1f) { ... }
public void StopBgs(float fadeDuration = 0.5f) { ... }
```

| 챕터 | BGS |
|------|-----|
| 1~3 (숲) | `Forest Day.ogg` |
| 4~6 (동굴) | `Cave.ogg` |
| 7~9 (화산) | 없음 (BGM만) |
| 10~12 (하늘) | `Sea.ogg` (바람 느낌) |
| 13+ (심연) | `Forest Night.ogg` |
| 타워 | `Cave Rain.ogg` |
| 던전 | `Interior Day.ogg` |

---

## 8. 구현 태스크 분해

### Phase A: 에디터 스크립트 (선행 작업)

| ID | 태스크 | 예상 시간 | 의존성 |
|----|--------|----------|--------|
| A-01 | VFX 스프라이트시트 자동 슬라이스 에디터 스크립트 | 2h | 없음 |
| A-02 | SpriteSheetVfxSO 자동 생성 에디터 스크립트 | 2h | A-01 |
| A-03 | 오디오 리소스 복사/리네임 에디터 스크립트 | 1.5h | 없음 |
| A-04 | 아이콘 에셋 정리/복사 에디터 스크립트 | 1h | 없음 |

### Phase B: 코드 확장

| ID | 태스크 | 예상 시간 | 의존성 |
|----|--------|----------|--------|
| B-01 | SfxType/BgmType enum 확장 | 0.5h | 없음 |
| B-02 | AudioManager SFX_FILE_MAP/BGM_FILE_MAP 확장 | 0.5h | B-01 |
| B-03 | PlaySfxVariant() 변형 SFX 시스템 | 1h | B-02 |
| B-04 | PlayBgmWithIntro() 인트로+루프 BGM | 1.5h | B-02 |
| B-05 | BGS 환경음 시스템 추가 | 1h | B-02 |
| B-06 | SpriteSheetVfx.cs 기존 코드 검증/보완 | 0.5h | A-02 |

### Phase C: 에셋 생성 실행

| ID | 태스크 | 예상 시간 | 의존성 |
|----|--------|----------|--------|
| C-01 | A-01 실행: VFX 스프라이트시트 일괄 슬라이스 | 0.5h | A-01 |
| C-02 | A-02 실행: SpriteSheetVfxSO 약 80~100개 생성 | 0.5h | A-02, C-01 |
| C-03 | A-03 실행: 오디오 파일 복사 (SFX ~100개, BGM ~20개) | 0.5h | A-03 |
| C-04 | A-04 실행: 아이콘 에셋 정리/분류 | 0.5h | A-04 |

### Phase D: 스킬 SO 연결

| ID | 태스크 | 예상 시간 | 의존성 |
|----|--------|----------|--------|
| D-01 | 전사 스킬 SO에 VFX SO 할당 (5티어 ~20스킬) | 1h | C-02 |
| D-02 | 궁수 스킬 SO에 VFX SO 할당 (5티어 ~20스킬) | 1h | C-02 |
| D-03 | 마법사 스킬 SO에 VFX SO 할당 (5티어 ~20스킬) | 1h | C-02 |
| D-04 | 보스/던전 VFX SO 할당 | 0.5h | C-02 |
| D-05 | 스킬 SO에 아이콘 할당 | 1h | C-04 |

### Phase E: 테스트/검증

| ID | 태스크 | 예상 시간 | 의존성 |
|----|--------|----------|--------|
| E-01 | VFX 재생 확인 (각 직업별 대표 스킬 3개씩) | 1h | D-01~D-03 |
| E-02 | SFX 재생 확인 (전체 SfxType) | 0.5h | C-03, B-02 |
| E-03 | BGM 재생/크로스페이드 확인 | 0.5h | C-03, B-04 |
| E-04 | BGS 환경음 확인 | 0.5h | B-05, C-03 |
| E-05 | 메모리/빌드 크기 체크 | 0.5h | E-01~E-04 |

### 전체 예상 소요 시간

| Phase | 소요 시간 |
|-------|----------|
| A (에디터 스크립트) | 6.5h |
| B (코드 확장) | 5h |
| C (에셋 생성 실행) | 2h |
| D (SO 연결) | 4.5h |
| E (테스트) | 3h |
| **합계** | **21h** |

### 병렬 실행 그룹

```
[A-01 + A-03 + A-04]  -- 에디터 스크립트 (독립, 동시 진행)
[B-01 + B-02]          -- enum/매핑 확장 (A와 독립)
[A-02]                 -- VFX SO 생성기 (A-01 완료 후)
[C-01 ~ C-04]          -- 에셋 생성 실행 (에디터 스크립트 완료 후)
[B-03 + B-04 + B-05]   -- AudioManager 확장 (B-02 완료 후)
[D-01 + D-02 + D-03]   -- 스킬 SO 연결 (C-02 완료 후, 3직업 동시)
[E-01 ~ E-05]          -- 통합 테스트
```

---

## 9. 미사용/보류 에셋

| 에셋 | 상태 | 사유 |
|------|------|------|
| `DAX/Magic Packs Vol1` | 보류 | 3D 파티클 기반, 2D 프로젝트에 직접 부적합 |
| `SPUM/` | 별도 관리 | 캐릭터 스프라이트 생성 도구 (별도 워크플로우) |
| `Fonts/` | 별도 관리 | UI 폰트 교체 시 사용 |
| `GUI Kit - Dark Geo/` | 보류 | 기본 RPG_UI 적용 후 서브 테마로 검토 |
| `Cyberpunk_UI` | 보류 | Phase 2 아레나 UI에 검토 |
| `Common/MagicEffects/2 Icons/` | 검토 중 | VFX 팩 내 아이콘 -- 500_skill_icons와 중복 여부 확인 필요 |
| `Common/MagicEffects/Font/` | 보류 | 데미지 텍스트 폰트 검토 |
