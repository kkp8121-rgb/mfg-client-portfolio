using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// Panel_Shop 내부 구성: 서브 탭 (동료 소환/장비 소환) + 루비 잔고 + 뽑기 버튼.
    /// SceneSetupEditor에서 호출된다.
    /// </summary>
    public static class ShopPanelSetupEditor
    {
        private const string KIT = "Assets/Folder_Assets/GUI Kit - Dark Geo/";
        private const string SPRITE = KIT + "ResourceData/Sprite/Component/";
        private const string BTN_ICON = SPRITE + "Icon_ButtonIcon_(x2)/128/";
        private const string ITEM_ICON = SPRITE + "Icon_ItemIcon_(x2)/128/";

        private static readonly Color PanelBg = new Color(0.10f, 0.09f, 0.08f, 0.96f);
        private static readonly Color SubTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        private static readonly Color SubTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new Color(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new Color(1f, 0.85f, 0.086f, 1f);
        private static readonly Color TextRuby = new Color(1f, 0.35f, 0.40f, 1f);
        private static readonly Color BtnPurple = new Color(0.45f, 0.25f, 0.65f, 1f);
        private static readonly Color BtnGold = new Color(0.65f, 0.50f, 0.15f, 1f);

        public static void Build(GameObject panel)
        {
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null) bgImg.color = PanelBg;

            // 기존 자식 제거
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0; vlg.padding = new RectOffset(0, 0, 0, 0);

            // ── 헤더 (제목 + 루비 잔고) ──
            var rubyText = BuildHeader(panel.transform);

            // ── 서브 탭 바 ──
            BuildSubTabBar(panel.transform, out var subTabBgs, out var subTabBtns);

            // ── 컨텐츠 영역 ──
            var contentArea = CreateEmpty("ContentArea", panel.transform);
            contentArea.AddComponent<LayoutElement>().flexibleHeight = 1;

            // 동료 소환 탭
            var companionContent = BuildGachaContent(contentArea.transform, "Content_Companion",
                "동료 소환", "동료를 소환하여 파티를 강화하세요!",
                ITEM_ICON + "icon_user.png",
                out var comp1Btn, out var comp10Btn,
                out var comp1Cost, out var comp10Cost,
                out var compPity, out var compResults);

            // 장비 소환 탭
            var equipContent = BuildGachaContent(contentArea.transform, "Content_Equipment",
                "장비 소환", "강력한 장비를 소환하세요!",
                ITEM_ICON + "icon_shield.png",
                out var equip1Btn, out var equip10Btn,
                out var equip1Cost, out var equip10Cost,
                out var equipPity, out var equipResults);

            // 무기 소환 탭
            var weaponContent = BuildGachaContent(contentArea.transform, "Content_Weapon",
                "무기 소환", "강력한 무기를 소환하세요!",
                ITEM_ICON + "icon_sword.png",
                out var weapon1Btn, out var weapon10Btn,
                out var weapon1Cost, out var weapon10Cost,
                out var weaponPity, out var weaponResults);

            var subTabContents = new GameObject[] { companionContent, equipContent, weaponContent };

            for (int i = 0; i < subTabContents.Length; i++)
            {
                FullStretch(subTabContents[i]);
                subTabContents[i].SetActive(i == 0);
            }

            // ── ShopPanel 컴포넌트 와이어링 ──
            var sp = panel.AddComponent<MkLike.UI.ShopPanel>();
            var so = new SerializedObject(sp);

            // subTabButtons
            var btnProp = so.FindProperty("subTabButtons");
            btnProp.arraySize = subTabBtns.Length;
            for (int i = 0; i < subTabBtns.Length; i++)
                btnProp.GetArrayElementAtIndex(i).objectReferenceValue = subTabBtns[i];

            // subTabBgs
            var bgProp = so.FindProperty("subTabBgs");
            bgProp.arraySize = subTabBgs.Length;
            for (int i = 0; i < subTabBgs.Length; i++)
                bgProp.GetArrayElementAtIndex(i).objectReferenceValue = subTabBgs[i];

            // subTabContents
            var cProp = so.FindProperty("subTabContents");
            cProp.arraySize = subTabContents.Length;
            for (int i = 0; i < subTabContents.Length; i++)
                cProp.GetArrayElementAtIndex(i).objectReferenceValue = subTabContents[i];

            // 루비
            so.FindProperty("rubyText").objectReferenceValue = rubyText;

            // 동료
            so.FindProperty("companionPull1Btn").objectReferenceValue = comp1Btn;
            so.FindProperty("companionPull10Btn").objectReferenceValue = comp10Btn;
            so.FindProperty("companionPull1CostText").objectReferenceValue = comp1Cost;
            so.FindProperty("companionPull10CostText").objectReferenceValue = comp10Cost;
            so.FindProperty("companionPityText").objectReferenceValue = compPity;
            so.FindProperty("companionResultArea").objectReferenceValue = compResults;

            // 장비
            so.FindProperty("equipmentPull1Btn").objectReferenceValue = equip1Btn;
            so.FindProperty("equipmentPull10Btn").objectReferenceValue = equip10Btn;
            so.FindProperty("equipmentPull1CostText").objectReferenceValue = equip1Cost;
            so.FindProperty("equipmentPull10CostText").objectReferenceValue = equip10Cost;
            so.FindProperty("equipmentPityText").objectReferenceValue = equipPity;
            so.FindProperty("equipmentResultArea").objectReferenceValue = equipResults;

            // 무기
            so.FindProperty("weaponPull1Btn").objectReferenceValue = weapon1Btn;
            so.FindProperty("weaponPull10Btn").objectReferenceValue = weapon10Btn;
            so.FindProperty("weaponPull1CostText").objectReferenceValue = weapon1Cost;
            so.FindProperty("weaponPull10CostText").objectReferenceValue = weapon10Cost;
            so.FindProperty("weaponPityText").objectReferenceValue = weaponPity;
            so.FindProperty("weaponResultArea").objectReferenceValue = weaponResults;

            so.FindProperty("subTabNormal").colorValue = SubTabNormal;
            so.FindProperty("subTabSelected").colorValue = SubTabSelected;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[ShopPanel] 상점 패널 내부 구성 완료");
        }

        // ══════════ Header ══════════

        private static TextMeshProUGUI BuildHeader(Transform parent)
        {
            var header = CreateEmpty("Header", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 56;
            header.AddComponent<Image>().color = new Color(0.16f, 0.14f, 0.12f, 1f);
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(20, 20, 0, 0);

            // 타이틀
            var title = MakeTMP("Title", header.transform, "상점", 28, new Vector2(120, 44));
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Left;
            title.color = TextWhite;

            // 스페이서
            var spacer = CreateEmpty("Spacer", header.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
            spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);

            // 루비 아이콘
            var rubyIcon = CreateIcon("RubyIcon", header.transform,
                ITEM_ICON + "icon_gem_pink.png", 36, 36);

            // 루비 텍스트
            var rubyTmp = MakeTMP("RubyText", header.transform, "0", 26, new Vector2(140, 44));
            rubyTmp.alignment = TextAlignmentOptions.Left;
            rubyTmp.color = TextRuby;
            rubyTmp.fontStyle = FontStyles.Bold;

            return rubyTmp;
        }

        // ══════════ Sub Tab Bar ══════════

        private static void BuildSubTabBar(Transform parent, out Image[] tabBgs, out Button[] tabBtns)
        {
            var bar = CreateEmpty("SubTabBar", parent);
            bar.AddComponent<LayoutElement>().preferredHeight = 52;
            bar.AddComponent<Image>().color = new Color(0.13f, 0.12f, 0.11f, 1f);
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.spacing = 4; hlg.padding = new RectOffset(4, 4, 4, 4);

            string[] names = { "동료 소환", "장비 소환", "무기 소환" };
            string[] icons = { "gift.png", "star.png", "sword.png" };
            tabBgs = new Image[names.Length];
            tabBtns = new Button[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                var tab = new GameObject($"SubTab_{names[i]}");
                tab.transform.SetParent(bar.transform, false);
                var bg = tab.AddComponent<Image>();
                bg.color = i == 0 ? SubTabSelected : SubTabNormal;

                var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
                if (spr != null) { bg.sprite = spr; bg.type = Image.Type.Sliced; }

                var btn = tab.AddComponent<Button>();
                btn.targetGraphic = bg;
                tabBgs[i] = bg;
                tabBtns[i] = btn;

                var tabHlg = tab.AddComponent<HorizontalLayoutGroup>();
                tabHlg.childAlignment = TextAnchor.MiddleCenter;
                tabHlg.childControlWidth = false; tabHlg.childControlHeight = false;
                tabHlg.spacing = 6;
                tabHlg.padding = new RectOffset(8, 8, 0, 0);

                CreateIcon($"Icon_{names[i]}", tab.transform, BTN_ICON + icons[i], 28, 28);
                var tabTmp = MakeTMP($"Text_{names[i]}", tab.transform, names[i], 18, new Vector2(100, 40));
                tabTmp.alignment = TextAlignmentOptions.Center;
                tabTmp.color = i == 0 ? TextWhite : TextTan;
            }
        }

        // ══════════ Gacha Content ══════════

        private static GameObject BuildGachaContent(
            Transform parent, string name, string title, string desc, string bannerIcon,
            out Button pull1Btn, out Button pull10Btn,
            out TextMeshProUGUI pull1Cost, out TextMeshProUGUI pull10Cost,
            out TextMeshProUGUI pityText, out Transform resultArea)
        {
            var content = CreateEmpty(name, parent);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 12;
            vlg.padding = new RectOffset(16, 16, 12, 12);

            // ── 배너 영역 ──
            var banner = CreateEmpty("Banner", content.transform);
            banner.AddComponent<LayoutElement>().preferredHeight = 150;
            var bannerBg = banner.AddComponent<Image>();
            bannerBg.color = new Color(0.14f, 0.13f, 0.12f, 1f);

            var bannerVlg = banner.AddComponent<VerticalLayoutGroup>();
            bannerVlg.childAlignment = TextAnchor.MiddleCenter;
            bannerVlg.childControlWidth = false; bannerVlg.childControlHeight = true;
            bannerVlg.spacing = 6;

            CreateIcon("BannerIcon", banner.transform, bannerIcon, 64, 64);

            var titleTmp = MakeTMP("Title", banner.transform, title, 28, new Vector2(300, 36));
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = TextGold;

            var descTmp = MakeTMP("Desc", banner.transform, desc, 16, new Vector2(400, 44));
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.color = TextTan;

            // ── 천장 정보 ──
            var pityObj = MakeTMP("PityText", content.transform, "천장까지 100회", 20, new Vector2(300, 40));
            pityObj.alignment = TextAlignmentOptions.Center;
            pityObj.color = TextWhite;
            pityObj.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            pityText = pityObj;

            // ── 뽑기 버튼 영역 ──
            var btnArea = CreateEmpty("ButtonArea", content.transform);
            btnArea.AddComponent<LayoutElement>().preferredHeight = 86;
            var btnHlg = btnArea.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childAlignment = TextAnchor.MiddleCenter;
            btnHlg.childControlWidth = false; btnHlg.childControlHeight = false;
            btnHlg.childForceExpandWidth = false;
            btnHlg.spacing = 20;

            // 1회 뽑기
            pull1Btn = BuildPullButton(btnArea.transform, "Pull1Btn", "1회 뽑기", "300",
                BtnPurple, out pull1Cost);

            // 10회 뽑기
            pull10Btn = BuildPullButton(btnArea.transform, "Pull10Btn", "10연차", "2,700",
                BtnGold, out pull10Cost);

            // ── 결과 영역 (스크롤뷰) ──
            var resultScroll = BuildResultScrollView(content.transform);
            resultArea = resultScroll;

            return content;
        }

        private static Button BuildPullButton(Transform parent, string name, string label, string cost,
            Color bgColor, out TextMeshProUGUI costText)
        {
            var btnObj = CreateEmpty(name, parent);
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 76);

            var bg = btnObj.AddComponent<Image>();
            var spr = LoadSprite(SPRITE + "Button/Button03_n.png");
            if (spr != null) { bg.sprite = spr; bg.type = Image.Type.Sliced; bg.color = bgColor; }
            else bg.color = bgColor;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            var sprS = LoadSprite(SPRITE + "Button/Button03_s.png");
            if (spr != null && sprS != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState { pressedSprite = sprS };
            }

            var vlg = btnObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(0, 0, 4, 4);

            var labelTmp = MakeTMP("Label", btnObj.transform, label, 20, new Vector2(180, 28));
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.color = TextWhite;

            // 비용 행
            var costRow = CreateEmpty("CostRow", btnObj.transform);
            costRow.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 28);
            var costHlg = costRow.AddComponent<HorizontalLayoutGroup>();
            costHlg.childAlignment = TextAnchor.MiddleCenter;
            costHlg.childControlWidth = false; costHlg.childControlHeight = false;
            costHlg.spacing = 4;

            CreateIcon("RubyIcon", costRow.transform, ITEM_ICON + "icon_gem_pink.png", 22, 22);
            costText = MakeTMP("CostText", costRow.transform, cost, 18, new Vector2(100, 28));
            costText.alignment = TextAlignmentOptions.Left;
            costText.color = TextWhite;

            return btn;
        }

        private static Transform BuildResultScrollView(Transform parent)
        {
            var scrollObj = CreateEmpty("ResultScroll", parent);
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
            contentVlg.childControlWidth = true; contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true; contentVlg.childForceExpandHeight = false;
            contentVlg.spacing = 6;
            contentVlg.padding = new RectOffset(8, 8, 8, 8);

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;

            return contentObj.transform;
        }

        // ══════════ Utilities ══════════

        private static Sprite LoadSprite(string path)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr == null)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                { imp.textureType = TextureImporterType.Sprite; imp.SaveAndReimport(); spr = AssetDatabase.LoadAssetAtPath<Sprite>(path); }
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

        private static GameObject CreateIcon(string name, Transform parent, string spritePath, float w, float h)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            var spr = LoadSprite(spritePath);
            if (spr != null) { img.sprite = spr; img.preserveAspect = true; }
            else img.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return obj;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size, Vector2 sizeDelta)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>().sizeDelta = sizeDelta;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = Color.white;
            ApplyKoreanFont(tmp);
            return tmp;
        }

        private static void ApplyKoreanFont(TextMeshProUGUI tmp)
        {
            var f = FontSetupEditor.GetOrCreateKoreanFont();
            if (f != null) tmp.font = f;
        }

        private static void FullStretch(GameObject obj)
        {
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
        }
    }
}
