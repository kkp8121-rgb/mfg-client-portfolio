---
read_count: 2
last_read: "2026-04-07"
status: reference
---

# MFG 데이터베이스 스키마

> MySQL 8.0, EF Core Code-First (MySql.EntityFrameworkCore, snake_case 네이밍)

---

## 핵심 테이블

### players
```sql
CREATE TABLE players (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firebase_uid    VARCHAR(128) UNIQUE NOT NULL,
    nickname        VARCHAR(32),
    level           INT DEFAULT 1,
    exp             BIGINT DEFAULT 0,
    job_id          VARCHAR(32) DEFAULT '',
    job_tier        INT DEFAULT 0,
    combat_power    BIGINT DEFAULT 0,
    current_floor   INT DEFAULT 1,
    max_floor       INT DEFAULT 1,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    last_login_at   TIMESTAMPTZ DEFAULT NOW(),
    last_logout_at  TIMESTAMPTZ
);
CREATE INDEX idx_players_firebase ON players(firebase_uid);
```

### currencies
```sql
CREATE TABLE currencies (
    id          BIGSERIAL PRIMARY KEY,
    player_id   UUID REFERENCES players(id),
    type        VARCHAR(32) NOT NULL,    -- Gold, Ruby, BlueDiamond, ...
    amount      BIGINT DEFAULT 0,
    UNIQUE(player_id, type)
);
CREATE INDEX idx_currencies_player ON currencies(player_id);
```

### currency_transactions (감사 로그)
```sql
CREATE TABLE currency_transactions (
    id              BIGSERIAL PRIMARY KEY,
    player_id       UUID REFERENCES players(id),
    currency_type   VARCHAR(32) NOT NULL,
    amount          BIGINT NOT NULL,       -- 양수=획득, 음수=소비
    balance_after   BIGINT NOT NULL,
    reason          VARCHAR(64) NOT NULL,  -- gacha_pull, stage_clear, iap_purchase, ...
    reference_id    VARCHAR(128),          -- 관련 아이템/스테이지 ID
    created_at      TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_ctx_player ON currency_transactions(player_id, created_at DESC);
```

### gacha_history
```sql
CREATE TABLE gacha_history (
    id              BIGSERIAL PRIMARY KEY,
    player_id       UUID REFERENCES players(id),
    pool_type       VARCHAR(32) NOT NULL,  -- Equipment, Weapon, Relic
    result_item_id  VARCHAR(64) NOT NULL,
    result_grade    VARCHAR(16) NOT NULL,
    pity_count      INT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);
```

### equipment_inventory
```sql
CREATE TABLE equipment_inventory (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id       UUID REFERENCES players(id),
    data_id         VARCHAR(64) NOT NULL,  -- EquipmentDataSO ID
    grade           VARCHAR(16) NOT NULL,  -- Normal, Rare, Epic, ...
    enhancement     INT DEFAULT 0,
    is_equipped     BOOLEAN DEFAULT FALSE,
    equipped_slot   VARCHAR(16),           -- Head, Body, Hands, Feet, Acc1, Acc2
    created_at      TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_equip_player ON equipment_inventory(player_id);
```

### progress_data
```sql
CREATE TABLE progress_data (
    id              BIGSERIAL PRIMARY KEY,
    player_id       UUID REFERENCES players(id) UNIQUE,
    save_json       JSON NOT NULL,         -- SaveData 전체 또는 서브셋 (MySQL JSON 타입)
    version         INT DEFAULT 1,
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);
```

### attendance_records
```sql
CREATE TABLE attendance_records (
    id                  BIGSERIAL PRIMARY KEY,
    player_id           UUID REFERENCES players(id),
    check_date          DATE NOT NULL,
    consecutive_days    INT DEFAULT 1,
    reward_type         VARCHAR(32),
    reward_amount       INT,
    UNIQUE(player_id, check_date)
);
```

### iap_receipts (결제 검증 로그)
```sql
CREATE TABLE iap_receipts (
    id              BIGSERIAL PRIMARY KEY,
    player_id       UUID REFERENCES players(id),
    platform        VARCHAR(16) NOT NULL,  -- GooglePlay, Apple, Web
    product_id      VARCHAR(128) NOT NULL,
    receipt_hash    VARCHAR(256) NOT NULL,
    is_valid        BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(receipt_hash)
);
```

---

## Phase 2 추가 테이블 (아레나/길드)

### arena_records, arena_seasons
### guild_members, guild_boss_records

> Phase 2 착수 시 상세 설계
