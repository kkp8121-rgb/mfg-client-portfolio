---
read_count: 3
last_read: "2026-04-07"
status: active
---

# MFG 서버 API 엔드포인트 (Phase 1)

> Phase 1 서버 검증 대상: 가챠, 재화, 결제(IAP), 진행도 저장, 출석

---

## 인증

### POST `/api/v1/auth/login`
Firebase ID Token으로 서버 인증. 최초 로그인 시 플레이어 레코드 생성.

**Request:**
```json
{ "firebaseIdToken": "eyJhbG..." }
```

**Response:**
```json
{
  "success": true,
  "data": {
    "playerId": "uuid",
    "nickname": "용사1호",
    "isNewPlayer": false,
    "serverTime": "2026-04-07T12:00:00Z"
  }
}
```

---

## 가챠

### POST `/api/v1/gacha/pull`
서버에서 확률 계산 + 재화 차감 + 결과 생성.

**Request:**
```json
{
  "poolType": "Equipment",  // Equipment | Weapon | Relic
  "pullCount": 10           // 1 | 10
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "results": [
      { "itemId": "eq_sword_001", "grade": "Epic", "isNew": true },
      { "itemId": "eq_helm_003", "grade": "Rare", "isNew": false }
  ],
    "currencySpent": { "type": "Ruby", "amount": 2700 },
    "currencyRemaining": 7300,
    "pityCount": 15,
    "summonLevel": { "pool": "Equipment", "totalPulls": 150, "level": 3 }
  }
}
```

---

## 재화

### POST `/api/v1/currency/spend`
서버에서 잔액 확인 + 차감 + 트랜잭션 기록.

**Request:**
```json
{
  "type": "Gold",
  "amount": 50000,
  "reason": "equipment_enhance",
  "referenceId": "eq_instance_abc"
}
```

### POST `/api/v1/currency/earn`
서버에서 재화 지급 + 트랜잭션 기록.

**Request:**
```json
{
  "type": "Gold",
  "amount": 10000,
  "reason": "stage_clear",
  "referenceId": "stage_3_5"
}
```

### GET `/api/v1/currency/balance`
전체 재화 잔액 조회.

---

## 결제 (IAP)

### POST `/api/v1/iap/verify`
영수증 검증 + 재화 지급.

**Request:**
```json
{
  "platform": "GooglePlay",  // GooglePlay | Apple | Web
  "receiptData": "...",
  "productId": "com.mfg.bluediamond.t1"
}
```

---

## 진행도 저장

### POST `/api/v1/save/sync`
클라이언트 SaveData를 서버에 동기화. 서버 시간 기준 검증.

**Request:**
```json
{
  "saveData": { ... },  // SaveData JSON (전체 또는 diff)
  "clientTimestamp": "2026-04-07T12:00:00Z"
}
```

### GET `/api/v1/save/load`
서버 저장 데이터 로드. 클라이언트 시작 시 호출.

---

## 출석

### POST `/api/v1/attendance/check`
서버 시간 기준 출석 체크 + 보상 지급.

**Response:**
```json
{
  "success": true,
  "data": {
    "consecutiveDays": 7,
    "reward": { "type": "Ruby", "amount": 100 },
    "serverDate": "2026-04-07"
  }
}
```

---

## 공통 에러 응답

```json
{
  "success": false,
  "error": "INSUFFICIENT_CURRENCY",
  "message": "골드가 부족합니다. 필요: 50000, 보유: 30000"
}
```

에러 코드: `INVALID_TOKEN`, `INSUFFICIENT_CURRENCY`, `INVALID_RECEIPT`, `ALREADY_CHECKED`, `RATE_LIMITED`, `SERVER_ERROR`
