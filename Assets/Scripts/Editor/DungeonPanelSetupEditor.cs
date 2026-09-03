using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// Panel_Dungeon 내부 구성: 3개 서브탭 (성장 던전 / 월드보스 / 보스 레이드).
    /// SceneSetupEditor에서 호출된다.
    /// </summary>
    public static class DungeonPanelSetupEditor
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
        private static readonly Color RowBg = new Color(0.18f, 0.17f, 0.15f, 0.92f);
        private static readonly Color BtnGreen = new Color(0.25f, 0.55f, 0.30f, 1f);
        private static readonly Color BtnRed = new Color(0.60f, 0.22f, 0.22f, 1f);
        private static readonly Color BtnOrange = new Color(0.65f, 0.45f, 0.15f, 1f);

        private const int DUNGEON_ROW_COUNT = 5;
        private const int RAID_DIFFICULTY_COUNT = 3;

        public static void Build(GameObject panel)
        {
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null) bgImg.color = PanelBg;

            // 기존 자식 제거
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);

            // CanvasGroup (페이드 연출용)
            if (panel.GetComponent<CanvasGroup>() == null)
                panel.AddComponent<CanvasGroup>();

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0; vlg.padding = new RectOffset(0, 0, 0, 0);

            // 1. 헤더
            BuildHeader(panel.transform);

            // 2. 서브탭 바
            BuildSubTabBar(panel.transform, out var subTabBgs, out var subTabBtns);

            // 3. 콘텐츠 영역
            var contentArea = CreateEmpty("ContentArea", panel.transform);
            contentArea.AddComponent<LayoutElement>().flexibleHeight = 1;

            // 성장 던전 탭
            var growthContent = BuildGrowthDungeonContent(contentArea.transform,
                out var keyCountText, out var dungeonRows);

            // 월드보스 탭
            var worldBossContent = BuildWorldBossContent(contentArea.transform,
                out var wbStageText, out var wbHpText, out var wbBestDmgText,
                out var wbResultText, out var wbChallengeBtn);

            // 보스 레이드 탭
            var bossRaidContent = BuildBossRaidContent(contentArea.transform,
                out var raidAttemptsText, out var raidResultText,
                out var raidDiffBtns, out var raidDiffBgs, out var raidExecBtn);

            var subTabContents = new GameObject[] { growthContent, worldBossContent, bossRaidContent };

            for (int i = 0; i < subTabContents.Length; i++)
            {
                FullStretch(subTabContents[i]);
                subTabContents[i].SetActive(i == 0);
            }

            // 4. DungeonPanel 컴포넌트 와이어링
            var dp = panel.AddComponent<MkLike.UI.DungeonPanel>();
            var so = new SerializedObject(dp);

            // 서브 탭
            SetArray(so, "_subTabButtons", subTabBtns);
            SetArray(so, "_subTabBgs", subTabBgs);
            SetObjectArray(so, "_subTabContents", subTabContents);

            // 색상
            so.FindProperty("_subTabNormal").colorValue = SubTabNormal;
            so.FindProperty("_subTabSelected").colorValue = SubTabSelected;

            // 성장 던전
            so.FindProperty("_keyCountText").objectReferenceValue = keyCountText;

            // _dungeonRows (DungeonRowUI[])
            var rowsProp = so.FindProperty("_dungeonRows");
            rowsProp.arraySize = dungeonRows.Length;
            for (int i = 0; i < dungeonRows.Length; i++)
            {
                var elem = rowsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("icon").objectReferenceValue = dungeonRows[i].icon;
                elem.FindPropertyRelative("nameText").objectReferenceValue = dungeonRows[i].nameText;
                elem.FindPropertyRelative("rewardText").objectReferenceValue = dungeonRows[i].rewardText;
                elem.FindPropertyRelative("enterButton").objectReferenceValue = dungeonRows[i].enterButton;
            }

            // _dungeonCatalog는 SO 에셋이므로 비워둔다 (에디터에서 수동 할당 또는 Phase2에서 할당)

            // 월드보스
            so.FindProperty("_worldBossStageText").objectReferenceValue = wbStageText;
            so.FindProperty("_worldBossHpText").objectReferenceValue = wbHpText;
            so.FindProperty("_worldBossBestDamageText").objectReferenceValue = wbBestDmgText;
            so.FindProperty("_worldBossResultText").objectReferenceValue = wbResultText;
            so.FindProperty("_worldBossChallengeButton").objectReferenceValue = wbChallengeBtn;

            // 보스 레이드
            so.FindProperty("_raidAttemptsText").objectReferenceValue = raidAttemptsText;
            so.FindProperty("_raidResultText").objectReferenceValue = raidResultText;
            SetArray(so, "_raidDifficultyButtons", raidDiffBtns);
            SetArray(so, "_raidDifficultyBgs", raidDiffBgs);
            so.FindProperty("_raidExecuteButton").objectReferenceValue = raidExecBtn;

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[DungeonPanel] 던전 패널 내부 구성 완료");
        }

        // ══════════ Header ══════════

        private static void BuildHeader(Transform parent)
        {
            var header = CreateEmpty("Header", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 50;
            header.AddComponent<Image>().color = new Color(0.16f, 0.14f, 0.12f, 1f);
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(16, 16, 0, 0);

            var spacerL = CreateEmpty("SpacerL", header.transform);
            spacerL.AddComponent<LayoutElement>().flexibleWidth = 1;

            var title = MakeTMP("Title", header.transform, "던전", 28, Vector2.zero);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = TextWhite;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

            var spacerR = CreateEmpty("SpacerR", header.transform);
            spacerR.AddComponent<LayoutElement>().flexibleWidth = 1;

            var closeObj = CreateEmpty("CloseBtn", header.transform);
            closeObj.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);
            var closeLE = closeObj.AddComponent<LayoutElement>();
            closeLE.preferredWidth = 44; closeLE.preferredHeight = 44;
            closeLE.minWidth = 44;
            var closeImg = closeObj.AddComponent<Image>();
            var closeSpr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");
            if (closeSpr != null)
            {
                closeImg.sprite = closeSpr;
                closeImg.type = Image.Type.Simple;
                closeImg.preserveAspect = true;
            }
            else
            {
                closeImg.color = new Color(0.7f, 0.25f, 0.25f, 1f);
            }
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
        }

        // ══════════ Sub Tab Bar (3탭) ══════════

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

            string[] names = { "성장 던전", "월드보스", "보스 레이드" };
            string[] icons = { "castle.png", "skull.png", "crown.png" };
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

                CreateIcon($"Icon_{names[i]}", tab.transform, BTN_ICON + icons[i], 26, 26);
                var tabTmp = MakeTMP($"Text_{names[i]}", tab.transform, names[i], 20, new Vector2(110, 40));
                tabTmp.alignment = TextAlignmentOptions.Center;
                tabTmp.color = i == 0 ? TextWhite : TextTan;
            }
        }

        // ══════════ 성장 던전 콘텐츠 ══════════

        private struct DungeonRowRefs
        {
            public Image icon;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI rewardText;
            public Button enterButton;
        }

        private static GameObject BuildGrowthDungeonContent(Transform parent,
            out TextMeshProUGUI keyCountText, out DungeonRowRefs[] rows)
        {
            var content = CreateEmpty("Content_GrowthDungeon", parent);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 10; vlg.padding = new RectOffset(12, 12, 10, 10);

            // 열쇠 표시 행
            var keyRow = CreateEmpty("KeyRow", content.transform);
            keyRow.AddComponent<LayoutElement>().preferredHeight = 44;
            keyRow.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.14f, 0.9f);
            var keyHlg = keyRow.AddComponent<HorizontalLayoutGroup>();
            keyHlg.childAlignment = TextAnchor.MiddleCenter;
            keyHlg.childControlWidth = false; keyHlg.childControlHeight = false;
            keyHlg.spacing = 8; keyHlg.padding = new RectOffset(16, 16, 4, 4);

            CreateIcon("KeyIcon", keyRow.transform, ITEM_ICON + "icon_key.png", 30, 30);
            var keyLabel = MakeTMP("KeyLabel", keyRow.transform, "던전 열쇠", 18, new Vector2(120, 34));
            keyLabel.alignment = TextAlignmentOptions.Left;
            keyLabel.color = TextWhite;

            var spacer = CreateEmpty("Spacer", keyRow.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
            spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 34);

            keyCountText = MakeTMP("KeyCountText", keyRow.transform, "0 / 10", 24, new Vector2(100, 34));
            keyCountText.alignment = TextAlignmentOptions.Right;
            keyCountText.fontStyle = FontStyles.Bold;
            keyCountText.color = TextGold;

            // 던전 목록 (5종)
            BuildSectionHeader(content.transform, "던전 목록");

            string[] dungeonNames = { "골드 던전", "경험치 던전", "재료 던전", "장비 던전", "스타 던전" };
            string[] dungeonIcons = { "icon_coin.png", "icon_star.png", "icon_gem_green.png", "icon_shield.png", "icon_gem_blue.png" };

            rows = new DungeonRowRefs[DUNGEON_ROW_COUNT];
            for (int i = 0; i < DUNGEON_ROW_COUNT; i++)
            {
                rows[i] = BuildDungeonRow(content.transform, dungeonNames[i],
                    ITEM_ICON + dungeonIcons[i], i);
            }

            return content;
        }

        private static DungeonRowRefs BuildDungeonRow(Transform parent, string name, string iconPath, int index)
        {
            var row = CreateEmpty($"DungeonRow_{name}", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 72;
            row.AddComponent<Image>().color = index % 2 == 0 ? RowBg : new Color(0.15f, 0.14f, 0.12f, 0.92f);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 12; hlg.padding = new RectOffset(14, 14, 6, 6);

            // 던전 아이콘
            var iconObj = CreateIcon("Icon", row.transform, iconPath, 48, 48);
            var iconImg = iconObj.GetComponent<Image>();
            var iconLE = iconObj.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 48; iconLE.minWidth = 48;

            // 정보 영역
            var infoArea = CreateEmpty("Info", row.transform);
            infoArea.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 56);
            var infoLE = infoArea.AddComponent<LayoutElement>();
            infoLE.flexibleWidth = 1; infoLE.minWidth = 120;
            var infoVlg = infoArea.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleLeft;
            infoVlg.childControlWidth = true; infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true; infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 2;

            var nameText = MakeTMP("NameText", infoArea.transform, name, 20, Vector2.zero);
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = TextWhite;

            var rewardText = MakeTMP("RewardText", infoArea.transform, "보상: ---", 16, Vector2.zero);
            rewardText.alignment = TextAlignmentOptions.Left;
            rewardText.color = TextTan;

            // 입장 버튼
            var enterBtn = BuildActionButton("EnterBtn", row.transform, "입장", 100, 48, BtnGreen, 16);

            return new DungeonRowRefs
            {
                icon = iconImg,
                nameText = nameText,
                rewardText = rewardText,
                enterButton = enterBtn
            };
        }

        // ══════════ 월드보스 콘텐츠 ══════════

        private static GameObject BuildWorldBossContent(Transform parent,
            out TextMeshProUGUI stageText, out TextMeshProUGUI hpText,
            out TextMeshProUGUI bestDmgText, out TextMeshProUGUI resultText,
            out Button challengeBtn)
        {
            var content = CreateEmpty("Content_WorldBoss", parent);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 10; vlg.padding = new RectOffset(16, 16, 16, 16);

            // 보스 배너
            var banner = CreateEmpty("BossBanner", content.transform);
            banner.AddComponent<LayoutElement>().preferredHeight = 140;
            banner.AddComponent<Image>().color = new Color(0.14f, 0.13f, 0.12f, 1f);
            var bannerVlg = banner.AddComponent<VerticalLayoutGroup>();
            bannerVlg.childAlignment = TextAnchor.MiddleCenter;
            bannerVlg.childControlWidth = false; bannerVlg.childControlHeight = false;
            bannerVlg.spacing = 6;

            CreateIcon("BossIcon", banner.transform, ITEM_ICON + "icon_skull.png", 56, 56);

            var titleTmp = MakeTMP("Title", banner.transform, "월드보스", 26, new Vector2(300, 34));
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = TextGold;

            // 정보 영역
            BuildSectionHeader(content.transform, "보스 정보");

            var infoArea = CreateEmpty("InfoArea", content.transform);
            infoArea.AddComponent<LayoutElement>().preferredHeight = 110;
            infoArea.AddComponent<Image>().color = RowBg;
            var infoVlg = infoArea.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleCenter;
            infoVlg.childControlWidth = true; infoVlg.childControlHeight = false;
            infoVlg.childForceExpandWidth = true; infoVlg.childForceExpandHeight = false;
            infoVlg.spacing = 4; infoVlg.padding = new RectOffset(16, 16, 8, 8);

            stageText = MakeTMP("StageText", infoArea.transform, "스테이지 1", 22, new Vector2(0, 30));
            stageText.fontStyle = FontStyles.Bold;
            stageText.alignment = TextAlignmentOptions.Center;
            stageText.color = TextWhite;
            stageText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            hpText = MakeTMP("HpText", infoArea.transform, "체력: 0", 20, new Vector2(0, 28));
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.color = TextTan;
            hpText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            bestDmgText = MakeTMP("BestDamageText", infoArea.transform, "최고 기록: 0", 20, new Vector2(0, 28));
            bestDmgText.alignment = TextAlignmentOptions.Center;
            bestDmgText.color = TextGold;
            bestDmgText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            // 도전 버튼
            var btnArea = CreateEmpty("ButtonArea", content.transform);
            btnArea.AddComponent<LayoutElement>().preferredHeight = 60;
            var btnHlg = btnArea.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childAlignment = TextAnchor.MiddleCenter;
            btnHlg.childControlWidth = false; btnHlg.childControlHeight = false;

            challengeBtn = BuildActionButton("ChallengeBtn", btnArea.transform, "도전", 200, 52, BtnRed);

            // 결과 영역
            BuildSectionHeader(content.transform, "전투 결과");

            var resultArea = CreateEmpty("ResultArea", content.transform);
            resultArea.AddComponent<LayoutElement>().preferredHeight = 60;
            resultArea.AddComponent<Image>().color = new Color(0.12f, 0.11f, 0.10f, 0.8f);

            resultText = MakeTMP("ResultText", resultArea.transform, "", 18, Vector2.zero);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = TextWhite;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 14; resultText.fontSizeMax = 20;
            resultText.richText = true;
            FullStretch(resultText.gameObject);

            // 하단 스페이서
            var bottomSpacer = CreateEmpty("BottomSpacer", content.transform);
            bottomSpacer.AddComponent<LayoutElement>().flexibleHeight = 1;

            return content;
        }

        // ══════════ 보스 레이드 콘텐츠 ══════════

        private static GameObject BuildBossRaidContent(Transform parent,
            out TextMeshProUGUI attemptsText, out TextMeshProUGUI resultText,
            out Button[] diffButtons, out Image[] diffBgs, out Button executeBtn)
        {
            var content = CreateEmpty("Content_BossRaid", parent);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 10; vlg.padding = new RectOffset(16, 16, 16, 16);

            // 남은 횟수
            var attemptsRow = CreateEmpty("AttemptsRow", content.transform);
            attemptsRow.AddComponent<LayoutElement>().preferredHeight = 44;
            attemptsRow.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.14f, 0.9f);
            var attHlg = attemptsRow.AddComponent<HorizontalLayoutGroup>();
            attHlg.childAlignment = TextAnchor.MiddleCenter;
            attHlg.childControlWidth = false; attHlg.childControlHeight = false;
            attHlg.spacing = 8; attHlg.padding = new RectOffset(16, 16, 4, 4);

            var attLabel = MakeTMP("AttemptsLabel", attemptsRow.transform, "남은 횟수", 18, new Vector2(120, 34));
            attLabel.alignment = TextAlignmentOptions.Left;
            attLabel.color = TextWhite;

            var attSpacer = CreateEmpty("Spacer", attemptsRow.transform);
            attSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;
            attSpacer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 34);

            attemptsText = MakeTMP("AttemptsText", attemptsRow.transform, "0 / 3", 24, new Vector2(100, 34));
            attemptsText.alignment = TextAlignmentOptions.Right;
            attemptsText.fontStyle = FontStyles.Bold;
            attemptsText.color = TextGold;

            // 난이도 선택
            BuildSectionHeader(content.transform, "난이도 선택");

            var diffArea = CreateEmpty("DifficultyArea", content.transform);
            diffArea.AddComponent<LayoutElement>().preferredHeight = 60;
            var diffHlg = diffArea.AddComponent<HorizontalLayoutGroup>();
            diffHlg.childAlignment = TextAnchor.MiddleCenter;
            diffHlg.childControlWidth = false; diffHlg.childControlHeight = false;
            diffHlg.spacing = 12;

            string[] diffNames = { "일반", "어려움", "지옥" };
            Color[] diffColors = { BtnGreen, BtnOrange, BtnRed };
            diffButtons = new Button[RAID_DIFFICULTY_COUNT];
            diffBgs = new Image[RAID_DIFFICULTY_COUNT];

            for (int i = 0; i < RAID_DIFFICULTY_COUNT; i++)
            {
                var diffObj = CreateEmpty($"Diff_{diffNames[i]}", diffArea.transform);
                diffObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 52);

                var bg = diffObj.AddComponent<Image>();
                bg.color = i == 0 ? SubTabSelected : SubTabNormal;
                var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
                if (spr != null) { bg.sprite = spr; bg.type = Image.Type.Sliced; }

                var btn = diffObj.AddComponent<Button>();
                btn.targetGraphic = bg;

                var diffVlg = diffObj.AddComponent<VerticalLayoutGroup>();
                diffVlg.childAlignment = TextAnchor.MiddleCenter;
                diffVlg.childControlWidth = true; diffVlg.childControlHeight = true;
                diffVlg.childForceExpandWidth = true; diffVlg.childForceExpandHeight = true;

                var diffTmp = MakeTMP("Label", diffObj.transform, diffNames[i], 20, Vector2.zero);
                diffTmp.fontStyle = FontStyles.Bold;
                diffTmp.alignment = TextAlignmentOptions.Center;
                diffTmp.color = TextWhite;

                diffButtons[i] = btn;
                diffBgs[i] = bg;
            }

            // 레이드 실행 버튼
            var execArea = CreateEmpty("ExecuteArea", content.transform);
            execArea.AddComponent<LayoutElement>().preferredHeight = 60;
            var execHlg = execArea.AddComponent<HorizontalLayoutGroup>();
            execHlg.childAlignment = TextAnchor.MiddleCenter;
            execHlg.childControlWidth = false; execHlg.childControlHeight = false;

            executeBtn = BuildActionButton("RaidExecuteBtn", execArea.transform, "레이드 시작", 220, 52, BtnRed);

            // 결과 영역
            BuildSectionHeader(content.transform, "레이드 결과");

            var resultArea = CreateEmpty("ResultArea", content.transform);
            resultArea.AddComponent<LayoutElement>().preferredHeight = 60;
            resultArea.AddComponent<Image>().color = new Color(0.12f, 0.11f, 0.10f, 0.8f);

            resultText = MakeTMP("ResultText", resultArea.transform, "", 18, Vector2.zero);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = TextWhite;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 14; resultText.fontSizeMax = 20;
            resultText.richText = true;
            FullStretch(resultText.gameObject);

            // 하단 스페이서
            var bottomSpacer = CreateEmpty("BottomSpacer", content.transform);
            bottomSpacer.AddComponent<LayoutElement>().flexibleHeight = 1;

            return content;
        }

        // ══════════ Shared UI Builders ══════════

        private static Button BuildActionButton(string name, Transform parent, string label,
            float w, float h, Color bgColor, float fontSize = 22f)
        {
            var obj = CreateEmpty(name, parent);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, Mathf.Max(h, 44f));

            var bg = obj.AddComponent<Image>();
            bg.color = bgColor;
            var spr = LoadSprite(SPRITE + "Button/Button03_n.png");
            if (spr != null) { bg.sprite = spr; bg.type = Image.Type.Sliced; }

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = bg;
            var sprS = LoadSprite(SPRITE + "Button/Button03_s.png");
            if (spr != null && sprS != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState { pressedSprite = sprS };
            }

            var tmp = MakeTMP("Label", obj.transform, label, fontSize, Vector2.zero);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextWhite;
            FullStretch(tmp.gameObject);

            return btn;
        }

        private static void BuildSectionHeader(Transform parent, string text)
        {
            var header = CreateEmpty($"Section_{text}", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 32;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(12, 12, 0, 0);

            var line = new GameObject("Line");
            line.transform.SetParent(header.transform, false);
            line.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line.AddComponent<LayoutElement>().preferredWidth = 30;

            var tmp = MakeTMP("Label", header.transform, text, 16, Vector2.zero);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextWhite;
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var line2 = new GameObject("Line2");
            line2.transform.SetParent(header.transform, false);
            line2.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line2.AddComponent<LayoutElement>().preferredWidth = 30;
        }

        // ══════════ SerializedObject Helpers ══════════

        private static void SetArray<T>(SerializedObject so, string propName, T[] items) where T : Object
        {
            var prop = so.FindProperty(propName);
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void SetObjectArray(SerializedObject so, string propName, GameObject[] items)
        {
            var prop = so.FindProperty(propName);
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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
