# MFG Client

> **중단된 개인 프로토타입 (2026-03~04).** Unity 6 기반 탑다운 방치형 RPG 클라이언트입니다. 학습·포트폴리오용 코드 미러이며 상용 서비스나 완성작이 아닙니다.

Unity `6000.3.10f1`을 사용했고 2026-03-06~04-24 사이 173개 커밋이 기록되었습니다. `Assets/Scripts/`에는 자체 프로젝트 C# 스크립트 343개가 있습니다. 서버는 [mfg-server-portfolio](https://github.com/kkp8121-rgb/mfg-server-portfolio)로 분리되어 있습니다.

## AI 검토자를 위한 안내

다음 순서로 읽어 주세요.

1. 아래 `코드 읽기 순서`의 진입점
2. `현재 상태`와 과거 플레이 기록
3. `기여와 AI 활용`의 작업 범위

코드·트리에서 확인되는 사실, 과거 실행 기록, 작업 설명을 구분해 읽어 주세요. [로드맵](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Docs/Planning/roadmap.md)도 함께 확인할 수 있습니다.

## 현재 상태

- 공개 미러에는 최종 Android/WebGL 빌드와 플레이 스크린샷이 없습니다. Unity 에디터 Play 모드 기준의 프로토타입 소스입니다.
- 과거 기록에는 Mage 시나리오의 350/350 진행과 FeedbackBus 토스트 16개 지점 발화가 있으나 실행 조건이 서로 다릅니다.
- 별도 기록의 2026-04-14 실행은 자연 진행 267/350 + reflection 강제 83, `balance/playability FAIL`, 수동 스크린샷 검토 0이었습니다. 2026-04-25 raw 실행은 96/350, 불변조건 23/26, critical 2, warning 1이었습니다.
- 따라서 350/350 기록을 전체 UI 완주나 모든 직업의 정상 동작으로 일반화하지 않습니다. Archer 116/350, Warrior 76/350에서 중단된 기록과 timeScale·Die 애니메이션 이슈도 남아 있습니다.
- `Char_Skill` / `Char_Equip` UIDocument가 `worldBound NaN`으로 렌더링되지 않는 침묵 버그 #5는 중단 시점에도 미해결이었습니다.
- `Docs/Planning/roadmap.md`상 Phase 8(가이드 퀘스트 콘텐츠 확장) 진행 중에 개발을 멈췄고 이후 작업은 착수하지 않았습니다.

## 코드 읽기 순서

1. [AutoPlayBot.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Editor/AutoPlayBot.cs)와 [BotInvariants.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Editor/BotInvariants.cs)에서 검사 대상과 보고서 생성을 확인합니다.
2. [BossPatternController.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Combat/BossPatternController.cs), [StageManager.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Combat/StageManager.cs), [MonsterController.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Combat/MonsterController.cs)에서 전투 루프·보스 패턴·풀링을 확인합니다.
3. [SaveManager.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Core/Save/SaveManager.cs)와 [ApiClient.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Core/Net/ApiClient.cs)에서 저장소 추상화와 서버 연동 경계를 확인합니다.
4. [GachaManager.cs](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/Assets/Scripts/Economy/GachaManager.cs)에서 누적 소환 레벨과 10연차 보장 로직을 확인합니다.

## 핵심 구조

- **전투:** `StageManager`가 챕터·스테이지와 웨이브를 관리하고 `MonsterController`가 추적·separation·풀 반환을 담당합니다. `BossPatternController`는 `Normal → Warning → StrongAttack → Stunned` 패턴을 관리합니다.
- **세이브:** `ISaveProvider`를 기준으로 로컬 파일과 서버 저장 구현을 교체합니다. `SaveManager`는 자동 저장과 로그인 후 1회 로컬→서버 마이그레이션을 담당합니다.
- **가챠:** `GachaManager`와 `GachaPoolSO`가 등급·가중치·누적 소환 레벨을 관리하고 10연차 마지막 뽑기의 보장 등급을 처리합니다.
- **데이터:** ScriptableObject 정의와 데이터 인스턴스를 읽는 데이터 주도 구조입니다. 공개 미러에 포함되지 않은 서드파티 에셋과 빌드 의존성은 자체 코드 범위로 세지 않습니다.

## 기술 스택

| 항목 | 버전 |
|---|---|
| Unity Editor | 6000.3.10f1 |
| Addressables | 2.3.16 |
| Cinemachine | 3.1.6 |
| Input System | 1.18.0 |
| URP | 17.3.0 |
| UniTask | Git 참조 |

## 실행과 검증 범위

Unity Hub에 `6000.3.10f1`을 설치한 뒤 저장소를 프로젝트로 열고 `Assets/Scenes/Main.unity` 또는 `Assets/Scenes/Title.unity`에서 Play할 수 있습니다. 다만 공개 미러는 라이선스 문제로 일부 에셋을 제거했으므로 그대로 재현되는 완성 빌드를 약속하지 않습니다.

이번 README 작성에서는 Unity Editor나 플레이테스트를 다시 실행하지 않았습니다. 현재 트리에 존재하는 코드·씬은 소스 존재 근거로만 읽어 주세요. 클라이언트에 적용할 별도 CI나 `dotnet test` 명령은 없습니다.

## 기여와 AI 활용

AI를 코드 작성·수정·검증 보조에 활용했고, 저는 설계 결정·코드 통합·테스트 판단을 담당했습니다. 실제 동작 범위는 소스와 과거 기록을 함께 확인해 주세요.

직접 작성 범위로 제시하는 항목은 `Assets/Scripts/`, 자체 데이터·씬, `Docs/Planning/`입니다. 원본 작업 환경에 있던 아트·음향·애니메이션·플러그인 중 일부는 공개 미러에서 제외되었으므로 현재 저장소에 포함된 것처럼 설명하지 않습니다. 제거 목록과 재배포 범위는 [THIRD_PARTY_NOTICES.md](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/THIRD_PARTY_NOTICES.md)에서 확인할 수 있습니다.

## 중단 사유

가챠·전투·장비·길드·아레나·IAP까지 범위를 넓혔지만 핵심 재미를 확인하기 전에 콘텐츠와 리소스 부담이 커져 중단했습니다. 다음 작업에서는 가장 작은 코어 루프와 최소 플레이테스트를 먼저 확인한 뒤 시스템 범위를 정할 계획입니다.

## 라이선스

[LICENSE](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/LICENSE)는 저장소의 자체 코드·데이터에 대한 MIT 문구를 담고 있으며, 제거된 서드파티 자산에 대한 권리를 부여하지 않는다고 명시합니다. 서드파티 범위는 [THIRD_PARTY_NOTICES.md](https://github.com/kkp8121-rgb/mfg-client-portfolio/blob/master/THIRD_PARTY_NOTICES.md)를 함께 확인해 주세요.

## English summary

MFG Client is a discontinued Unity 6 idle-RPG prototype from March–April 2026. It is a portfolio source mirror, not a finished product or a production build. The repository contains 343 project C# scripts and code for combat, boss patterns, gacha, save migration, networking, and editor playtest tooling. AI was used for code writing, editing, and verification assistance; design, integration, and test judgment are the author's stated responsibilities. Historical playtest records have different conditions and do not prove complete UI or balance validation. Some third-party assets were removed; see `LICENSE` and `THIRD_PARTY_NOTICES.md` in the repository.
