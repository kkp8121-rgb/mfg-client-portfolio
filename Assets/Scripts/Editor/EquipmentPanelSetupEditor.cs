using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using MkLike.Core;

namespace MkLike.Editor
{
    /// <summary>
    /// CharacterPanel의 장비 서브 탭 내부를 구성한다.
    /// 상단: 서브탭 버튼 3개 (장비 / 무기 / 강화)
    /// 장비탭: 12개 장비 슬롯 (4행 x 3열) + 보너스 스탯 + 인벤토리
    /// 무기/강화 탭: 런타임 동적 생성
    /// </summary>
    public static class EquipmentPanelSetupEditor
    {
        private const string KIT = "Assets/Folder_Assets/GUI Kit - Dark Geo/";
        private const string SPRITE = KIT + "ResourceData/Sprite/Component/";
        private const string ITEM_ICON = SPRITE + "Icon_ItemIcon_(x2)/128/";

        private static readonly Color PanelBg = new(0.10f, 0.09f, 0.08f, 0.96f);
        private static readonly Color SlotBg = new(0.16f, 0.15f, 0.13f, 0.95f);
        private static readonly Color SlotBorder = new(0.30f, 0.27f, 0.23f, 0.8f);
        private static readonly Color TextWhite = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new(1f, 0.85f, 0.086f, 1f);
        private static readonly Color TabActive = new(0.3f, 0.35f, 0.4f, 1f);
        private static readonly Color TabInactive = new(0.15f, 0.14f, 0.12f, 0.9f);

        private const int SLOT_COUNT = 12;

        // EquipmentSlot enum 순서에 대응하는 표시명
        private static readonly string[] SlotNames =
        {
            "무기", "투구", "상의", "하의", "장갑", "망토",
            "어깨", "벨트", "신발", "반지", "목걸이", "얼굴장식"
        };

        // 슬롯별 아이콘 (없으면 빈 아이콘)
        private static readonly string[] SlotIcons =
        {
            "icon_sword.png", "icon_helmet.png", "icon_armor.png",
            "icon_armor.png", "icon_glove.png", "icon_armor.png",
            "icon_armor.png", "icon_armor.png", "icon_boot.png",
            "icon_ring.png", "icon_ring.png", "icon_ring.png"
        };

        /// <summary>
        /// 장비 탭 컨텐츠를 구성하고 EquipmentPanel 컴포넌트를 와이어링한다.
        /// </summary>
        public static GameObject Build(Transform parent)
        {
            var root = CreateEmpty("Content_Equipment", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true;
            rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true;
            rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 6;
            rootVlg.padding = new RectOffset(12, 12, 8, 10);

            // ── 서브탭 버튼 ──
            var tabRow = CreateEmpty("SubTabRow", root.transform);
            tabRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var tabHlg = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;
            tabHlg.spacing = 4;

            var tabEquipBtn = BuildTabButton(tabRow.transform, "장비", true);
            var tabWeaponBtn = BuildTabButton(tabRow.transform, "무기", false);
            var tabEnhanceBtn = BuildTabButton(tabRow.transform, "강화", false);

            // ── 장비 탭 컨테이너 (서브탭 전환 시 통째로 표시/숨김) ──
            var equipTabContent = CreateEmpty("EquipTabContent", root.transform);
            var equipTabVlg = equipTabContent.AddComponent<VerticalLayoutGroup>();
            equipTabVlg.childAlignment = TextAnchor.UpperCenter;
            equipTabVlg.childControlWidth = true;
            equipTabVlg.childControlHeight = false;
            equipTabVlg.childForceExpandWidth = true;
            equipTabVlg.childForceExpandHeight = false;
            equipTabVlg.spacing = 6;
            var equipTabLE = equipTabContent.AddComponent<LayoutElement>();
            equipTabLE.flexibleHeight = 1;

            // ── 섹션: 장착 슬롯 (12개 = 4행 x 3열) ──
            BuildSectionHeader(equipTabContent.transform, "장착 장비");

            var slotUIs = new MkLike.UI.EquipmentSlotUI[SLOT_COUNT];

            // 4행 x 3열 그리드
            for (int row = 0; row < 4; row++)
            {
                var rowObj = CreateEmpty($"SlotRow_{row}", equipTabContent.transform);
                rowObj.AddComponent<LayoutElement>().preferredHeight = 84;
                var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 6;

                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    if (idx < SLOT_COUNT)
                        slotUIs[idx] = BuildSlotUI(rowObj.transform, idx);
                }
            }

            // ── 섹션: 보너스 스탯 ──
            var bonusRow = CreateEmpty("BonusStats", equipTabContent.transform);
            bonusRow.AddComponent<LayoutElement>().preferredHeight = 40;
            bonusRow.AddComponent<Image>().color = new Color(0.14f, 0.20f, 0.14f, 0.7f);
            var bonusHlg = bonusRow.AddComponent<HorizontalLayoutGroup>();
            bonusHlg.childAlignment = TextAnchor.MiddleCenter;
            bonusHlg.childControlWidth = true;
            bonusHlg.childControlHeight = true;
            bonusHlg.childForceExpandWidth = true;
            bonusHlg.childForceExpandHeight = true;
            bonusHlg.spacing = 12;
            bonusHlg.padding = new RectOffset(12, 12, 4, 4);

            var bonusAtkTmp = MakeTMP("BonusAtk", bonusRow.transform, "ATK +0", 18);
            bonusAtkTmp.alignment = TextAlignmentOptions.Center;
            bonusAtkTmp.color = TextGold;
            bonusAtkTmp.fontStyle = FontStyles.Bold;

            var bonusHpTmp = MakeTMP("BonusHp", bonusRow.transform, "HP +0", 18);
            bonusHpTmp.alignment = TextAlignmentOptions.Center;
            bonusHpTmp.color = TextGold;
            bonusHpTmp.fontStyle = FontStyles.Bold;

            var bonusDefTmp = MakeTMP("BonusDef", bonusRow.transform, "DEF +0", 18);
            bonusDefTmp.alignment = TextAlignmentOptions.Center;
            bonusDefTmp.color = TextGold;
            bonusDefTmp.fontStyle = FontStyles.Bold;

            // ── 섹션: 인벤토리 ──
            BuildSectionHeader(equipTabContent.transform, "보유 장비");

            var countTmp = MakeTMP("CountText", equipTabContent.transform, "보유: 0개", 16);
            countTmp.alignment = TextAlignmentOptions.Left;
            countTmp.color = TextTan;
            countTmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            // 스크롤뷰
            var scrollObj = CreateEmpty("InventoryScroll", equipTabContent.transform);
            scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.11f, 0.10f, 0.09f, 0.8f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateEmpty("Viewport", scrollObj.transform);
            FullStretch(viewport);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObj = CreateEmpty("Content", viewport.transform);
            var contentRT = contentObj.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0, 0);

            var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.spacing = 4;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;

            // ── EquipmentPanel 컴포넌트 와이어링 ──
            var ep = root.AddComponent<MkLike.UI.EquipmentPanel>();
            var so = new SerializedObject(ep);

            // 서브탭 버튼
            so.FindProperty("_tabEquipmentBtn").objectReferenceValue = tabEquipBtn;
            so.FindProperty("_tabWeaponBtn").objectReferenceValue = tabWeaponBtn;
            so.FindProperty("_tabEnhanceBtn").objectReferenceValue = tabEnhanceBtn;

            // 장비 탭 컨테이너
            so.FindProperty("_equipmentTabContent").objectReferenceValue = equipTabContent;

            // 슬롯 UI (12개)
            var slotProp = so.FindProperty("_slotUIs");
            slotProp.arraySize = SLOT_COUNT;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var elem = slotProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("gradeMarker").objectReferenceValue = slotUIs[i].gradeMarker;
                elem.FindPropertyRelative("iconImage").objectReferenceValue = slotUIs[i].iconImage;
                elem.FindPropertyRelative("nameText").objectReferenceValue = slotUIs[i].nameText;
                elem.FindPropertyRelative("gradeText").objectReferenceValue = slotUIs[i].gradeText;
                elem.FindPropertyRelative("emptyText").objectReferenceValue = slotUIs[i].emptyText;
                elem.FindPropertyRelative("unequipBtn").objectReferenceValue = slotUIs[i].unequipBtn;
            }

            so.FindProperty("_inventoryContent").objectReferenceValue = contentObj.transform;
            so.FindProperty("_inventoryCountText").objectReferenceValue = countTmp;
            so.FindProperty("_bonusAtkText").objectReferenceValue = bonusAtkTmp;
            so.FindProperty("_bonusHpText").objectReferenceValue = bonusHpTmp;
            so.FindProperty("_bonusDefText").objectReferenceValue = bonusDefTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ── 서브탭 버튼 빌더 ──

        private static Button BuildTabButton(Transform parent, string label, bool isActive)
        {
            var btnObj = new GameObject($"Tab_{label}");
            btnObj.transform.SetParent(parent, false);
            btnObj.AddComponent<RectTransform>();
            var bg = btnObj.AddComponent<Image>();
            bg.color = isActive ? TabActive : TabInactive;
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            var labelTmp = MakeTMP("Label", btnObj.transform, label, 16);
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = TextWhite;
            labelTmp.fontStyle = FontStyles.Bold;
            FullStretch(labelTmp.gameObject);

            return btn;
        }

        // ── 슬롯 UI 빌더 ──

        private static MkLike.UI.EquipmentSlotUI BuildSlotUI(Transform parent, int index)
        {
            var slot = CreateEmpty($"Slot_{SlotNames[index]}", parent);
            slot.AddComponent<Image>().color = SlotBg;

            var vlg = slot.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(8, 8, 4, 4);

            // 등급 마커 (상단 바)
            var markerObj = new GameObject("GradeMarker");
            markerObj.transform.SetParent(slot.transform, false);
            markerObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 3);
            var markerImg = markerObj.AddComponent<Image>();
            markerImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            markerObj.AddComponent<LayoutElement>().preferredHeight = 3;

            // 슬롯 타입 아이콘 (빈 슬롯일 때 표시)
            var iconPath = ITEM_ICON + SlotIcons[index];
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slot.transform, false);
            var iconImg = iconObj.AddComponent<Image>();
            var spr = LoadSprite(iconPath);
            if (spr != null) { iconImg.sprite = spr; iconImg.preserveAspect = true; }
            else iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
            iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);

            // 장비 아이템 아이콘 (장착 시 표시, 초기에는 비활성)
            var equipIconObj = new GameObject("EquipIcon");
            equipIconObj.transform.SetParent(slot.transform, false);
            var equipIconImg = equipIconObj.AddComponent<Image>();
            equipIconImg.preserveAspect = true;
            equipIconImg.color = Color.white;
            equipIconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);
            equipIconObj.SetActive(false);

            // 장비 이름
            var nameTmp = MakeTMP("Name", slot.transform, "", 14);
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = TextWhite;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 18);

            // 등급
            var gradeTmp = MakeTMP("Grade", slot.transform, "", 12);
            gradeTmp.alignment = TextAlignmentOptions.Center;
            gradeTmp.color = TextTan;
            gradeTmp.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 16);

            // 빈 슬롯 텍스트
            var emptyTmp = MakeTMP("Empty", slot.transform, SlotNames[index], 13);
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            emptyTmp.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 17);

            // 해제 버튼 (장착 시에만 활성)
            var unequipObj = new GameObject("UnequipBtn");
            unequipObj.transform.SetParent(slot.transform, false);
            unequipObj.AddComponent<RectTransform>().sizeDelta = new Vector2(48, 20);
            var unequipBg = unequipObj.AddComponent<Image>();
            unequipBg.color = new Color(0.4f, 0.2f, 0.2f, 0.8f);
            var unequipBtn = unequipObj.AddComponent<Button>();
            unequipBtn.targetGraphic = unequipBg;

            var unequipLabel = MakeTMP("Label", unequipObj.transform, "해제", 12);
            unequipLabel.alignment = TextAlignmentOptions.Center;
            unequipLabel.color = TextWhite;
            FullStretch(unequipLabel.gameObject);

            // 해제 버튼 클릭 이벤트
            int slotIndex = index;
            unequipBtn.onClick.AddListener(() =>
            {
                if (MkLike.Equipment.EquipmentManager.Instance != null)
                    MkLike.Equipment.EquipmentManager.Instance.Unequip((EquipmentSlot)slotIndex);
            });

            return new MkLike.UI.EquipmentSlotUI
            {
                gradeMarker = markerImg,
                iconImage = equipIconImg,
                nameText = nameTmp,
                gradeText = gradeTmp,
                emptyText = emptyTmp,
                unequipBtn = unequipBtn
            };
        }

        // ── 유틸 ──

        private static void BuildSectionHeader(Transform parent, string text)
        {
            var header = CreateEmpty($"Section_{text}", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 30;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(8, 8, 0, 0);

            var line = new GameObject("Line");
            line.transform.SetParent(header.transform, false);
            line.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line.AddComponent<LayoutElement>().preferredWidth = 20;

            var tmp = MakeTMP("Label", header.transform, text, 18);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextTan;
            tmp.fontStyle = FontStyles.Bold;
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var line2 = new GameObject("Line2");
            line2.transform.SetParent(header.transform, false);
            line2.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line2.AddComponent<LayoutElement>().preferredWidth = 20;
        }

        private static Sprite LoadSprite(string path)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr == null)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.SaveAndReimport();
                    spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            return spr;
        }

        private static GameObject CreateEmpty(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            var f = FontSetupEditor.GetOrCreateKoreanFont();
            if (f != null) tmp.font = f;
            return tmp;
        }

        private static void FullStretch(GameObject obj)
        {
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            r.anchoredPosition = Vector2.zero;
        }
    }
}
