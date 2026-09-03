---
read_count: 8
last_read: "2026-04-15"
status: active
---

# MFG 출시 사전 준비 체크리스트 (수동 작업)

> 코드네임 `mkLike` → 출시 브랜드 **MFG** (Mobile Fantasy Game)
> 패키지 prefix: `com.mfg.*` · Firebase Project: `mfg-prod-4cb30` / `mfg-dev-187b4` · 도메인: `mf-game.com` · 표시명: `MFG`
>
> 완료 시 [x] 체크. 결과물(ID, 키 파일)은 Claude에게 전달 → 코드 반영.
> **⚠ 키 파일은 절대 git 커밋 금지.** (하단 보안 체크리스트 참조)

## 브라우저 작업은 별도 문서

> 크롬 Claude 익스텐션에 붙여 넣어 자동 실행할 작업 스크립트는 **`server-manual-setup-web.md`** 에 있다.
> 이 문서(`server-manual-setup.md`)는 **사람이 상태를 추적하는 대시보드**이며, 결제/로컬 CLI/의사결정만 직접 처리한다.

## 진행 현황 스냅샷 (2026-04-15)

**완료 ✅**: Firebase prod/dev 생성 · Auth 3종 · Android/iOS/Web 앱 등록 · Analytics prod(532833082)+dev(532888431) · Crashlytics prod+dev · firebaseConfig 추출 · 서비스 계정 키 2종 배치 · GCP OAuth 브랜딩 · Google Play Android Developer API · Pub/Sub `mfg-iap-notifications`+Publisher 권한 · AdMob 가입 + 광고 단위 16개 · Cloudflare 도메인 + R2 버킷+API 토큰

**범위 변경 ❌**: WebGL 출시 계획 취소 (2026-04-15) → Cloudflare Pages `mfg-webgl`, `play.mf-game.com` 서브도메인 모두 불필요. Android + iOS만 배포.

**블로커 🔥**: **Google Play Developer 신규 계정 등록 필요** (기존 BHS_000 계정 2024-04-08 정책 위반 해지됨 — 재등록 전 정책 재검토)

**미완료 주요 작업**:
1. Android debug SHA-1 등록 (Firebase) — `keytool` 실행 필요
2. AWS Lightsail 인스턴스 생성 + 고정 IP → Cloudflare `api.mf-game.com` A 레코드
3. Google Play Dev 재등록($25) · Apple Dev 등록($99/y)
4. 로컬 환경: Docker Desktop + EF Core CLI 설치
5. 서버 코드 개발 착수 (Sprint 18-2~18-6)

**🔜 DEFERRED (추후 작업 — 2026-04-16 결정)**:
- Phase 11 (모니터링 + 알림) 전체: Uptime Robot, Slack Workspace/Webhook, AWS Chatbot
- 사유: 출시 전 필수 아님. 서버 인프라/IAP/심사 흐름 선행.
- 재개 조건: Lightsail 배포 + `api.mf-game.com` 가동 완료 후 별도 세션

---

## 전체 타임라인 한눈에

```
[P0] 로컬 개발환경 (1h)
  └→ [P1] Firebase 프로젝트 (1h)
        └→ [P2] 서버 개발 (Claude 작업, 수 주)
              └→ [P3] 앱 서명키 + 법적자산 (2h) ← 출시 1개월 전
                    └→ [P4] Google Play + GCP (2h)
                          └→ [P5] Apple Dev + App Store Connect (3h)
                                └→ [P6] IAP 상품 등록 + 영수증 검증 (3h)
                                      └→ [P7] AdMob 광고 (1h)
                                            └→ [P8] Cloudflare (1h)
                                                  └→ [P9] AWS Lightsail (2h)
                                                        └→ [P10] 스토어 자산 + 심사 (반복)
                                                              └→ [P11] 모니터링 (30m)
                                                                    └→ 🚀 출시
```

---

## Phase 0: 로컬 개발 환경 (즉시)

- [ ] **Docker Desktop 설치** — https://docker.com/products/docker-desktop
  - 설치 후 확인: `docker --version`
  - 용도: `docker-compose up -d`로 로컬 MySQL 8.0 실행
- [ ] **EF Core CLI 설치**:
  ```
  dotnet tool install --global dotnet-ef
  ```
  - 확인: `dotnet ef --version`
- [ ] **.NET 8 SDK** 설치 (Unity는 별개) — https://dotnet.microsoft.com/download/dotnet/8.0

---

## Phase 1: Firebase (인증 + 푸시 + 분석)

### 1-1. 프로젝트 생성
- [x] https://console.firebase.google.com 로그인
- [x] "프로젝트 추가" → 이름: **`mfg-prod`** (출시용)
- [x] "프로젝트 추가" → 이름: **`mfg-dev`** (개발용, 프로덕션 오염 방지) — Project ID: `mfg-dev-187b4`
- [ ] Google Analytics 연결 (무료, 권장)
- [x] **Project ID 기록**: prod=`mfg-prod-4cb30` (project_number: 188303570676), dev=`mfg-dev-187b4`

### 1-2. Authentication 활성화 (prod + dev 양쪽)
- [x] Build → Authentication → "시작하기"
- [x] Sign-in method 탭:
  - [x] **Google** 활성화 (지원 이메일: <owner-email>)
  - [x] **Apple** 활성화 (iOS 출시 시 필수, 미활성 시 App Store 반려)
  - [x] **익명(Anonymous)** 활성화 (게스트 모드 지원 시)
- [x] 승인된 도메인에 `mf-game.com` 추가 (양쪽 프로젝트)

### 1-3. 앱 등록 — Android
- [x] 프로젝트 설정(⚙) → "앱 추가" → **Android**
- [x] 패키지명: `com.mfg.game` (App ID: `1:188303570676:android:95d89aad049aff8b2510a1`)
- [ ] 디버그 SHA-1 등록 (Google Sign-In 필수):
  ```
  keytool -list -v -alias androiddebugkey -keystore "%USERPROFILE%\.android\debug.keystore" -storepass android
  ```
- [x] `google-services.json` 다운로드 → `client/Assets/google-services.json` (gitignore 적용됨)

### 1-4. 앱 등록 — iOS
- [x] "앱 추가" → **iOS**
- [x] Bundle ID: `com.mfg.game` (App ID: `1:188303570676:ios:764bfc8f31a3df392510a1`)
- [x] `GoogleService-Info.plist` 다운로드 → `client/Assets/GoogleService-Info.plist` (gitignore 적용됨)

### 1-5. 앱 등록 — 웹 (WebGL)
- [x] "앱 추가" → **웹** (양쪽 프로젝트 등록 완료)
- [ ] `firebaseConfig` 객체 메모 (WebGL JS 브릿지용) — Unity WebGL 빌드 시 필요

### 1-6. FCM 푸시 설정
- [ ] **Android**: 자동 (`google-services.json`만으로 동작)
- [ ] **iOS**: APNs 인증키 필요 (Phase 5-2 완료 후):
  - Apple Developer → Keys → APNs 키(.p8) 발급 → Firebase Cloud Messaging → iOS 앱 → APNs 인증키 업로드
  - **Team ID, Key ID 기록**

### 1-7. Crashlytics + Analytics
- [x] Build → Crashlytics → "시작하기" (mfg-prod 활성화 확인)
- [ ] Unity SDK: `com.google.firebase.crashlytics` 설치 (Claude 작업)

### 1-8. 서버용 서비스 계정 키
- [x] 프로젝트 설정 → **서비스 계정** 탭 → "새 비공개 키 생성"
- [x] `serviceAccountKey.json` 다운로드 → `server/src/MkLike.Server/serviceAccountKey.json`
  - 서비스 계정: `firebase-adminsdk-fbsvc@mfg-prod-4cb30.iam.gserviceaccount.com`
  - (서버 리브랜딩 시 `MFG.Server/`로 이동)
- [x] **⚠ git 커밋 금지** — `server/.gitignore`의 `serviceAccountKey.json` 규칙으로 차단됨

### 결과물 → Claude 전달
```
Firebase Project ID (prod): mfg-prod-4cb30   ✅
Firebase Project ID (dev):  mfg-dev-187b4    ✅
Android 패키지명:           com.mfg.game    ✅
iOS Bundle ID:              com.mfg.game    ✅
APNs Team ID / Key ID:      (Phase 5-2 이후)
Android API Key:            <redacted>
iOS API Key:                <redacted>
iOS Reversed Client ID:     com.googleusercontent.apps.188303570676-gbuevn23o6sdqqtblbqfabdmbut0q2q3
```

---

## Phase 2: 서버 개발 (Claude 작업)

Claude가 Sprint 18-2 ~ 18-6 코드 구현.
사용자는 이 기간 Phase 3 이후 병행 진행 가능.

---

## Phase 3: 앱 서명 키 + 법적 자산 (출시 1개월 전 🔥)

### 3-1. Android Release Keystore 생성 (최우선)
- [ ] 생성:
  ```
  keytool -genkey -v -keystore mfg-release.keystore ^
    -alias mfg -keyalg RSA -keysize 2048 -validity 25000
  ```
- [ ] Keystore 비밀번호 + Alias 비밀번호 2개를 1Password 등에 저장
- [ ] `mfg-release.keystore` 파일을 **3곳 이상** 백업 (로컬 + 클라우드 2곳)
- [ ] **⚠⚠⚠ 이 키 분실 시 앱 업데이트 영구 불가.**
  - Phase 4에서 **Play App Signing** 활성화 필수 (Google이 원본 키 보관 → 업로드 키 재발급 가능)

### 3-2. 개인정보처리방침 (Play Store 심사 필수)
- [ ] 공개 URL 준비 (Notion 공개, GitHub Pages, 블로그 등)
- [ ] 포함 항목 (한/영 2개 언어 권장):
  - 수집 정보: Firebase Auth 이메일, 기기 식별자, 결제 정보, 광고 ID
  - 수집 목적: 계정/진행도 관리, 결제 검증, 광고 개인화
  - 제3자 제공: Google(Firebase/AdMob), Apple, 결제 플랫폼
  - 보관 기간: 계정 삭제 시 즉시
  - 유저 권리: 열람/삭제 요청 방법, 연락처
- [ ] 템플릿 참고: https://app-privacy-policy-generator.firebaseapp.com
- [ ] **이용약관 URL**도 함께 준비

### 3-3. 연령 등급
- [ ] 한국: 게임물관리위원회 자체등급분류 — Play Console/App Store에서 자동
- [ ] 글로벌: **IARC** 설문 — Play Console에서 진행
- [ ] Apple: App Store Connect 자체 등급 설문

### 결과물
```
Keystore 백업 위치:    _______________
개인정보처리방침 URL:  _______________
이용약관 URL:          _______________
```

---

## Phase 4: Google Play Console + GCP

### 4-1. Google Play 개발자 등록 ($25 일회성) ⚠ 재등록 필요
> **⚠ 기존 계정 `BHS_000` 2024-04-08 정책 위반으로 해지됨.** 동일 정책 위반 반복 금지.
> 신규 계정 등록 전 Play Developer Program Policies 재검토 필수.
- [ ] https://play.google.com/console → **신규** 개발자 등록 + $25 결제
- [ ] 개발자 프로필 작성 (국가, 연락처)
- [ ] 이 작업은 다음의 선행 조건: IAP 상품 등록(Phase 6), AdMob 스토어 연결, AdMob 결제 프로필

### 4-2. 앱 생성
- [ ] "앱 만들기"
  - 앱 이름: `MFG` (마케팅 표시명, 최대 30자)
  - 기본 언어: 한국어
  - 앱/게임: **게임**
  - 무료/유료: 무료
- [ ] 패키지명: `com.mfg.game` ← **변경 불가**
- [ ] **Play App Signing 활성화** ← Keystore 분실 보험, 필수

### 4-3. Google Cloud Console (IAP 검증용)
- [x] https://console.cloud.google.com → **Firebase와 동일 프로젝트** 선택 (`mfg-prod-4cb30`)
- [ ] API 라이브러리:
  - [ ] **Google Play Android Developer API** 활성화 (IAP 영수증 검증)
- [x] IAM → 서비스 계정:
  - [x] "서비스 계정 만들기" → 이름 `play-iap-verifier` (`play-iap-verifier@mfg-prod-4cb30.iam.gserviceaccount.com`)
  - [x] JSON 키 생성 → `server/src/MkLike.Server/play-services.json` (gitignore 적용됨)
- [ ] Play Console → 설정 → **API 액세스** → 위 서비스 계정 연결 + 권한 부여 (앱 재무 데이터 보기) — Phase 4-1 등록 후

### 4-4. Real-Time Developer Notifications (구독 이벤트)
- [ ] GCP Pub/Sub → 토픽 생성: `mfg-iap-notifications`
- [ ] Play Console → 수익 창출 → 수익 창출 설정 → Cloud Pub/Sub 토픽 이름 입력
- [ ] 서버가 이 토픽 구독 → 구독 갱신/취소/환불 자동 반영

### 결과물
```
GCP 서비스 계정 JSON:  play-services.json
Pub/Sub 토픽 이름:     mfg-iap-notifications
```

---

## Phase 5: Apple Developer + App Store Connect (iOS)

### 5-1. Apple Developer Program ($99/년)
- [ ] https://developer.apple.com/programs/enroll
- [ ] 개인 추천 (D-U-N-S 불필요) · 결제 $99/년 (자동 갱신)
- [ ] **Team ID 기록**: `______________`

### 5-2. 인증서 / App ID / 프로비저닝 프로파일
- [ ] **Identifiers** → "+" → App IDs → Bundle ID: `com.mfg.game`
  - Capabilities 체크: **Sign In with Apple**, **In-App Purchase**, **Push Notifications**
- [ ] **Certificates**:
  - [ ] Apple Development (개발용)
  - [ ] Apple Distribution (App Store 제출용)
- [ ] **Profiles**:
  - [ ] Development
  - [ ] App Store Distribution
- [ ] **Keys** → APNs Auth Key (.p8) 발급 → Firebase에 업로드 (Phase 1-6)

### 5-3. App Store Connect 앱 등록
- [ ] https://appstoreconnect.apple.com → 나의 앱 → "+" → 새 앱
  - 플랫폼: iOS
  - 이름: `MFG`
  - 기본 언어: 한국어
  - Bundle ID: `com.mfg.game` (위 App ID 선택)
  - SKU: `mfg001` (내부 관리, 변경 불가)

### 5-4. App Store Server API 키 (영수증 검증)
- [ ] 사용자 및 액세스 → **통합** → App Store Connect API → "+" 키 생성
- [ ] 액세스: **App Manager**
- [ ] `.p8` 파일 다운로드 (**1회만 가능**)
- [ ] **Issuer ID, Key ID 기록**

### 5-5. App Store Server Notifications V2
- [ ] 앱 정보 → App Store Server Notifications → URL 입력: `https://api.mf-game.com/api/v1/iap/apple-webhook` (Phase 8-2에서 확정한 도메인 사용)
- [ ] 버전: **V2 (권장)**
- [ ] 서버가 이 엔드포인트로 구독 갱신/취소/환불 이벤트 수신

### 결과물
```
Apple Team ID:        _______________
Key ID / Issuer ID:   ______ / ______
.p8 키 파일:          server/src/MFG.Server/ (커밋 금지)
```

---

## Phase 6: IAP 상품 등록 + 영수증 검증

### 6-1. 상품 카탈로그 (MVP 출시 — 13종)

> **상품 ID는 Google Play / Apple 공통으로 통일**. 한 번 등록하면 변경 불가.

**구독 (3종, 자동 갱신)**
| 상품 ID | 표시명 | 가격 (KRW) |
|---------|--------|-----------|
| `com.mfg.sub.hunter_basic` | 헌터의 증표 | ₩5,500/월 |
| `com.mfg.sub.hunter_elite` | 정예 헌터 패스 | ₩15,000/월 |
| `com.mfg.sub.hunter_master` | 마스터 헌터 패스 | ₩30,000/월 |

**배틀패스 (2종, 시즌별 일회성)**
| 상품 ID | 표시명 | 가격 |
|---------|--------|------|
| `com.mfg.bp.premium` | 프리미엄 배틀패스 | ₩12,000 |
| `com.mfg.bp.premium_plus` | 프리미엄+ 배틀패스 | ₩25,000 |

**스타터팩 (3종, 계정당 1회 비소모성)**
| 상품 ID | 가격 |
|---------|------|
| `com.mfg.starter.basic` | ₩1,200 |
| `com.mfg.starter.growth` | ₩6,500 |
| `com.mfg.starter.elite` | ₩15,000 |

**블루다이아 (5종, 소비성 무제한)**
| 상품 ID | 블루다이아 | 가격 |
|---------|-----------|------|
| `com.mfg.bluediamond.t1` | 60 | ₩3,500 |
| `com.mfg.bluediamond.t2` | 220 | ₩12,000 |
| `com.mfg.bluediamond.t3` | 480 | ₩25,000 |
| `com.mfg.bluediamond.t4` | 1,200 | ₩60,000 |
| `com.mfg.bluediamond.t5` | 2,800 | ₩130,000 |

**출시 후 추가 확장** (기획서 `monetization-strategy.md` 참조):
- 주간 한정팩 3종 / 전직 축하팩 3종 / 챕터 돌파팩 4종 / 복귀 팩 4종

### 6-2. Google Play Console — 상품 등록
- [ ] Play Console → **수익 창출 → 인앱 상품**: 스타터팩 3 + 배틀패스 2 + 블루다이아 5 = 10종
  - 유형: **관리 상품(Managed Product)**
  - 소비성은 서버에서 `consume` 처리
- [ ] Play Console → **수익 창출 → 구독**: 구독 3종
  - 갱신 주기: 1개월
  - 기본 요금제 + 선택 혜택(무료 체험 7일 등)
- [ ] 각 상품: 한국 기준가 입력 → **글로벌 가격 자동 환산** 체크
- [ ] 상태: "활성"

### 6-3. App Store Connect — 상품 등록
- [ ] 앱 → 기능 → **앱 내 구입**
- [ ] Subscription Group 생성: `mfg_hunter_pass` (구독 3종 하나의 그룹)
- [ ] 상품 유형별 등록:
  - **자동 갱신 구독**: 구독 3종
  - **비소모성(Non-Consumable)**: 스타터팩 3, 배틀패스 2
  - **소모성(Consumable)**: 블루다이아 5
- [ ] 각 상품: 참조 이름 + 상품 ID + 가격 + 로컬라이즈
- [ ] 리뷰용 스크린샷 업로드 (게임 내 구매 화면 캡처, 640x920 이상)
- [ ] 상태: "심사 대기" → 앱 심사 시 함께 검토

### 6-4. 서버 영수증 검증 흐름
```
[클라이언트]
  Unity IAP SDK → 구매 완료 → receiptData 획득
  POST /api/v1/iap/verify { platform, receiptData, productId }

[서버]
  Android: Google Play Developer API → purchases.products.get / subscriptions.get
  iOS:     App Store Server API → inApps/v1/transactions/{id}
  → 검증 성공: 재화 지급 + iap_receipts 테이블에 트랜잭션 ID 기록 (중복 방지)
  → 검증 실패: 거부 + 사기 로그

[구독 갱신/취소 — 서버 웹훅]
  Google: Pub/Sub RTDN 토픽 구독 → 서버 처리
  Apple:  App Store Server Notifications V2 → POST /api/v1/iap/apple-webhook
  → 서버가 players.subscription_expires_at 자동 갱신
```

### 6-5. 테스트 트랙
- [ ] **Play 내부 테스트**: 테스터 이메일 등록 → 샌드박스 IAP 자동
- [ ] **Apple TestFlight**: 테스터 초대 → Sandbox 계정으로 IAP 테스트 (실결제 아님)

### 결과물 → Claude 전달
```
IAP 상품 ID 목록:                   (등록 완료된 13종 ID)
Google Play 서비스 계정 JSON:       play-services.json
Apple App Store Server API .p8:     AuthKey_XXX.p8
Apple Webhook URL:                  https://api.mf-game.com/api/v1/iap/apple-webhook
Google Pub/Sub 토픽:                mfg-iap-notifications
```

---

## Phase 7: AdMob (광고 수익)

> 방치형 RPG 수익의 60~70%는 광고. 기획 광고 슬롯 8종 매핑.

### 7-1. AdMob 계정
- [x] https://admob.google.com — Firebase/Google 계정 연동 로그인 (AdSense 약관 동의 완료)
- [x] 앱 등록: Android `com.mfg.game` + iOS `com.mfg.game`
- [x] **AdMob Publisher ID**: `ca-app-pub-4444631054737272`
- [x] **AdMob App ID (Android)**: `ca-app-pub-4444631054737272~1410946323`
- [x] **AdMob App ID (iOS)**: `ca-app-pub-4444631054737272~4515621818`
- [ ] AdMob 앱-스토어 연결 (Phase 4-1 Play 계정 + Phase 5-3 App Store Connect 등록 후)
- [ ] AdMob 결제 프로필 설정 (수익 수령용)

### 7-2. 광고 단위 생성 (8종 × 2 플랫폼 = 16개)

| 광고 단위 이름 | 유형 | 기획 용도 (monetization-strategy.md 4장) |
|---------------|------|---------|
| `reward_offline_2x` | 리워드 | 오프라인 보상 2배 |
| `reward_dungeon_key` | 리워드 | 던전 열쇠 추가 |
| `reward_gold_boost` | 리워드 | 골드 부스트 5분 |
| `reward_free_summon` | 리워드 | 무료 단일 소환 |
| `reward_revive` | 리워드 | 보스전 부활 |
| `reward_exp_boost` | 리워드 | EXP 부스트 10분 |
| `reward_enhance_protect` | 리워드 | 강화 보호 |
| `reward_daily_chest` | 리워드 | 일일 보너스 상자 |

- [x] Android 8종 + iOS 8종 생성 완료 → 광고 단위 ID 16개 (AdMob 콘솔에서 확인)

### 7-3. 광고 중재 (선택 — 출시 후)
- Unity Ads, Meta Audience Network 추가 SDK → 입찰 경쟁으로 eCPM 상승
- 초기 출시엔 AdMob만으로 충분

### 결과물
```
AdMob Publisher ID:     ca-app-pub-4444631054737272  ✅
AdMob App ID (Android): ca-app-pub-4444631054737272~1410946323  ✅
AdMob App ID (iOS):     ca-app-pub-4444631054737272~4515621818  ✅
광고 단위 ID 16개:      AdMob 콘솔에서 추출 필요 (Unity SDK 연동 시 Claude에게 전달)
```

---

## Phase 8: Cloudflare (CDN + 스토리지 + 도메인)

### 8-1. 계정
- [x] https://cloudflare.com 가입 + 이메일 인증 완료

### 8-2. 도메인 구매 (`.com` 권장)

> ⚠ 네이버 블로그 / Tistory는 DNS 제어권이 없어 **사용 불가**.
> 호스팅형 블로그 서비스는 API 서버 주소로 쓸 수 없음.

> 브랜드 패키지명은 `com.mfg.game`으로 고정이지만, **도메인은 API 서버용이라 유저 눈에 거의 안 보임** — 브랜드와 일치시킬 필요 없음.

#### 등록 기관 비교 (`.com` 기준, 2026)

| 등록 기관 | 첫해 | 갱신 | 비고 |
|----------|------|------|------|
| **Cloudflare Registrar** | **$10.44** | **$10.44** | ⭐ 원가 판매, 마크업 $0. Cloudflare DNS/R2/Pages와 동일 계정 |
| Porkbun | $9.73 | $11.73 | 첫해 조금 쌈, 갱신 더 비쌈 |
| Namecheap | $5.98~$8.88 (프로모) | $14.58+ | 갱신 함정 — 장기 비추 |
| 가비아 (한국) | ~₩18,000 | ~₩18,000 | 한국어 지원만 장점 |
| Squarespace(구 Google Domains) | $12 | $12 | 단순하지만 비쌈 |

**결론**: Cloudflare Registrar에서 `.com` 구매가 장기적으로 최저가. R2/Pages와 한 곳에서 관리.

#### 구매 완료 ✅
- [x] **`mf-game.com`** (Cloudflare Registrar, 2026-04-15 구매, **만료 2027-04-15**, Auto-renew ON)
- [x] WHOIS 프라이버시 자동 적용, NS 자동 등록
- 등록 주소: `123 Gangnam-daero, Seoul, 06123, Korea South`
- 서브도메인 확장 계획: `api.mf-game.com`(서버), `cdn.mf-game.com`(R2), `play.mf-game.com`(WebGL), `blog.mf-game.com`(마케팅, 선택)

### 8-3. DNS 레코드 (`mf-game.com` 확정)
| 서브도메인 | 타입 | 대상 | 용도 |
|-----------|------|------|------|
| `api.mf-game.com` | A | Lightsail 고정 IP | API 서버 |
| `cdn.mf-game.com` | CNAME | R2 public URL | 정적 리소스 |
| `play.mf-game.com` | CNAME | Cloudflare Pages | WebGL 빌드 |

### 8-4. R2 버킷 (파일 저장, 무료 10GB) ✅
- [x] R2 → 버킷 생성: `mfg-assets` (2026-04-15)
- [x] Public Access 활성화 → 커스텀 도메인 `cdn.mf-game.com` 연결
- [x] R2 API 토큰 생성 (Object Read & Write) → `server/.env`에 R2_ACCESS_KEY / R2_SECRET 저장 (gitignore 차단됨)
- [x] 용도: 패치 리소스, 이벤트 배너, 공지 이미지

### 8-5. Pages (WebGL 호스팅)
- [ ] Pages → "Create project" → Direct Upload 또는 GitHub 연동
- [ ] 커스텀 도메인 `play.mf-game.com` 연결
- [ ] Brotli/Gzip 자동 압축 (WebGL `.wasm` 최적화)

### 결과물
```
구매 도메인:            mf-game.com
API 도메인:             api.mf-game.com
CDN 도메인:             cdn.mf-game.com
WebGL 도메인:           play.mf-game.com
R2 Access Key / Secret: 발급 완료 → server/.env 저장 ✅
```

---

## Phase 9: AWS Lightsail (게임 서버)

### 9-1. AWS 가입
- [ ] https://aws.amazon.com — 신용카드 등록

### 9-2. Lightsail 인스턴스
- [ ] https://lightsail.aws.amazon.com → "인스턴스 생성"
  - 리전: **서울 (ap-northeast-2)**
  - OS: **Ubuntu 22.04 LTS**
  - 플랜: **$5/월** (1 vCPU, 1GB RAM, 40GB SSD)
- [ ] 고정 IP 연결: Networking → "고정 IP 생성" → 인스턴스에 연결
- [ ] 방화벽: TCP 80, 443 오픈
- [ ] SSH 키 `.pem` 파일 다운로드 + 안전 보관

### 9-3. Cloudflare DNS 연결
- [ ] Cloudflare DNS → `api.mf-game.com` A 레코드 → Lightsail 고정 IP
- [ ] Proxy(주황 구름): Let's Encrypt 발급 시까지 **비활성화** → 발급 후 활성화 (DDoS 방어)

### 9-4. 서버 초기 설정
```bash
ssh -i key.pem ubuntu@<고정IP>
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
# 재접속
docker --version

git clone <repo> && cd mfg/server
cp .env.example .env    # DB_PASSWORD, FIREBASE_PROJECT_ID, DOMAIN, R2 키 등 입력
docker-compose -f docker-compose.prod.yml up -d
```

### 결과물
```
Lightsail 고정 IP:     _______________
API 도메인:            api.mf-game.com
SSH 키 보관 위치:      _______________
```

---

## Phase 10: 스토어 출시 자산 + 심사

### 10-1. 공통 자산
- [ ] **앱 아이콘** 512x512 PNG (알파 없음, 원본 1024x1024 별도 보관)
- [ ] **피처 그래픽** 1024x500 PNG (Play Store 상단 배너)

### 10-2. 스크린샷
- [ ] **Android (Play Store)**: 최소 2장, 권장 5~8장
  - 폰 세로 1080x1920 (비율 9:16) — MFG는 세로 게임이라 적합
  - 권장 구성: 전투 / 캐릭터 성장 / 가챠 / 아레나 / 상점
- [ ] **iOS (App Store)**:
  - 6.7" (1290x2796) — iPhone 14/15 Pro Max **필수**
  - 5.5" (1242x2208) — 옵션이지만 심사 빠름

### 10-3. 마케팅 텍스트
- [ ] **앱 이름** (30자): `MFG: Idle Ascent` (서브타이틀 확정 필요)
- [ ] **짧은 설명** (80자): 한 줄 핵심
- [ ] **긴 설명** (4,000자): 기능 + ASO 키워드
- [ ] **프로모션 영상** (30초, 선택)
- [ ] **키워드 목록** (App Store 100자): `방치,키우기,RPG,탭,영웅,아이들,성장,...`

### 10-4. Play Store 제출
- [ ] AAB 번들 `.aab` 업로드 → **Internal Testing** → **Closed(Alpha)** → **Open(Beta)** → **Production**
- [ ] Data Safety 설문 (수집 데이터 선언)
- [ ] Content Rating 설문
- [ ] **심사 기간**: 2~7일 (첫 제출은 최대 2주)

### 10-5. App Store 제출
- [ ] Xcode → Archive → App Store Connect 업로드
- [ ] TestFlight 내부 → 외부 베타 → 정식 제출
- [ ] **심사 기간**: 평균 24시간, 최대 3일
- [ ] 리젝 주의: Guideline 3.1.1 (IAP 우회 금지), 4.0 (디자인)

---

## Phase 11: 모니터링 + 알림 🔜 DEFERRED (2026-04-16)

> **이 Phase 전체는 서버 가동 후 별도 세션에서 진행한다.** 출시 전 필수가 아니며, IAP/심사/스토어 자산 작업을 먼저 마무리.
> 재개 조건: `api.mf-game.com` 가동 + 헬스체크 엔드포인트 응답 확인 후.

### 11-1. Uptime Robot (무료)
- [ ] https://uptimerobot.com 가입
- [ ] 모니터 추가: `https://api.mf-game.com/health` HTTP(S), 5분 간격
- [ ] 알림 채널: 이메일 + Slack (`#mfg-alerts` Webhook) 연동

### 11-2. Firebase Console
- [ ] Analytics 대시보드 즐겨찾기
- [ ] Crashlytics 알림 설정 (이메일)
- [ ] Cloud Messaging 전송 로그 모니터링

### 11-3. AdMob 대시보드
- [ ] 일일 eCPM, 노출/클릭, 채움률 모니터링

### 11-4. Slack Workspace + 알림 채널 (Phase 22 S22-07 연계)

> 1인 운영자용 통합 알림 허브. GitHub Actions 빌드 / 서버 에러 / AWS 알람 3종을 하나의 Slack으로 수집.

**11-4-1. Workspace 생성**
- [ ] https://slack.com/get-started → "Create a Workspace"
- [ ] Workspace 이름: `MFG Dev` (또는 원하는 이름)
- [ ] 플랜: **Free** (90일 히스토리, 1인 운영엔 충분 — 장기 로그는 서버에 보관)

**11-4-2. 채널 3개 분리 생성**
- [ ] `#mfg-builds` — GitHub Actions 빌드/배포 알림
- [ ] `#mfg-errors` — 서버 ERROR 이상 로그 (Serilog Sink)
- [ ] `#mfg-alerts` — AWS CloudWatch / Lightsail 알람 · Uptime Robot

**11-4-3. Incoming Webhook 발급**
- [ ] Slack Apps → "Incoming Webhooks" 추가
- [ ] 각 채널별 Webhook URL 3개 발급
- [ ] GitHub Secrets (`SLACK_WEBHOOK_BUILDS`) + 서버 `.env` (`Slack__ErrorWebhook`) 저장
- [ ] **⚠ Webhook URL은 비밀**. 클라이언트 코드에 절대 노출 금지

**11-4-4. AWS Chatbot (Amazon Q) 연결 — CloudWatch → Slack**
- [ ] https://console.aws.amazon.com/chatbot/ → "Configure new client" → Slack 선택
- [ ] Slack Workspace OAuth 승인 (무료)
- [ ] Configured channel: `#mfg-alerts` 추가
- [ ] SNS 토픽 생성: `mfg-cloudwatch-alarms`
- [ ] Chatbot에 SNS 토픽 연결 (IAM role 자동 생성)
- [ ] CloudWatch/Lightsail Alarm 생성 시 Action → SNS 토픽 발행 선택

**11-4-5. 대상 알람 예시**
- Lightsail CPU 사용률 > 80% (5분 지속)
- Lightsail Network Out 급증
- `api.mf-game.com/health` 응답 시간 > 2s
- DB 디스크 사용량 > 85%
- IAP 영수증 검증 실패율 > 5% (커스텀 지표)

### 결과물 → Claude 전달
```
Slack Webhook URLs:
  #mfg-builds:  https://hooks.slack.com/services/...
  #mfg-errors:  https://hooks.slack.com/services/...
  #mfg-alerts:  https://hooks.slack.com/services/...
AWS SNS Topic ARN: arn:aws:sns:ap-northeast-2:...:mfg-cloudwatch-alarms
```

---

## 전체 비용 요약

| 항목 | 비용 | 주기 |
|------|------|------|
| Google Play 개발자 | $25 | 일회성 |
| Apple Developer Program | $99 | 연간 |
| 도메인 (.com @ Cloudflare) | ~$10 | 연간 |
| AWS Lightsail | $5 | 월간 |
| Firebase (Spark 플랜) | $0 | — |
| Cloudflare (Free) | $0 | — |
| AdMob | $0 | — |
| **출시 초기 월 비용** | **~$7** | 월 |
| **첫 해 총 비용** | **~$200** | — |

---

## 보안 체크리스트 (절대 git 커밋 금지)

```
client/Assets/google-services.json
client/Assets/GoogleService-Info.plist
server/src/MFG.Server/serviceAccountKey.json
server/src/MFG.Server/play-services.json
server/src/MFG.Server/AuthKey_*.p8        ← Apple
server/.env
appsettings.*.local.json
mfg-release.keystore                         ← Android 서명 키 (최중요)
mfg-release.keystore.password.txt
```

**키 분실 대응**:
| 키 | 재발급 가능? | 대응 |
|----|------------|------|
| Firebase 서비스 계정 | ✅ | 새 키 생성 후 교체 |
| GCP 서비스 계정 (play-services) | ✅ | 새 키 생성 |
| Apple .p8 (App Store Server API) | ✅ | 새 키 생성 (기존 무효화) |
| APNs Auth Key (.p8) | ✅ | 새 키 발급 |
| **Android Release Keystore** | ⚠ **Play App Signing 활성화 시에만** | Phase 4-2 필수 |

---

## 결과물 최종 집계 (Claude에게 한 번에 전달)

```yaml
# Phase 1
firebase_project_id_prod: "mfg-prod-4cb30"
firebase_project_id_dev:  "(미생성)"
firebase_project_number:  "188303570676"
android_app_id:           "1:188303570676:android:95d89aad049aff8b2510a1"
ios_app_id:               "1:188303570676:ios:764bfc8f31a3df392510a1"

# Phase 3
privacy_policy_url:   "___________"
terms_of_service_url: "___________"

# Phase 4
gcp_service_account_json: "play-services.json"
pubsub_topic:             "mfg-iap-notifications"

# Phase 5
apple_team_id:  "___________"
apple_key_id:   "___________"
apple_issuer_id: "___________"
apple_p8_file:  "AuthKey_XXX.p8"

# Phase 6
iap_product_ids: [
  "com.mfg.sub.hunter_basic", "com.mfg.sub.hunter_elite", "com.mfg.sub.hunter_master",
  "com.mfg.bp.premium", "com.mfg.bp.premium_plus",
  "com.mfg.starter.basic", "com.mfg.starter.growth", "com.mfg.starter.elite",
  "com.mfg.bluediamond.t1", "com.mfg.bluediamond.t2", "com.mfg.bluediamond.t3",
  "com.mfg.bluediamond.t4", "com.mfg.bluediamond.t5"
]

# Phase 7
admob_publisher_id:   "ca-app-pub-4444631054737272"
admob_app_id_android: "ca-app-pub-4444631054737272~1410946323"
admob_app_id_ios:     "ca-app-pub-4444631054737272~4515621818"
admob_ad_unit_ids:    # 16개 (8종 × 2 플랫폼) — AdMob 콘솔에서 추출 후 Unity SDK 연동 시 입력
  # reward_offline_2x / reward_dungeon_key / reward_gold_boost / reward_free_summon
  # reward_revive / reward_exp_boost / reward_enhance_protect / reward_daily_chest

# Phase 8
domain:          "mf-game.com"           # Cloudflare Registrar, 만료 2027-04-15
domain_api:      "api.mf-game.com"
domain_cdn:      "cdn.mf-game.com"
domain_play:     "play.mf-game.com"
r2_access_key: "___________"
r2_secret:     "___________"

# Phase 9
lightsail_static_ip: "___________"
```

---

## 선택 사항 / 출시 후 확장

- [ ] Firebase Remote Config — 라이브 서버 밸런스 조정 (무료)
- [ ] Firebase A/B Testing — 튜토리얼/UI 실험
- [ ] Google Play Games Services — 업적/리더보드 (서버 있으면 선택)
- [ ] Game Center (iOS) — 업적 (서버 있으면 선택)
- [ ] Stripe / TossPayments — WebGL 결제 (Android/iOS IAP 우회용)
- [ ] 광고 중재 (Unity Ads, Meta Audience Network)
- [ ] Firebase Dynamic Links — 초대/복귀 딥링크
- [ ] Sentry / Datadog — 서버 APM (Lightsail 단독 운영 시 불필요)

---

## 참고 문서

- 인프라 비용/대안 상세: `../../indie-game-dev-infra-guide.md`
- 서버 아키텍처: `systems/server-architecture.md`
- 인증 플로우: `systems/server-auth-flow.md`
- API 엔드포인트: `systems/server-api-endpoints.md`
- DB 스키마: `systems/server-db-schema.md`
- IAP 상품 설계 상세: `_archived/monetization-strategy.md`
