using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// Panel_Character 내부 구성: 서브 탭 (스탯/스킬/전직/등반자의 힘) + 컨텐츠.
    /// SceneSetupEditor에서 호출된다.
    /// </summary>
    public static class CharacterPanelSetupEditor
    {
        private const string KIT = "Assets/Folder_Assets/GUI Kit - Dark Geo/";
        private const string SPRITE = KIT + "ResourceData/Sprite/Component/";
        private const string SL = SPRITE + "Slider/";
        private const string BTN_ICON = SPRITE + "Icon_ButtonIcon_(x2)/128/";

        // Stone Kit — Resources.Load 경로 (런타임에서도 동일하게 사용)
        private const string STONE = "Assets/Resources/UI/Stone/";
        private const string ST_FRAME = STONE + "Frame/";
        private const string ST_BTN = STONE + "Button/";
        private const string ST_GAGE = STONE + "Gage/";
        private const string ST_TAB = STONE + "Tab/";
        private const string ST_SKILL = STONE + "Skill/";
        private const string ST_POPUP = STONE + "Popup/";

        private static readonly Color PanelBg = new Color(0.10f, 0.09f, 0.08f, 0.96f);
        private static readonly Color SubTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        private static readonly Color SubTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new Color(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new Color(1f, 0.85f, 0.086f, 1f);
        private static readonly Color RowBg = new Color(0.16f, 0.15f, 0.13f, 0.9f);
        private static readonly Color BtnGreen = new Color(0.25f, 0.55f, 0.30f, 1f);
        private static readonly Color BtnGreenHover = new Color(0.30f, 0.65f, 0.35f, 1f);

        // 스탯 이름별 색상 힌트
        private static readonly Color StatColorAtk = new Color(1f, 0.45f, 0.40f, 1f);   // 공격력 = 빨강
        private static readonly Color StatColorHp = new Color(0.40f, 0.85f, 0.45f, 1f);  // HP = 초록
        private static readonly Color StatColorDef = new Color(0.45f, 0.65f, 1f, 1f);    // 방어력 = 파랑
        private static readonly Color StatColorCrit = new Color(1f, 0.75f, 0.20f, 1f);   // 치명타 = 주황
        private static readonly Color StatColorAcc = new Color(0.75f, 0.55f, 1f, 1f);    // 명중 = 보라
        private static readonly Color PointGlow = new Color(1f, 0.92f, 0.30f, 1f);       // 포인트 강조

        /// <summary>
        /// Panel_Character 내부를 구성한다.
        /// </summary>
        public static void Build(GameObject panel)
        {
            // 배경
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null)
            {
                var panelSpr = LoadSprite(ST_FRAME + "panel_bg.png");
                if (panelSpr != null) { bgImg.sprite = panelSpr; bgImg.type = Image.Type.Sliced; bgImg.color = Color.white; }
                else bgImg.color = PanelBg;
            }

            // 기존 자식 제거 (Label 등)
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);

            // 메인 VLG
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0; vlg.padding = new RectOffset(0, 0, 0, 0);

            // ── 헤더 ──
            var header = BuildHeader(panel.transform);

            // ── 서브 탭 바 ──
            var subTabBar = BuildSubTabBar(panel.transform, out var subTabBgs, out var subTabBtns);

            // ── 컨텐츠 영역 ──
            var contentArea = CreateEmpty("ContentArea", panel.transform);
            contentArea.AddComponent<LayoutElement>().flexibleHeight = 1;

            var statsContent = BuildStatsContent(contentArea.transform,
                out var lvText, out var cpText, out var gradeText, out var pointsText,
                out var statRows);
            var equipmentContent = EquipmentPanelSetupEditor.Build(contentArea.transform);
            var skillsContent = BuildSkillsContent(contentArea.transform,
                out var skillListRoot, out var skillRowPrefab);
            var jobContent = BuildJobContent(contentArea.transform,
                out var jobCurrentText, out var jobNextLevelText,
                out var jobChoiceGroup, out var btnJobWarrior, out var btnJobArcher, out var btnJobMage);
            // BuildRelicContent: 2026-04-20 유물 시스템 완전 제거
            var climberContent = BuildClimberContent(contentArea.transform,
                out var climberLevelText, out var climberCostText, out var climberSlotsText, out var btnClimberUpgrade);
            var abilityContent = BuildAbilityContent(contentArea.transform,
                out var abilityListRoot, out var abilityRowPrefab, out var btnAbilityReroll);
            var heroPowerContent = BuildHeroPowerContent(contentArea.transform);

            var subTabContents = new GameObject[] { statsContent, equipmentContent, skillsContent, jobContent, climberContent, abilityContent, heroPowerContent };

            // 초기: 스탯 탭만 활성
            for (int i = 0; i < subTabContents.Length; i++)
            {
                FullStretch(subTabContents[i]);
                subTabContents[i].SetActive(i == 0);
            }

            // ── CharacterPanel 컴포넌트 와이어링 ──
            var cp = panel.AddComponent<MkLike.UI.CharacterPanel>();
            var so = new SerializedObject(cp);

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

            // stat tab refs
            so.FindProperty("levelText").objectReferenceValue = lvText;
            so.FindProperty("cpText").objectReferenceValue = cpText;
            so.FindProperty("gradeText").objectReferenceValue = gradeText;
            so.FindProperty("availablePointsText").objectReferenceValue = pointsText;

            // statRows
            var rowsProp = so.FindProperty("statRows");
            rowsProp.arraySize = statRows.Length;
            for (int i = 0; i < statRows.Length; i++)
            {
                var elem = rowsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("nameText").objectReferenceValue = statRows[i].nameText;
                elem.FindPropertyRelative("progressBar").objectReferenceValue = statRows[i].progressBar;
                elem.FindPropertyRelative("valueText").objectReferenceValue = statRows[i].valueText;
                elem.FindPropertyRelative("btnPlus1").objectReferenceValue = statRows[i].btnPlus1;
                elem.FindPropertyRelative("btnPlus10").objectReferenceValue = statRows[i].btnPlus10;
                elem.FindPropertyRelative("btnMax").objectReferenceValue = statRows[i].btnMax;
            }

            // 스킬 탭
            so.FindProperty("_skillListRoot").objectReferenceValue = skillListRoot;
            so.FindProperty("_skillRowPrefab").objectReferenceValue = skillRowPrefab;

            // 전직 탭
            so.FindProperty("_jobCurrentText").objectReferenceValue = jobCurrentText;
            so.FindProperty("_jobNextLevelText").objectReferenceValue = jobNextLevelText;
            so.FindProperty("_jobChoiceGroup").objectReferenceValue = jobChoiceGroup;
            so.FindProperty("_btnJobWarrior").objectReferenceValue = btnJobWarrior;
            so.FindProperty("_btnJobArcher").objectReferenceValue = btnJobArcher;
            so.FindProperty("_btnJobMage").objectReferenceValue = btnJobMage;

            // 유물 탭: 2026-04-20 유물 시스템 완전 제거

            // 등반자의 힘 탭
            so.FindProperty("_climberLevelText").objectReferenceValue = climberLevelText;
            so.FindProperty("_climberCostText").objectReferenceValue = climberCostText;
            so.FindProperty("_climberSlotsText").objectReferenceValue = climberSlotsText;
            so.FindProperty("_btnClimberUpgrade").objectReferenceValue = btnClimberUpgrade;

            // 어빌리티 탭
            so.FindProperty("_abilityListRoot").objectReferenceValue = abilityListRoot;
            so.FindProperty("_abilityRowPrefab").objectReferenceValue = abilityRowPrefab;
            so.FindProperty("_btnAbilityReroll").objectReferenceValue = btnAbilityReroll;

            so.FindProperty("subTabNormal").colorValue = SubTabNormal;
            so.FindProperty("subTabSelected").colorValue = SubTabSelected;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[CharacterPanel] 캐릭터 팝업 내부 구성 완료");
        }

        // ══════════ Header ══════════

        private static GameObject BuildHeader(Transform parent)
        {
            var header = CreateEmpty("Header", parent);
            header.AddComponent<LayoutElement>().preferredHeight = 56;
            var headerImg = header.AddComponent<Image>();
            var labelSpr = LoadSprite(ST_FRAME + "label_brown.png");
            if (labelSpr != null) { headerImg.sprite = labelSpr; headerImg.type = Image.Type.Sliced; headerImg.color = Color.white; }
            else headerImg.color = new Color(0.22f, 0.18f, 0.14f, 1f);

            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(16, 16, 0, 0);

            var spacerL = CreateEmpty("SpacerL", header.transform);
            spacerL.AddComponent<LayoutElement>().flexibleWidth = 1;

            var title = MakeTMP("Title", header.transform, "캐릭터", 28);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = TextWhite;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

            var spacerR = CreateEmpty("SpacerR", header.transform);
            spacerR.AddComponent<LayoutElement>().flexibleWidth = 1;

            return header;
        }

        // ══════════ Sub Tab Bar ══════════

        private static GameObject BuildSubTabBar(Transform parent, out Image[] tabBgs, out Button[] tabBtns)
        {
            var bar = CreateEmpty("SubTabBar", parent);
            bar.AddComponent<LayoutElement>().preferredHeight = 52;
            var barImg = bar.AddComponent<Image>();
            ApplyStoneFrame(barImg, ST_FRAME + "status_bg_common.png", new Color(0.13f, 0.12f, 0.11f, 1f));
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.spacing = 2; hlg.padding = new RectOffset(4, 4, 4, 4);

            string[] names = { "스탯", "장비", "스킬", "전직", "유물", "동반자", "어빌리티", "영웅의 힘" };
            tabBgs = new Image[names.Length];
            tabBtns = new Button[names.Length];

            var tabNSpr = LoadSprite(ST_TAB + "tab_normal.png");
            var tabSSpr = LoadSprite(ST_TAB + "tab_selected.png");

            for (int i = 0; i < names.Length; i++)
            {
                var tab = new GameObject($"SubTab_{names[i]}");
                tab.transform.SetParent(bar.transform, false);
                var bg = tab.AddComponent<Image>();
                if (i == 0 && tabSSpr != null)
                    { bg.sprite = tabSSpr; bg.type = Image.Type.Sliced; bg.color = Color.white; }
                else if (tabNSpr != null)
                    { bg.sprite = tabNSpr; bg.type = Image.Type.Sliced; bg.color = Color.white; }
                else
                    bg.color = i == 0 ? SubTabSelected : SubTabNormal;

                tabBgs[i] = bg;
                tabBtns[i] = tab.AddComponent<Button>();
                tabBtns[i].targetGraphic = bg;

                var tmp = MakeTMP("Text", tab.transform, names[i], 13);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = TextWhite;
                tmp.fontStyle = FontStyles.Bold;
                FullStretch(tmp.gameObject);
            }

            return bar;
        }

        // ══════════ Stats Content ══════════

        private static GameObject BuildStatsContent(Transform parent,
            out TextMeshProUGUI lvText, out TextMeshProUGUI cpText,
            out TextMeshProUGUI gradeText, out TextMeshProUGUI pointsText,
            out MkLike.UI.StatRowUI[] statRows)
        {
            var root = CreateEmpty("Content_Stats", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 8; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            // ── 캐릭터 정보 요약 ──
            var infoRow = CreateEmpty("InfoRow", root.transform);
            infoRow.AddComponent<LayoutElement>().preferredHeight = 70;
            var infoImg = infoRow.AddComponent<Image>();
            ApplyStoneFrame(infoImg, ST_FRAME + "mission_frame_brown.png", RowBg);
            var infoHlg = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoHlg.childAlignment = TextAnchor.MiddleLeft;
            infoHlg.childControlWidth = true; infoHlg.childControlHeight = true;
            infoHlg.childForceExpandWidth = false; infoHlg.childForceExpandHeight = true;
            infoHlg.spacing = 12; infoHlg.padding = new RectOffset(16, 16, 8, 8);

            // 캐릭터 아이콘 자리
            var portrait = CreateEmpty("Portrait", infoRow.transform);
            portrait.AddComponent<LayoutElement>().preferredWidth = 54;
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.color = new Color(0.3f, 0.28f, 0.25f, 1f);
            var iconSpr = LoadSprite(BTN_ICON + "profile.png");
            if (iconSpr != null) { portraitImg.sprite = iconSpr; portraitImg.preserveAspect = true; }

            // 레벨 + CP
            var infoVlg = CreateEmpty("InfoText", infoRow.transform);
            infoVlg.AddComponent<LayoutElement>().flexibleWidth = 1;
            var itVlg = infoVlg.AddComponent<VerticalLayoutGroup>();
            itVlg.childAlignment = TextAnchor.MiddleLeft;
            itVlg.childControlWidth = true; itVlg.childControlHeight = true;
            itVlg.childForceExpandWidth = true; itVlg.childForceExpandHeight = true;
            itVlg.spacing = 0;

            lvText = MakeTMP("LevelText", infoVlg.transform, "Lv.1 전사", 22);
            lvText.fontStyle = FontStyles.Bold;
            lvText.alignment = TextAlignmentOptions.Left;
            lvText.color = TextWhite;

            cpText = MakeTMP("CPText", infoVlg.transform, "전투력 0", 18);
            cpText.alignment = TextAlignmentOptions.Left;
            cpText.color = TextGold;

            // 등급
            gradeText = MakeTMP("GradeText", infoRow.transform, "등급 F", 20);
            gradeText.fontStyle = FontStyles.Bold;
            gradeText.alignment = TextAlignmentOptions.Center;
            gradeText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            gradeText.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

            // ── 포인트 행 ──
            var pointsRow = CreateEmpty("PointsRow", root.transform);
            pointsRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var ptsImg = pointsRow.AddComponent<Image>();
            ApplyStoneFrame(ptsImg, ST_FRAME + "status_bg_color.png", new Color(0.20f, 0.35f, 0.22f, 0.8f));
            var ptHlg = pointsRow.AddComponent<HorizontalLayoutGroup>();
            ptHlg.childAlignment = TextAnchor.MiddleCenter;
            ptHlg.childControlWidth = true; ptHlg.childControlHeight = true;
            ptHlg.childForceExpandWidth = false; ptHlg.childForceExpandHeight = true;
            ptHlg.spacing = 8; ptHlg.padding = new RectOffset(16, 16, 4, 4);

            var ptLabel = MakeTMP("Label", pointsRow.transform, "사용 가능 포인트", 18);
            ptLabel.alignment = TextAlignmentOptions.Left;
            ptLabel.color = TextWhite;
            ptLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            pointsText = MakeTMP("Points", pointsRow.transform, "0", 28);
            pointsText.fontStyle = FontStyles.Bold;
            pointsText.alignment = TextAlignmentOptions.Right;
            pointsText.color = PointGlow;
            pointsText.outlineWidth = 0.15f;
            pointsText.outlineColor = new Color32(255, 200, 50, 128);
            pointsText.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;

            var autoBtn = CreateButton("AutoBtn", pointsRow.transform, "자동분배", 90, 34);

            // ── 스탯 행들 (단순 VLG) ──
            string[] statNames = { "공격력", "최대HP", "방어력", "치명타", "명중" };
            int[] mockValues = { 10, 100, 5, 5, 5 };

            BuildSectionHeader(root.transform, "일반 능력치");

            statRows = new MkLike.UI.StatRowUI[statNames.Length + 3];

            for (int i = 0; i < statNames.Length; i++)
            {
                statRows[i] = BuildStatRow(root.transform, statNames[i], mockValues[i], i);
            }

            BuildSectionHeader(root.transform, "특수 능력치 (등급 C 해금)");

            string[] specNames = { "공격속도", "크리뎀", "보스뎀" };
            for (int i = 0; i < specNames.Length; i++)
            {
                statRows[statNames.Length + i] = BuildStatRow(root.transform, specNames[i], 0, statNames.Length + i, locked: true);
            }

            // ── 하단 초기화 버튼 ──
            var resetRow = CreateEmpty("ResetRow", root.transform);
            resetRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var resetHlg = resetRow.AddComponent<HorizontalLayoutGroup>();
            resetHlg.childAlignment = TextAnchor.MiddleCenter;
            resetHlg.childControlWidth = false; resetHlg.childControlHeight = false;
            resetHlg.childForceExpandWidth = false;

            var resetBtn = CreateButton("ResetBtn", resetRow.transform, "초기화 (루비100)", 200, 36);
            var resetImg = resetBtn.GetComponent<Image>();
            if (resetImg != null) resetImg.color = new Color(0.5f, 0.2f, 0.2f, 1f);

            return root;
        }

        // ══════════ Stat Row ══════════

        private static MkLike.UI.StatRowUI BuildStatRow(Transform parent, string name, int value, int index, bool locked = false)
        {
            var row = CreateEmpty($"StatRow_{name}", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 70;
            var rowImg = row.AddComponent<Image>();
            string rowSpr = index % 2 == 0 ? ST_FRAME + "mission_frame_brown.png" : ST_FRAME + "mission_frame_silver.png";
            ApplyStoneFrame(rowImg, rowSpr, index % 2 == 0 ? RowBg : new Color(0.14f, 0.13f, 0.11f, 0.9f));
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 8; hlg.padding = new RectOffset(12, 8, 6, 6);

            // 이름 (스탯별 색상 힌트)
            var nameT = MakeTMP("Name", row.transform, name, 20);
            nameT.alignment = TextAlignmentOptions.Left;
            nameT.color = locked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : GetStatColor(name);
            nameT.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

            // 프로그래스 바 (Slider)
            var barObj = new GameObject("Bar");
            barObj.transform.SetParent(row.transform, false);
            barObj.AddComponent<RectTransform>();
            barObj.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Background (프레임) - Slider01 직사각형 바 사용
            var barBg = new GameObject("Background");
            barBg.transform.SetParent(barObj.transform, false);
            var barBgImg = barBg.AddComponent<Image>();
            var frameSpr = LoadSprite(SL + "Slider01_Frame.png");
            if (frameSpr != null) { barBgImg.sprite = frameSpr; barBgImg.type = Image.Type.Sliced; }
            else barBgImg.color = new Color(0.12f, 0.11f, 0.10f, 1f);
            FullStretch(barBg);

            // InnerFrame (내부 프레임)
            var innerFrame = new GameObject("InnerFrame");
            innerFrame.transform.SetParent(barObj.transform, false);
            var innerImg = innerFrame.AddComponent<Image>();
            var innerSpr = LoadSprite(SL + "Slider01_InnerFrame.png");
            if (innerSpr != null) { innerImg.sprite = innerSpr; innerImg.type = Image.Type.Sliced; }
            else innerImg.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            var innerR = innerFrame.GetComponent<RectTransform>();
            innerR.anchorMin = Vector2.zero; innerR.anchorMax = Vector2.one;
            innerR.offsetMin = new Vector2(4, 4); innerR.offsetMax = new Vector2(-4, -4);

            // Fill Area (FillArea 스프라이트)
            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(barObj.transform, false);
            var faImg = fillArea.AddComponent<Image>();
            var faSpr = LoadSprite(SL + "Slider01_FillArea.png");
            if (faSpr != null) { faImg.sprite = faSpr; faImg.type = Image.Type.Sliced; }
            else faImg.color = new Color(0.05f, 0.05f, 0.04f, 1f);
            var faR = fillArea.GetComponent<RectTransform>();
            faR.anchorMin = Vector2.zero; faR.anchorMax = Vector2.one;
            faR.offsetMin = new Vector2(6, 6); faR.offsetMax = new Vector2(-6, -6);

            // Fill (실제 채움 바 - Image.fillAmount 방식)
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            var fillSpr = LoadSprite(SL + "Slider01_Fill.png");
            if (fillSpr != null) { fillImg.sprite = fillSpr; fillImg.type = Image.Type.Filled; }
            else { fillImg.color = new Color(0.3f, 0.7f, 0.4f, 1f); fillImg.type = Image.Type.Filled; }
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0; // 왼쪽에서 오른쪽으로
            fillImg.fillAmount = locked ? 0f : Mathf.Clamp01(value / 100f);
            if (locked) fillImg.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            FullStretch(fill);

            // 수치
            var valT = MakeTMP("Value", row.transform, locked ? "🔒" : value.ToString(), 20);
            valT.fontStyle = FontStyles.Bold;
            valT.alignment = TextAlignmentOptions.Center;
            valT.color = locked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : TextGold;
            valT.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;

            // +1, +10, MAX 버튼
            Button btn1 = null, btn10 = null, btnMax = null;
            if (!locked)
            {
                btn1 = CreateSmallBtn("+1", row.transform, 48);
                btn10 = CreateSmallBtn("+10", row.transform, 48);
                btnMax = CreateSmallBtn("최대", row.transform, 48);
            }
            else
            {
                // 잠금 스페이서
                var spacer = CreateEmpty("Spacer", row.transform);
                spacer.AddComponent<LayoutElement>().preferredWidth = 148;
            }

            return new MkLike.UI.StatRowUI
            {
                nameText = nameT,
                progressBar = fillImg,
                valueText = valT,
                btnPlus1 = btn1,
                btnPlus10 = btn10,
                btnMax = btnMax
            };
        }

        private static Button CreateSmallBtn(string label, Transform parent, float width)
        {
            var obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();

            // Stone Kit 컬러 버튼: +1=blue, +10=green, 최대=purple
            string btnPath = ST_BTN + "common_btn_color_green.png";
            if (label.Contains("+1") && !label.Contains("+10")) btnPath = ST_BTN + "common_btn_color_blue.png";
            else if (label.Contains("최대")) btnPath = ST_BTN + "common_btn_color_purple.png";
            ApplyStoneFrame(img, btnPath, BtnGreen);

            obj.AddComponent<LayoutElement>().preferredWidth = width;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.normalColor = Color.white;
            btn.colors = colors;

            var tmp = MakeTMP("Label", obj.transform, label, 14);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextWhite;
            tmp.fontStyle = FontStyles.Bold;
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
            hlg.padding = new RectOffset(8, 8, 0, 0);

            var line = new GameObject("Line");
            line.transform.SetParent(header.transform, false);
            var lineImg = line.AddComponent<Image>();
            ApplyStoneFrame(lineImg, ST_FRAME + "common_ribbon_brown.png", new Color(0.35f, 0.32f, 0.27f, 0.6f));
            line.AddComponent<LayoutElement>().flexibleWidth = 1;

            var tmp = MakeTMP("Label", header.transform, text, 14);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextTan;
            tmp.fontStyle = FontStyles.Bold;
            tmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

            var line2 = new GameObject("Line2");
            line2.transform.SetParent(header.transform, false);
            var line2Img = line2.AddComponent<Image>();
            ApplyStoneFrame(line2Img, ST_FRAME + "common_ribbon_brown.png", new Color(0.35f, 0.32f, 0.27f, 0.6f));
            line2.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        // ══════════ Skills Content ══════════

        private static GameObject BuildSkillsContent(Transform parent,
            out Transform skillListRoot, out GameObject skillRowPrefab)
        {
            var root = CreateEmpty("Content_Skills", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 8; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            BuildSectionHeader(root.transform, "학습된 스킬");

            // 스크롤뷰
            var scrollObj = CreateEmpty("SkillScroll", root.transform);
            scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1;
            scrollObj.AddComponent<Image>().color = new Color(0.11f, 0.10f, 0.09f, 0.8f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateEmpty("Viewport", scrollObj.transform);
            FullStretch(viewport);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObj = CreateEmpty("Content", viewport.transform);
            var contentRT = contentObj.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f); contentRT.sizeDelta = Vector2.zero;

            var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true; contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true; contentVlg.childForceExpandHeight = false;
            contentVlg.spacing = 4; contentVlg.padding = new RectOffset(4, 4, 4, 4);

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;

            skillListRoot = contentObj.transform;

            // 스킬 행 프리팹 (비활성 템플릿)
            skillRowPrefab = BuildSkillRowTemplate(contentObj.transform);
            skillRowPrefab.SetActive(false);

            return root;
        }

        private static GameObject BuildSkillRowTemplate(Transform parent)
        {
            var row = CreateEmpty("SkillRow_Template", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 66;
            row.AddComponent<Image>().color = RowBg;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 8; hlg.padding = new RectOffset(10, 10, 6, 6);

            // 스킬 아이콘
            var iconObj = CreateEmpty("Icon", row.transform);
            iconObj.AddComponent<LayoutElement>().preferredWidth = 48;
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.28f, 0.25f, 1f);
            iconImg.preserveAspect = true;
            var iconSpr = LoadSprite(BTN_ICON + "magic.png");
            if (iconSpr != null) iconImg.sprite = iconSpr;

            // 이름 + 타입 컬럼
            var infoCol = CreateEmpty("InfoCol", row.transform);
            infoCol.AddComponent<LayoutElement>().flexibleWidth = 1;
            var infoVlg = infoCol.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleLeft;
            infoVlg.childControlWidth = true; infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true; infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 2;

            var nameTmp = MakeTMP("SkillName", infoCol.transform, "스킬 이름", 16);
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = TextWhite;
            nameTmp.fontStyle = FontStyles.Bold;

            var typeTmp = MakeTMP("SkillType", infoCol.transform, "Active", 14);
            typeTmp.alignment = TextAlignmentOptions.Left;
            typeTmp.color = TextTan;

            // 레벨
            var lvTmp = MakeTMP("Level", row.transform, "Lv.1", 18);
            lvTmp.fontStyle = FontStyles.Bold;
            lvTmp.alignment = TextAlignmentOptions.Center;
            lvTmp.color = TextGold;
            lvTmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            // 비용
            var costTmp = MakeTMP("Cost", row.transform, "100G", 16);
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.color = TextTan;
            costTmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            // 레벨업 버튼
            var lvUpBtn = CreateButton("BtnLevelUp", row.transform, "강화", 70, 36);

            return row;
        }

        // ══════════ Job Content ══════════

        private static GameObject BuildJobContent(Transform parent,
            out TextMeshProUGUI jobCurrentText, out TextMeshProUGUI jobNextLevelText,
            out Transform jobChoiceGroup, out Button btnWarrior, out Button btnArcher, out Button btnMage)
        {
            var root = CreateEmpty("Content_Job", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 10; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            // 현재 직업 정보
            BuildSectionHeader(root.transform, "현재 직업");

            var infoRow = CreateEmpty("JobInfoRow", root.transform);
            infoRow.AddComponent<LayoutElement>().preferredHeight = 88;
            infoRow.AddComponent<Image>().color = RowBg;
            var infoVlg = infoRow.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleCenter;
            infoVlg.childControlWidth = true; infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true; infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 4; infoVlg.padding = new RectOffset(16, 16, 8, 8);

            jobCurrentText = MakeTMP("JobCurrentText", infoRow.transform, "전사 (Tier 0)", 22);
            jobCurrentText.fontStyle = FontStyles.Bold;
            jobCurrentText.alignment = TextAlignmentOptions.Center;
            jobCurrentText.color = TextWhite;

            jobNextLevelText = MakeTMP("JobNextLevelText", infoRow.transform, "다음 전직: Lv.40", 18);
            jobNextLevelText.alignment = TextAlignmentOptions.Center;
            jobNextLevelText.color = TextTan;

            // 직업 선택
            BuildSectionHeader(root.transform, "직업 선택");

            var choiceRow = CreateEmpty("JobChoiceGroup", root.transform);
            choiceRow.AddComponent<LayoutElement>().preferredHeight = 100;
            var choiceHlg = choiceRow.AddComponent<HorizontalLayoutGroup>();
            choiceHlg.childAlignment = TextAnchor.MiddleCenter;
            choiceHlg.childControlWidth = true; choiceHlg.childControlHeight = true;
            choiceHlg.childForceExpandWidth = true; choiceHlg.childForceExpandHeight = true;
            choiceHlg.spacing = 12; choiceHlg.padding = new RectOffset(8, 8, 8, 8);

            jobChoiceGroup = choiceRow.transform;

            btnWarrior = BuildJobChoiceButton(choiceRow.transform, "전사", "근거리 물리 공격\n높은 HP/방어력");
            btnArcher = BuildJobChoiceButton(choiceRow.transform, "궁수", "원거리 물리 공격\n높은 치명타/명중");
            btnMage = BuildJobChoiceButton(choiceRow.transform, "마법사", "원거리 마법 공격\n높은 공격력");

            // 전직 트리 미리보기
            BuildSectionHeader(root.transform, "전직 경로");

            var treeRow = CreateEmpty("JobTreePreview", root.transform);
            treeRow.AddComponent<LayoutElement>().preferredHeight = 120;
            treeRow.AddComponent<Image>().color = RowBg;
            var treeVlg = treeRow.AddComponent<VerticalLayoutGroup>();
            treeVlg.childAlignment = TextAnchor.UpperLeft;
            treeVlg.childControlWidth = true; treeVlg.childControlHeight = true;
            treeVlg.childForceExpandWidth = true; treeVlg.childForceExpandHeight = true;
            treeVlg.spacing = 2; treeVlg.padding = new RectOffset(16, 16, 8, 8);

            string[] tierLabels = { "0단계: 기본직", "1단계: 1차 전직 (Lv.40)", "2단계: 2차 전직 (Lv.80)", "3단계: 3차 전직 (Lv.120)" };
            for (int i = 0; i < tierLabels.Length; i++)
            {
                var tierTmp = MakeTMP($"Tier{i}", treeRow.transform, tierLabels[i], 14);
                tierTmp.alignment = TextAlignmentOptions.Left;
                tierTmp.color = i == 0 ? TextGold : TextTan;
            }

            return root;
        }

        private static Button BuildJobChoiceButton(Transform parent, string jobName, string desc)
        {
            var obj = CreateEmpty($"Btn_{jobName}", parent);
            var img = obj.AddComponent<Image>();
            img.color = SubTabNormal;
            var spr = LoadSprite(SPRITE + "Button/Button01_n.png");
            if (spr != null) { img.sprite = spr; img.type = Image.Type.Sliced; }

            var vlg = obj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            vlg.spacing = 4; vlg.padding = new RectOffset(8, 8, 8, 8);

            var nameTmp = MakeTMP("Name", obj.transform, jobName, 18);
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = TextWhite;

            var descTmp = MakeTMP("Desc", obj.transform, desc, 11);
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.color = TextTan;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = SubTabSelected;
            colors.pressedColor = new Color(0.35f, 0.30f, 0.25f, 1f);
            btn.colors = colors;

            return btn;
        }

        // ══════════ Relic Content: 2026-04-20 유물 시스템 완전 제거 ══════════

        // ══════════ Climber Content ══════════

        private static GameObject BuildClimberContent(Transform parent,
            out TextMeshProUGUI climberLevelText, out TextMeshProUGUI climberCostText,
            out TextMeshProUGUI climberSlotsText, out Button btnClimberUpgrade)
        {
            var root = CreateEmpty("Content_Climber", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 10; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            BuildSectionHeader(root.transform, "등반자의 힘");

            // 현재 단계 정보
            var infoRow = CreateEmpty("ClimberInfoRow", root.transform);
            infoRow.AddComponent<LayoutElement>().preferredHeight = 120;
            infoRow.AddComponent<Image>().color = RowBg;
            var infoVlg = infoRow.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleCenter;
            infoVlg.childControlWidth = true; infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true; infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 8; infoVlg.padding = new RectOffset(16, 16, 12, 12);

            climberLevelText = MakeTMP("ClimberLevelText", infoRow.transform, "등반자의 힘 Lv.0", 24);
            climberLevelText.fontStyle = FontStyles.Bold;
            climberLevelText.alignment = TextAlignmentOptions.Center;
            climberLevelText.color = TextWhite;

            climberCostText = MakeTMP("ClimberCostText", infoRow.transform, "강화 비용: 등반의 증표 x10", 18);
            climberCostText.alignment = TextAlignmentOptions.Center;
            climberCostText.color = TextTan;

            climberSlotsText = MakeTMP("ClimberSlotsText", infoRow.transform, "어빌리티 슬롯: 0 / 3", 18);
            climberSlotsText.alignment = TextAlignmentOptions.Center;
            climberSlotsText.color = TextGold;

            // 강화 버튼
            var btnRow = CreateEmpty("BtnRow", root.transform);
            btnRow.AddComponent<LayoutElement>().preferredHeight = 52;
            var btnHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childAlignment = TextAnchor.MiddleCenter;
            btnHlg.childControlWidth = false; btnHlg.childControlHeight = false;
            btnHlg.childForceExpandWidth = false;

            var btnObj = CreateButton("BtnClimberUpgrade", btnRow.transform, "강화하기", 200, 44);
            btnClimberUpgrade = btnObj.GetComponent<Button>();

            // 설명
            var descRow = CreateEmpty("DescRow", root.transform);
            descRow.AddComponent<LayoutElement>().preferredHeight = 90;
            descRow.AddComponent<Image>().color = new Color(0.13f, 0.12f, 0.11f, 0.7f);
            var descVlg = descRow.AddComponent<VerticalLayoutGroup>();
            descVlg.childAlignment = TextAnchor.UpperLeft;
            descVlg.childControlWidth = true; descVlg.childControlHeight = true;
            descVlg.childForceExpandWidth = true; descVlg.childForceExpandHeight = true;
            descVlg.padding = new RectOffset(12, 12, 8, 8);

            var descTmp = MakeTMP("Description", descRow.transform,
                "등반자의 힘을 강화하면 어빌리티 슬롯이 늘어납니다.\n" +
                "어빌리티 슬롯에 다양한 옵션을 장착하여 캐릭터를 강화할 수 있습니다.", 15);
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.color = TextTan;

            return root;
        }

        // ══════════ Ability Content ══════════

        private static GameObject BuildAbilityContent(Transform parent,
            out Transform abilityListRoot, out GameObject abilityRowPrefab, out Button btnAbilityReroll)
        {
            var root = CreateEmpty("Content_Ability", parent);

            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.childAlignment = TextAnchor.UpperCenter;
            rootVlg.childControlWidth = true; rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;
            rootVlg.spacing = 8; rootVlg.padding = new RectOffset(12, 12, 10, 10);

            BuildSectionHeader(root.transform, "어빌리티 슬롯");

            // 스크롤뷰
            var scrollObj = CreateEmpty("AbilityScroll", root.transform);
            scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1;
            scrollObj.AddComponent<Image>().color = new Color(0.11f, 0.10f, 0.09f, 0.8f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateEmpty("Viewport", scrollObj.transform);
            FullStretch(viewport);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObj = CreateEmpty("Content", viewport.transform);
            var contentRT = contentObj.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f); contentRT.sizeDelta = Vector2.zero;

            var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true; contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true; contentVlg.childForceExpandHeight = false;
            contentVlg.spacing = 4; contentVlg.padding = new RectOffset(4, 4, 4, 4);

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;

            abilityListRoot = contentObj.transform;

            // 어빌리티 행 프리팹 (비활성 템플릿)
            abilityRowPrefab = BuildAbilityRowTemplate(contentObj.transform);
            abilityRowPrefab.SetActive(false);

            // 하단: 재롤 버튼
            var btnRow = CreateEmpty("BtnRow", root.transform);
            btnRow.AddComponent<LayoutElement>().preferredHeight = 48;
            var btnHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childAlignment = TextAnchor.MiddleCenter;
            btnHlg.childControlWidth = false; btnHlg.childControlHeight = false;
            btnHlg.childForceExpandWidth = false;

            var rerollObj = CreateButton("BtnAbilityReroll", btnRow.transform, "재롤 (잠재의 수정 x1)", 240, 40);
            btnAbilityReroll = rerollObj.GetComponent<Button>();

            return root;
        }

        private static GameObject BuildAbilityRowTemplate(Transform parent)
        {
            var row = CreateEmpty("AbilityRow_Template", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 50;
            row.AddComponent<Image>().color = RowBg;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 8; hlg.padding = new RectOffset(12, 12, 6, 6);

            // 옵션 이름
            var nameTmp = MakeTMP("OptionName", row.transform, "공격력 +5%", 16);
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = TextWhite;
            nameTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // 수치
            var valueTmp = MakeTMP("OptionValue", row.transform, "+5%", 16);
            valueTmp.fontStyle = FontStyles.Bold;
            valueTmp.alignment = TextAlignmentOptions.Center;
            valueTmp.color = TextGold;
            valueTmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            // 잠금 토글
            var toggleObj = CreateEmpty("LockToggle", row.transform);
            toggleObj.AddComponent<LayoutElement>().preferredWidth = 40;
            var toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = new Color(0.25f, 0.23f, 0.20f, 1f);
            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBg;

            var checkmark = CreateEmpty("Checkmark", toggleObj.transform);
            FullStretch(checkmark);
            var checkImg = checkmark.AddComponent<Image>();
            checkImg.color = TextGold;
            var lockSpr = LoadSprite(BTN_ICON + "lock.png");
            if (lockSpr != null) { checkImg.sprite = lockSpr; checkImg.preserveAspect = true; }
            toggle.graphic = checkImg;

            return row;
        }

        private static GameObject BuildPlaceholder(Transform parent, string name, string text)
        {
            var obj = CreateEmpty(name, parent);
            var tmp = MakeTMP("PlaceholderText", obj.transform, text, 24);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextTan;
            FullStretch(tmp.gameObject);
            return obj;
        }

        private static GameObject CreateButton(string name, Transform parent, string text, float w, float h)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            ApplyStoneFrame(img, ST_BTN + "btn_confirm.png", BtnGreen);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var tmp = MakeTMP("Label", obj.transform, text, 15);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextWhite;
            tmp.fontStyle = FontStyles.Bold;
            FullStretch(tmp.gameObject);

            return obj;
        }

        // ══════════ Utilities ══════════

        /// <summary>Stone Kit 스프라이트를 Image에 적용. 실패 시 fallback 색상 사용.</summary>
        private static void ApplyStoneFrame(Image img, string spritePath, Color fallbackColor)
        {
            var spr = LoadSprite(spritePath);
            if (spr != null)
            {
                img.sprite = spr;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = fallbackColor;
            }
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

        // ══════════ Hero Power Content ══════════

        private static GameObject BuildHeroPowerContent(Transform parent)
        {
            var root = CreateEmpty("Content_HeroPower", parent);

            // UIDocument 기반 — HeroPowerTabUI가 UI Toolkit 렌더링 담당
            var uiDoc = root.AddComponent<UnityEngine.UIElements.UIDocument>();
            var visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                "Assets/UI Toolkit/Views/CharacterHeroPowerTab.uxml");
            if (visualTree != null)
            {
                var soDoc = new SerializedObject(uiDoc);
                soDoc.FindProperty("sourceAsset").objectReferenceValue = visualTree;
                soDoc.ApplyModifiedPropertiesWithoutUndo();
            }

            root.AddComponent<MkLike.UI.HeroPowerTabUI>();
            return root;
        }

        private static void FullStretch(GameObject obj)
        {
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
        }

        private static Color GetStatColor(string statName)
        {
            if (statName.Contains("공격속도")) return StatColorCrit;
            if (statName.Contains("보스뎀")) return StatColorAtk;
            if (statName.Contains("공격")) return StatColorAtk;
            if (statName.Contains("HP")) return StatColorHp;
            if (statName.Contains("방어")) return StatColorDef;
            if (statName.Contains("치명") || statName.Contains("크리")) return StatColorCrit;
            if (statName.Contains("명중")) return StatColorAcc;
            return TextWhite;
        }
    }
}
