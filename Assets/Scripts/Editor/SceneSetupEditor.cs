using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// UI Canvas + HUD + TabBar 전체 셋업 (세로 뷰 1080x1920).
    /// GUI Kit - Dark Geo 스프라이트/프리팹 활용.
    /// 탭: 캐릭터(장비+동료 포함) / 던전 / 상점 (3개)
    /// 전투는 기본 화면, 기타(설정)는 상단 톱니 아이콘.
    /// </summary>
    public static class SceneSetupEditor
    {
        private const string KIT = "Assets/Folder_Assets/GUI Kit - Dark Geo/";
        private const string PREFAB = KIT + "Prefab/";
        private const string SPRITE = KIT + "ResourceData/Sprite/Component/";
        private const string BTN_ICON = SPRITE + "Icon_ButtonIcon_(x2)/128/";
        private const string ITEM_ICON = SPRITE + "Icon_ItemIcon_(x2)/128/";
        private const string SL = SPRITE + "Slider/";

        private const string PREFAB_SLIDER02_RED = PREFAB + "Prefabs_Component_Sliders/Slider02_Red.prefab";

        // 세로 뷰: 상단 2줄
        private const float TOP_ROW1_H = 56f;   // Kill | Stage | Setting
        private const float TOP_ROW2_H = 62f;   // Lv/CP | HP | Gold | Gem
        private const float TOP_TOTAL_H = TOP_ROW1_H + TOP_ROW2_H; // 118
        private const float TAB_BAR_H = 130f;   // 3개 탭 → 넉넉하게
        private const float EXP_BAR_H = 44f;

        private static readonly Color TopBarBg = new Color(0.11f, 0.10f, 0.09f, 0.97f);
        private static readonly Color TextWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new Color(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color TabBarBg = new Color(0.12f, 0.11f, 0.10f, 1f);

        // ══════════ Public API ══════════

        public static void CreateAll()
        {
            FontSetupEditor.SetupKoreanFont();
            CreateManagers();
            CreateUICanvas();
            Debug.Log("[SceneSetup] 전체 셋업 완료!");
        }

        public static void CreateManagers()
        {
            CreateManager<MkLike.Core.GameManager>("GameManager");
            CreateManager<MkLike.Core.Save.SaveManager>("SaveManager");
            CreateManager<MkLike.Data.DataManager>("DataManager");
            CreateManager<MkLike.Economy.CurrencyManager>("CurrencyManager");
            CreateManager<MkLike.Core.SettingsManager>("SettingsManager");
            CreateManager<MkLike.Core.AudioManager>("AudioManager");
            CreateManager<MkLike.Core.PerformanceManager>("PerformanceManager");
            CreateManager<MkLike.Core.TutorialManager>("TutorialManager");
            CreateManager<MkLike.Utils.UpdateManager>("UpdateManager");
            Debug.Log("[SceneSetup] 매니저 9개 생성 완료!");
        }

        // ══════════ UI Canvas (세로 1080x1920) ══════════

        public static void CreateUICanvas()
        {
            FontSetupEditor.SetupKoreanFont();

            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            var uiManager = canvasObj.AddComponent<MkLike.UI.UIManager>();

            // HUD (전투 화면이 기본)
            var hudRoot = CreateEmpty("HudRoot", canvasObj.transform);
            SetFullStretch(hudRoot);
            var hudPanel = hudRoot.AddComponent<MkLike.UI.HudPanel>();
            var topResult = BuildTopSection(hudRoot.transform);
            var expResult = BuildExpBar(hudRoot.transform);

            // PopupParent (탭 팝업 + 일반 팝업 모두 여기에)
            var popupParent = CreateEmpty("PopupParent", canvasObj.transform);
            var ppRect = popupParent.GetComponent<RectTransform>();
            ppRect.anchorMin = Vector2.zero; ppRect.anchorMax = Vector2.one;
            ppRect.offsetMin = new Vector2(0, TAB_BAR_H + EXP_BAR_H);
            ppRect.offsetMax = new Vector2(0, -TOP_TOTAL_H);

            // 탭 팝업 패널 (PopupParent 아래, 기본 비활성)
            string[] panelNames = { "Panel_Character", "Panel_Dungeon", "Panel_Shop" };
            var panelObjects = new GameObject[panelNames.Length];
            for (int i = 0; i < panelNames.Length; i++)
            {
                var panel = new GameObject(panelNames[i]);
                panel.transform.SetParent(popupParent.transform, false);
                panel.AddComponent<Image>().color = new Color(0.12f, 0.11f, 0.11f, 0.94f);

                SetFullStretch(panel);
                switch (i)
                {
                    case 0: // Panel_Character: 서브탭 + 스탯 등
                        CharacterPanelSetupEditor.Build(panel);
                        break;
                    case 1: // Panel_Dungeon: 성장 던전 / 월드보스 / 보스 레이드
                        DungeonPanelSetupEditor.Build(panel);
                        break;
                    case 2: // Panel_Shop: 가챠 상점 (동료/장비 소환)
                        ShopPanelSetupEditor.Build(panel);
                        break;
                    default:
                        var lbl = CreateTMP(panelNames[i] + "_Label", panel.transform, panelNames[i], 32);
                        SetFullStretch(lbl);
                        lbl.GetComponent<TextMeshProUGUI>().color = TextTan;
                        break;
                }

                panel.SetActive(false);
                panelObjects[i] = panel;
            }

            // 하단 UI 영역 불투명 배경 (전투 오브젝트가 비치지 않도록)
            BuildBottomBarBackground(canvasObj.transform);

            // TabBar
            BuildTabBar(canvasObj.transform, panelObjects);

            // 재도전 버튼 (파밍 모드에서 표시, 기본 비활성)
            var retryBtn = BuildRetryButton(hudRoot.transform);

            // 스킬 슬롯 (EXP 바 위)
            var skillSlots = BuildSkillSlots(hudRoot.transform);

            // Wire
            WireHudPanel(hudPanel, topResult, expResult, retryBtn, skillSlots);
            var uiSo = new SerializedObject(uiManager);
            uiSo.FindProperty("popupParent").objectReferenceValue = popupParent.GetComponent<RectTransform>();
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            Selection.activeGameObject = canvasObj;
            Debug.Log("[SceneSetup] UI Canvas 생성 완료 (세로 뷰)!");
        }

        // ══════════ Top Section (2줄) ══════════

        private struct TopBarResult
        {
            public TextMeshProUGUI killCountText, stageNameText, stageProgressText;
            public TextMeshProUGUI levelText, cpText, goldText, rubyText, hpText;
            public Slider hpSlider;
        }

        private static TopBarResult BuildTopSection(Transform parent)
        {
            // 전체 상단 컨테이너
            var topSection = new GameObject("TopSection");
            topSection.transform.SetParent(parent, false);
            var tsRect = topSection.AddComponent<RectTransform>();
            tsRect.anchorMin = new Vector2(0, 1); tsRect.anchorMax = new Vector2(1, 1);
            tsRect.pivot = new Vector2(0.5f, 1f);
            tsRect.sizeDelta = new Vector2(0, TOP_TOTAL_H);
            topSection.AddComponent<Image>().color = TopBarBg;

            var vlg = topSection.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0; vlg.padding = new RectOffset(0, 0, 0, 0);

            // ── Row 1: Kill | Stage | Setting ──
            var row1 = CreateEmpty("Row1", topSection.transform);
            row1.AddComponent<LayoutElement>().preferredHeight = TOP_ROW1_H;
            var r1Hlg = row1.AddComponent<HorizontalLayoutGroup>();
            r1Hlg.childAlignment = TextAnchor.MiddleCenter;
            r1Hlg.childControlWidth = true; r1Hlg.childControlHeight = false;
            r1Hlg.childForceExpandWidth = false;
            r1Hlg.spacing = 0; r1Hlg.padding = new RectOffset(16, 16, 0, 0);

            // Kill 그룹
            var killGrp = CreateEmpty("KillGroup", row1.transform);
            killGrp.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 46);
            var killLE = killGrp.AddComponent<LayoutElement>();
            killLE.preferredWidth = 130; killLE.flexibleWidth = 0;
            var kHlg = killGrp.AddComponent<HorizontalLayoutGroup>();
            kHlg.childAlignment = TextAnchor.MiddleLeft;
            kHlg.childControlWidth = false; kHlg.childControlHeight = false;
            kHlg.spacing = 6;
            CreateIcon("SkullIcon", killGrp.transform, ITEM_ICON + "icon_skull.png", 36, 36);
            var killTmp = MakeTMP("KillText", killGrp.transform, "x 0", 22, new Vector2(80, 36));
            killTmp.alignment = TextAlignmentOptions.Left;
            killTmp.color = TextWhite;

            // 중앙 Stage (flexible)
            var stageGrp = CreateEmpty("StageGroup", row1.transform);
            stageGrp.AddComponent<LayoutElement>().flexibleWidth = 1;
            stageGrp.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 46);
            var sVlg = stageGrp.AddComponent<VerticalLayoutGroup>();
            sVlg.childAlignment = TextAnchor.MiddleCenter;
            sVlg.childControlWidth = true; sVlg.childControlHeight = true;
            sVlg.childForceExpandWidth = true; sVlg.childForceExpandHeight = true;
            sVlg.spacing = -2;
            var snTmp = MakeTMP("StageName", stageGrp.transform, "1-1", 26, Vector2.zero);
            snTmp.fontStyle = FontStyles.Bold;
            snTmp.alignment = TextAlignmentOptions.Center;
            snTmp.color = TextWhite;
            var spTmp = MakeTMP("StageProgress", stageGrp.transform, "스테이지 1/10", 18, Vector2.zero);
            spTmp.alignment = TextAlignmentOptions.Center;
            spTmp.color = TextTan;

            // Setting 아이콘
            var stBtn = CreateIcon("Setting", row1.transform, BTN_ICON + "setting.png", 40, 40);
            stBtn.AddComponent<Button>().targetGraphic = stBtn.GetComponent<Image>();
            var stLE = stBtn.AddComponent<LayoutElement>();
            stLE.preferredWidth = 40; stLE.flexibleWidth = 0;

            // ── Row 2: Lv/CP | HP | Gold | Gem ──
            var row2 = CreateEmpty("Row2", topSection.transform);
            row2.AddComponent<LayoutElement>().preferredHeight = TOP_ROW2_H;
            var r2Hlg = row2.AddComponent<HorizontalLayoutGroup>();
            r2Hlg.childAlignment = TextAnchor.MiddleCenter;
            r2Hlg.childControlWidth = false; r2Hlg.childControlHeight = false;
            r2Hlg.childForceExpandWidth = false;
            r2Hlg.spacing = 8; r2Hlg.padding = new RectOffset(12, 12, 4, 4);

            // Lv+CP
            var lvCp = CreateEmpty("LvCp", row2.transform);
            lvCp.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 50);
            var lcVlg = lvCp.AddComponent<VerticalLayoutGroup>();
            lcVlg.childAlignment = TextAnchor.MiddleLeft;
            lcVlg.childControlWidth = true; lcVlg.childControlHeight = true;
            lcVlg.childForceExpandWidth = true; lcVlg.childForceExpandHeight = true;
            lcVlg.spacing = -2;
            var lvTmp = MakeTMP("LevelText", lvCp.transform, "Lv.1", 24, Vector2.zero);
            lvTmp.fontStyle = FontStyles.Bold;
            lvTmp.alignment = TextAlignmentOptions.Left;
            lvTmp.color = TextWhite;
            var cpTmp = MakeTMP("CPText", lvCp.transform, "전투력 ---", 18, Vector2.zero);
            cpTmp.alignment = TextAlignmentOptions.Left;
            cpTmp.color = TextGold;
            cpTmp.enableAutoSizing = true;
            cpTmp.fontSizeMin = 12;
            cpTmp.fontSizeMax = 18;
            cpTmp.overflowMode = TextOverflowModes.Overflow;

            // HP bar
            var hpRes = BuildHP(row2.transform);

            // Gold
            var goldTmp = BuildStatusBar("Gold", row2.transform,
                SPRITE + "UI_Etc/status_icon_coin.png", "0", 190, 48);

            // Gem (Ruby)
            var gemTmp = BuildStatusBar("Gem", row2.transform,
                SPRITE + "UI_Etc/Status_Icon_Gem.png", "0", 165, 48);

            return new TopBarResult
            {
                killCountText = killTmp, stageNameText = snTmp, stageProgressText = spTmp,
                levelText = lvTmp, cpText = cpTmp, goldText = goldTmp, rubyText = gemTmp,
                hpSlider = hpRes.slider, hpText = hpRes.hpText
            };
        }

        // ══════════ HP: Slider02_Red 프리팹 ══════════

        private struct HpResult { public Slider slider; public TextMeshProUGUI hpText; }

        private static HpResult BuildHP(Transform parent)
        {
            // 세로 뷰에서 Row2에 맞는 크기: 0.78 스케일 (357x48)
            const float SCALE = 0.78f;
            const float VIS_W = 457f * SCALE;
            const float VIS_H = 62f * SCALE;

            var wrapper = CreateEmpty("HP_Wrapper", parent);
            wrapper.GetComponent<RectTransform>().sizeDelta = new Vector2(VIS_W, VIS_H);

            // HP 바 배경 (어두운 회색으로 가시성 확보)
            var wrapperImg = wrapper.AddComponent<Image>();
            wrapperImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var inst = InstantiatePrefab(PREFAB_SLIDER02_RED, wrapper.transform);
            Slider slider = null;

            if (inst != null)
            {
                inst.name = "HP_Slider";
                inst.transform.localScale = Vector3.one * SCALE;
                var ir = inst.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.anchoredPosition = Vector2.zero;

                slider = inst.GetComponent<Slider>();
                if (slider != null) { slider.value = 1f; slider.interactable = false; }

                var iconTf = inst.transform.Find("Icon");
                if (iconTf != null)
                {
                    var iconImg = iconTf.GetComponent<Image>();
                    var lifeSpr = LoadSprite(SPRITE + "UI_Etc/Status_Icon_Life00.png");
                    if (iconImg != null && lifeSpr != null)
                    {
                        iconImg.sprite = lifeSpr;
                        iconImg.preserveAspect = true;
                    }
                }
                ApplyKoreanFontToAll(inst);
            }

            var hpObj = CreateTMP("HpText", wrapper.transform, "0/0", 16);
            var hr = hpObj.GetComponent<RectTransform>();
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(36, 0); hr.offsetMax = new Vector2(-6, 0);
            var hpTmp = hpObj.GetComponent<TextMeshProUGUI>();
            hpTmp.alignment = TextAlignmentOptions.Center;
            hpTmp.color = TextWhite;
            hpTmp.enableAutoSizing = true;
            hpTmp.fontSizeMin = 14; hpTmp.fontSizeMax = 22;
            hpObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            return new HpResult { slider = slider, hpText = hpTmp };
        }

        // ══════════ Status Bar (Gold/Gem) ══════════

        private static TextMeshProUGUI BuildStatusBar(
            string name, Transform parent, string iconPath, string text, float w, float h)
        {
            var grp = new GameObject("Status_" + name);
            grp.transform.SetParent(parent, false);
            grp.AddComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var bg = grp.AddComponent<Image>();
            var frameSpr = LoadSprite(SPRITE + "UI_Etc/Status_Frame_Small.png");
            if (frameSpr != null) { bg.sprite = frameSpr; bg.type = Image.Type.Sliced; }
            else bg.color = new Color(0.22f, 0.20f, 0.18f, 0.9f);

            float iconSz = h * 1.15f;
            var icon = CreateIcon("Icon", grp.transform, iconPath, iconSz, iconSz);
            var iconR = icon.GetComponent<RectTransform>();
            iconR.anchorMin = new Vector2(0, 0.5f); iconR.anchorMax = new Vector2(0, 0.5f);
            iconR.pivot = new Vector2(0.3f, 0.5f);
            iconR.anchoredPosition = Vector2.zero;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(grp.transform, false);
            var tr = txtObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(iconSz * 0.5f, 0);
            tr.offsetMax = new Vector2(-h * 0.5f, 0);
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextTan;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 14; tmp.fontSizeMax = 24;
            ApplyKoreanFont(tmp);

            var addBtn = CreateIcon("AddBtn", grp.transform,
                SPRITE + "UI_Etc/Status_Btn_Add_n.png", h * 0.6f, h * 0.6f);
            var ar = addBtn.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(1, 0.5f); ar.anchorMax = new Vector2(1, 0.5f);
            ar.pivot = new Vector2(0.5f, 0.5f); ar.anchoredPosition = new Vector2(2, 0);
            addBtn.AddComponent<Button>().targetGraphic = addBtn.GetComponent<Image>();

            return tmp;
        }

        // ══════════ EXP: 스프라이트 수동 빌드 ══════════

        private struct ExpResult { public Slider slider; public TextMeshProUGUI text; }

        private static ExpResult BuildExpBar(Transform parent)
        {
            var container = new GameObject("BottomExpBar");
            container.transform.SetParent(parent, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 0); cRect.anchorMax = new Vector2(1, 0);
            cRect.pivot = new Vector2(0.5f, 0);
            cRect.anchoredPosition = new Vector2(0, TAB_BAR_H);
            cRect.sizeDelta = new Vector2(0, EXP_BAR_H);

            // Frame
            var frameSpr = LoadSprite(SL + "Slider01_Frame.png");
            var frameObj = new GameObject("Frame");
            frameObj.transform.SetParent(container.transform, false);
            var frameImg = frameObj.AddComponent<Image>();
            if (frameSpr != null) { frameImg.sprite = frameSpr; frameImg.type = Image.Type.Sliced; }
            else frameImg.color = new Color(0.18f, 0.16f, 0.14f, 1f);
            SetFullStretch(frameObj);

            // InnerFrame
            var innerSpr = LoadSprite(SL + "Slider01_InnerFrame.png");
            var innerObj = new GameObject("InnerFrame");
            innerObj.transform.SetParent(container.transform, false);
            var innerImg = innerObj.AddComponent<Image>();
            if (innerSpr != null) { innerImg.sprite = innerSpr; innerImg.type = Image.Type.Sliced; }
            else innerImg.color = new Color(0.10f, 0.09f, 0.08f, 1f);
            var innerR = innerObj.GetComponent<RectTransform>();
            innerR.anchorMin = Vector2.zero; innerR.anchorMax = Vector2.one;
            innerR.offsetMin = new Vector2(6, 6); innerR.offsetMax = new Vector2(-6, -6);

            // FillArea
            var fillAreaSpr = LoadSprite(SL + "Slider01_FillArea.png");
            var fillAreaObj = new GameObject("FillArea");
            fillAreaObj.transform.SetParent(container.transform, false);
            var fillAreaImg = fillAreaObj.AddComponent<Image>();
            if (fillAreaSpr != null) { fillAreaImg.sprite = fillAreaSpr; fillAreaImg.type = Image.Type.Sliced; }
            else fillAreaImg.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            var faR = fillAreaObj.GetComponent<RectTransform>();
            faR.anchorMin = Vector2.zero; faR.anchorMax = Vector2.one;
            faR.offsetMin = new Vector2(10, 10); faR.offsetMax = new Vector2(-10, -10);

            // Fill
            var fillSpr = LoadSprite(SL + "Slider01_Fill.png");
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillImg = fillObj.AddComponent<Image>();
            if (fillSpr != null) { fillImg.sprite = fillSpr; fillImg.type = Image.Type.Sliced; }
            fillImg.color = new Color(0.45f, 0.85f, 0.25f, 1f); // 연두색
            var fillR = fillObj.GetComponent<RectTransform>();
            fillR.anchorMin = Vector2.zero; fillR.anchorMax = new Vector2(0, 1);
            fillR.offsetMin = Vector2.zero; fillR.offsetMax = Vector2.zero;

            var slider = container.AddComponent<Slider>();
            slider.fillRect = fillR;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            var expObj = CreateTMP("ExpText", container.transform, "경험치 0%", 22);
            SetFullStretch(expObj);
            var expTmp = expObj.GetComponent<TextMeshProUGUI>();
            expTmp.alignment = TextAlignmentOptions.Center;
            expTmp.color = TextWhite;
            expObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            return new ExpResult { slider = slider, text = expTmp };
        }

        // ══════════ 하단 UI 불투명 배경 ══════════

        /// <summary>
        /// 하단 UI 전체(스킬 슬롯 + EXP 바 + 탭 바) 뒤에 불투명 배경을 배치하여
        /// 전투 오브젝트(몬스터, 이펙트, 경험치 구슬)가 비치지 않도록 한다.
        /// </summary>
        private static void BuildBottomBarBackground(Transform parent)
        {
            float totalHeight = TAB_BAR_H + EXP_BAR_H + SKILL_BAR_H;

            var bgObj = new GameObject("BottomBarBackground");
            bgObj.transform.SetParent(parent, false);
            bgObj.transform.SetAsFirstSibling(); // 가장 먼저 렌더링되어 다른 UI 요소 뒤에 깔림

            var rect = bgObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, totalHeight);

            var img = bgObj.AddComponent<Image>();
            img.color = new Color(0.08f, 0.07f, 0.06f, 1f); // 완전 불투명 어두운 배경
            img.raycastTarget = false; // 터치 이벤트 차단 안 함
        }

        // ══════════ Tab Bar: 3개 (캐릭터/던전/상점) ══════════

        private static void BuildTabBar(Transform parent, GameObject[] panelObjects)
        {
            var tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(parent, false);
            var rect = tabBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, TAB_BAR_H);
            tabBar.AddComponent<Image>().color = TabBarBg;

            var border = new GameObject("TopBorder");
            border.transform.SetParent(tabBar.transform, false);
            border.AddComponent<Image>().color = new Color(0.30f, 0.27f, 0.23f, 0.6f);
            var bRect = border.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0, 1); bRect.anchorMax = new Vector2(1, 1);
            bRect.pivot = new Vector2(0.5f, 1f); bRect.sizeDelta = new Vector2(0, 1);
            border.AddComponent<LayoutElement>().ignoreLayout = true;

            var hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8; hlg.padding = new RectOffset(12, 12, 6, 6);

            var tabCtrl = tabBar.AddComponent<MkLike.UI.TabController>();

            // 3개 탭만: 캐릭터(장비+동료 포함), 던전, 상점
            string[] names = { "캐릭터", "던전", "상점" };
            string[] icons = { "profile.png", "castle.png", "store.png" };
            var btnN = LoadSprite(SPRITE + "Button/Button01_n.png");
            var btnS = LoadSprite(SPRITE + "Button/Button01_s.png");

            var tabBtns = new MkLike.UI.TabButton[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                var bObj = new GameObject($"Tab_{names[i]}");
                bObj.transform.SetParent(tabBar.transform, false);
                var bImg = bObj.AddComponent<Image>();
                bObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 106);
                if (btnN != null) { bImg.sprite = btnN; bImg.type = Image.Type.Sliced; }
                else bImg.color = new Color(0.20f, 0.18f, 0.16f, 1f);

                var btn = bObj.AddComponent<Button>();
                btn.targetGraphic = bImg;
                if (btnN != null && btnS != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    btn.spriteState = new SpriteState { pressedSprite = btnS };
                }

                var bVlg = bObj.AddComponent<VerticalLayoutGroup>();
                bVlg.childAlignment = TextAnchor.MiddleCenter;
                bVlg.childControlWidth = false; bVlg.childControlHeight = false;
                bVlg.childForceExpandWidth = false; bVlg.childForceExpandHeight = false;
                bVlg.spacing = 5; bVlg.padding = new RectOffset(0, 0, 10, 8);

                CreateIcon("Icon", bObj.transform, BTN_ICON + icons[i], 52, 52);
                var tTmp = MakeTMP("Text", bObj.transform, names[i], 22, new Vector2(100, 30));
                tTmp.alignment = TextAlignmentOptions.Center;
                tTmp.color = TextTan;

                var tabBtn = bObj.AddComponent<MkLike.UI.TabButton>();
                tabBtns[i] = tabBtn;

                var so = new SerializedObject(tabBtn);
                so.FindProperty("tabIndex").intValue = i;
                so.FindProperty("backgroundImage").objectReferenceValue = bImg;
                so.FindProperty("normalColor").colorValue = Color.white;
                so.FindProperty("selectedColor").colorValue = new Color(1f, 0.95f, 0.85f, 1f);
                so.FindProperty("tabText").objectReferenceValue = tTmp;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var tcSo = new SerializedObject(tabCtrl);
            var bp = tcSo.FindProperty("tabButtons"); bp.arraySize = tabBtns.Length;
            for (int i = 0; i < tabBtns.Length; i++) bp.GetArrayElementAtIndex(i).objectReferenceValue = tabBtns[i];
            var pp = tcSo.FindProperty("tabPanels"); pp.arraySize = panelObjects.Length;
            for (int i = 0; i < panelObjects.Length; i++) pp.GetArrayElementAtIndex(i).objectReferenceValue = panelObjects[i];
            tcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ══════════ Skill Slots (EXP 바 바로 위) ══════════

        private const int SKILL_SLOT_COUNT = 4;
        private const float SKILL_SLOT_SIZE = 76f;
        private const float SKILL_BAR_H = 88f;

        private static readonly Color CooldownOverlayColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color DurationBarColor = new Color(0.3f, 0.85f, 1f, 0.9f);
        private static readonly Color ActiveBorderColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        private static MkLike.UI.SkillSlotUI[] BuildSkillSlots(Transform parent)
        {
            var container = new GameObject("SkillSlotBar");
            container.transform.SetParent(parent, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 0);
            cRect.anchorMax = new Vector2(1, 0);
            cRect.pivot = new Vector2(0.5f, 0);
            cRect.anchoredPosition = new Vector2(0, TAB_BAR_H + EXP_BAR_H);
            cRect.sizeDelta = new Vector2(0, SKILL_BAR_H);

            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 12;
            hlg.padding = new RectOffset(16, 16, 4, 4);

            var slots = new MkLike.UI.SkillSlotUI[SKILL_SLOT_COUNT];

            for (int i = 0; i < SKILL_SLOT_COUNT; i++)
            {
                slots[i] = BuildSingleSkillSlot(container.transform, i);
            }

            return slots;
        }

        private static MkLike.UI.SkillSlotUI BuildSingleSkillSlot(Transform parent, int index)
        {
            var slotObj = new GameObject($"SkillSlot_{index}");
            slotObj.transform.SetParent(parent, false);
            var slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(SKILL_SLOT_SIZE, SKILL_SLOT_SIZE + 11f);

            var canvasGroup = slotObj.AddComponent<CanvasGroup>();

            // 슬롯 배경 프레임
            var frameSpr = LoadSprite(SPRITE + "Frame/ItemFrame01.png");
            var frameObj = new GameObject("Frame");
            frameObj.transform.SetParent(slotObj.transform, false);
            var frameImg = frameObj.AddComponent<Image>();
            if (frameSpr != null) { frameImg.sprite = frameSpr; frameImg.type = Image.Type.Sliced; }
            else frameImg.color = new Color(0.22f, 0.20f, 0.18f, 0.9f);
            var frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = new Vector2(1, 1);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = new Vector2(0, -8f);

            // 스킬 아이콘
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            iconImg.preserveAspect = true;
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.1f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = new Vector2(0, -8f);

            // 쿨타임 오버레이 (Filled, Radial360)
            var cdObj = new GameObject("CooldownOverlay");
            cdObj.transform.SetParent(slotObj.transform, false);
            var cdImg = cdObj.AddComponent<Image>();
            cdImg.color = CooldownOverlayColor;
            cdImg.type = Image.Type.Filled;
            cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.fillOrigin = (int)Image.Origin360.Top;
            cdImg.fillClockwise = false;
            cdImg.fillAmount = 0f;
            cdImg.raycastTarget = false;
            var cdRect = cdObj.GetComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(0.05f, 0.05f);
            cdRect.anchorMax = new Vector2(0.95f, 0.95f);
            cdRect.offsetMin = Vector2.zero;
            cdRect.offsetMax = new Vector2(0, -8f);
            cdObj.SetActive(false);

            // 쿨타임 텍스트
            var cdTextObj = new GameObject("CooldownText");
            cdTextObj.transform.SetParent(slotObj.transform, false);
            var cdTextRect = cdTextObj.AddComponent<RectTransform>();
            cdTextRect.anchorMin = Vector2.zero;
            cdTextRect.anchorMax = new Vector2(1, 1);
            cdTextRect.offsetMin = Vector2.zero;
            cdTextRect.offsetMax = new Vector2(0, -8f);
            var cdTmp = cdTextObj.AddComponent<TextMeshProUGUI>();
            cdTmp.text = "";
            cdTmp.fontSize = 18;
            cdTmp.fontStyle = FontStyles.Bold;
            cdTmp.alignment = TextAlignmentOptions.Center;
            cdTmp.color = Color.white;
            cdTmp.raycastTarget = false;
            ApplyKoreanFont(cdTmp);
            cdTextObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            cdTextObj.SetActive(false);

            // 활성 보더 (버프 활성 시 빛남)
            var borderObj = new GameObject("ActiveBorder");
            borderObj.transform.SetParent(slotObj.transform, false);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = ActiveBorderColor;
            borderImg.raycastTarget = false;

            var borderFrameSpr = LoadSprite(SPRITE + "Frame/ItemFrame01.png");
            if (borderFrameSpr != null) { borderImg.sprite = borderFrameSpr; borderImg.type = Image.Type.Sliced; }

            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(-0.05f, -0.05f);
            borderRect.anchorMax = new Vector2(1.05f, 1.05f);
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = new Vector2(0, -8f);
            borderObj.transform.SetAsFirstSibling();
            borderObj.SetActive(false);

            // 지속시간 바 (하단 작은 바)
            var durObj = new GameObject("DurationBar");
            durObj.transform.SetParent(slotObj.transform, false);
            var durImg = durObj.AddComponent<Image>();
            durImg.color = DurationBarColor;
            durImg.type = Image.Type.Filled;
            durImg.fillMethod = Image.FillMethod.Horizontal;
            durImg.fillAmount = 1f;
            durImg.raycastTarget = false;
            var durRect = durObj.GetComponent<RectTransform>();
            durRect.anchorMin = new Vector2(0.05f, 0);
            durRect.anchorMax = new Vector2(0.95f, 0);
            durRect.pivot = new Vector2(0.5f, 0);
            durRect.anchoredPosition = Vector2.zero;
            durRect.sizeDelta = new Vector2(0, 6f);
            durObj.SetActive(false);

            // 자물쇠 아이콘 (미해금 시 표시)
            var lockObj = new GameObject("LockIcon");
            lockObj.transform.SetParent(slotObj.transform, false);
            var lockImg = lockObj.AddComponent<Image>();
            var lockSpr = LoadSprite("Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Icon_ItemIcon_(x2)/128/icon_lock.png");
            if (lockSpr != null) lockImg.sprite = lockSpr;
            lockImg.color = Color.white;
            lockImg.raycastTarget = false;
            lockImg.preserveAspect = true;
            var lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.2f, 0.2f);
            lockRect.anchorMax = new Vector2(0.8f, 0.8f);
            lockRect.offsetMin = Vector2.zero;
            lockRect.offsetMax = new Vector2(0, -8f);
            lockObj.SetActive(false);

            // SkillSlotUI 컴포넌트 추가 및 연결
            var slotUI = slotObj.AddComponent<MkLike.UI.SkillSlotUI>();
            var so = new SerializedObject(slotUI);
            so.FindProperty("_iconImage").objectReferenceValue = iconImg;
            so.FindProperty("_cooldownOverlay").objectReferenceValue = cdImg;
            so.FindProperty("_cooldownText").objectReferenceValue = cdTmp;
            so.FindProperty("_durationBar").objectReferenceValue = durImg;
            so.FindProperty("_activeBorder").objectReferenceValue = borderImg;
            so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_lockIcon").objectReferenceValue = lockImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            slotObj.SetActive(false);
            return slotUI;
        }

        // ══════════ Retry Button ══════════

        private static GameObject BuildRetryButton(Transform parent)
        {
            var btnObj = new GameObject("RetryButton");
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 280f);
            rect.sizeDelta = new Vector2(280, 80);

            var bg = btnObj.AddComponent<Image>();
            var btnSpr = LoadSprite(SPRITE + "Button/Button01_n.png");
            if (btnSpr != null) { bg.sprite = btnSpr; bg.type = Image.Type.Sliced; }
            else bg.color = new Color(0.25f, 0.22f, 0.20f, 1f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            var label = MakeTMP("Label", btnObj.transform, "재도전", 28, new Vector2(240, 60));
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = TextWhite;

            btnObj.SetActive(false);
            return btnObj;
        }

        // ══════════ Wire ══════════

        private static void WireHudPanel(MkLike.UI.HudPanel hud, TopBarResult t, ExpResult e,
            GameObject retryBtn, MkLike.UI.SkillSlotUI[] skillSlots)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_killCountText").objectReferenceValue = t.killCountText;
            so.FindProperty("_stageNameText").objectReferenceValue = t.stageNameText;
            so.FindProperty("_stageProgressText").objectReferenceValue = t.stageProgressText;
            so.FindProperty("_levelText").objectReferenceValue = t.levelText;
            so.FindProperty("_cpText").objectReferenceValue = t.cpText;
            so.FindProperty("_goldText").objectReferenceValue = t.goldText;
            so.FindProperty("_rubyText").objectReferenceValue = t.rubyText;
            so.FindProperty("_hpSlider").objectReferenceValue = t.hpSlider;
            so.FindProperty("_hpText").objectReferenceValue = t.hpText;
            so.FindProperty("_expSlider").objectReferenceValue = e.slider;
            so.FindProperty("_expText").objectReferenceValue = e.text;
            so.FindProperty("_retryButtonObj").objectReferenceValue = retryBtn;

            // 스킬 슬롯 배열 연결
            var slotsProp = so.FindProperty("_skillSlots");
            slotsProp.arraySize = skillSlots.Length;
            for (int i = 0; i < skillSlots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = skillSlots[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            // 재도전 버튼 OnClick → HudPanel.OnRetryButtonClicked
            var button = retryBtn.GetComponent<Button>();
            if (button != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    button.onClick, hud.OnRetryButtonClicked);
            }
        }

        // ══════════ Utilities ══════════

        private static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[SceneSetup] 프리팹 없음: {path}"); return null; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (inst != null) Undo.RegisterCreatedObjectUndo(inst, $"Create {prefab.name}");
            return inst;
        }

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

        private static void CreateManager<T>(string name) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() != null) { Debug.Log($"[SceneSetup] {name} 이미 존재"); return; }
            var obj = new GameObject(name); obj.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
        }

        private static GameObject CreateEmpty(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private static GameObject CreateTMP(string name, Transform parent, string text, float fontSize)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
            ApplyKoreanFont(tmp);
            return obj;
        }

        private static void ApplyKoreanFont(TextMeshProUGUI tmp)
        { var f = FontSetupEditor.GetOrCreateKoreanFont(); if (f != null) tmp.font = f; }

        private static void ApplyKoreanFontToAll(GameObject root)
        { var f = FontSetupEditor.GetOrCreateKoreanFont(); if (f == null) return;
          foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true)) t.font = f; }

        private static void SetFullStretch(GameObject obj)
        { var r = obj.GetComponent<RectTransform>();
          r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
          r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero; }
    }
}
