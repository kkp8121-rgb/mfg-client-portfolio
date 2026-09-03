---
read_count: 2
last_read: "2026-04-07"
status: reference
---

# MFG 인증 플로우

---

## 전체 흐름

```
[클라이언트]                          [서버 (.NET 8)]                    [Firebase]
    │                                      │                                │
    ├─── Firebase Auth SDK 로그인 ─────────────────────────────────────────▶│
    │    (Google/Apple 소셜 로그인)         │                                │
    │◀── Firebase ID Token 발급 ───────────────────────────────────────────│
    │                                      │                                │
    ├─── POST /api/v1/auth/login ─────────▶│                                │
    │    (헤더: Bearer {idToken})           │                                │
    │                                      ├─── ID Token 검증 ─────────────▶│
    │                                      │◀── 검증 결과 (uid, email) ────│
    │                                      │                                │
    │                                      ├─── players 테이블 조회/생성    │
    │                                      ├─── 초기 재화 지급 (신규)       │
    │                                      │                                │
    │◀── 200 OK (playerData) ──────────────│                                │
    │                                      │                                │
    ├─── 이후 API 호출 ───────────────────▶│                                │
    │    (헤더: Bearer {idToken})           ├─── JWT 미들웨어 자동 검증     │
    │◀── 200 OK ───────────────────────────│                                │
```

## 서버 JWT 미들웨어

```csharp
// Program.cs에서 Firebase JWT 검증 설정
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/{PROJECT_ID}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/{PROJECT_ID}",
            ValidateAudience = true,
            ValidAudience = "{PROJECT_ID}",
            ValidateLifetime = true
        };
    });
```

## 플랫폼별 차이

### Android
- `com.google.firebase.auth` Unity 패키지 사용
- Google Sign-In → Firebase Auth → ID Token

### WebGL
- Firebase JS SDK 브릿지 필요 (Unity WebGL에서 네이티브 SDK 사용 불가)
- `jslib` 플러그인으로 JS → C# 브릿지
- 또는 REST API 직접 호출 (`identitytoolkit.googleapis.com`)

### 공통
- ID Token은 1시간 유효 → 만료 전 `User.TokenAsync(true)` 갱신
- 서버는 토큰 검증만 수행 (세션/쿠키 불필요 — stateless)

## 신규 유저 초기화

서버 `/auth/login` 시 `firebase_uid`로 players 조회:
- 없으면: 새 레코드 생성, 초기 재화 지급 (Gold 10000, Ruby 10000 등)
- 있으면: `last_login_at` 업데이트, 기존 데이터 반환

## 보안 원칙

- 클라이언트는 **Firebase ID Token**만 전달 (비밀키 노출 없음)
- 서버는 **Firebase Admin SDK** 또는 **JWT 공개키 검증**으로 토큰 유효성 확인
- 모든 API는 인증 필수 (`[Authorize]` 어트리뷰트)
- 유저는 자신의 데이터만 접근 가능 (미들웨어에서 `firebase_uid` 추출 → 본인 확인)
