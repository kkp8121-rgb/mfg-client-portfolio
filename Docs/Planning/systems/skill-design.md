---
read_count: 5
last_read: "2026-04-03"
status: reference
---

# 스킬 시스템 기획서

> 3직업 × 4전직(Tier 0~3) × 4스킬 = 총 48개 스킬
> 방어력(DEF) 관련 효과는 모든 스킬에서 **제외** — 방어는 장비/스탯으로 커버
> 스킬은 **유틸리티** 또는 **강해진 체감**을 주는 방향으로 설계

---

## 1. 스킬 시스템 개요

### 1-1. 스킬 타입 (SkillType enum)

| 타입 | 설명 | 자동시전 | 쿨타임 |
|------|------|----------|--------|
| Active | 기본공격 대체 스킬. 전직할수록 상위 Active로 교체 | 자동 (기본공격) | 없음 |
| Passive | 영구 효과. 습득 즉시 적용 | - | 없음 |
| Buff | 일정 시간 자신을 강화. 쿨타임 후 자동 시전 | 자동 | 있음 |
| Awakening | 궁극기. 대범위 + 유틸리티. 쿨타임 후 자동 시전 | 자동 | 있음 |

### 1-2. 특수효과 (SpecialEffect enum)

| 효과 | 설명 | 사용 직업 |
|------|------|-----------|
| None | 없음 | - |
| Knockback | 넉백 (밀어냄) | 전사 |
| Stun | 스턴 (행동불능) | 전사 |
| Lifesteal | 흡혈 (데미지의 n% 회복) | 전사 |
| Revive | 부활 (사망 시 1회 HP 회복) | 전사 |
| AllStatUp | 전 스탯 상승 | 전사 |
| Penetrate | 관통 (뒤의 적도 타격) | 궁수 |
| MultiShot | 다중 발사 (여러 방향 동시) | 궁수 |
| Dodge | 회피 (피격 시 데미지 무효 확률) | 궁수 |
| Burn | 화상 DoT (3초간 추가 피해) | 마법사 |
| Freeze | 빙결 (이동/공격 정지) | 마법사 |
| Slow | 감속 (이동속도 감소) | 마법사 |

### 1-3. 전직 구조

```
Tier 0 (기본, Lv.1)  →  Tier 1 (1차, Lv.40)  →  Tier 2 (2차, Lv.80)  →  Tier 3 (3차, Lv.120)
```

각 전직 Tier마다 4개 스킬 해금 (Active 1 + Passive 1 + Buff 1 + Awakening 1)
- 스킬 해금 레벨: Tier 시작 + 0/10/20/30

### 1-4. 직업별 설계 철학

| 직업 | 키워드 | 강점 | 생존 수단 |
|------|--------|------|-----------|
| **전사** | 근접 파워, 광역 CC | 넓은 범위, 스턴, 안정성 | 흡혈, 부활, 높은 HP |
| **궁수** | 원거리 연사, 치명타 | 빠른 공속, 다단히트, 관통 | 회피, 높은 이동속도 |
| **마법사** | 최대 범위, 원소 마법 | 최고 배율, 넓은 AOE, DoT | 빙결/감속 CC, 시간 마법 |

---

## 2. 전사(Warrior) 계열 — 근접 파워 + 광역 CC + 흡혈

> 애니메이션 타입: `AttackAnimType.Normal` (skill_normal)
> 사거리: 1.5 유닛 (근접)

### Tier 0 — 전사 (Warrior)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 1 | warrior_strike | 강타 | Active | 1 | 200% | 0 | 1 | 0s | - | - | None | - |
| 2 | warrior_training | 단련 | Passive | 10 | - | - | - | - | 영구 | ATK +10% | None | - |
| 3 | warrior_warcry | 전투 함성 | Buff | 20 | - | - | - | 20s | 8s | AtkSpd +30% | None | - |
| 4 | warrior_fury | 분노의 일격 | Awakening | 30 | 500% | 4 | 1 | 60s | - | - | Stun | 2초 |

**컨셉**: 기본적인 근접 전투. 강타로 때리고, 단련으로 기본기 강화, 함성으로 공속 올려 DPS 증가.

### Tier 1 — 나이트 (Knight)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 5 | knight_charge | 돌진 | Active | 40 | 250% | 2 | 1 | 0s | - | - | Knockback | 3 |
| 6 | knight_weakness | 약점 간파 | Passive | 50 | - | - | - | - | 영구 | CritRate +15% | None | - |
| 7 | knight_rally | 전의 고양 | Buff | 60 | - | - | - | 25s | 10s | ATK +25%, AtkSpd +15% | None | - |
| 8 | knight_judgment | 심판의 검 | Awakening | 70 | 800% | 6 | 1 | 90s | - | - | Stun | 3초, 자힐15% |

**컨셉**: 돌진하며 넉백, 약점을 찔러 치명타 강화, 전의를 고양시켜 팀 버프. 공격적인 기사.
**변경**: 기존 철벽(DEF+15%) → 약점 간파(CritRate+15%), 수호의 방패(DEF+30%) → 전의 고양(ATK+25%, AtkSpd+15%)

### Tier 2 — 워로드 (Warlord)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 9 | warlord_whirlwind | 선풍참 | Active | 80 | 300% | 3 | 1 | 0s | - | - | None | - |
| 10 | warlord_frenzy | 전투 광기 | Passive | 90 | - | - | - | - | 영구 | ATK +25%, CritRate +15% | None | HP 50%↓ 조건 |
| 11 | warlord_bloodpact | 피의 서약 | Buff | 100 | - | - | - | 30s | 12s | - | Lifesteal | 10% |
| 12 | warlord_earthshatter | 대지 분쇄 | Awakening | 110 | 1200% | 8 | 1 | 120s | - | - | Stun | 3초, 자힐20% |

**컨셉**: 피가 적을수록 강해지는 광전사. 흡혈로 생존하면서 폭딜. 위험하지만 강력한 파워 판타지.

### Tier 3 — 타이탄 (Titan)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 13 | titan_annihilate | 멸살의 칼날 | Active | 120 | 400% | 3 | 3 | 0s | - | - | None | - |
| 14 | titan_immortal | 불멸의 의지 | Passive | 130 | - | - | - | - | 1회 | - | Revive | HP 30% |
| 15 | titan_might | 타이탄의 힘 | Buff | 140 | - | - | - | 35s | 15s | ATK +50% | None | - |
| 16 | titan_cataclysm | 천지개벽 | Awakening | 150 | 2000% | 10 | 1 | 180s | 10s | ATK+20%, CritRate+20% | AllStatUp | 자힐10% |

**컨셉**: 최종 병기. 3연타 기본공격, 죽어도 살아나며, 천지개벽으로 전장을 초토화.

---

## 3. 궁수(Archer) 계열 — 원거리 연사 + 치명타 + 관통

> 애니메이션 타입: `AttackAnimType.Bow` (skill_bow)
> 사거리: 6.0 유닛 (원거리)

### Tier 0 — 궁수 (Archer)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 17 | archer_aimshot | 정조준 | Active | 1 | 180% | 0 | 1 | 0s | - | - | None | - |
| 18 | archer_keeneye | 명사수의 눈 | Passive | 10 | - | - | - | - | 영구 | CritRate +10% | None | - |
| 19 | archer_rapidfire | 연사 | Buff | 20 | - | - | - | 18s | 8s | AtkSpd +40% | None | - |
| 20 | archer_arrowrain | 화살비 | Awakening | 30 | 300% | 5 | 3 | 55s | - | - | None | - |

**컨셉**: 기본적인 원거리 사수. 정확한 조준, 빠른 연사, 화살비로 범위 공격. 공속 +40%로 전사보다 빠른 연사감.

### Tier 1 — 스카우트 (Scout)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 21 | scout_pierceshot | 관통 사격 | Active | 40 | 220% | 2 | 1 | 0s | - | - | Penetrate | - |
| 22 | scout_agility | 민첩 | Passive | 50 | - | - | - | - | 영구 | AtkSpd +15%, MoveSpeed +10% | None | - |
| 23 | scout_windblessing | 바람의 가호 | Buff | 60 | - | - | - | 22s | 10s | CritRate +20%, MoveSpeed +20% | None | - |
| 24 | scout_stormshot | 폭풍 사격 | Awakening | 70 | 600% | 6 | 5 | 80s | - | - | Knockback | 2 |

**컨셉**: 바람을 타는 정찰병. 관통으로 여러 적을 꿰뚫고, 빠른 이동으로 위치를 잡는 기동 사수.

### Tier 2 — 윈드워커 (Windwalker)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 25 | windwalker_windpierce | 바람 꿰뚫기 | Active | 80 | 280% | 3 | 2 | 0s | - | - | Penetrate | - |
| 26 | windwalker_afterimage | 잔상 | Passive | 90 | - | - | - | - | 영구 | - | Dodge | 15% |
| 27 | windwalker_gale | 질풍 | Buff | 100 | - | - | - | 28s | 12s | ATK +30%, AtkSpd +25% | None | - |
| 28 | windwalker_typhon | 태풍의 눈 | Awakening | 110 | 1000% | 8 | 7 | 110s | - | - | Knockback | 4 |

**컨셉**: 바람 그 자체. 잔상으로 피격을 회피하고, 질풍으로 극한의 딜 사이클, 태풍으로 전장을 쓸어버림.
**잔상(Dodge)**: 피격 시 15% 확률로 데미지를 완전 무효화. 방어력 대신 "회피"로 생존.

### Tier 3 — 호크아이 (Hawkeye)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 29 | hawkeye_extinction | 멸절의 화살 | Active | 120 | 350% | 4 | 3 | 0s | - | - | Penetrate | - |
| 30 | hawkeye_eagleeye | 매의 눈 | Passive | 130 | - | - | - | - | 영구 | CritRate +25%, CritDmg +30% | None | - |
| 31 | hawkeye_huntingtime | 사냥의 시간 | Buff | 140 | - | - | - | 35s | 15s | ATK +40%, CritRate +20% | None | - |
| 32 | hawkeye_judgment | 천벌의 비 | Awakening | 150 | 1800% | 10 | 10 | 170s | - | - | Stun | 2초, 자힐10% |

**컨셉**: 궁극의 사냥꾼. 3연 관통 기본공격, 치명타 +25%에 크뎀 +30%로 폭딜, "사냥의 시간" 발동 시 모든 화살이 급소를 꿰뚫음.
**매의 눈**: CritDmg 보너스는 새 필드 추가 필요 (기존 SkillDataSO에 `buffCritDmgRate` 추가)

---

## 4. 마법사(Mage) 계열 — 최대 범위 + 원소 마법 + CC

> 애니메이션 타입: `AttackAnimType.Magic` (skill_magic)
> 사거리: 5.0 유닛 (원거리)

### Tier 0 — 마법사 (Mage)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 33 | mage_magicbolt | 마력탄 | Active | 1 | 220% | 0 | 1 | 0s | - | - | None | - |
| 34 | mage_concentration | 마력 집중 | Passive | 10 | - | - | - | - | 영구 | ATK +12% | None | - |
| 35 | mage_manacharge | 마력 충전 | Buff | 20 | - | - | - | 20s | 8s | ATK +25% | None | - |
| 36 | mage_fireburst | 화염 폭발 | Awakening | 30 | 450% | 5 | 1 | 55s | - | - | Burn | 3초, 총50% |

**컨셉**: 기본 마법사. 마력탄으로 원거리 공격, 마력을 집중/충전하여 폭딜, 화염 폭발로 범위 5의 넓은 AOE.
**화상(Burn)**: 3초간 스킬 데미지의 50%를 추가 DoT. 실질 배율 = 450% + 225% = 675%

### Tier 1 — 소서러 (Sorcerer)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 37 | sorcerer_fireball | 화염구 | Active | 40 | 260% | 3 | 1 | 0s | - | - | Burn | 3초, 총30% |
| 38 | sorcerer_elemental | 원소 친화 | Passive | 50 | - | - | - | - | 영구 | ATK +15%, AtkSpd +10% | None | - |
| 39 | sorcerer_chainlightning | 연쇄 번개 | Buff | 60 | - | - | - | 22s | 10s | ATK +20%, CritRate +15% | None | - |
| 40 | sorcerer_froststorm | 빙결 폭풍 | Awakening | 70 | 700% | 7 | 1 | 85s | - | - | Freeze | 3초 |

**컨셉**: 원소 마법 전문가. 화염구로 화상 기본공격, 연쇄 번개로 크리티컬 강화, 빙결 폭풍으로 광역 CC.
**화염구 기본공격**: 범위 3으로 기본공격 자체가 AOE. 느린 공속을 범위로 보상.
**빙결(Freeze)**: 3초간 이동+공격 완전 정지. 스턴과 동일하나 마법사 전용 연출.

### Tier 2 — 세이지 (Sage)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 41 | sage_arcanemissile | 비전 화살 | Active | 80 | 320% | 3 | 2 | 0s | - | - | Slow | 30%, 2초 |
| 42 | sage_timewarp | 시간 왜곡 | Passive | 90 | - | - | - | - | 영구 | AtkSpd +20%, CooldownReduce +10% | None | - |
| 43 | sage_dimensionrift | 차원 균열 | Buff | 100 | - | - | - | 28s | 12s | ATK +35%, CritRate +20% | None | - |
| 44 | sage_meteorshower | 유성우 | Awakening | 110 | 1200% | 9 | 1 | 115s | - | - | Burn | 5초, 총80% |

**컨셉**: 시간과 공간을 다루는 현자. 감속으로 적을 제어하고, 시간 왜곡으로 쿨감+공속, 유성우로 화면을 뒤덮음.
**시간 왜곡**: 쿨다운 감소(CooldownReduce)는 새 필드 필요. 모든 스킬 쿨타임 10% 단축.
**유성우**: 배율 1200% + 화상 80%(=960%) = 실질 2160%. 범위 9로 거의 전장 커버.

### Tier 3 — 룬마스터 (Runemaster)

| # | ID | 스킬명 | 타입 | 습득Lv | 배율 | 범위 | 히트 | 쿨타임 | 지속 | 버프 효과 | 특수효과 | 효과값 |
|---|-----|--------|------|--------|------|------|------|--------|------|-----------|----------|--------|
| 45 | runemaster_runeblast | 룬 폭발 | Active | 120 | 400% | 4 | 2 | 0s | - | - | Burn | 3초, 총40% |
| 46 | runemaster_runemastery | 룬 마스터리 | Passive | 130 | - | - | - | - | 영구 | ATK +30%, CooldownReduce +15% | None | - |
| 47 | runemaster_absolutedomain | 절대 영역 | Buff | 140 | - | - | - | 35s | 15s | ATK +50%, CritRate +25% | None | - |
| 48 | runemaster_apocalypse | 종말의 룬 | Awakening | 150 | 2200% | 10 | 1 | 175s | 10s | ATK+25%, CritRate+25% | Freeze | 4초, 자힐15% |

**컨셉**: 고대 룬의 힘을 해방한 궁극의 마법사. 모든 공격에 룬이 새겨져 화상, 쿨감으로 스킬 회전율 극대화, 종말의 룬으로 전장 소멸.
**종말의 룬**: 배율 2200% + 빙결 4초 + ATK/CritRate +25% 버프 10초 + 자힐 15%. 마법사 최종 궁극기.

---

## 5. 아이콘 매핑

> 경로 기준: `Assets/500_skill_icons/`
> noBG 버전 사용 (투명 배경)

### 5-1. 전사 계열

| # | ID | 스킬명 | 아이콘 경로 | 아이콘 설명 |
|---|-----|--------|------------|------------|
| 1 | warrior_strike | 강타 | `Bonus/Bonus_skills/Nobg/24_heavy_blow_nobg` | 철퇴 강타 |
| 2 | warrior_training | 단련 | `Skill_nobg/skill_190_noBG` | 주황 상승 화살표 |
| 3 | warrior_warcry | 전투 함성 | `Skill_nobg/skill_45_noBG` | 함성 (후드 인물) |
| 4 | warrior_fury | 분노의 일격 | `Bonus/Bonus_skills/Nobg/49_red_wave_nobg` | 붉은 충격파 |
| 5 | knight_charge | 돌진 | `Bonus/Bonus_skills/Nobg/11_bull_nobg` | 황소 돌진 |
| 6 | knight_weakness | 약점 간파 | `Bonus/Bonus_skills/Nobg/79_headshot_nobg` | 급소 관통 |
| 7 | knight_rally | 전의 고양 | `Bonus/Bonus_skills/Nobg/54_Holy_power_nobg` | 성스러운 검 |
| 8 | knight_judgment | 심판의 검 | `Bonus/Bonus_skills/Nobg/67_swords_of_light_nobg` | 빛의 교차검 |
| 9 | warlord_whirlwind | 선풍참 | `Bonus/Bonus_skills/Nobg/30_vortex_nobg` | 회전 베기 |
| 10 | warlord_frenzy | 전투 광기 | `Bonus/Bonus_skills/Nobg/73_unholy_energy_nobg` | 광기 에너지 |
| 11 | warlord_bloodpact | 피의 서약 | `Bonus/Bonus_skills/Nobg/34_wound_nobg` | 피의 X자국 |
| 12 | warlord_earthshatter | 대지 분쇄 | `Skill_nobg/skill_68_noBG` | 지면 폭발 |
| 13 | titan_annihilate | 멸살의 칼날 | `Bonus/Bonus_skills/Nobg/58_thousand_hits_nobg` | 다중 검격 |
| 14 | titan_immortal | 불멸의 의지 | `Bonus/Bonus_skills/Nobg/14_phoenix_nobg` | 불사조 소용돌이 |
| 15 | titan_might | 타이탄의 힘 | `Skill_nobg/skill_210_noBG` | 근육/파워 |
| 16 | titan_cataclysm | 천지개벽 | `Bonus/Bonus_skills/Nobg/75_Sun_nobg` | 태양 폭발 |

### 5-2. 궁수 계열

| # | ID | 스킬명 | 아이콘 경로 | 아이콘 설명 |
|---|-----|--------|------------|------------|
| 17 | archer_aimshot | 정조준 | `Bonus/Skills_upgrates/Nobg/25_bow_shot_nobg` | 기본 활 조준 |
| 18 | archer_keeneye | 명사수의 눈 | `Skill_nobg/skill_80_noBG` | 녹색 조준경 |
| 19 | archer_rapidfire | 연사 | `Bonus/Skills_upgrates/Nobg/28_bow_shot_nobg` | 다중 화살 발사 |
| 20 | archer_arrowrain | 화살비 | `Skill_nobg/skill_150_noBG` | 녹색 화살 다수 |
| 21 | scout_pierceshot | 관통 사격 | `Bonus/Skills_upgrates/Nobg/26_bow_shot_nobg` | 관통 화살 |
| 22 | scout_agility | 민첩 | `Bonus/Bonus_skills/Nobg/57_run_nobg` | 달리는 실루엣 |
| 23 | scout_windblessing | 바람의 가호 | `Skill_nobg/skill_91_noBG` | 시안 에너지 폭발 |
| 24 | scout_stormshot | 폭풍 사격 | `Skill_nobg/skill_430_noBG` | 초록 크리스탈 화살 |
| 25 | windwalker_windpierce | 바람 꿰뚫기 | `Bonus/Skills_upgrates/Nobg/27_bow_shot_nobg` | 강화 활+화살 |
| 26 | windwalker_afterimage | 잔상 | `Bonus/Bonus_skills/Nobg/35_clones_nobg` | 분신/잔상 |
| 27 | windwalker_gale | 질풍 | `Skill_nobg/skill_105_noBG` | 바람 속 실루엣 |
| 28 | windwalker_typhon | 태풍의 눈 | `Skill_nobg/skill_100_noBG` | 파란 에너지 구체 |
| 29 | hawkeye_extinction | 멸절의 화살 | `Skill_nobg/skill_470_noBG` | 붉은 파워 화살 |
| 30 | hawkeye_eagleeye | 매의 눈 | `Skill_nobg/skill_320_noBG` | 시안 조준경 |
| 31 | hawkeye_huntingtime | 사냥의 시간 | `Bonus/Bonus_skills/Nobg/40_timechanging_nobg` | 시계 (시간 조작) |
| 32 | hawkeye_judgment | 천벌의 비 | `Bonus/Bonus_skills/Nobg/69_starfall_nobg` | 별비/유성 |

### 5-3. 마법사 계열

| # | ID | 스킬명 | 아이콘 경로 | 아이콘 설명 |
|---|-----|--------|------------|------------|
| 33 | mage_magicbolt | 마력탄 | `Bonus/Skills_upgrates/Nobg/33_wand_shot_nobg` | 보라 번개 지팡이 |
| 34 | mage_concentration | 마력 집중 | `Bonus/Bonus_skills/Nobg/62_light_nobg` | 빛의 눈/집중 |
| 35 | mage_manacharge | 마력 충전 | `Skill_nobg/skill_240_noBG` | 보라 에너지 충전 |
| 36 | mage_fireburst | 화염 폭발 | `Skill_nobg/skill_22_noBG` | 붉은 화염 |
| 37 | sorcerer_fireball | 화염구 | `Skill_nobg/skill_390_noBG` | 주황 에너지 방사 |
| 38 | sorcerer_elemental | 원소 친화 | `Skill_nobg/skill_360_noBG` | 분홍 에너지 구체 |
| 39 | sorcerer_chainlightning | 연쇄 번개 | `Bonus/Skills_upgrates/Nobg/34_wand_shot_nobg` | 구체+번개 지팡이 |
| 40 | sorcerer_froststorm | 빙결 폭풍 | `Skill_nobg/skill_50_noBG` | 푸른 냉기 손 |
| 41 | sage_arcanemissile | 비전 화살 | `Skill_nobg/skill_120_noBG` | 보라 에너지 방사 |
| 42 | sage_timewarp | 시간 왜곡 | `Bonus/Bonus_skills/Nobg/44_obsession_nobg` | 후드 마법사 |
| 43 | sage_dimensionrift | 차원 균열 | `Bonus/Bonus_skills/Nobg/28_portal_nobg` | 포탈 |
| 44 | sage_meteorshower | 유성우 | `Skill_nobg/skill_460_noBG` | 붉은 반원 폭발 |
| 45 | runemaster_runeblast | 룬 폭발 | `Bonus/Skills_upgrates/Nobg/35_wand_shot_nobg` | 핑크 번개 지팡이 |
| 46 | runemaster_runemastery | 룬 마스터리 | `Bonus/Skills_upgrates/Nobg/36_wand_shot_nobg` | 붉은 룬 구체 |
| 47 | runemaster_absolutedomain | 절대 영역 | `Skill_nobg/skill_280_noBG` | 보라 마법 손 |
| 48 | runemaster_apocalypse | 종말의 룬 | `Skill_nobg/skill_200_noBG` | 해골+폭발 에너지 |

---

## 6. DPS 비교 분석 (Tier 3 기준, 180초 사이클)

### 6-1. 기본공격 DPS (스킬 배율 × 공속)

| 직업 | Active 배율 | 히트 | 공속 | 실질 DPS 배율/초 |
|------|------------|------|------|----------------|
| 타이탄 | 400% × 3hit | 3 | 1.0 | 1200%/s |
| 호크아이 | 350% × 3hit | 3 | 1.3 | 1365%/s |
| 룬마스터 | 400% × 2hit + Burn40% | 2 | 0.8 | 896%/s (단일) |

> 궁수가 단일 DPS 최강, 전사가 범위 DPS 최강, 마법사는 CC+DoT로 안전하게 사냥

### 6-2. 궁극기 기여 (180초 1회)

| 직업 | 궁극기 | 배율 | 유틸리티 |
|------|--------|------|----------|
| 타이탄 | 천지개벽 | 2000% | 전스탯+20%(10초), 자힐10% |
| 호크아이 | 천벌의 비 | 1800% × 10hit | 스턴2초, 자힐10% |
| 룬마스터 | 종말의 룬 | 2200% + Burn80% | 빙결4초, 전스탯+25%(10초), 자힐15% |

> 마법사 궁극기가 실질 배율(2200+1760=3960%)과 유틸리티 모두 최강이나, 쿨타임도 가장 김

---

## 7. 구현 시 필요한 코드 변경사항

### 7-1. SpecialEffect enum 확장
```csharp
public enum SpecialEffect
{
    None,
    Knockback,    // 기존
    Stun,         // 기존
    Lifesteal,    // 기존
    Revive,       // 기존
    DefReduce,    // 기존 (사용 안 함, 하위호환)
    AllStatUp,    // 기존
    Penetrate,    // 신규: 관통 (궁수)
    MultiShot,    // 신규: 다중 발사 (궁수)
    Dodge,        // 신규: 회피 (궁수)
    Burn,         // 신규: 화상 DoT (마법사)
    Freeze,       // 신규: 빙결 (마법사)
    Slow          // 신규: 감속 (마법사)
}
```

### 7-2. SkillDataSO 필드 추가
```csharp
[Header("추가 버프")]
[Tooltip("치명타 데미지 증가율")] public float buffCritDmgRate;
[Tooltip("이동속도 증가율")] public float buffMoveSpeedRate;
[Tooltip("쿨타임 감소율")] public float buffCooldownReduceRate;

[Header("DoT/CC")]
[Tooltip("DoT 총 데미지 비율 (스킬 데미지 대비)")] public float dotDamageRate;
[Tooltip("DoT/CC 지속시간")] public float ccDuration;
[Tooltip("감속률 (Slow 전용)")] public float slowRate;
```

### 7-3. CombatStats 확장
```csharp
// 기존 AddBonus에 추가 파라미터
public void AddBonus(int atk = 0, int def = 0, float critRate = 0f,
    float atkSpd = 0f, float critDmg = 0f, float moveSpeed = 0f);

// 회피 시스템
public float DodgeRate { get; private set; }
public bool TryDodge() => Random.value < DodgeRate;
```

### 7-4. MonsterController 확장
```csharp
// CC 시스템
public void ApplyBurn(float totalDamage, float duration);
public void ApplyFreeze(float duration);
public void ApplySlow(float slowRate, float duration);
```

### 7-5. SkillSystem 확장
- `ApplyPassive()`: Dodge, CritDmg, MoveSpeed, CooldownReduce 처리
- `ApplyBuff()`: 새 버프 필드들 처리
- `DealAreaDamage()`: Penetrate(직선 관통), Burn(DoT 적용), Freeze/Slow 처리
- `AutoCastSkills()`: CooldownReduce 반영

### 7-6. Phase2SetupEditor 확장 (or Phase3SetupEditor)
- `CreateArcherSkills()`: 궁수 16개 스킬 SO 생성
- `CreateMageSkills()`: 마법사 16개 스킬 SO 생성
- 아이콘 로드 경로를 `500_skill_icons/` 하위로 변경

---

## 8. 전사 스킬 변경 요약 (기존 → 신규)

| 기존 스킬 | 기존 효과 | 신규 스킬 | 신규 효과 | 변경 이유 |
|-----------|-----------|-----------|-----------|-----------|
| 철벽 | DEF +15% (Passive) | **약점 간파** | CritRate +15% (Passive) | 방어 → 공격적 파워 체감 |
| 수호의 방패 | DEF +30% (Buff) | **전의 고양** | ATK +25%, AtkSpd +15% (Buff) | 방어 → 공격 버프 체감 |
| 실드 차지 | 250%, 넉백 | **돌진** | 250%, 넉백 (이름만 변경) | "실드" 제거, 공격적 이름 |

> DEF 관련 효과가 스킬에서 완전 제거됨.
> 방어력은 장비 강화/스탯 분배로만 올리는 구조.

---

## 변경 이력

| 날짜 | 변경 |
|------|------|
| 2026-03-08 | 초안 작성. 3직업 48스킬 전체 설계 |
| 2026-03-08 | 전사 DEF 스킬 제거 (철벽→약점 간파, 수호의 방패→전의 고양) |
| 2026-03-08 | 궁수/마법사 Tier 0~3 전체 스킬 신규 설계 |
| 2026-03-08 | 아이콘 48개 매핑 (500_skill_icons 에셋 기반) |
