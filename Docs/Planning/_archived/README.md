# mkLike 기획 문서

이 폴더는 mkLike 프로젝트의 모든 기획 문서를 관리합니다.

## 폴더 구조

```
Docs/Planning/
  README.md               <- 이 파일
  core-design.md           <- 핵심 게임 디자인 (세계관, 루프, 비전)
  guideline.md             <- 개발 가이드라인 및 컨벤션
  roadmap.md               <- 7단계 개발 로드맵 + 태스크 추적
  systems/                 <- 시스템별 상세 기획서
    combat-formula.md      <- 전투 공식 (데미지, CP, 스케일링)
    character-state-machine.md <- 캐릭터 상태 머신
    monster-system.md      <- 몬스터 AI, 스폰, 보스
    character-spum-guide.md <- SPUM 에셋 활용 가이드
    character-growth.md    <- 캐릭터 성장 (레벨, 스탯, 전직, 스킬)
    equipment.md           <- 장비 (장착, 주문서, 스타포스, 큐브)
    weapon.md              <- 무기 (소환, 각성, 승급)
    companion.md           <- 동료 (소환, 메인/서브, 장착효과)
    relic.md               <- 유물 (등급별 효과, 각성)
    dungeon.md             <- 성장 던전 (5종 + 보스/레이드)
    economy.md             <- 재화/경제 시스템 (12종 재화)
    quest.md               <- 퀘스트/미션/업적
  balance/                 <- 밸런스 시트
    damage-formula.md      <- 데미지 공식
    growth-curve.md        <- 성장 곡선
    drop-table.md          <- 드롭 테이블
    gacha-rates.md         <- 가챠 확률표 (무기/동료)
    starforce-table.md     <- 스타포스 확률표
    cube-rates.md          <- 큐브 등급 승급 확률
```

## 문서 상태

| 문서 | 상태 | 최종 수정 |
|------|------|-----------|
| core-design.md | v3 (탑다운 전환) | 2026-03-08 |
| guideline.md | v3 (탑다운 전환) | 2026-03-08 |
| roadmap.md | v3 (Phase 2 탑다운 재설계) | 2026-03-08 |
| work-log-2026-03-07.md | 작업 로그 (Phase 2 재설계 상세) | 2026-03-07 |
| systems/stage-combat.md | v3 (탑다운 아레나 재설계) | 2026-03-08 |
| systems/combat-formula.md | 초안 완료 | 2026-03-07 |
| systems/character-state-machine.md | 초안 완료 | 2026-03-07 |
| systems/monster-system.md | 초안 완료 | 2026-03-07 |
| systems/character-spum-guide.md | 초안 완료 | 2026-03-07 |
| systems/character-growth.md | 미작성 | - |
| systems/equipment.md | 미작성 | - |
| systems/weapon.md | 미작성 | - |
| systems/companion.md | 미작성 | - |
| systems/relic.md | 미작성 | - |
| systems/dungeon.md | 미작성 | - |
| systems/economy.md | 미작성 | - |
| systems/quest.md | 미작성 | - |
| balance/* | 미작성 | - |

## 레퍼런스

- **메이플 키우기**: 육성/컨텐츠 시스템 전반 (직업 제외)
- **뱀서라이크**: 전투 방식 차용 (몰려오는 몬스터, 탑다운 시점). 로그라이크/랜덤 스킬 선택 없음.
- **직업 시스템**: mkLike 오리지널 (전사/궁수/마법사 × 3차 전직)

## PM 연동

- PM 에이전트는 `roadmap.md`의 태스크 목록을 기준으로 진행 상황을 추적합니다
- 각 Phase의 태스크는 `[대기]`, `[진행중]`, `[완료]`, `[블로커]` 상태를 가집니다
- 기획 변경 시 변경 이력을 해당 문서 하단에 기록합니다
