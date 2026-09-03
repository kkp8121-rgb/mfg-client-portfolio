using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// Panel_Misc 내부 구성: 5개 서브 탭 (퀘스트/업적/출석/우편함/설정) + 컨텐츠.
    /// SceneSetupEditor에서 호출된다.
    /// </summary>
    public static class MiscPanelSetupEditor
    {
        private const string KIT = "Assets/Folder_Assets/GUI Kit - Dark Geo/";
        private const string SPRITE = KIT + "ResourceData/Sprite/Component/";
        private const string SL = SPRITE + "Slider/";
        private const string BTN_ICON = SPRITE + "Icon_ButtonIcon_(x2)/128/";
        private const string ITEM_ICON = SPRITE + "Icon_ItemIcon_(x2)/128/";

        private static readonly Color PanelBg = new Color(0.10f, 0.09f, 0.08f, 0.96f);
        private static readonly Color SubTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        private static readonly Color SubTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new Color(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new Color(1f, 0.85f, 0.086f, 1f);
        private static readonly Color RowBg = new Color(0.16f, 0.15f, 0.13f, 0.9f);
        private static readonly Color BtnGreen = new Color(0.25f, 0.55f, 0.30f, 1f);
        private static readonly Color BtnGreenHover = new Color(0.30f, 0.65f, 0.35f, 1f);
        private static readonly Color BtnBlue = new Color(0.20f, 0.35f, 0.60f, 1f);

        private const int ATTENDANCE_DAYS = 7;

        public static void Build(GameObject panel)
        {
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null) bgImg.color = PanelBg;

            // 기존 자식 제거
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);

            // CanvasGroup
            if (panel.GetComponent<CanvasGroup>() == null)
                panel.AddComponent<CanvasGroup>();

            // 메인 VLG
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0; vlg.padding = new RectOffset(0, 0, 0, 0);

            // 헤더
            BuildHeader(panel.transform);

            // 서브 탭 바
            BuildSubTabBar(panel.transform, out var subTabBgs, out var subTabBtns);

            // 컨텐츠 영역
            var contentArea = CreateEmpty("ContentArea", panel.transform);
            contentArea.AddComponent<LayoutElement>().flexibleHeight = 1;

            // 5개 탭 컨텐츠
            var questContent = BuildQuestContent(contentArea.transform);
            var achievementContent = BuildAchievementContent(contentArea.transform);
            var attendanceContent = BuildAttendanceContent(contentArea.transform,
                out var dayButtons, out var dayChecks, out var attendanceCheckBtn);
            var mailContent = BuildMailContent(contentArea.transform,
                out var mailListContent, out var claimAllBtn);
            var settingsContent = BuildSettingsContent(contentArea.transform,
                out var bgmSlider, out var sfxSlider,
                out var bgmValueText, out var sfxValueText,
                out var qualityButtons, out var qualityBgs);

            var subTabContents = new GameObject[]
            {
                questContent, achievementContent, attendanceContent, mailContent, settingsContent
            };

            // 초기: 퀘스트 탭만 활성
            for (int i = 0; i < subTabContents.Length; i++)
            {
                FullStretch(subTabContents[i]);
                subTabContents[i].SetActive(i == 0);
            }

            // MiscPanel 컴포넌트 와이어링
            var mp = panel.AddComponent<MkLike.UI.MiscPanel>();
            var so = new SerializedObject(mp);

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

            // 출석 체크
            var dayBtnProp = so.FindProperty("attendanceDayButtons");
            dayBtnProp.arraySize = dayButtons.Length;
            for (int i = 0; i < dayButtons.Length; i++)
                dayBtnProp.GetArrayElementAtIndex(i).objectReferenceValue = dayButtons[i];

            var dayCheckProp = so.FindProperty("attendanceDayChecks");
            dayCheckProp.arraySize = dayChecks.Length;
            for (int i = 0; i < dayChecks.Length; i++)
                dayCheckProp.GetArrayElementAtIndex(i).objectReferenceValue = dayChecks[i];

            so.FindProperty("attendanceCheckBtn").objectReferenceValue = attendanceCheckBtn;

            // 우편함
            so.FindProperty("mailListContent").objectReferenceValue = mailListContent;
            so.FindProperty("claimAllBtn").objectReferenceValue = claimAllBtn;

            // 설정 - 사운드
            so.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("bgmValueText").objectReferenceValue = bgmValueText;
            so.FindProperty("sfxValueText").objectReferenceValue = sfxValueText;

            // 설정 - 그래픽
            var qualBtnProp = so.FindProperty("qualityButtons");
            qualBtnProp.arraySize = qualityButtons.Length;
            for (int i = 0; i < qualityButtons.Length; i++)
                qualBtnProp.GetArrayElementAtIndex(i).objectReferenceValue = qualityButtons[i];

            var qualBgProp = so.FindProperty("qualityBgs");
            qualBgProp.arraySize = qualityBgs.Length;
            for (int i = 0; i < qualityBgs.Length; i++)
                qualBgProp.GetArrayElementAtIndex(i).objectReferenceValue = qualityBgs[i];

            // 색상
            so.FindProperty("subTabNormal").colorValue = SubTabNormal;
            so.FindProperty("subTabSelected").colorValue = SubTabSelected;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[MiscPanel] 기타 패널 내부 구성 완료");
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

            var title = MakeTMP("Title", header.transform, "설정 & 기타", 28);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = TextWhite;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 250;

            var spacerR = CreateEmpty("SpacerR", header.transform);
            spacerR.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        // ══════════ Sub Tab Bar ══════════

        private static void BuildSubTabBar(Transform parent, out Image[] tabBgs, out Button[] tabBtns)
        {
            var bar = CreateEmpty("SubTabBar", parent);
            bar.AddComponent<LayoutElement>().preferredHeight = 48;
            bar.AddComponent<Image>().color = new Color(0.13f, 0.12f, 0.11f, 1f);
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.spacing = 2; hlg.padding = new RectOffset(4, 4, 4, 4);

            string[] names = { "퀘스트", "업적", "출석", "우편함", "설정" };
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

                tabBgs[i] = bg;
                tabBtns[i] = tab.AddComponent<Button>();
                tabBtns[i].targetGraphic = bg;

                var tmp = MakeTMP("Text", tab.transform, names[i], 16);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = TextWhite;
                FullStretch(tmp.gameObject);
            }
        }

        // ══════════ 퀘스트 Content ══════════

        private static GameObject BuildQuestContent(Transform parent)
        {
            var root = CreateEmpty("Content_Quest", parent);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 4; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            BuildSectionHeader(root.transform, "활성 퀘스트");

            // 스크롤뷰
            var scrollView = BuildScrollView(root.transform, "QuestScroll");

            // 퀘스트 예시 아이템 3개
            var scrollContent = scrollView.GetComponentInChildren<ContentSizeFitter>().transform;
            string[] quests = { "몬스터 100마리 처치", "스테이지 1-5 클리어", "장비 3회 강화" };
            string[] progress = { "45/100", "3/5", "0/3" };
            for (int i = 0; i < quests.Length; i++)
            {
                BuildQuestRow(scrollContent, quests[i], progress[i], i);
            }

            return root;
        }

        private static void BuildQuestRow(Transform parent, string questName, string progressText, int index)
        {
            var row = CreateEmpty($"Quest_{index}", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 70;
            row.AddComponent<Image>().color = index % 2 == 0 ? RowBg : new Color(0.14f, 0.13f, 0.11f, 0.9f);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 10; hlg.padding = new RectOffset(12, 12, 8, 8);

            // 퀘스트 아이콘 자리
            var icon = CreateEmpty("Icon", row.transform);
            icon.AddComponent<LayoutElement>().preferredWidth = 48;
            var iconImg = icon.AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.28f, 0.25f, 1f);
            var iconSpr = LoadSprite(BTN_ICON + "list.png");
            if (iconSpr != null) { iconImg.sprite = iconSpr; iconImg.preserveAspect = true; }

            // 퀘스트 정보
            var infoVlg = CreateEmpty("Info", row.transform);
            infoVlg.AddComponent<LayoutElement>().flexibleWidth = 1;
            var vlg = infoVlg.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            vlg.spacing = 2;

            var nameText = MakeTMP("Name", infoVlg.transform, questName, 18);
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = TextWhite;

            var progText = MakeTMP("Progress", infoVlg.transform, progressText, 14);
            progText.alignment = TextAlignmentOptions.Left;
            progText.color = TextTan;

            // 보상 수령 버튼
            var claimBtn = CreateButton("ClaimBtn", row.transform, "수령", 80, 40);
            claimBtn.GetComponent<Image>().color = BtnGreen;
            var btnComp = claimBtn.GetComponent<Button>();
            btnComp.interactable = false;
        }

        // ══════════ 업적 Content ══════════

        private static GameObject BuildAchievementContent(Transform parent)
        {
            var root = CreateEmpty("Content_Achievement", parent);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 4; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            // 카테고리별 섹션
            string[] categories = { "전투", "성장", "수집" };
            string[][] achievements =
            {
                new[] { "첫 번째 처치|몬스터 1마리 처치|1/1", "학살자|몬스터 1000마리 처치|234/1000" },
                new[] { "레벨 10 달성|캐릭터 레벨 10|5/10", "첫 전직|1차 전직 완료|0/1" },
                new[] { "수집가|장비 10종 수집|3/10", "행운아|Unique 등급 획득|0/1" }
            };

            var scrollView = BuildScrollView(root.transform, "AchievementScroll");
            var scrollContent = scrollView.GetComponentInChildren<ContentSizeFitter>().transform;

            for (int c = 0; c < categories.Length; c++)
            {
                BuildSectionHeaderInScroll(scrollContent, categories[c]);
                string[] items = achievements[c];
                for (int i = 0; i < items.Length; i++)
                {
                    string[] parts = items[i].Split('|');
                    BuildAchievementRow(scrollContent, parts[0], parts[1], parts[2], c * 10 + i);
                }
            }

            return root;
        }

        private static void BuildAchievementRow(Transform parent, string title, string desc, string progress, int index)
        {
            var row = CreateEmpty($"Achievement_{index}", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 70;
            row.AddComponent<Image>().color = index % 2 == 0 ? RowBg : new Color(0.14f, 0.13f, 0.11f, 0.9f);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 10; hlg.padding = new RectOffset(12, 12, 8, 8);

            // 업적 아이콘
            var icon = CreateEmpty("Icon", row.transform);
            icon.AddComponent<LayoutElement>().preferredWidth = 48;
            var iconImg = icon.AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.28f, 0.25f, 1f);
            var iconSpr = LoadSprite(BTN_ICON + "star.png");
            if (iconSpr != null) { iconImg.sprite = iconSpr; iconImg.preserveAspect = true; }

            // 업적 정보
            var infoVlg = CreateEmpty("Info", row.transform);
            infoVlg.AddComponent<LayoutElement>().flexibleWidth = 1;
            var vlg = infoVlg.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            vlg.spacing = 0;

            var titleText = MakeTMP("Title", infoVlg.transform, title, 18);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = TextGold;
            titleText.fontStyle = FontStyles.Bold;

            var descText = MakeTMP("Desc", infoVlg.transform, desc, 14);
            descText.alignment = TextAlignmentOptions.Left;
            descText.color = TextTan;

            // 진행도
            var progText = MakeTMP("Progress", row.transform, progress, 16);
            progText.alignment = TextAlignmentOptions.Center;
            progText.color = TextWhite;
            progText.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        }

        // ══════════ 출석 Content ══════════

        private static GameObject BuildAttendanceContent(Transform parent,
            out Button[] dayButtons, out Image[] dayChecks, out Button checkBtn)
        {
            var root = CreateEmpty("Content_Attendance", parent);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 12; rootVlg.padding = new RectOffset(16, 16, 20, 20);

            // 타이틀
            var titleText = MakeTMP("Title", root.transform, "7일 출석 보상", 24);
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = TextGold;
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var descText = MakeTMP("Desc", root.transform, "매일 접속하여 보상을 받으세요!", 16);
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = TextTan;
            descText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            // 7일 그리드
            var grid = CreateEmpty("DayGrid", root.transform);
            grid.AddComponent<LayoutElement>().preferredHeight = 130;
            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(120, 120);
            glg.spacing = new Vector2(8, 8);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.padding = new RectOffset(4, 4, 4, 4);

            dayButtons = new Button[ATTENDANCE_DAYS];
            dayChecks = new Image[ATTENDANCE_DAYS];
            string[] rewards = { "골드 x500", "루비 x50", "골드 x1000", "루비 x100", "골드 x2000", "루비 x200", "루비 x500" };

            for (int i = 0; i < ATTENDANCE_DAYS; i++)
            {
                var dayObj = new GameObject($"Day_{i + 1}");
                dayObj.transform.SetParent(grid.transform, false);
                var dayBg = dayObj.AddComponent<Image>();
                dayBg.color = RowBg;
                var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
                if (spr != null) { dayBg.sprite = spr; dayBg.type = Image.Type.Sliced; }

                dayButtons[i] = dayObj.AddComponent<Button>();
                dayButtons[i].targetGraphic = dayBg;
                dayButtons[i].interactable = false;

                var dayVlg = dayObj.AddComponent<VerticalLayoutGroup>();
                dayVlg.childAlignment = TextAnchor.MiddleCenter;
                dayVlg.childControlWidth = false; dayVlg.childControlHeight = false;
                dayVlg.spacing = 2;
                dayVlg.padding = new RectOffset(4, 4, 6, 6);

                var dayLabel = MakeTMP("DayLabel", dayObj.transform, $"Day {i + 1}", 14);
                dayLabel.alignment = TextAlignmentOptions.Center;
                dayLabel.color = TextWhite;
                dayLabel.fontStyle = FontStyles.Bold;
                dayLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 20);

                // 보상 아이콘 자리
                var rewardIcon = CreateEmpty("RewardIcon", dayObj.transform);
                rewardIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);
                var rewardImg = rewardIcon.AddComponent<Image>();
                rewardImg.color = new Color(0.4f, 0.38f, 0.34f, 0.8f);
                var gemSpr = i % 2 == 0
                    ? LoadSprite(ITEM_ICON + "icon_coin.png")
                    : LoadSprite(ITEM_ICON + "icon_gem_pink.png");
                if (gemSpr != null) { rewardImg.sprite = gemSpr; rewardImg.preserveAspect = true; }

                var rewardText = MakeTMP("RewardText", dayObj.transform, rewards[i], 12);
                rewardText.alignment = TextAlignmentOptions.Center;
                rewardText.color = TextTan;
                rewardText.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 18);

                // 체크 오버레이
                var checkOverlay = CreateEmpty("CheckOverlay", dayObj.transform);
                FullStretch(checkOverlay);
                var checkImg = checkOverlay.AddComponent<Image>();
                checkImg.color = new Color(0.15f, 0.6f, 0.25f, 0.6f);
                var checkSpr = LoadSprite(BTN_ICON + "check.png");
                if (checkSpr != null) { checkImg.sprite = checkSpr; checkImg.preserveAspect = true; }
                checkOverlay.SetActive(false);
                dayChecks[i] = checkImg;
            }

            // 체크 버튼
            var checkBtnObj = CreateButton("AttendanceCheckBtn", root.transform, "출석 체크", 200, 50);
            checkBtnObj.GetComponent<Image>().color = BtnGreen;
            checkBtn = checkBtnObj.GetComponent<Button>();

            return root;
        }

        // ══════════ 우편함 Content ══════════

        private static GameObject BuildMailContent(Transform parent,
            out Transform mailContent, out Button claimAllBtn)
        {
            var root = CreateEmpty("Content_Mail", parent);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 8; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            // 전체 수령 버튼
            var topRow = CreateEmpty("TopRow", root.transform);
            topRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var topHlg = topRow.AddComponent<HorizontalLayoutGroup>();
            topHlg.childAlignment = TextAnchor.MiddleRight;
            topHlg.childControlWidth = false; topHlg.childControlHeight = false;
            topHlg.childForceExpandWidth = false;
            topHlg.padding = new RectOffset(12, 12, 4, 4);

            var mailCountText = MakeTMP("MailCount", topRow.transform, "메일 0통", 16);
            mailCountText.alignment = TextAlignmentOptions.Left;
            mailCountText.color = TextTan;
            mailCountText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var claimAllObj = CreateButton("ClaimAllBtn", topRow.transform, "전체 수령", 120, 36);
            claimAllObj.GetComponent<Image>().color = BtnBlue;
            claimAllBtn = claimAllObj.GetComponent<Button>();

            // 메일 리스트 스크롤뷰
            var scrollView = BuildScrollView(root.transform, "MailScroll");
            var scrollContentObj = scrollView.GetComponentInChildren<ContentSizeFitter>();
            mailContent = scrollContentObj != null ? scrollContentObj.transform : scrollView.transform;

            // 빈 메일 안내
            var emptyText = MakeTMP("EmptyText", mailContent, "받은 메일이 없습니다.", 18);
            emptyText.alignment = TextAlignmentOptions.Center;
            emptyText.color = TextTan;
            emptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            return root;
        }

        // ══════════ 설정 Content ══════════

        private static GameObject BuildSettingsContent(Transform parent,
            out Slider bgmSlider, out Slider sfxSlider,
            out TextMeshProUGUI bgmValueText, out TextMeshProUGUI sfxValueText,
            out Button[] qualityButtons, out Image[] qualityBgs)
        {
            var root = CreateEmpty("Content_Settings", parent);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 8; rootVlg.padding = new RectOffset(16, 16, 16, 16);

            // ── 사운드 섹션 ──
            BuildSectionHeaderInline(root.transform, "사운드");

            bgmSlider = BuildSliderRow(root.transform, "BGM", 0.8f, out bgmValueText);
            sfxSlider = BuildSliderRow(root.transform, "효과음", 1f, out sfxValueText);

            // ── 그래픽 섹션 ──
            BuildSectionHeaderInline(root.transform, "그래픽 품질");

            string[] qualities = { "낮음", "보통", "높음" };
            qualityButtons = new Button[qualities.Length];
            qualityBgs = new Image[qualities.Length];

            var qualityRow = CreateEmpty("QualityRow", root.transform);
            qualityRow.AddComponent<LayoutElement>().preferredHeight = 50;
            var qHlg = qualityRow.AddComponent<HorizontalLayoutGroup>();
            qHlg.childAlignment = TextAnchor.MiddleCenter;
            qHlg.childControlWidth = true; qHlg.childControlHeight = true;
            qHlg.childForceExpandWidth = true; qHlg.childForceExpandHeight = true;
            qHlg.spacing = 8; qHlg.padding = new RectOffset(8, 8, 4, 4);

            for (int i = 0; i < qualities.Length; i++)
            {
                var qBtn = new GameObject($"Quality_{qualities[i]}");
                qBtn.transform.SetParent(qualityRow.transform, false);
                var qBg = qBtn.AddComponent<Image>();
                qBg.color = i == 1 ? SubTabSelected : SubTabNormal;
                var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
                if (spr != null) { qBg.sprite = spr; qBg.type = Image.Type.Sliced; }

                qualityBgs[i] = qBg;
                qualityButtons[i] = qBtn.AddComponent<Button>();
                qualityButtons[i].targetGraphic = qBg;

                var qText = MakeTMP("Text", qBtn.transform, qualities[i], 18);
                qText.alignment = TextAlignmentOptions.Center;
                qText.color = TextWhite;
                FullStretch(qText.gameObject);
            }

            // ── 기타 설정 섹션 ──
            BuildSectionHeaderInline(root.transform, "기타");

            // 언어 설정 (플레이스홀더)
            var langRow = CreateEmpty("LanguageRow", root.transform);
            langRow.AddComponent<LayoutElement>().preferredHeight = 50;
            langRow.AddComponent<Image>().color = RowBg;
            var langHlg = langRow.AddComponent<HorizontalLayoutGroup>();
            langHlg.childAlignment = TextAnchor.MiddleLeft;
            langHlg.childControlWidth = false; langHlg.childControlHeight = true;
            langHlg.childForceExpandWidth = false; langHlg.childForceExpandHeight = true;
            langHlg.spacing = 12; langHlg.padding = new RectOffset(16, 16, 4, 4);

            var langLabel = MakeTMP("Label", langRow.transform, "언어", 18);
            langLabel.alignment = TextAlignmentOptions.Left;
            langLabel.color = TextWhite;
            langLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var langValue = MakeTMP("Value", langRow.transform, "한국어", 18);
            langValue.alignment = TextAlignmentOptions.Right;
            langValue.color = TextTan;
            langValue.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

            // 계정 연동 (플레이스홀더)
            var accountRow = CreateEmpty("AccountRow", root.transform);
            accountRow.AddComponent<LayoutElement>().preferredHeight = 50;
            accountRow.AddComponent<Image>().color = new Color(0.14f, 0.13f, 0.11f, 0.9f);
            var accHlg = accountRow.AddComponent<HorizontalLayoutGroup>();
            accHlg.childAlignment = TextAnchor.MiddleLeft;
            accHlg.childControlWidth = false; accHlg.childControlHeight = true;
            accHlg.childForceExpandWidth = false; accHlg.childForceExpandHeight = true;
            accHlg.spacing = 12; accHlg.padding = new RectOffset(16, 16, 4, 4);

            var accLabel = MakeTMP("Label", accountRow.transform, "계정 연동", 18);
            accLabel.alignment = TextAlignmentOptions.Left;
            accLabel.color = TextWhite;
            accLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var accBtn = CreateButton("LinkBtn", accountRow.transform, "연동", 80, 36);
            accBtn.GetComponent<Image>().color = BtnBlue;

            return root;
        }

        private static Slider BuildSliderRow(Transform parent, string label, float defaultValue,
            out TextMeshProUGUI valueText)
        {
            var row = CreateEmpty($"Slider_{label}", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 50;
            row.AddComponent<Image>().color = RowBg;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 12; hlg.padding = new RectOffset(16, 16, 8, 8);

            // 라벨
            var labelText = MakeTMP("Label", row.transform, label, 18);
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = TextWhite;
            labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;

            // 슬라이더
            var sliderObj = new GameObject($"{label}Slider");
            sliderObj.transform.SetParent(row.transform, false);
            sliderObj.AddComponent<RectTransform>();
            sliderObj.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            var frameSpr = LoadSprite(SL + "Slider01_Frame.png");
            if (frameSpr != null) { bgImage.sprite = frameSpr; bgImage.type = Image.Type.Sliced; }
            else bgImage.color = new Color(0.12f, 0.11f, 0.10f, 1f);
            FullStretch(bgObj);

            // Fill Area
            var fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            var fillAreaRT = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero; fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(6, 6); fillAreaRT.offsetMax = new Vector2(-6, -6);

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillImg = fillObj.AddComponent<Image>();
            var fillSpr = LoadSprite(SL + "Slider01_Fill.png");
            if (fillSpr != null) { fillImg.sprite = fillSpr; fillImg.type = Image.Type.Sliced; }
            else fillImg.color = new Color(0.3f, 0.7f, 0.4f, 1f);
            FullStretch(fillObj);

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero; handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(8, 0); handleAreaRT.offsetMax = new Vector2(-8, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = TextWhite;
            var handleSpr = LoadSprite(SL + "Slider01_Handle.png");
            if (handleSpr != null) { handleImg.sprite = handleSpr; }
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(24, 24);

            // Slider 컴포넌트
            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillObj.GetComponent<RectTransform>();
            slider.handleRect = handleRT;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;
            slider.wholeNumbers = false;

            // 값 텍스트
            valueText = MakeTMP("Value", row.transform, $"{Mathf.RoundToInt(defaultValue * 100)}%", 18);
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.color = TextGold;
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            return slider;
        }

        // ══════════ 공통 빌더 ══════════

        private static GameObject BuildScrollView(Transform parent, string name)
        {
            var scrollObj = CreateEmpty(name, parent);
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
            contentVlg.spacing = 4;
            contentVlg.padding = new RectOffset(8, 8, 8, 8);

            contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;

            return scrollObj;
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

            var tmp = MakeTMP("Label", header.transform, text, 14);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextTan;
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var line2 = new GameObject("Line2");
            line2.transform.SetParent(header.transform, false);
            line2.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line2.AddComponent<LayoutElement>().preferredWidth = 30;
        }

        private static void BuildSectionHeaderInScroll(Transform parent, string text)
        {
            var header = CreateEmpty($"Section_{text}", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 32;

            var tmp = MakeTMP("Label", header.transform, text, 16);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextGold;
            tmp.fontStyle = FontStyles.Bold;
            FullStretch(tmp.gameObject);
        }

        private static void BuildSectionHeaderInline(Transform parent, string text)
        {
            var header = CreateEmpty($"Section_{text}", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 36;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(4, 4, 0, 0);

            var line = new GameObject("Line");
            line.transform.SetParent(header.transform, false);
            line.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line.AddComponent<LayoutElement>().preferredWidth = 20;

            var tmp = MakeTMP("Label", header.transform, text, 16);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextGold;
            tmp.fontStyle = FontStyles.Bold;
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var line2 = new GameObject("Line2");
            line2.transform.SetParent(header.transform, false);
            line2.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.27f, 0.5f);
            line2.AddComponent<LayoutElement>().preferredWidth = 20;
        }

        private static GameObject CreateButton(string name, Transform parent, string text, float w, float h)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.color = BtnGreen;
            var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
            if (spr != null) { img.sprite = spr; img.type = Image.Type.Sliced; }
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            obj.AddComponent<Button>().targetGraphic = img;

            var tmp = MakeTMP("Label", obj.transform, text, 15);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextWhite;
            tmp.fontStyle = FontStyles.Bold;
            FullStretch(tmp.gameObject);

            return obj;
        }

        // ══════════ Utilities ══════════

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
            tmp.text = text; tmp.fontSize = size; tmp.color = Color.white;
            var f = FontSetupEditor.GetOrCreateKoreanFont();
            if (f != null) tmp.font = f;
            return tmp;
        }

        private static void FullStretch(GameObject obj)
        {
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
        }
    }
}
