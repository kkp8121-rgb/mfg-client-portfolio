---
read_count: 5
last_read: "2026-04-07"
status: active
---

# MFG 서버 아키텍처

> 상세 인프라 비용/비교: `indie-game-dev-infra-guide.md` 참조

---

## 핵심 설계 원칙

1. **서버 = 유일한 진실의 원천** — 재화/가챠/PvP/오프라인 보상은 서버 권한
2. **REST API only** — idle 게임에 WebSocket 불필요. stateless, 수평 확장 용이
3. **서버 시간 권위** — 오프라인 보상은 서버 시간 기준 (클라 시계 조작 방지)
4. **모듈러 모놀리스** — 단일 프로세스 시작, 필요 시 서비스 분리
5. **Optimistic Update** — 클라 즉시 UI 반영 → 서버 응답으로 확정/롤백

## 데이터 분류 (SaveData 기준)

| 분류 | 시스템 | 이유 |
|------|--------|------|
| **SERVER_REQUIRED** | Currency(11종), Gacha Pity, Player Level, Progress, Offline Rewards, Equipment Enhancement, Weapon, Relic/Artifact, Arena, Guild, Battle Pass, Attendance, Dungeon Keys, Event Dungeon, Mail, Booster, IAP | 치트 가능 (재화 조작, 시간 점프, 랭킹 조작 등) |
| **SERVER_SYNC** | StatData, equippedSlots, mastery nodes, ability nodes, inscriptions, skill levels, pet equipped | 파생값/편의. 검증 불필요하나 동기화 필요 |
| **CLIENT_ONLY** | Costume, Tutorial, Achievement, Skill Mastery | 순수 외형/표시용. 치트 무의미 |

---

## 스택

| 계층 | 기술 | 비고 |
|------|------|------|
| 런타임 | .NET 8 (ASP.NET Core Web API) | Unity C#과 언어 통일 |
| DB | MySQL 8.0 (EF Core, MySql.EntityFrameworkCore) | 서버 내 설치, 무료 |
| 인증 | Firebase Auth → JWT | Google/Apple 소셜 로그인 |
| 호스팅 | AWS Lightsail ($5/월) | Ubuntu, 고정 IP, Caddy SSL. 서울 리전 |
| 파일 | Cloudflare R2 | 패치 리소스, 이벤트 배너 |
| 푸시 | FCM | 리텐션 알림 |

## 프로젝트 구조

```
server/
├── MFG.Server.sln
├── src/
│   ├── MFG.Server/              ← ASP.NET Core Web API (진입점)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/            ← API 컨트롤러
│   │   ├── Middleware/             ← Firebase JWT 검증 미들웨어
│   │   ├── Services/              ← 비즈니스 로직
│   │   └── DTOs/                  ← Request/Response 모델
│   │
│   ├── MFG.Domain/              ← 도메인 엔티티 + 인터페이스
│   │   ├── Entities/              ← Player, Currency, Equipment 등
│   │   ├── Enums/                 ← CurrencyType, GachaPoolType 등 (클라와 공유)
│   │   └── Interfaces/            ← IPlayerRepository 등
│   │
│   └── MFG.Data/                ← EF Core DbContext + 마이그레이션
│       ├── AppDbContext.cs
│       ├── Migrations/
│       └── Repositories/
│
├── tests/
│   └── MFG.Server.Tests/        ← xUnit 테스트
│
├── docker-compose.yml               ← MySQL 8.0 로컬 개발
├── Dockerfile                        ← 배포용
└── .gitignore
```

## API 컨벤션

- **Base URL**: `https://api.mfg.game/api/v1/`
- **인증**: `Authorization: Bearer {firebase_id_token}` 헤더
- **응답 형식**: JSON, 공통 래퍼 `{ success: bool, data: T, error: string }`
- **에러 코드**: 400 (잘못된 요청), 401 (인증 실패), 403 (권한 없음), 409 (충돌), 500 (서버 오류)
- **네이밍**: `camelCase` (JSON), `PascalCase` (C# 내부)

## 클라이언트 ↔ 서버 통신

```
[Unity 클라이언트]
  → Firebase Auth SDK 로그인 → ID Token 획득
  → API 요청: POST /api/v1/gacha/pull (헤더: Bearer {token})
  → 서버: JWT 검증 → 비즈니스 로직 → DB 저장 → 응답
  → 클라이언트: 응답 처리 → UI 업데이트
```

## 점진적 이행 전략

| 단계 | 서버 이행 대상 | 클라이언트 변경 |
|------|---------------|----------------|
| Phase 1 | 가챠, 재화, 결제, 진행도 저장, 출석 | LocalSaveProvider → ServerSaveProvider |
| Phase 2 | 아레나, 길드 | 로컬 시뮬레이션 → API 호출 |
| Phase 3 | 장비 강화, 이벤트 보상 | 로컬 판정 → 서버 판정 |

## 인프라 비용 전략

| 단계 | 인프라 | 월 비용 |
|------|--------|---------|
| 출시 초기 (DAU 100↓) | Lightsail $3.5 + 서버 내 PG + 블로그 서브도메인 | **~$4** |
| 성장기 (DAU 1,000) | Lightsail $5 + 서버 내 PG | **~$6** |
| 확장기 (DAU 10,000) | Lightsail $20 + RDS $15 | **~$36** |

**이관 원칙:** Docker 컨테이너 기반. `docker-compose up -d` + DB 덤프로 30분 내 이관.
**벤더 종속 회피:** 표준 SQL, 환경변수 외부화, S3 호환 API, stdout 로그.
**대안:** Oracle Cloud Always Free ($0) — 스펙 좋지만 셋업 복잡. Docker 기반이므로 언제든 전환 가능.

## 배포 파일

- `server/Dockerfile` — 멀티스테이지 빌드 (SDK → Runtime)
- `server/docker-compose.yml` — 로컬 개발용 (MySQL 8.0, port 3306)
- `server/docker-compose.prod.yml` — 프로덕션 (API + DB + Caddy SSL)
- `server/Caddyfile` — 리버스 프록시 + Let's Encrypt 자동 SSL
- `server/.env.example` — 환경변수 템플릿
