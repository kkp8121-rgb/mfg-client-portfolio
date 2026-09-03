# 장비 시스템 개편 기획서

## 개요
메이플 키우기 스타일 장비 시스템으로 전면 개편.
핵심: **슬롯 기반 인벤토리 + 중복 장비 각성(겹치기) + 슬롯 강화**

## 참조 이미지
- Image 2: 메이플 키우기 무기 탭 — 보유 무기 그리드, 각성 단계(3/5, 0/5), 레벨업/승급/장착 버튼
- Image 3: 메이플 키우기 장비 슬롯 강화 — 좌측 장착 슬롯 목록, 우측 강화 패널(주문서/스타포스/잠재옵션)
- Image 4: 현재 mkLike — 반지/목걸이/얼굴장식/무기 슬롯 중복 문제

---

## 1. 슬롯 구조 정리 (8슬롯)

### EquipmentSlot enum (변경 없음)
```
Weapon(0), Helmet(1), Top(2), Gloves(3), Boots(4), Ring(5), Necklace(6), FaceAccessory(7)
```

### UXML 수정
- 현재 12슬롯 → **8슬롯**으로 축소
- 4열 x 2행 그리드
- 1행: 투구, 상의, 장갑, 신발
- 2행: 반지, 목걸이, 얼굴장식, 무기

### 삭제 슬롯
- 하의(slot-2), 망토(slot-4), 어깨(slot-5), 벨트(slot-6)

---

## 2. 중복 장비 각성 (Awakening)

### 개념
- 같은 ID + 같은 등급의 장비를 중복 획득하면 각성 재료로 사용
- 각성 단계: 0~5성 (같은 장비 5개 겹치면 최대)
- 각성 시 스탯 보너스: 각성당 기본 스탯 +10%

### EquipmentInstance 수정
```csharp
public int awakeningStars;  // 0~5 (무기와 동일 패턴)
```

### 각성 로직 (EquipmentManager)
```
조건: 같은 equipmentId + 같은 grade
비용: 재료 장비 1개 소모 (인벤토리에서 제거)
결과: 대상 장비의 awakeningStars + 1
스탯: baseAtk * (1 + awakeningStars * 0.1)
```

### UI 표시
- 슬롯 아이콘 하단에 별 ★ 개수 표시
- 각성 가능 시 빨간 점(!) 알림

---

## 3. 슬롯 기반 인벤토리 UI

### 현재 문제
- 평면 리스트에 모든 장비가 섞여 있음
- 어떤 슬롯의 장비인지 구분 어려움

### 새로운 UI 흐름
1. 장비 탭 진입 → 8슬롯 그리드 표시 (장착 중인 장비)
2. **슬롯 클릭** → 팝업 열림
3. 팝업: 해당 슬롯의 보유 장비 목록 (그리드)
4. 장비 항목: 아이콘 + 등급 + 레벨 + 각성(★) + 장착 상태
5. 장비 클릭 → 상세 정보 + 장착/각성/강화 버튼

### 팝업 UXML 구조
```
PopupEquipSlotInventory.uxml
├── 팝업 헤더 (슬롯명: "투구", "반지" 등)
├── 장비 그리드 (ScrollView)
│   └── 장비 카드 (아이콘, 등급텍스트, Lv, ★각성, 장착뱃지)
├── 선택된 장비 상세
│   ├── 스탯 (ATK, HP, DEF + 보너스)
│   ├── 각성 상태 (★★★☆☆ 3/5)
│   └── 버튼: [장착] [각성] [강화]
└── 닫기 버튼
```

---

## 4. 슬롯 강화 (기존 시스템 활용)

### 현재 구현 (변경 없음)
- scrollLevel: 주문서 강화 (0~10)
- starForce: 성급 강화 (0~15+)
- potentialGrade: 잠재능력 (None~Mythic)

### UI 변경
- 강화 탭 → 슬롯 선택 → 강화 패널
- 강화 패널: 주문서 강화 / 스타포스 / 잠재 옵션 서브탭
- 참조: Image 3 (메이플 키우기 강화 UI)

---

## 5. 뽑기 시스템 수정

### 현재 상태
- GachaPool: Equipment/Weapon/Relic 분리 ✅
- SummonLevel: 구현됨 ✅
- 문제: 장비 뽑기에서 무기가 나오는지 확인 필요 (SO 데이터)

### 확인 사항
- GachaPool_Equipment.asset의 entries에 무기 ID가 포함되어 있는지
- ShopPanel에서 뽑기 레벨 표시 여부
- 유물 소환 버튼/아이콘 유무

---

## 6. 상점 유물소환 아이콘

### ShopPanel에 유물 소환 탭 추가
- 기존: 장비 소환 / 무기 소환
- 추가: 유물 소환 (GachaPoolType.Relic)

---

## 구현 순서

1. UXML 12→8 슬롯 축소
2. EquipmentInstance에 awakeningStars 추가
3. EquipmentManager에 각성 로직 추가
4. PopupEquipSlotInventory (슬롯 클릭 팝업) UXML+USS+CS 구현
5. EquipmentTabUI 리팩토링 (슬롯 클릭 → 팝업)
6. ShopPanel 유물 소환 추가
7. 뽑기 레벨 UI 표시 확인
8. GachaPool SO 데이터 검증
