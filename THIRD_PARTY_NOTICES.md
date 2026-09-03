# Third-Party Notices

이 저장소는 `kkp8121-rgb/mfg-client`(비공개, 173커밋)의 히스토리 없는 포트폴리오 스냅샷입니다.
공개 전 검수 보고서(`mfg-publish-review.md`, 소유자 보관)에서 라이선스가 불명확하거나 유료/재배포
제한이 있는 것으로 식별된 자산은 이 미러에서 전부 제거되었습니다.

## 제거된 서드파티 폴더/파일 (원본 → 이 미러에는 없음)

| 원본 경로 | 추정 출처 | 사유 |
|---|---|---|
| `Assets/Folder_Assets/DAX` | DAX "Magic Packs Vol1" (Asset Store) | 유료 VFX 추정, 라이선스 파일 미동봉 |
| `Assets/Folder_Assets/BGM` | AlkaKrab / PGS Fantasy RPG / TomMusic / Legendary_JRPG_Battle_Music_Pack | PGS는 "DO NOT REPOST" 명시, 나머지 재배포 허가 문구 부재 |
| `Assets/SPUM`, `Assets/Folder_Assets/SPUM`, `Assets/Resources/Prefabs/SPUM` | SPUM(Soonsoon), Asset Store 유료 Standard License | "Asset Store 정식 구매만 인정" 명시 |
| `Assets/Folder_Assets/500_skill_icons` | 스킬 아이콘 500종 | 라이선스 파일 없음, 출처 검증 불가 |
| `Assets/Folder_Assets/SFX`(TomMusic) | TomMusic Free SFX (itch.io) | 재배포 허가 문구 부재 |
| `Assets/Folder_Assets/Admurin's Pixel Items` | Admurin's Pixel Item, Asset Store 유료 추정 | 라이선스 미동봉 |
| `Assets/GUI Kit The Stone`, `Assets/Folder_Assets/GUI_Kit_Stone`, `Assets/Folder_Assets/GUI Kit - Dark Geo` | "GUI Kit" 시리즈, Asset Store 유료 추정 | PDF만 존재, 유료 시리즈 추정 |
| `Assets/Folder_Assets/craftpix-net-501088`, `-825597`, `Icons/PaladinSkills`, `VFX_Packs/Common/MagicEffects` | CraftPix.net Free 라이선스 4종 | CraftPix 표준 라이선스가 원본 파일 재배포/재공유를 명시적으로 금지 |
| `Assets/Folder_Assets/VFX_Packs`(CodeManu 하위 제외) | Frostwindz License Agreement 다수, Gothicvania Magic Pack(license.pdf) | 상업 제한 조항 추정(본문 미검증) |
| `Assets/Fonts/KoreanFont.ttf`(2곳) | 출처 미상 | 상용 폰트 무단 포함 가능성 배제 불가 |
| `Assets/Prefabs/VFX/Skills/DAX_*.prefab` (47개, 프로젝트 자체 검수로 추가 발견) | DAX 패키지의 프리팹 정의를 그대로 프로젝트 자체 `Prefabs/` 경로에 배치한 것으로 판단 | 파일명이 전부 `DAX_` 접두사 — 검수 보고서의 "51개 자체 Prefab" 집계에는 이 47개가 포함돼 있었으나, 실제로는 DAX 자산의 프리팹 정의이므로 이번 미러링 작업에서 별도로 식별해 제외함. **자체 작성 Prefab은 `Assets/Prefabs/Combat/` 4개(Monster_Bandit/DarkMage/SkeletonArcher/Slime)뿐** |
| `Assets/Scenes/SpumBrowser.unity` | SPUM 에셋 브라우징용 개발 씬 | SPUM 종속 + 8.8MB(단일 파일 5MB 초과 규칙 적용) |
| `Assets/Resources/Audio`(174MB), `Resources/UI/DarkGeo`, `Resources/UI/Stone`, `Resources/Icons/DarkGeo`, `Resources/Icons/Stone`, `Resources/VFX/GachaSummon` 등 | 위 BGM/SFX/GUI Kit/SPUM 계열 리소스의 런타임 로드용 사본으로 추정 | `Assets/Resources/**`는 "자체 제작 + 소용량"인 경우만 포함하는 원칙에 따라 제외. 예외적으로 포함한 것은 `Resources/Data/**`(3.2MB, 자체 SO 데이터)와 `Resources/CharacterDesigns/**`(18KB, 자체 JSON)뿐 |
| `Assets/Plugins/**`(Demigiant DOTween/DOTween Pro, Lean.Pool 등), `Assets/TextMesh Pro`, `Assets/Joystick Pack` | DOTween(무료+유료 Pro 혼재), Lean.Pool(CW), Unity 공식 TextMesh Pro, Fenerax Studios Joystick Pack(무료) | 검수 보고서는 이들을 LOW~OK로 분류했으나, 이 미러는 "코드 열람용, 서드파티 플러그인 재배포 최소화" 원칙에 따라 보수적으로 전부 제외 |
| `Docs/Reference/MapleIdle/screenshots/`(90개 PNG, 100MB) | 경쟁작(MapleIdle류) 레퍼런스 캡처 | 자체 저작물이 아닌 타사 게임 스크린샷 + 저장소 용량(<50MB 목표)의 대부분을 차지해 제외. 텍스트 분석 문서(`Docs/Reference/MapleIdle/ANALYSIS.md`, `FLOW_TREE.md`)는 유지 |
| `BalanceTestKit-v1.0.0/`, `.mcp.json`, `CLAUDE.md`, `Ref_Example/`, `Screenshots/`, `Tools/`, `fastlane/`, `testGif/`, 루트 GIF/슬루션 파일 등 | 저장소 루트의 기타 폴더 (제3자 자산 여부 무관) | 이번 미러링 작업 지시 범위(Scripts/Data/Prefabs/Scenes/Resources 일부/Packages manifest/ProjectSettings/.gitignore/Docs)에 명시적으로 포함되지 않아 제외 |

## 유지된 항목

- `Assets/Scripts/**` — 자체 작성 C# 스크립트 343개
- `Assets/Data/**`, `Assets/ScriptableObjects/**` — 자체 ScriptableObject 데이터 정의/인스턴스
  - 단, `Assets/Data/SO/VFX_Imported/*.asset` 일부 항목은 내부 카탈로그 필드에 제거된 서드파티 VFX 팩(예: Gothicvania Magic Pack, CodeManu)의 원본 파일명을 참조 문자열로 담고 있습니다. 실제 아트/바이너리는 포함되어 있지 않으며 CodeManu 계열은 Public Domain으로 알려져 있으나, 나머지 이름이 남아있다는 점은 소유자 재확인을 권장합니다.
- `Assets/Prefabs/Combat/*.prefab`(4개) — 자체 몬스터 프리팹
- `Assets/Scenes/{Main,Main_backup_2026-04-20,SampleScene,Title}.unity`
- `Assets/Resources/Data/**`, `Assets/Resources/CharacterDesigns/**`
- `ProjectSettings/**`(텍스트), `Packages/manifest.json`, `Packages/packages-lock.json`

## `Packages/manifest.json`에 선언된 패키지 (코드/바이너리는 이 저장소에 포함되어 있지 않음 — Unity Package Manager가 오픈 시 별도 리졸브)

패키지 캐시(`Library/PackageCache`)가 원본 클론에 존재하지 않아 각 패키지의 정확한 라이선스 파일을 로컬에서 확인하지 못했습니다. 아래는 전부 "라이선스 확인 필요"로 표기합니다 — 공개 전 각 패키지 공식 저장소/Asset Store 페이지에서 재확인 권장.

| 패키지 | 참조 | 라이선스 |
|---|---|---|
| `com.coplaydev.unity-mcp` | GitHub (CoplayDev/unity-mcp) | 라이선스 확인 필요 |
| `com.cysharp.unitask` | GitHub (Cysharp/UniTask) | 라이선스 확인 필요 |
| `com.ditto.balance-test-kit` | 로컬 파일 참조(`file:../BalanceTestKit-v1.0.0`) — 해당 폴더는 이 미러에 포함되지 않아 리졸브 불가 | 라이선스 확인 필요 |
| `com.unity.*` (2d.animation, addressables, cinemachine, inputsystem, render-pipelines.universal, timeline, visualeffectgraph 등 Unity 공식 패키지 전체) | Unity Package Manager 레지스트리 | 라이선스 확인 필요 |
| `com.unity.modules.*` (엔진 내장 모듈) | Unity Editor 번들 | 라이선스 확인 필요 |

## 참고

- 원본 173커밋의 전체 커밋 이력은 이 미러에 포함되지 않았습니다(비공개 원본 저장소에만 존재).
- 위 제거 목록으로 인해 이 저장소는 그대로 빌드되지 않습니다 — `README.md`의 안내를 참고하세요.
