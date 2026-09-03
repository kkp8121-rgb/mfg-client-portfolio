# MapleStory Idle RPG Wiki 원본 데이터

> 출처: idle.maplestorywiki.net (2026-03-26 크롤링)
> 목적: 웹 재접속 없이 위키 데이터 참조용
> 이 파일은 위키에서 수집한 원본 수치/구조를 가공 없이 보존한다.

---

## 1. 직업 (Classes)

### 직업 목록 (10종)
| 계열 | 직업명 | 메인스탯 | 서브스탯 |
|------|--------|---------|---------|
| 전사 (Warrior) | Hero | STR | DEX (4 DEX = 1 Attack) |
| 전사 | Dark Knight | STR | DEX |
| 전사 | Paladin | STR | DEX |
| 마법사 (Magician) | Arch Mage (Ice Lightning) | INT | LUK (4 LUK = 1 Attack) |
| 마법사 | Arch Mage (Fire Poison) | INT | LUK |
| 마법사 | Bishop | INT | LUK |
| 궁수 (Bowman) | Bowmaster | DEX | STR (4 STR = 1 Attack) |
| 궁수 | Marksman | DEX | STR |
| 도적 (Thief) | Night Lord | LUK | DEX (4 DEX = 1 Attack) |
| 도적 | Shadower | LUK | DEX |

### 전직 구조
- Beginner → Lv.10에 직업 선택 (변경 불가)
- 1차 전직: Lv.1~
- 2차 전직: Lv.30
- 3차 전직: Lv.60
- 4차 전직: Lv.100
- 최대 레벨: Lv.140

### 스킬 포인트 배분
- 레벨당 3포인트
- 1차: 60pt (Lv.1~20), 2차: 90pt (Lv.30~59), 3차: 120pt (Lv.60~99), 4차: 120pt (Lv.100~139)

### 스킬 카테고리
- Active: 장착하여 사용
- Passive: 상시 적용
- Basic Attack Effect: 쿨타임 없는 기본 공격 (전직마다 1개씩 업그레이드)
- Skill Mastery: 스킬 강화 트리

---

## 2. 캐릭터 스탯 (Character Stats)

### 기본 4스탯
| 스탯 | 해당 클래스 | 환산 |
|------|-----------|------|
| STR | Warrior | 1 STR = 1 Attack |
| DEX | Bowman | 1 DEX = 1 Attack |
| INT | Magician | 1 INT = 1 Attack |
| LUK | Thief | 1 LUK = 1 Attack |
서브스탯: 4 서브스탯 = 1 Attack

### 파생 스탯 (13종)

**Attack Speed** (공격 속도)
- 최대 150%, 곱연산 체감 감소
- 공식: `150 * (1 - (1-as1/150) * (1-as2/150) * ... * (1-asn/150))`
- 예: +15%, +10%, +7%, +5% = 최종 +33.884%

**Stat Prop. Damage** (스탯 비례 데미지)
- 메인스탯의 1% + 서브스탯의 0.25% (0.1% 단위 버림)

**Defense Penetration** (방어 관통)
- 최대 100%, 곱연산
- 공식: `100 * (1 - (1-ied1/100) * (1-ied2/100) * ...)`
- 예: +15%, +10%, +7%, +5% = 최종 32.41%

**Final Damage** (최종 데미지)
- 각 소스별 곱연산 적용

**Skill Cooldown Decrease** (쿨타임 감소)
- % 감소 먼저 적용 (곱연산, 최대 100%)
- 이후 고정값 감소 적용
- 7초 미만이 되면 0.5초씩만 감소, 최소 4초

**Damage Taken Decrease** (피해 감소)
- 최대 95%, 곱연산

**기타**: Evasion, Debuff Resistance, Debuff Tolerance, Critical Resistance, Boss Damage, Basic Attack Damage, Skill Damage, Skill Level

---

## 3. 직업별 스킬 상세

### 3-1. Hero (전사 - 히어로)
총 9 Active + 12 Passive = 21개

**1차 전직**
| 스킬명 | 타입 | 요구Lv | 효과 (Lv.1) |
|--------|------|--------|------------|
| Slash Blast | Active(BA) | - | 전방 3타겟 26% x2회 |
| Nimble Feet | Active | - | 15초간 AS+15%, 이속+10% |
| Iron Body | Passive | 10 | STR+30, 방어+15% |
| Warrior Mastery | Passive | 15 | Max HP+10%, 이속+10% |

**2차 전직**
| 스킬명 | 타입 | 요구Lv | 효과 (Lv.1) |
|--------|------|--------|------------|
| Brandish | Active(BA) | 30 | 전방 5타겟 40% x3회 |
| Flash Slash | Active | 35 | 전방 7타겟 350% (콤보3+ 시 +50%) |
| Spirit Blade | Active | 40 | 20초간 피해-8%, 아군 공격력+10% |
| Combo Attack | Passive | 45 | 공격 시 30% 확률 콤보 (공격력+4%, 최대5중첩) |
| Final Attack | Passive | 50 | 25% 확률 35% 추가피해 |
| Weapon Mastery | Passive | 43 | 최소 데미지 배율 +15% |
| Weapon Acceleration | Passive | 33 | AS +5% |
| Physical Training | Passive | 38 | 기본공격 데미지 +10% |

**3차 전직**
| 스킬명 | 타입 | 요구Lv | 효과 (Lv.1) |
|--------|------|--------|------------|
| Intrepid Slash | Active(BA) | 60 | 전방 6타겟 80% x5회 |
| Beam Blade | Active | 69 | 전방 8타겟 250% x4회 |
| Rush | Active | 63 | 12타겟 600% + 기절 2.5초 |
| Scaring Sword | Active | 66 | 15초간, 25%확률 적 공격력-10%, 받피+20% |
| Combo Synergy | Passive | 75 | 콤보 확률+10%p, 콤보당 최종뎀+5% |
| Self Recovery | Passive | 60 | 매초 HP 1%, MP 0.5% 회복 |
| Chance Attack | Passive | 72 | 크리율+8%, 상태이상 데미지+12% |
| Endure | Passive | 74 | 디버프 내성 +15 |

**4차 전직**
| 스킬명 | 타입 | 요구Lv | 효과 (Lv.1) |
|--------|------|--------|------------|
| Raging Blow | Active(BA) | 100 | 전방 6타겟 290% x5회 |
| Puncture | Active | 103 | 5타겟 900% x3회 + 10초 DoT 150%/초 (보스 피해+10%) |
| Enhanced Raging Blow | Active | 107 | 9타겟 550% x6회 (보스뎀+100%, 콤보5+ 필요) |
| Magic Crash | Active | 117 | 5타겟 4800% x1회 + 버프 1개 제거 |
| Maple Hero | Passive | 100 | Beam Blade FD+20%, Rush FD+30%, Flash Slash FD+80% |
| Advanced Final Attack | Passive | 105 | FA 최종뎀 +500% |
| Advanced Combo | Passive | 110 | 콤보 최대 중첩 +2 |
| Enrage | Passive | 115 | 10초마다 5초간 FD+12%, 크뎀+15% |
| Combat Mastery | Passive | 120 | 스킬뎀+15%, 최대뎀배율+20% |
| Power Stance | Passive | 125 | 피해감소 5%, FD+15% |

### 3-2. Dark Knight (전사 - 다크나이트)
총 9 Active + 12 Passive = 21개. 핵심 메카닉: Evil Eye (비홀더) 소환

**2차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Spear Sweep (BA) | 전방 5타겟 40% x3회 |
| Evil Eye | 20초 소환, 주변 6타겟 받피+15% |
| Evil Eye Shock | 6타겟 70% x6회 + 기절 1.5초 |
| Hyper Body | 15초 공격력+12%, 아군 방어+15% |
| Iron Wall | 방어력의 10%만큼 STR 증가 |

**3차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| La Mancha Spear (BA) | 전방 6타겟 80% x5회 |
| Rush | 12타겟 600% + 기절 2.5초 |
| Cross Over Chains | 공격력+15%, HP 50% 이하 시 피해감소+10% |
| Evil Eye of Dominant | 비홀더 DoT 6타겟 60%/초 |
| Lord of Darkness | 30%확률 HP 1.5% 회복, 크리율+8%, 크뎀+30% |

**4차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Dark Impale (BA) | 전방 6타겟 290% x5회 |
| Gungnir's Descent | 1타겟 1800% x2회 |
| Dark Resonance | 20초간 공격력+25% |
| Magic Crash | 4800% x1, 버프 제거 |
| Final Pact | FD+10%, HP 1% 이하 시 2초 무적 (1회) |
| Revenge of Evil Eye | 비홀더 추가타 650% x2회 |
| Maple Hero | Evil Eye of Dominant FD+40%, Rush FD+30%, Evil Eye Shock FD+30% |

### 3-3. Arch Mage (Ice Lightning)
총 10 Active + 11 Passive = 21개. 핵심 메카닉: Frost 중첩 (최대 5, 이속-5%, 회피-1 per stack)

**2차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Cold Beam (BA) | 전방 5타겟 40% x3회 |
| Freezing Effect | 얼음스킬로 25%확률 Frost 부여 |
| Thunder Bolt | 6타겟 180% x3회 |
| Meditation | 아군 공격력+20% 15초 |
| MP Eater | 50%확률 MP 1.5% 회복 |

**3차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Ice Strike (BA) | 6타겟 80% x5회 |
| Glacier Wall | 8타겟 290% x3회 + 빙결 2초 |
| Thunder Sphere | 10초 소환, 2초마다 6타겟 100% x3회 (일몹뎀+150%) |
| Frozen Break | Frost 중첩 x 3% 추가뎀 |
| Element Amplification | MP 50%+ 시 FD+15% (MP 소모+10%) |

**4차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Chain Lightning (BA) | 6타겟 290% x5회 |
| Freezing Breath | 5초간 0.5초마다 10타겟 800% |
| Blizzard | 5타겟 600% x3회, 빙창 3개 |
| Frozen Orb | 5초간 0.5초마다 10타겟 900% (1타겟 시 절반) |
| Infinity | 15초간 FD+15%, 매초 +1% 추가 (최대 10중첩) |
| Elquines | 30초 소환, 4초마다 3타겟 3500% |
| Frost Clutch | Frost 중첩 x 6% 번개 스킬 피해 증가 |
| Arcane Aim | 25%확률 FD+3%, 최대 5중첩 |
| Maple Hero | Thunder Sphere FD+20%, Glacier Wall FD+30%, Thunder Bolt FD+100% |

### 3-4. Bowmaster (궁수)
총 7 Active + 14 Passive = 21개. 핵심 메카닉: 공격속도 비례 스케일링 + Flash Mirage

**2차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Wind Arrow (BA) | 전방 5타겟 40% x3회 |
| Covering Fire | 후퇴하며 3회 각 250% |
| Quiver Cartridge | 1초마다 22% 추가뎀 (AS비례 최대 2배) |
| Soul Arrow: Bow | DEX+50 (AS비례 최대 150) |

**3차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Wind Arrow II (BA) | 전방 6타겟 80% x5회 |
| Flash Mirage | 20%확률 잔영 550% (AS비례 최대 2배 확률) |
| Phoenix | 20초 소환, 3초마다 3타겟 600% |
| Arrow Platter | 60초 설치, 0.3초마다 1타겟 50% (일몹뎀+200%) |
| Extreme Archery | 방어-10%, FD+15% |
| Mortal Blow | 50회 직접타격 후 FD+10% 5초 |
| Concentration | 스킬 발동 시 명중+1, 크뎀+3%, 최대 7중첩 |

**4차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Arrow Stream (BA) | 전방 6타겟 290% x5회 |
| Hurricane | 20회 연사 800% (AS비례 속도 증가) |
| Sharp Eyes | 아군 크리율+8%, 크뎀+40% 15초 |
| Enchanted Quiver | 퀴버 타겟+2, FD+500% |
| Flash Mirage II | 추가 4타겟, FD+400% |
| Illusion Step | 공격력+10%, 15초마다 5초간 회피+15/피해감소+10% |
| Armor Break | 방관+10%, FD+10% |
| Maple Hero | Arrow Platter FD+25%, Phoenix FD+30%, Covering Fire FD+100% |

### 3-5. Night Lord (도적)
총 9 Active + 12 Passive = 21개. 핵심 메카닉: Mark + Shadow Partner + Venom

**2차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Shuriken Burst (BA) | 주변 5타겟 40% x3회 |
| Gust Charm | 6타겟 400% + 기절 1.5초 |
| Mark of Assassin | 5초마다 5타겟 표식, 타격 시 300% |
| Critical Throw | 크리율+6%, 크뎀+10% |
| Shadow Surge | 즉시발동 전방 이동 (스킬 캔슬) |

**3차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Shuriken Challenge (BA) | 6타겟 80% x5회 |
| Shadow Partner | 25%확률 60% 추가뎀 |
| Triple Throw | 360% x3회 |
| Dark Flare | 20초간, 5초마다 5타겟 400% x2회 |
| Alchemic Adrenaline | 10회 스킬 발동 시 FD+10%, AS+8% 10초 |
| Venom | 30%확률 10초 독 (45%/초 DoT) |
| Expert Throwing Star | 공격력+18% |

**4차 전직 주요**
| 스킬명 | 효과 |
|--------|------|
| Showdown (BA) | 6타겟 290% x5회 |
| Quad Star | 1150% x4회 |
| Sudden Raid | 8타겟 1400% x3회 + 5초 DoT 360%/초 |
| Frailty Curse | 20초 결계, 적 받피+10%, 이속-10%, 자신 FD+15% |
| Night Lord's Mark | Mark 타겟+3, FD+300% |
| Shadow Shifter | 공격력+10%, 피격 시 20%확률 2500% 반격 |
| Toxic Venom | 독 상태 적 공격 시 20%확률 600% 추가뎀 |
| Maple Hero | Dark Flare FD+15%, Venom FD+50%, Shadow Partner FD+50%, Gust Charm FD+130% |

### 3-6. 공통 패턴

**기본공격(BA) 스케일링**
| 전직 | 타겟 수 | 데미지% | 타격 횟수 |
|------|--------|--------|----------|
| 1차 | 3 | 26% | 2회 |
| 2차 | 5 | 40% | 3회 |
| 3차 | 6 | 80% | 5회 |
| 4차 | 6 | 290% | 5회 |

**공통 패시브**
- 1차: Nimble Feet (AS+15%), 이동기
- 2차: Weapon/Spell Mastery (최소뎀+15%), Acceleration (AS+5%), Final Attack (25%, 35%)
- 3차: Endure (디버프 내성+15), 크리 관련
- 4차: Maple Hero (특정 스킬 FD), Advanced Final Attack (FA FD+500%), Expert Mastery (스킬뎀+15%, 최대뎀배율+20%)

---

## 4. 경험치 테이블 (Experience)

### 필요 EXP (발췌)
| 레벨 | 필요 EXP | 레벨간 배율 |
|------|---------|------------|
| 2 | 15 | - |
| 5 | 180 | ~1.5x |
| 10 | 13,396 | ~1.4x |
| 15 | 117,908 | ~1.47x |
| 20 | 681,980 | ~1.28x |
| 25 | 3,230,714 | ~1.28x |
| 30 | 8,951,974 | ~1.33x |
| 40 | 141,587,192 | ~1.29x |
| 50 | 4,242,128,285 | ~1.5x점프 |
| 60 | 54,362,868,145 | ~1.72x점프 |
| 70 | 254,606,497,760 | ~1.25x |
| 80 | 1,533,653,753,560 | ~1.35x점프 |
| 90 | 9,237,764,571,710 | ~1.35x점프 |
| 100 | 915,907,513,551,302 | ~x2.0점프 |
| 110 | 10,236,064,123,563,100 | ~x1.5점프 |
| 120 | 114,396,931,121,831,000 | ~x1.5점프 |
| 130 | 1,278,485,333,045,920,000 | ~x1.5점프 |
| 139 | 9,525,458,015,843,620,000 | |

### 레벨 구간별 배율 패턴
- Lv.1~9: x1.5
- Lv.10~19: x1.4~1.47
- Lv.20~29: x1.28
- Lv.30~39: x1.33~1.45
- Lv.40~49: x1.29
- Lv.50: x1.5 점프
- Lv.51~59: x1.25
- Lv.60: x1.72 점프 (3차 전직)
- Lv.70~99: x1.25 (80, 90에서 x1.35 점프)
- Lv.100: x2.0 점프 (4차 전직)
- Lv.110+: x1.25 (110, 120, 130에서 x1.5 점프)

### 오프라인 vs 온라인
- 온라인이 유리한 기준: 분당 50킬 이상 (멤버십 없으면 75킬)
- 슬립 모드 = 온라인 플레이로 취급
- 앱 완전 종료 = 오프라인

---

## 5. 장비 (Equipment)

### 장비 슬롯 (11종)
Cap, Clothes, Pants, Glove, Shoes, Cape, Belt, Shoulder, Ring, Pendant, Eye Accessory

### 장비 등급 (6단계, "Tier")
| Tier | 등급명 |
|------|--------|
| T1 | Normal |
| T2 | Rare |
| T3 | Epic |
| T4 | Unique |
| T5 | Legendary |
| T6 | Legendary+ |

### 장비 획득: Elite Monster Summoning
- 엘리트 몬스터 소환 → 처치 → 장비 드롭
- 소환 레벨 1~30 (레벨업 시 더 좋은 장비)
- 비용: Lv.1: 70 Gear Stone → Lv.30: 32,248
- 업그레이드 시간: 5초(Lv1) → 96시간(Lv30)
- Mesos: 1,500(Lv2) → 1,687,500(Lv30)

---

## 6. 장비 강화 (Equipment Enhancement)

3가지 강화 경로:
1. Scroll Enhancement (스크롤 슬롯 소비)
2. Star Force Enhancement (스크롤 후 메인/서브 옵션 강화)
3. Potential / Bonus Potential (잠재능력 큐브)

- 슬롯 강화 효과는 장비 교체 시에도 유지
- 13→14성: 하락 가능, 15→16성: 파괴 가능, 19성+: 완화 옵션

---

## 7. 무기 (Weapons)

### 등급 체계 (7단계 × T1~T4)
| 색상 | 등급 |
|------|------|
| Grey | Normal |
| Blue | Rare |
| Purple | Epic |
| Orange | Unique |
| Teal | Legendary |
| Red | Mystic |
| Dark Blue | Ancient (T4만) |

T4가 가장 약하고 T1이 가장 강함

### 무기 성장
- 레벨업: Weapon Enhancer 소비, 최대 Lv100 (각성 전)
- 각성(Awakening): 같은 무기 중복, 최대 5회, 회당 +20 레벨캡 (최종 Lv200)
- 승급(Promotion): 5성 각성 완료 복사본 5개 → 다음 등급 무기

### 레벨업 비용 (Weapon Enhancer 기본)
| 등급 | T4 | T3 | T2 | T1 |
|------|----|----|----|----|
| Normal | 10 | 12 | 14.4 | 17.28 |
| Rare | 40 | 48 | 57.6 | 69.12 |
| Epic | 140 | 168 | 201.6 | 241.92 |
| Unique | 490 | 588 | 705.6 | 846.72 |
| Legendary | 1,470 | 1,764 | 2,116.8 | 2,540.16 |
| Mystic | 5,880 | 7,056 | 8,467.2 | 10,160.64 |
| Ancient | 23,520 | - | - | - |

등급간 배율: Normal→Rare 4x, Rare→Epic 3.5x, Epic→Unique 3.5x, Unique→Legendary 3x, Legendary→Mystic 4x, Mystic→Ancient 4x
Tier간 배율: 항상 1.2x
레벨 비용 곱: Lv1~50: 1.01^(Lv-1), Lv51~100: 1.015^(Lv-50), Lv101~150: 1.02^(Lv-100), Lv151~200: 1.025^(Lv-150)

### 장착 공격력 (Base ATK %)
| 등급 | T4 | T3 | T2 | T1 |
|------|----|----|----|----|
| Normal | 15% | 18% | 21% | 25% |
| Rare | 31.3% | 39.1% | 48.9% | 61.1% |
| Epic | 76.4% | 95.5% | 119.4% | 149.3% |
| Unique | 194.1% | 252.3% | 328% | 426.4% |
| Legendary | 554.3% | 720.6% | 936.8% | 1,217.8% |
| Mystic | 1,619.7% | 2,154.2% | 2,865.1% | 3,810.6% |
| Ancient T4 | 5,144.3% | - | - | - |

등급간 ATK 스케일링: Normal~Epic 1.25x, Unique~Legendary 1.3x, Mystic 1.33x, Ancient 1.35x
레벨 공격력 공식: Lv1~100: BaseATT x (0.997 + 0.003 x Level)
인벤토리 효과: 장착 ATK의 1/3.5 (Normal~Unique), 1/4 (Legendary 이상)

---

## 8. 무기 뽑기 (Summon Weapons) — 확률 데이터

뽑기 수단: Weapon Summoning Ticket, Red Diamond (20개/1회), 광고

### 소환 레벨별 확률 (주요 발췌)

**Lv.1**:
- Normal T4~T1: 36.8% / 27.6% / 18.4% / 9.2%
- Rare T4~T1: 5.6% / 2.4% / 0 / 0
- Epic 이상: 0%

**Lv.5**:
- Normal: 30.94% / 23.20% / 15.47% / 7.74%
- Rare: 8.06% / 6.04% / 4.03% / 2.02%
- Epic: 0.92% / 0.69% / 0.46% / 0.23%
- Unique: 0.14% / 0.06%

**Lv.10**:
- Normal: 23.46% / 17.60% / 11.73% / 5.87%
- Rare: 13.88% / 10.41% / 6.94% / 3.47%
- Epic: 1.84% / 1.38% / 0.92% / 0.46%
- Unique: 0.72% / 0.54% / 0.36% / 0.18%
- Legendary: 0.12% / 0.07% / 0.04% / 0.01%

**Lv.15**:
- Normal: 15.75% / 11.82% / 7.88% / 3.94%
- Rare: 19.88% / 14.91% / 9.94% / 4.97%
- Epic: 2.84% / 2.13% / 1.42% / 0.71%
- Unique: 1.32% / 0.99% / 0.66% / 0.33%
- Legendary: 0.16% / 0.13% / 0.08% / 0.03%
- Mythic: 0.06% / 0.03% / 0.01% / 0.01%

**Lv.17**:
- Mythic T4~T1: 0.08% / 0.06% / 0.04% / 0.01%
- Ancient T4: 0.00%~

### 천장(Pity): 없음
대신 Promotion 시스템 (5각성 x 5복사본 = 상위 등급)

---

## 9. 동반자 뽑기 (Summon Companions)

뽑기 수단: Companion Summoning Ticket, Red Diamond (50개/1회)

### 소환 레벨별 확률
| 레벨 | Normal | Rare | Epic | Unique | Legendary |
|------|--------|------|------|--------|-----------|
| 1 | 97.0% | 3.0% | 0% | 0% | 0% |
| 3 | 92.0% | 7.0% | 1.0% | 0% | 0% |
| 5 | 84.9% | 13.0% | 2.09% | 0.01% | 0% |
| 8 | 78.05% | 19.5% | 2.3% | 0.15% | 0% |
| 9 | 77.425% | 20.0% | 2.4% | 0.17% | 0.005% |
| 10 | 76.8% | 20.5% | 2.5% | 0.19% | 0.01% |
| 13 | 75.21% | 22.0% | 2.5% | 0.25% | 0.04% |

---

## 10. 동반자 (Companions)

### 기본 구조
- 10종 (8직업 대응): Hero, Paladin, Dark Knight, I/L Mage, F/P Mage, Bishop, Bowmaster, Marksman, Night Lord, Shadower
- 소환 시 30초 활성, 90초 쿨다운
- 메인 1 + 서브 6 = 총 7슬롯

### 등급 = 전직 레벨
| 색상 | 등급 | 전직 |
|------|------|------|
| White | Common | 무직 |
| Blue | Rare | 1차 |
| Purple | Epic | 2차 |
| Orange | Unique | 3차 |
| Teal | Legendary | 4차 |

### 장착 보너스 (직업별)
- Hero: Flat ATK + Max Damage Multiplier
- Dark Knight: Flat ATK + Accuracy
- I/L Mage: Flat ATK + Normal Monster Damage
- F/P Mage: Flat ATK + Critical Rate
- Bowmaster: Flat ATK + Attack Speed
- Marksman: Flat ATK + Status Damage
- Night Lord: Flat ATK + Boss Damage
- Shadower: Flat ATK + Min Damage Multiplier

### 인벤토리 보너스 (패시브)
- No Job / 1st Job: Flat ATK + Flat Max HP
- 2nd Job: Flat Main Stat + Flat Max HP
- 3rd Job: 5% Damage + 10% Max HP
- 4th Job: 10% Damage + 15% Max HP

---

## 11. 아티팩트 (Artifacts)

Guide Quest 348 완료 시 해금.

### 전체 목록 (25종)

**Epic (4종)**
| 이름 | 효과 |
|------|------|
| Charm of the Undead | 10초마다 5초간 ATK +10~20% |
| Pig's Ribbon | 공격 시 20% 확률 HP/MP 1~2% 회복 (쿨 5초) |
| Shamaness Marble | 버프 지속시간 +6~12% |
| The Contract of Darkness | 보스 공격 시 크리율 +8~16% |

**Unique (7종)**
| 이름 | 효과 |
|------|------|
| Rainbow Snail Shell | 전투 시작 15초간 크리율 +15~30%, 크뎀 +20~40% |
| Hexagon Necklace | 20초마다 30초간 데미지 +15~30% (3중첩) |
| Arwen's Glass Shoes | 동반자 소환 시간 +20~40% |
| Mushmom's Cap | 명중 +5~10, 초과분당 데미지 +1~2% (최대 20~40%) |
| Clear Spring Water | [성장던전] FD +10~20% |
| Athena Pierce's Old Gloves | 공속 +8~16%, Max Damage Multiplier +공속의 25~50% |
| Zakum's Stone Piece | [레이드] FD +20~60%, 디버프 내성 +15~45 |

**Legendary (14종)**
| 이름 | 효과 |
|------|------|
| Chalice | 처치 시 2% 확률 FD +15~30% 30초 (보스는 100%) |
| Old Music Box | 디버프 시 1개 해제 + ATK +25~50% 25초 (쿨 20초) |
| Silver Pendant | 공격 시 15% 확률 피해증가 +10~20% 5초, HP 회복 -5~10% (5중첩) |
| Star Rock | 피해증가 +20~40%, Boss Damage +50~100% |
| The Book of Ancient | 크리율 +10~20%, 크뎀 +크리율의 30~60% |
| Lunar Dew | 피격 시 5% 확률 HP 3~6% 회복 + 무적 1~2초 (쿨 5초) |
| Fire Flower | 주변 적 1당 FD +1~2% (최대 10적) |
| Soul Contract | [챕터] 스킬 쿨 -20~40% |
| Lit Lamp | [월드보스] FD +20~40% |
| Soul Pouch | [아레나] FD +20~40% |
| Ancient Text Piece | [길드정복] FD +20~40% |
| Icy Soul Rock | MP 2초마다 1% 회복, MP 50%+ 시 크뎀 +20~40%, 75%+ 시 2배 |
| Flaming Lava | 버프 대상 FD +4~8%, 디버프 대상 +8~16%, 배리어 대상 +60~120% |
| Sayram's Necklace | 2적+: Normal Monster Damage +30~60%, 1적: Boss Damage +10~20% |

모든 아티팩트 6단계 레벨업 가능

---

## 12. 코스튬 (Costumes)

- Blue Diamond으로 구매
- 슬롯 10종: Weapon, Hat, Overall, Top, Bottom, Gloves, Cape, Shoes, Face Accessory, Eye Accessory
- 일부 스탯 부여
- Royal 코스튬: 번들 구매 전용, Bonus EXP 증가

---

## 13. 핫딜 (Hot Deals)

### 전직 핫딜 (6시간)
- 2차 전직: BD 3,000 + Weapon Ticket 300
- 3차 전직: BD 6,000 + Companion Ticket 250
- 4차 전직: BD 9,000 + Bonus Potential Cube 10

### 소환 레벨 핫딜 (12시간)
- 무기 Lv8: BD 9,000 → Weapon Ticket 1,000
- 무기 Lv11: BD 15,000 → Weapon Ticket 1,700
- 무기 Lv14: BD 30,000 → Weapon Ticket 3,400
- 무기 Lv16: BD 45,000 → Weapon Ticket 5,100
- 동반자 Lv3: BD 3,000 → Companion Ticket 140
- 동반자 Lv5: BD 15,000 → Companion Ticket 700
- 동반자 Lv6: BD 15,000 → 3차 전직 동반자 티켓 1장
- 동반자 Lv9: BD 30,000 → Companion Ticket 1,400
- 동반자 Lv11: BD 45,000 → Companion Ticket 2,100

### 잠재능력 핫딜 (12시간)
- Epic: BD 6,000 → Regular Potential Cube 16
- Unique: BD 15,000 → Cube 30 + Memorial Scroll 30
- Legendary: BD 30,000 → Cube 60 + Scroll 60
- Mystic: BD 45,000 → Cube 90 + Scroll 90

### 스타포스 핫딜 (12시간)
- Lv8: BD 6,000 → SF Scroll 30
- Lv11: BD 15,000 → SF Scroll 60 + 3M Mesos
- Lv15: BD 30,000 → SF Scroll 120 + 6M Mesos
- Lv20: BD 45,000 → SF Scroll 180 + 9M Mesos

### BD 누적 소비 핫딜 (6시간, 최대 30단계)
1M~50M BD 소비 마일스톤

---

## 14. 던전 (Dungeons)

매일 던전별 무료 키 3개. 가이드 퀘스트로 순차 해금. 총 5종.

### 14-1. 무기 던전 (Weapon Dungeon)
- 보상: Weapon Summoning Tickets, Weapon Enhancers
- 보스: Orange Mushmom
- 제한시간: 22초, 보스 슬램 쿨 9초
- 스테이지: 70단계
- 실패 시 티켓 반환

### 14-2. 경험치 던전 (EXP Dungeon)
- 보상: Experience
- 몬스터: Horny Mushroom, Evil Eye, Zombie Mushroom (처치 목표), Jr. Boogie (처치 시 +4초)
- 제한시간: 22초
- 스테이지: 90단계

### 14-3. 장비 던전 (Equipment Dungeon)
- 보상: Elite Monster Summoning Points
- 몬스터: Poopa, Poison Poopa (범위 폭발), Mask Fish (이속 증가)
- 제한시간: 30초
- 스테이지: 90단계

### 14-4. 강화 던전 (Enhancement Dungeon)
- 보상: Spell Traces, Enhancement Scrolls
- 보스: Jr. Balrog
- 제한시간: 30초
- 스테이지: 90단계, 10단계마다 난이도 급상승
- 해금: 가이드 퀘스트 131

### 14-5. 영웅 수련장 (Hero Training Ground)
- 보상: Hero Tokens

---

## 15. 맵 (Maps)

### 구조
- 31개 챕터 (Maple Island ~ Golden Temple)
- 챕터당 7~15개 맵 + 보스 맵

### 챕터별 맵 수 및 EXP (발췌)
| 챕터 | 맵 수 | 시작 EXP | 보스 전 최종 EXP |
|------|-------|----------|-----------------|
| Ch.1 Maple Island | 3+보스 | 2 | 14 |
| Ch.5 Kerning City | 9+보스 | 2,053 | 5,459 |
| Ch.10 Orbis | 9+보스 | 27,310 | 50,549 |
| Ch.15 Eos Tower | 9+보스 | 510,430 | 754,138 |
| Ch.20 Korean Folk Town | 9+보스 | 4,498,866 | 5,699,029 |
| Ch.25 Leafre Minar | 9+보스 | 17,012,895 | 21,551,426 |
| Ch.28 Herb Town | 9+보스 | 37,790,557 | 47,871,947 |
| Ch.29~31 | 각 15맵 (보스 없음) | ~485M | ~894M |

---

## 16. 사냥 & 오프라인 보상 (Hunting & Offline)

### 온라인 스폰
| 챕터 | 스폰 수 | 간격 |
|------|--------|------|
| 1-1 | 12마리 | 10초 |
| Ch.1~3 (1-1 제외) | 64마리 | 20초 |
| Ch.4~5 | 64마리 | 25초 |
| Ch.6~10 | 64마리 | 30초 |
| Ch.11+ | 64마리 | 35초 |

### 온라인 드롭률
| 아이템 | 확률 | 수량 |
|--------|------|------|
| 메소 | 7% (1/14.3) | 40 (Ch.1), 38+챕터# (Ch.2+) |
| 무기강화석 | 0.5% (1/200) | 94+3×챕터 (Ch.2+, 맵별 +1) |
| 엘리트 포인트 | 0.25% (1/400) | 10 (Ch.1), 8+챕터 (Ch.2+) |
| 명예의 메달 | 0.15% (1/667) | 10 (3-7~3-9), 6+챕터 (Ch.4+) |
| 레드 다이아 | 1% (1/100) | 1개 |
| 무기 소환 티켓 | 0.15% (1/667) | 1개 (Ch.2+) |
| 아티팩트 강화석 | 0.6% (1/167) | 20개 (Ch.11+) |

### 빠른 사냥 (Quick Hunt)
- 최종 맵 기준 지정 수량 처치 효과
- 초기 1,000마리, 사용마다 +5, 최대 3,000마리/회
- 길드 멤버 스킬: +50/100/150
- 부스터 미적용

### 오프라인
- 킬 속도: 51마리/분 고정
- 최소 5분 오프라인 필요
- 드롭: 결정론적 계산
- 프리미엄 멤버십: x1.5
- 앱 완전 종료 필요 (백그라운드 = 림보)

---

## 17. Hero Power & Ability

### Hero Power
- Hero Token 사용, 영웅 수련장에서만 획득
- Hero Power Stage: 최대 7단계
- 스테이지 1 올릴 때마다 어빌리티 슬롯 +1 (최대 6)

### Ability
- Medal of Honor 소비하여 리롤
- 6티어: Normal / Rare / Epic / Unique / Legendary / Mystic
- 프리셋: 최대 10개
- 슬롯 잠금 (추가 Medal 소비)
- 현재 옵션 저장: 500 Medal of Honor

### 어빌리티 옵션 수치
| 스탯 | Normal | Rare | Epic | Unique | Legendary | Mystic |
|------|--------|------|------|--------|-----------|--------|
| Main Stat | 40~60 | 100~150 | 200~300 | 400~700 | 800~1200 | 1500~2500 |
| Max HP | 1.2k~1.5k | 1.8k~3k | 4.5k~9k | 15k~30k | 35k~65k | 70k~115k |
| Damage | - | 3~5% | 7~10% | 12~15% | 18~25% | 28~40% |
| Crit Rate | - | - | 3~6% | 7~9% | 10~14% | 15~20% |
| Atk Speed | - | - | - | 7~9% | 10~14% | 15~20% |
| Dmg Taken Decrease | - | - | - | 2~3% | 4~6% | 7~10% |
| Defense Penetration | - | - | - | - | 8~12% | 14~20% |
| Boss/Normal Monster Dmg | - | - | - | - | 18~25% | 28~40% |

### 리롤 등급 확률 (Reconfiguration Level)
| Lv | Normal | Rare | Epic | Unique | Legendary | Mystic |
|----|--------|------|------|--------|-----------|--------|
| 1 | 60.00% | 35.00% | 4.70% | 0.30% | 0.00% | 0.00% |
| 5 | 30.00% | 32.00% | 34.30% | 3.50% | 0.20% | 0.00% |
| 8 | 25.00% | 32.00% | 38.33% | 3.65% | 1.00% | 0.02% |
| 15 | 25.00% | 32.00% | 37.93% | 3.30% | 1.63% | 0.14% |
| 20 | 25.00% | 32.00% | 37.63% | 3.05% | 2.08% | 0.24% |

### Reconfiguration 레벨업 비용 (Medal of Honor)
| Level | 필요 | 누적 |
|-------|------|------|
| 1 | 5,000 | 5,000 |
| 5 | 35,300 | 95,500 |
| 10 | 176,400 | 623,100 |
| 15 | 604,600 | 2,646,400 |
| 19 | 1,253,600 | 6,540,800 |

---

## 18. 가이드 퀘스트 (Guide Quest)

### 컨텐츠 해금 시점
| 퀘스트# | 해금 콘텐츠 |
|---------|------------|
| 8 | 스킬 |
| 13 | 무기 |
| 25 | 스킬 강화 |
| 33 | 스킬 마스터리 |
| 34 | 무기 던전 |
| 45 | EXP 던전 |
| 58 | 월드보스 |
| 67 | 장비 던전 |
| 75 | 빠른 사냥 |
| 79 | Hero Power |
| 89 | 부스터 |
| 96 | 동반자 |
| 113 | 어빌리티 |
| 131 | 강화 던전 |
| 140 | 길드 |
| 239 | 장비 강화 (Star Force) |
| 247 | 아레나 |
| 347 | 아티팩트 |

### 반복 사이클 퀘스트
맵돌파 → 적처치 → 엘리트소환 → 무기소환 → 동반자소환 → 월드보스 → 빠른사냥 → 무기던전 → 강화던전 → 경험치던전 → 장비던전 → 아레나 → 무기소환레벨 → 엘리트소환레벨

---

## 19. 패스 (Pass) — 7종

| 패스 | 유형 | 진행 기준 | 핵심 보상 |
|------|------|----------|----------|
| Battle Pass | 일일 리셋 | 매일 몬스터 처치 | Red Diamonds |
| Weapon Summoning Pass | 누적 | 무기 소환 횟수 | 유료: 무기 지급 |
| Weapon Summoning Pass 2 | 누적 | 무기 소환 | Mystic T4 (30k회), T3 (45k회) |
| Weapon Summoning Pass 3 | 누적 | 무기 소환 | Mystic T2 (55k회), T1 (70k회) |
| Companion Summoning Pass | 누적 | 동반자 소환 | 유료: 직업 동반자 소환 티켓 |
| Ability Pass | 누적 | Medal of Honor 사용 | 유료: Hero Tokens, Medal |
| Ability Pass 2 | 누적 | Medal of Honor | 20k Hero Tokens (52k), 최종 150k에 3000 |

무료/유료 이중 트랙

---

## 20. 월드보스 (World Boss)

- 보스: King Castle Golem
- 해금: 가이드 퀘스트 58
- 시간 내 최대 데미지 (랭킹)
- 보스 처치 시 스테이지 상승, 최종 스테이지 100% HP 유지 (처치 불가)
- 전용 화폐: World Boss Coin → World Boss Shop
- 프리미엄 멤버십 시 Chapter Hunt 동시 진행

---

## 21. 파티 퀘스트 (Party Quest)

- 해금: 챕터 7-5
- 인원: 1~4인

| PQ 이름 | 난이도 | 해금 | 보상 |
|---------|--------|------|------|
| First Time Together | Easy | Ch.7-5 | T2 Epic Squashy Ring |
| First Time Together | Hard | Ch.12-5 | T2 Unique Squashy Ring |
| First Time Together | Very Hard | Ch.19-5 | T4 Legendary Squashy Ring |
| Dimensional Crack | Easy | Ch.15-5 | T2 Unique Cracked Necklace |
| Dimensional Crack | Hard | Ch.20-5 | - |
| Dimensional Crack | Very Hard | Ch.25-5 | - |

---

## 22. 길드 (Guild)

해금: 가이드 퀘스트 140

| 구성 요소 | 내용 |
|-----------|------|
| Guild Help | 멤버 도움 요청 (연구 시간 -10분), 일 10회, 12시간 만료 |
| Guild Contribution | 주간 리셋 (월요일 자정) |
| Guild Skills | 3종: 멤버 스킬, 길드 마스터리 (월간), 길드 노블레스 (정복전) |
| Guild Buildings | 3종: Guild Base (레벨), Warehouse (보급/상점), Lab (연구 시간 단축) |
| Guild Content | Guild Conquest, Guild War |

---

## 23. 아레나 (Arena)

해금: 가이드 퀘스트 247

- PvP 1:1 대전
- 티켓: 매일 5장, 최대 20장
- 상대 갱신: 무료 3회 (10분마다 1회 복구), 이후 150 Red Diamond
- 밸런스: 데미지 & 포션 감소, 레벨 비례 피해 감소
- 보상: 랭킹 기반

---

## 24. 메달 (Medals)

- 인벤토리 패시브 (장착 불필요)
- Main Stat 합산 상한: 1,500
- 기타 효과 (보스뎀, FD 등) 상한 없음

### 메달 분류 (~40종)

**성장 메달** (각 Main Stat +30):
- 3차/4차 전직
- 잠재능력 Legendary/Mystic, 보너스 잠재 Legendary/Mystic
- 스타포스 15/18/20/22/23/24/25성
- Hero Power Stage 6/7
- 엘리트 레벨 20/30
- Unique/Legendary 동반자 8종+ 보유

**전투 메달** (각 Main Stat +30):
- 챕터 9 보스 클리어
- PQ 50회 클리어 (3종)
- 성장던전 50/70/100층
- 아레나 100/500/1000승

**보스 메달** (특수):
- Zakum Very Hard 5회: Boss Monster Damage +10%
- Guild Raid Zakum: Final Damage +3%

**기타** (각 Main Stat +30):
- 코스튬 30/100/200/300개
- 헤어/페이스 변경
- 명성 50/100/1000

---

## 25. 엘리트 몬스터 (Elite Monsters)

- 화면 좌하단 소환, Elite Summoning Points 소비
- 처치 시 장비 드롭

### 소환 레벨 & 티어 해금
| 소환 레벨 | 해금 등급 |
|-----------|----------|
| Lv.5 | Epic |
| Lv.13 | Unique |
| Lv.21 | Legendary |
| Lv.29 | Legendary+ |

### 등급 확률 (발췌)
- Lv.1: Normal T4 89.85%, Rare 0.30%
- Lv.10: 20개 등급 분산, Unique T4 0.35%
- Lv.21: Legendary T4 첫 등장 0.10%
- Lv.30: Legendary+ T4 0.25%, Legendary T1 0.40%

레벨업 필요 포인트: Lv.1: 60, Lv.10: 91, Lv.20: 133, Lv.30: 185

---

## 26. 타운 (Town)

- 몬스터 없는 휴식 공간, 타운에 있어도 사냥 자동 진행
- 3 구역: Henesys (퀘스트 NPC), Pet Walkway (파쿠르, 히든퀘스트 - 1000 타임 티켓), Henesys Park (NPC 2명)

---

## 27. 시스템 해금 요약

| 시스템 | 해금 조건 |
|--------|----------|
| Party Quest | 챕터 7-5 |
| Guild | 가이드 퀘스트 140 |
| Arena | 가이드 퀘스트 247 |
| Artifacts | 가이드 퀘스트 347 |
