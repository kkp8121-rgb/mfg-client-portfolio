using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;

namespace MkLike.UI
{
    /// <summary>
    /// 캐릭터 팝업 패널. 4개 서브 탭 + 스탯 분배 시스템.
    /// </summary>
    public class CharacterPanel : MonoBehaviour
    {
        [Header("서브 탭")]
        [SerializeField] private Button[] subTabButtons;
        [SerializeField] private Image[] subTabBgs;
        [SerializeField] private GameObject[] subTabContents;

        [Header("스탯 탭 - 상단")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI cpText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI availablePointsText;

        [Header("스탯 탭 - 스탯 행")]
        [SerializeField] private StatRowUI[] statRows;

        [Header("스킬 탭")]
        [SerializeField] private Transform _skillListRoot;
        [SerializeField] private GameObject _skillRowPrefab;

        [Header("전직 탭")]
        [SerializeField] private TextMeshProUGUI _jobCurrentText;
        [SerializeField] private TextMeshProUGUI _jobNextLevelText;
        [SerializeField] private Transform _jobChoiceGroup;
        [SerializeField] private Button _btnJobWarrior;
        [SerializeField] private Button _btnJobArcher;
        [SerializeField] private Button _btnJobMage;

        [Header("유물 탭")]
        [SerializeField] private Transform _relicEquipSlots;
        [SerializeField] private Transform _relicListRoot;
        [SerializeField] private GameObject _relicRowPrefab;

        [Header("등반자의 힘 탭")]
        [SerializeField] private TextMeshProUGUI _climberLevelText;
        [SerializeField] private TextMeshProUGUI _climberCostText;
        [SerializeField] private TextMeshProUGUI _climberSlotsText;
        [SerializeField] private Button _btnClimberUpgrade;

        [Header("추천 분배")]
        [SerializeField] private Button _recommendStatButton;
        [SerializeField] private TMP_Text _recommendStatDescText;

        [Header("어빌리티 탭")]
        [SerializeField] private Transform _abilityListRoot;
        [SerializeField] private GameObject _abilityRowPrefab;
        [SerializeField] private Button _btnAbilityReroll;

        [Header("색상")]
        [SerializeField] private Color subTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        [SerializeField] private Color subTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);

        private int _currentSubTab = -1;
        private LevelSystem _levelSystem;
        private CombatStats _combatStats;
        private SkillSystem _skillSystem;

        // 스킬 탭 행 추적 (동적 생성)
        private readonly List<GameObject> _skillRowInstances = new();

        // 스탯별 투자 포인트 추적
        private int[] _allocatedPoints;

        // 스탯별 보너스 배율 (1포인트당)
        // 0:공격력(+2ATK), 1:최대HP(+10HP), 2:방어력(+1DEF), 3:치명타(+0.5%), 4:명중(미구현)
        private static readonly int[] AtkPerPoint = { 2, 0, 0, 0, 0 };
        private static readonly int[] HpPerPoint  = { 0, 10, 0, 0, 0 };
        private static readonly int[] DefPerPoint = { 0, 0, 1, 0, 0 };
        private static readonly float[] CritPerPoint = { 0, 0, 0, 0.005f, 0 };

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            _allocatedPoints = new int[statRows != null ? statRows.Length : 0];

            // 서브 탭 버튼 연결
            for (int i = 0; i < subTabButtons.Length; i++)
            {
                int idx = i;
                subTabButtons[i].onClick.AddListener(() => SwitchSubTab(idx));
            }

            // 스탯 버튼 연결
            for (int i = 0; i < statRows.Length; i++)
            {
                int statIdx = i;
                if (statRows[i].btnPlus1 != null)
                    statRows[i].btnPlus1.onClick.AddListener(() => AllocateStat(statIdx, 1));
                if (statRows[i].btnPlus10 != null)
                    statRows[i].btnPlus10.onClick.AddListener(() => AllocateStat(statIdx, 10));
                if (statRows[i].btnMax != null)
                    statRows[i].btnMax.onClick.AddListener(() => AllocateStatMax(statIdx));
            }

            if (_recommendStatButton != null)
                _recommendStatButton.onClick.AddListener(OnRecommendStatClicked);

            SwitchSubTab(0);
            FindPlayerRefs();
            RefreshStatTab();
        }

        private void OnEnable()
        {
            if (_allocatedPoints == null) return; // Start 전이면 스킵
            FindPlayerRefs();
            RefreshStatTab();
        }

        private void FindPlayerRefs()
        {
            // _combatStats와 _skillSystem 모두 있어야 완전히 초기화된 것
            if (_combatStats != null && _skillSystem != null) return;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                _levelSystem = player.GetComponent<LevelSystem>();
                _combatStats = player.GetComponent<CombatStats>();
                _skillSystem = player.GetComponent<SkillSystem>();
            }
        }

        // ── 서브 탭 ──

        public void SwitchSubTab(int index)
        {
            if (index == _currentSubTab) return;

            for (int i = 0; i < subTabContents.Length; i++)
            {
                bool active = i == index;
                subTabContents[i].SetActive(active);

                if (subTabBgs != null && i < subTabBgs.Length && subTabBgs[i] != null)
                {
                    var bg = subTabBgs[i];
                    var tm = UIThemeManager.Instance;
                    if (tm != null && tm.TabNormal != null && tm.TabSelected != null)
                    {
                        bg.sprite = active ? tm.TabSelected : tm.TabNormal;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;
                    }
                    else
                    {
                        var targetColor = active ? subTabSelected : subTabNormal;
                        DOTween.To(
                            () => bg.color,
                            c => bg.color = c,
                            targetColor, 0.15f)
                            .SetUpdate(true).SetLink(gameObject);
                    }
                }
            }

            if (index >= 0 && index < subTabContents.Length)
            {
                var cg = subTabContents[index].GetComponent<CanvasGroup>();
                if (cg == null) cg = subTabContents[index].AddComponent<CanvasGroup>();
                cg.alpha = 1f;
            }

            _currentSubTab = index;
            if (index == 0) RefreshStatTab();

            // 스킬 탭: _skillListRoot가 있는 서브탭이 활성화되면 갱신
            if (_skillListRoot != null && index >= 0 && index < subTabContents.Length
                && _skillListRoot.IsChildOf(subTabContents[index].transform))
            {
                RefreshSkillTab();
            }
        }

        // ── 스탯 분배 ──

        private void AllocateStat(int statIndex, int amount)
        {
            if (_levelSystem == null || _combatStats == null) return;
            if (statIndex < 0 || statIndex >= _allocatedPoints.Length) return;
            // 특수 능력치(5~7)는 잠금
            if (statIndex >= 5) return;

            int available = _levelSystem.AvailableStatPoints;
            int actual = Mathf.Min(amount, available);
            if (actual <= 0) return;

            if (!_levelSystem.ConsumeStatPoints(actual)) return;

            _allocatedPoints[statIndex] += actual;
            ApplyStatBonus(statIndex, actual);
            RefreshStatTab();

            // 버튼 펀치 피드백
            if (statRows[statIndex].valueText != null)
            {
                var rt = statRows[statIndex].valueText.transform;
                rt.DOKill();
                rt.localScale = Vector3.one;
                rt.DOPunchScale(Vector3.one * 0.2f, 0.2f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            }
        }

        private void AllocateStatMax(int statIndex)
        {
            if (_levelSystem == null) return;
            AllocateStat(statIndex, _levelSystem.AvailableStatPoints);
        }

        private void ApplyStatBonus(int statIndex, int points)
        {
            if (statIndex >= AtkPerPoint.Length) return;

            // 전체 할당 포인트 기반으로 수정자 재계산
            int totalHp = 0, totalAtk = 0, totalDef = 0;
            float totalCrit = 0f;

            for (int i = 0; i < _allocatedPoints.Length && i < AtkPerPoint.Length; i++)
            {
                totalAtk += AtkPerPoint[i] * _allocatedPoints[i];
                totalHp += HpPerPoint[i] * _allocatedPoints[i];
                totalDef += DefPerPoint[i] * _allocatedPoints[i];
                totalCrit += CritPerPoint[i] * _allocatedPoints[i];
            }

            _combatStats.SetCpReason("능력치 분배");

            // 기존 수정자 제거 후 재적용
            _combatStats.ClearModifiers(ModifierSource.StatAllocation);

            if (totalAtk != 0)
                _combatStats.AddModifier("statalloc_atk",
                    new StatModifier(ModifierSource.StatAllocation, "statalloc", StatType.Atk, totalAtk, 0f));

            if (totalHp != 0)
                _combatStats.AddModifier("statalloc_hp",
                    new StatModifier(ModifierSource.StatAllocation, "statalloc", StatType.MaxHp, totalHp, 0f));

            if (totalDef != 0)
                _combatStats.AddModifier("statalloc_def",
                    new StatModifier(ModifierSource.StatAllocation, "statalloc", StatType.Def, totalDef, 0f));

            if (totalCrit != 0f)
                _combatStats.AddModifier("statalloc_critrate",
                    new StatModifier(ModifierSource.StatAllocation, "statalloc", StatType.CritRate, totalCrit, 0f));
        }

        // ── UI 갱신 ──

        public void RefreshStatTab()
        {
            int available = _levelSystem != null ? _levelSystem.AvailableStatPoints : 0;

            if (availablePointsText != null)
                availablePointsText.text = available.ToString();

            if (_levelSystem != null && levelText != null)
                levelText.text = $"Lv.{_levelSystem.CurrentLevel}";

            if (_combatStats != null && cpText != null)
            {
                int cp = CombatFormula.CalculateCP(
                    _combatStats.Atk, _combatStats.Def,
                    _combatStats.MaxHp, _combatStats.CritRate);
                cpText.text = $"전투력 {NumberFormatter.FormatKorean(cp)}";
            }

            // 각 행 갱신
            if (statRows == null) return;

            int[] currentValues = GetCurrentStatValues();
            for (int i = 0; i < statRows.Length && i < currentValues.Length; i++)
            {
                if (statRows[i].valueText != null)
                    statRows[i].valueText.text = currentValues[i].ToString();

                if (statRows[i].progressBar != null)
                {
                    // 100포인트 기준 비율 (시각적 표현)
                    float pts = _allocatedPoints.Length > i ? _allocatedPoints[i] : 0;
                    statRows[i].progressBar.fillAmount = Mathf.Clamp01(pts / 100f);
                }

                // 버튼 인터랙티브 상태
                bool canAllocate = available > 0 && i < 5; // 특수 능력치 잠금
                if (statRows[i].btnPlus1 != null) statRows[i].btnPlus1.interactable = canAllocate;
                if (statRows[i].btnPlus10 != null) statRows[i].btnPlus10.interactable = canAllocate;
                if (statRows[i].btnMax != null) statRows[i].btnMax.interactable = canAllocate;
            }
        }

        // ── 스킬 탭 ──

        private void RefreshSkillTab()
        {
            if (_skillListRoot == null) return;
            FindPlayerRefs();

            // 기존 행 제거
            for (int i = _skillRowInstances.Count - 1; i >= 0; i--)
            {
                if (_skillRowInstances[i] != null)
                    Destroy(_skillRowInstances[i]);
            }
            _skillRowInstances.Clear();

            if (_skillSystem == null)
            {
                // SkillSystem을 다시 한 번 탐색
                FindPlayerRefs();
            }
            if (_skillSystem == null)
            {
                // SkillSystem 초기화 전이면 로딩 안내 표시
                var loadingRow = CreateSkillEmptyRow();
                _skillRowInstances.Add(loadingRow);
                return;
            }

            var learned = _skillSystem.LearnedSkills;
            if (learned == null || learned.Count == 0)
            {
                // 스킬이 없을 때 안내 메시지 표시
                var emptyRow = CreateSkillEmptyRow();
                _skillRowInstances.Add(emptyRow);
                return;
            }

            for (int i = 0; i < learned.Count; i++)
            {
                var skill = learned[i];
                if (skill == null) continue;
                var row = CreateSkillRow(skill);
                _skillRowInstances.Add(row);
            }
        }

        private GameObject CreateSkillEmptyRow()
        {
            var go = new GameObject("SkillRow_Empty", typeof(RectTransform));
            go.transform.SetParent(_skillListRoot, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "아직 습득한 스킬이 없습니다.\n레벨을 올려 스킬을 해금하세요!";
            tmp.fontSize = 16f;
            tmp.color = new Color(0.6f, 0.6f, 0.6f);
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private GameObject CreateSkillRow(SkillDataSO skill)
        {
            var go = new GameObject($"SkillRow_{skill.id}", typeof(RectTransform));
            go.transform.SetParent(_skillListRoot, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 66f;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.15f, 0.13f, 0.9f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(10, 10, 6, 6);

            // 아이콘
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            iconGo.AddComponent<LayoutElement>().preferredWidth = 48f;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.28f, 0.25f, 1f);
            iconImg.preserveAspect = true;
            if (skill.icon != null) iconImg.sprite = skill.icon;

            // 이름 + 타입 컬럼
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(go.transform, false);
            infoGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var infoVlg = infoGo.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleLeft;
            infoVlg.childControlWidth = true;
            infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true;
            infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 2f;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(infoGo.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = skill.displayName;
            nameTmp.fontSize = 16f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = new Color(0.95f, 0.93f, 0.88f);
            nameTmp.alignment = TextAlignmentOptions.Left;

            var typeGo = new GameObject("Type", typeof(RectTransform));
            typeGo.transform.SetParent(infoGo.transform, false);
            var typeTmp = typeGo.AddComponent<TextMeshProUGUI>();
            typeTmp.text = GetSkillTypeDisplay(skill.skillType);
            typeTmp.fontSize = 14f;
            typeTmp.color = new Color(0.68f, 0.63f, 0.56f);
            typeTmp.alignment = TextAlignmentOptions.Left;

            // 레벨
            int level = _skillSystem.GetSkillLevel(skill.id);
            var lvGo = new GameObject("Level", typeof(RectTransform));
            lvGo.transform.SetParent(go.transform, false);
            lvGo.AddComponent<LayoutElement>().preferredWidth = 60f;
            var lvTmp = lvGo.AddComponent<TextMeshProUGUI>();
            lvTmp.text = $"Lv.{level}";
            lvTmp.fontSize = 18f;
            lvTmp.fontStyle = FontStyles.Bold;
            lvTmp.color = new Color(1f, 0.85f, 0.086f);
            lvTmp.alignment = TextAlignmentOptions.Center;

            // 비용
            long cost = CombatFormula.SkillLevelUpCost(level);
            var costGo = new GameObject("Cost", typeof(RectTransform));
            costGo.transform.SetParent(go.transform, false);
            costGo.AddComponent<LayoutElement>().preferredWidth = 60f;
            var costTmp = costGo.AddComponent<TextMeshProUGUI>();
            costTmp.text = $"{cost}G";
            costTmp.fontSize = 16f;
            costTmp.color = new Color(0.68f, 0.63f, 0.56f);
            costTmp.alignment = TextAlignmentOptions.Center;

            // 강화 버튼
            var btnGo = new GameObject("BtnLevelUp", typeof(RectTransform));
            btnGo.transform.SetParent(go.transform, false);
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 70f;
            var btnBg = btnGo.AddComponent<Image>();
            btnBg.color = new Color(0.25f, 0.55f, 0.30f, 1f);
            var btn = btnGo.AddComponent<Button>();

            var btnLblGo = new GameObject("Label", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnGo.transform, false);
            var btnLblRt = btnLblGo.GetComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;
            var btnTmp = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "강화";
            btnTmp.fontSize = 15f;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.color = Color.white;
            btnTmp.alignment = TextAlignmentOptions.Center;

            // 강화 버튼 클릭 핸들러
            string skillId = skill.id;
            btn.onClick.AddListener(() => OnSkillLevelUp(skillId));

            return go;
        }

        private void OnSkillLevelUp(string skillId)
        {
            if (_skillSystem == null) return;

            int currentLevel = _skillSystem.GetSkillLevel(skillId);
            long cost = CombatFormula.SkillLevelUpCost(currentLevel);

            if (CurrencyManager.Instance == null) return;

            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, cost))
            {
                // CurrencyShortageEvent가 자동 발행됨
                return;
            }

            if (!_skillSystem.LevelUpSkill(skillId))
            {
                // 실패 시 골드 환불
                CurrencyManager.Instance.Add(CurrencyType.Gold, cost);
                return;
            }

            // 성공 — 탭 새로고침
            RefreshSkillTab();
        }

        private static string GetSkillTypeDisplay(SkillType type)
        {
            return type switch
            {
                SkillType.Active => "기본공격",
                SkillType.Passive => "패시브",
                SkillType.Buff => "버프",
                SkillType.Awakening => "각성기",
                _ => type.ToString()
            };
        }

        private int[] GetCurrentStatValues()
        {
            if (_combatStats == null)
                return new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };

            return new int[]
            {
                _combatStats.Atk,
                _combatStats.MaxHp,
                _combatStats.Def,
                Mathf.RoundToInt(_combatStats.CritRate * 100), // %로 표시
                0, // 명중 (미구현)
                0, // 공격속도 (특수, 잠금)
                0, // 크리뎀 (특수, 잠금)
                0, // 보스뎀 (특수, 잠금)
            };
        }

        private void EnsureComponents()
        {
            // ── 캔버스 확인: 항상 자체 Canvas로 HUD 위에 표시 ──
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas == null)
            {
                myCanvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            myCanvas.overrideSorting = true;
            myCanvas.sortingOrder = 100;

            // ── 풀스크린 오버레이 배경 ──
            var myRt = GetComponent<RectTransform>();
            if (myRt != null) SetStretch(myRt);

            var overlayBg = GetComponent<Image>();
            if (overlayBg == null) overlayBg = gameObject.AddComponent<Image>();
            overlayBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            // ── 내부 콘텐츠 패널 (90% 중앙) ──
            var contentPanel = EnsureChild(transform, "ContentPanel");
            var contentRt = contentPanel.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.05f, 0.05f);
            contentRt.anchorMax = new Vector2(0.95f, 0.95f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var contentBg = EnsureComponent<Image>(contentPanel);
            var tm = UIThemeManager.Instance;
            if (tm != null) tm.ApplyPanelBackground(contentBg);
            else contentBg.color = new Color(0.1f, 0.1f, 0.18f, 0.95f);

            // ── 타이틀 / 닫기 버튼 — Header에서 이미 표시하므로 비활성화 ──
            var titleGo = contentPanel.transform.Find("TitleText");
            if (titleGo != null) titleGo.gameObject.SetActive(false);
            var closeGo = contentPanel.transform.Find("CloseButton");
            if (closeGo != null) closeGo.gameObject.SetActive(false);

            // ── 서브탭 버튼 영역 (타이틀 아래 가로 행) ──
            int subTabCount = 7;
            string[] tabNames = { "스탯", "장비", "스킬", "전직", "유물", "동반자", "어빌리티" };

            var tabRow = EnsureChild(contentPanel.transform, "TabRow");
            var tabRowRt = tabRow.GetComponent<RectTransform>();
            tabRowRt.anchorMin = new Vector2(0f, 1f);
            tabRowRt.anchorMax = new Vector2(1f, 1f);
            tabRowRt.pivot = new Vector2(0.5f, 1f);
            tabRowRt.anchoredPosition = new Vector2(0f, -5f);
            tabRowRt.sizeDelta = new Vector2(-20f, 44f);
            var tabHlg = EnsureComponent<HorizontalLayoutGroup>(tabRow);
            tabHlg.spacing = 2f;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.padding = new RectOffset(4, 4, 4, 4);

            if (subTabButtons == null || subTabButtons.Length == 0)
            {
                subTabButtons = new Button[subTabCount];
                subTabBgs = new Image[subTabCount];
                for (int i = 0; i < subTabCount; i++)
                {
                    var btnGo = EnsureChild(tabRow.transform, $"SubTabBtn_{i}");
                    var btnBg = EnsureComponent<Image>(btnGo);
                    if (tm != null)
                    {
                        var (tabN, tabS) = tm.GetTabSprites();
                        UIThemeManager.ApplySpriteOrColor(btnBg, tabN, subTabNormal);
                    }
                    else btnBg.color = subTabNormal;
                    subTabButtons[i] = EnsureComponent<Button>(btnGo);
                    subTabBgs[i] = btnBg;

                    var labelGo = EnsureChild(btnGo.transform, "Label");
                    SetStretch(labelGo.GetComponent<RectTransform>());
                    var labelTmp = EnsureComponent<TextMeshProUGUI>(labelGo);
                    labelTmp.text = tabNames[i];
                    labelTmp.fontSize = 13f;
                    labelTmp.fontStyle = FontStyles.Bold;
                    labelTmp.color = Color.white;
                    labelTmp.alignment = TextAlignmentOptions.Center;
                }
            }
            if (subTabBgs == null || subTabBgs.Length == 0)
            {
                subTabBgs = new Image[subTabCount];
                for (int i = 0; i < subTabCount; i++)
                {
                    if (subTabButtons[i] != null)
                        subTabBgs[i] = subTabButtons[i].GetComponent<Image>();
                }
            }

            // ── 서브탭 콘텐츠 영역 (탭 아래 나머지 공간) ──
            var contentArea = EnsureChild(contentPanel.transform, "ContentArea");
            var contentAreaRt = contentArea.GetComponent<RectTransform>();
            contentAreaRt.anchorMin = new Vector2(0f, 0f);
            contentAreaRt.anchorMax = new Vector2(1f, 1f);
            contentAreaRt.offsetMin = new Vector2(10f, 10f);
            contentAreaRt.offsetMax = new Vector2(-10f, -55f);

            if (subTabContents == null || subTabContents.Length == 0)
            {
                subTabContents = new GameObject[subTabCount];
                for (int i = 0; i < subTabCount; i++)
                {
                    subTabContents[i] = EnsureChild(contentArea.transform, $"SubTabContent_{i}");
                    SetStretch(subTabContents[i].GetComponent<RectTransform>());
                    subTabContents[i].SetActive(false);
                }
            }

            // ── 탭 0: 스탯 ──
            var statContent = subTabContents[0];

            // 상단 정보 행 (레벨, CP, 등급, 포인트)
            var statHeader = EnsureChild(statContent.transform, "StatHeader");
            var statHeaderRt = statHeader.GetComponent<RectTransform>();
            statHeaderRt.anchorMin = new Vector2(0f, 1f);
            statHeaderRt.anchorMax = new Vector2(1f, 1f);
            statHeaderRt.pivot = new Vector2(0.5f, 1f);
            statHeaderRt.anchoredPosition = Vector2.zero;
            statHeaderRt.sizeDelta = new Vector2(0f, 40f);
            var statHeaderBg = EnsureComponent<Image>(statHeader);
            if (tm != null && tm.FrameBackground != null)
            {
                statHeaderBg.sprite = tm.FrameBackground;
                statHeaderBg.type = Image.Type.Sliced;
                statHeaderBg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            }
            else statHeaderBg.color = new Color(0.14f, 0.13f, 0.11f, 0.95f);
            var statHeaderHlg = EnsureComponent<HorizontalLayoutGroup>(statHeader);
            statHeaderHlg.spacing = 10f;
            statHeaderHlg.childForceExpandWidth = true;
            statHeaderHlg.childForceExpandHeight = true;
            statHeaderHlg.padding = new RectOffset(12, 12, 4, 4);

            if (levelText == null)
            {
                var go = EnsureChild(statHeader.transform, "LevelText");
                levelText = EnsureComponent<TextMeshProUGUI>(go);
                levelText.fontSize = 22f;
                levelText.fontStyle = FontStyles.Bold;
                levelText.color = Color.white;
                levelText.text = "Lv.1";
                levelText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (cpText == null)
            {
                var go = EnsureChild(statHeader.transform, "CpText");
                cpText = EnsureComponent<TextMeshProUGUI>(go);
                cpText.fontSize = 20f;
                cpText.color = new Color(1f, 0.85f, 0.1f);
                cpText.text = "전투력 0";
                cpText.alignment = TextAlignmentOptions.Center;
            }
            if (gradeText == null)
            {
                var go = EnsureChild(statHeader.transform, "GradeText");
                gradeText = EnsureComponent<TextMeshProUGUI>(go);
                gradeText.fontSize = 18f;
                gradeText.color = new Color(0.7f, 0.85f, 1f);
                gradeText.alignment = TextAlignmentOptions.MidlineRight;
            }

            // 사용 가능 포인트 행
            var pointsRow = EnsureChild(statContent.transform, "PointsRow");
            var pointsRowRt = pointsRow.GetComponent<RectTransform>();
            pointsRowRt.anchorMin = new Vector2(0f, 1f);
            pointsRowRt.anchorMax = new Vector2(1f, 1f);
            pointsRowRt.pivot = new Vector2(0.5f, 1f);
            pointsRowRt.anchoredPosition = new Vector2(0f, -44f);
            pointsRowRt.sizeDelta = new Vector2(0f, 30f);
            var pointsBg = EnsureComponent<Image>(pointsRow);
            if (tm != null && tm.FrameBackground != null)
            {
                pointsBg.sprite = tm.FrameBackground;
                pointsBg.type = Image.Type.Sliced;
                pointsBg.color = new Color(0.7f, 0.7f, 0.65f, 1f);
            }
            else pointsBg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            var pointsHlg = EnsureComponent<HorizontalLayoutGroup>(pointsRow);
            pointsHlg.spacing = 6f;
            pointsHlg.childForceExpandWidth = true;
            pointsHlg.childForceExpandHeight = true;
            pointsHlg.padding = new RectOffset(12, 12, 2, 2);

            var apLabel = EnsureChild(pointsRow.transform, "APLabel");
            var apLabelTmp = EnsureComponent<TextMeshProUGUI>(apLabel);
            apLabelTmp.text = "남은 포인트:";
            apLabelTmp.fontSize = 16f;
            apLabelTmp.color = new Color(0.8f, 0.8f, 0.8f);
            apLabelTmp.alignment = TextAlignmentOptions.MidlineRight;

            if (availablePointsText == null)
            {
                var go = EnsureChild(pointsRow.transform, "AvailablePointsText");
                availablePointsText = EnsureComponent<TextMeshProUGUI>(go);
                availablePointsText.fontSize = 18f;
                availablePointsText.fontStyle = FontStyles.Bold;
                availablePointsText.color = new Color(1f, 0.85f, 0.1f);
                availablePointsText.text = "0";
                availablePointsText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            // 스탯 행 스크롤 영역
            var statScrollArea = EnsureChild(statContent.transform, "StatScrollArea");
            var statScrollRt = statScrollArea.GetComponent<RectTransform>();
            statScrollRt.anchorMin = new Vector2(0f, 0f);
            statScrollRt.anchorMax = new Vector2(1f, 1f);
            statScrollRt.offsetMin = new Vector2(0f, 50f);
            statScrollRt.offsetMax = new Vector2(0f, -78f);
            var statVlg = EnsureComponent<VerticalLayoutGroup>(statScrollArea);
            statVlg.spacing = 4f;
            statVlg.childForceExpandWidth = true;
            statVlg.childForceExpandHeight = false;
            statVlg.padding = new RectOffset(6, 6, 4, 4);

            // 스탯 행 (8행) — 일반(0~4) + 구분선 + 특수(5~7)
            if (statRows == null || statRows.Length == 0)
            {
                string[] statNames = { "공격력", "최대HP", "방어력", "치명타", "명중", "공격속도", "크리뎀", "보스뎀" };
                Color statNameColor = new Color(1f, 0.78f, 0.25f); // 황금색
                Color statValueColor = Color.white;
                Sprite rowSprite = tm != null ? tm.FrameBackground : null;

                statRows = new StatRowUI[statNames.Length];
                for (int i = 0; i < statNames.Length; i++)
                {
                    // 일반/특수 구분선 (인덱스 5 직전)
                    if (i == 5)
                    {
                        var sep = EnsureChild(statScrollArea.transform, "StatSeparator");
                        var sepLe = sep.AddComponent<LayoutElement>();
                        sepLe.preferredHeight = 24f;
                        var sepTmp = EnsureComponent<TextMeshProUGUI>(sep);
                        sepTmp.text = "특수 능력치";
                        sepTmp.fontSize = 13f;
                        sepTmp.color = new Color(0.6f, 0.6f, 0.55f);
                        sepTmp.alignment = TextAlignmentOptions.MidlineLeft;
                    }

                    var row = EnsureChild(statScrollArea.transform, $"StatRow_{i}");
                    var rowLe = row.AddComponent<LayoutElement>();
                    rowLe.preferredHeight = 48f;
                    var rowBg = EnsureComponent<Image>(row);
                    if (rowSprite != null)
                    {
                        rowBg.sprite = rowSprite;
                        rowBg.type = Image.Type.Sliced;
                        rowBg.color = (i % 2 == 0)
                            ? new Color(0.85f, 0.85f, 0.85f, 1f)
                            : new Color(0.75f, 0.75f, 0.75f, 1f);
                    }
                    else
                    {
                        rowBg.color = (i % 2 == 0)
                            ? new Color(0.12f, 0.12f, 0.2f, 0.8f)
                            : new Color(0.15f, 0.15f, 0.24f, 0.8f);
                    }
                    var rowHlg = EnsureComponent<HorizontalLayoutGroup>(row);
                    rowHlg.spacing = 4f;
                    rowHlg.childForceExpandHeight = true;
                    rowHlg.childForceExpandWidth = false;
                    rowHlg.childAlignment = TextAnchor.MiddleLeft;
                    rowHlg.padding = new RectOffset(10, 8, 2, 2);

                    // 이름
                    var nameGo = EnsureChild(row.transform, "Name");
                    var nameLe = nameGo.AddComponent<LayoutElement>();
                    nameLe.preferredWidth = 72f;
                    var nameTmp = EnsureComponent<TextMeshProUGUI>(nameGo);
                    nameTmp.fontSize = 16f;
                    nameTmp.fontStyle = FontStyles.Bold;
                    nameTmp.color = statNameColor;
                    nameTmp.text = statNames[i];
                    nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

                    // 프로그레스 바 (배경 + 채움)
                    var barHolder = EnsureChild(row.transform, "BarHolder");
                    var barHolderLe = barHolder.AddComponent<LayoutElement>();
                    barHolderLe.preferredWidth = 80f;
                    barHolderLe.flexibleWidth = 1f;
                    var barBg = EnsureComponent<Image>(barHolder);
                    barBg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

                    var barFill = EnsureChild(barHolder.transform, "Fill");
                    var barFillRt = barFill.GetComponent<RectTransform>();
                    SetStretch(barFillRt);
                    var barImg = EnsureComponent<Image>(barFill);
                    barImg.sprite = CreateWhiteSprite();
                    barImg.color = new Color(0.3f, 0.65f, 0.35f);
                    barImg.type = Image.Type.Filled;
                    barImg.fillMethod = Image.FillMethod.Horizontal;
                    barImg.fillAmount = 0f;

                    // 값
                    var valGo = EnsureChild(row.transform, "Value");
                    var valLe = valGo.AddComponent<LayoutElement>();
                    valLe.preferredWidth = 60f;
                    var valTmp = EnsureComponent<TextMeshProUGUI>(valGo);
                    valTmp.fontSize = 18f;
                    valTmp.fontStyle = FontStyles.Bold;
                    valTmp.color = statValueColor;
                    valTmp.text = "0";
                    valTmp.alignment = TextAlignmentOptions.Center;

                    // 버튼 (일반 스탯만 — 특수 스탯은 버튼 없음)
                    Button btn1 = null, btn10 = null, btnMax = null;
                    if (i < 5)
                    {
                        btn1 = CreateStatButton(row.transform, "+1", 50f);
                        btn10 = CreateStatButton(row.transform, "+10", 54f);
                        btnMax = CreateStatButton(row.transform, "최대", 54f);
                    }

                    statRows[i] = new StatRowUI
                    {
                        nameText = nameTmp,
                        valueText = valTmp,
                        progressBar = barImg,
                        btnPlus1 = btn1,
                        btnPlus10 = btn10,
                        btnMax = btnMax
                    };
                }
            }

            // 추천 분배 (스탯 하단)
            var recommendRow = EnsureChild(statContent.transform, "RecommendRow");
            var recommendRowRt = recommendRow.GetComponent<RectTransform>();
            recommendRowRt.anchorMin = new Vector2(0f, 0f);
            recommendRowRt.anchorMax = new Vector2(1f, 0f);
            recommendRowRt.pivot = new Vector2(0.5f, 0f);
            recommendRowRt.anchoredPosition = Vector2.zero;
            recommendRowRt.sizeDelta = new Vector2(0f, 46f);
            var recommendHlg = EnsureComponent<HorizontalLayoutGroup>(recommendRow);
            recommendHlg.spacing = 8f;
            recommendHlg.childForceExpandHeight = true;
            recommendHlg.childForceExpandWidth = false;
            recommendHlg.padding = new RectOffset(8, 8, 4, 4);
            recommendHlg.childAlignment = TextAnchor.MiddleLeft;

            if (_recommendStatButton == null)
            {
                var go = EnsureChild(recommendRow.transform, "RecommendStatButton");
                var goLe = go.AddComponent<LayoutElement>();
                goLe.preferredWidth = 100f;
                var recBtnImg = EnsureComponent<Image>(go);
                _recommendStatButton = EnsureComponent<Button>(go);
                if (tm != null) tm.ApplyButtonFull(_recommendStatButton);
                else recBtnImg.color = new Color(0.3f, 0.35f, 0.5f, 0.9f);
                var rBtnLabel = EnsureChild(go.transform, "Label");
                SetStretch(rBtnLabel.GetComponent<RectTransform>());
                var rBtnTmp = EnsureComponent<TextMeshProUGUI>(rBtnLabel);
                rBtnTmp.text = "추천 분배";
                rBtnTmp.fontSize = 14f;
                rBtnTmp.fontStyle = FontStyles.Bold;
                rBtnTmp.color = Color.white;
                rBtnTmp.alignment = TextAlignmentOptions.Center;
            }
            if (_recommendStatDescText == null)
            {
                var go = EnsureChild(recommendRow.transform, "RecommendStatDescText");
                var goLe = go.AddComponent<LayoutElement>();
                goLe.flexibleWidth = 1f;
                _recommendStatDescText = EnsureComponent<TextMeshProUGUI>(go);
                _recommendStatDescText.fontSize = 13f;
                _recommendStatDescText.color = new Color(0.75f, 0.75f, 0.75f);
                _recommendStatDescText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            // ── 탭 1: 스킬 ──
            var skillContent = subTabContents[1];
            if (_skillListRoot == null)
            {
                var go = EnsureChild(skillContent.transform, "SkillListRoot");
                SetStretch(go.GetComponent<RectTransform>());
                var vlg = EnsureComponent<VerticalLayoutGroup>(go);
                vlg.spacing = 6f;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.padding = new RectOffset(8, 8, 8, 8);
                _skillListRoot = go.transform;
            }
            if (_skillRowPrefab == null)
            {
                _skillRowPrefab = EnsureChild(skillContent.transform, "SkillRowPrefab");
                _skillRowPrefab.SetActive(false);
            }

            // ── 탭 2: 전직 ──
            var jobContent = subTabContents[2];
            var jobVlg = EnsureComponent<VerticalLayoutGroup>(jobContent);
            jobVlg.spacing = 10f;
            jobVlg.childForceExpandWidth = true;
            jobVlg.childForceExpandHeight = false;
            jobVlg.padding = new RectOffset(12, 12, 12, 12);

            if (_jobCurrentText == null)
            {
                var go = EnsureChild(jobContent.transform, "JobCurrentText");
                go.AddComponent<LayoutElement>().preferredHeight = 44f;
                var jobBg = EnsureComponent<Image>(go);
                if (tm != null && tm.FrameBackground != null)
                {
                    jobBg.sprite = tm.FrameBackground;
                    jobBg.type = Image.Type.Sliced;
                    jobBg.color = new Color(0.8f, 0.8f, 0.75f, 1f);
                }
                else jobBg.color = new Color(0.14f, 0.14f, 0.2f, 0.8f);
                _jobCurrentText = EnsureComponent<TextMeshProUGUI>(go);
                _jobCurrentText.fontSize = 22f;
                _jobCurrentText.fontStyle = FontStyles.Bold;
                _jobCurrentText.color = Color.white;
                _jobCurrentText.alignment = TextAlignmentOptions.Center;
            }
            if (_jobNextLevelText == null)
            {
                var go = EnsureChild(jobContent.transform, "JobNextLevelText");
                go.AddComponent<LayoutElement>().preferredHeight = 32f;
                var jobNextBg = EnsureComponent<Image>(go);
                if (tm != null && tm.FrameBackground != null)
                {
                    jobNextBg.sprite = tm.FrameBackground;
                    jobNextBg.type = Image.Type.Sliced;
                    jobNextBg.color = new Color(0.7f, 0.7f, 0.65f, 1f);
                }
                else jobNextBg.color = new Color(0.12f, 0.12f, 0.18f, 0.7f);
                _jobNextLevelText = EnsureComponent<TextMeshProUGUI>(go);
                _jobNextLevelText.fontSize = 16f;
                _jobNextLevelText.color = new Color(0.8f, 0.8f, 0.7f);
                _jobNextLevelText.alignment = TextAlignmentOptions.Center;
            }
            if (_jobChoiceGroup == null)
            {
                var go = EnsureChild(jobContent.transform, "JobChoiceGroup");
                go.AddComponent<LayoutElement>().preferredHeight = 60f;
                var jHlg = EnsureComponent<HorizontalLayoutGroup>(go);
                jHlg.spacing = 12f;
                jHlg.childForceExpandWidth = true;
                jHlg.childForceExpandHeight = true;
                jHlg.childAlignment = TextAnchor.MiddleCenter;
                jHlg.padding = new RectOffset(20, 20, 4, 4);
                _jobChoiceGroup = go.transform;
            }
            if (_btnJobWarrior == null)
                _btnJobWarrior = CreateJobButton(_jobChoiceGroup, "전사", new Color(0.7f, 0.3f, 0.3f, 1f));
            if (_btnJobArcher == null)
                _btnJobArcher = CreateJobButton(_jobChoiceGroup, "궁수", new Color(0.3f, 0.6f, 0.3f, 1f));
            if (_btnJobMage == null)
                _btnJobMage = CreateJobButton(_jobChoiceGroup, "마법사", new Color(0.3f, 0.3f, 0.7f, 1f));

            // 전직 버튼에 테마 스프라이트 적용
            if (tm != null)
            {
                foreach (var btn in new[] { _btnJobWarrior, _btnJobArcher, _btnJobMage })
                {
                    if (btn != null) tm.ApplyButtonFull(btn);
                }
            }

            // ── 탭 3: 유물 + 등반자 + 어빌리티 (합산) ──
            var relicContent = subTabContents[3];
            var relicVlg = EnsureComponent<VerticalLayoutGroup>(relicContent);
            relicVlg.spacing = 6f;
            relicVlg.childForceExpandWidth = true;
            relicVlg.childForceExpandHeight = false;
            relicVlg.padding = new RectOffset(8, 8, 8, 8);

            // 유물 장착 슬롯
            if (_relicEquipSlots == null)
            {
                var go = EnsureChild(relicContent.transform, "RelicEquipSlots");
                go.AddComponent<LayoutElement>().preferredHeight = 60f;
                var rHlg = EnsureComponent<HorizontalLayoutGroup>(go);
                rHlg.spacing = 6f;
                rHlg.childForceExpandWidth = true;
                rHlg.childForceExpandHeight = true;
                rHlg.padding = new RectOffset(4, 4, 4, 4);
                var relicSlotBg = EnsureComponent<Image>(go);
                if (tm != null) tm.ApplyFrameBackground(relicSlotBg);
                else relicSlotBg.color = new Color(0.12f, 0.12f, 0.2f, 0.7f);
                _relicEquipSlots = go.transform;
            }
            // 유물 목록
            if (_relicListRoot == null)
            {
                var go = EnsureChild(relicContent.transform, "RelicListRoot");
                go.AddComponent<LayoutElement>().flexibleHeight = 1f;
                var rVlg = EnsureComponent<VerticalLayoutGroup>(go);
                rVlg.spacing = 4f;
                rVlg.childForceExpandWidth = true;
                rVlg.childForceExpandHeight = false;
                rVlg.padding = new RectOffset(4, 4, 4, 4);
                _relicListRoot = go.transform;
            }
            if (_relicRowPrefab == null)
            {
                _relicRowPrefab = EnsureChild(relicContent.transform, "RelicRowPrefab");
                _relicRowPrefab.SetActive(false);
            }

            // 등반자의 힘 섹션
            var climberSection = EnsureChild(relicContent.transform, "ClimberSection");
            climberSection.AddComponent<LayoutElement>().preferredHeight = 80f;
            var climberBg = EnsureComponent<Image>(climberSection);
            if (tm != null && tm.FrameBackground != null)
            {
                climberBg.sprite = tm.FrameBackground;
                climberBg.type = Image.Type.Sliced;
                climberBg.color = new Color(0.75f, 0.75f, 0.7f, 1f);
            }
            else climberBg.color = new Color(0.12f, 0.14f, 0.2f, 0.8f);
            var climberVlg = EnsureComponent<VerticalLayoutGroup>(climberSection);
            climberVlg.spacing = 4f;
            climberVlg.childForceExpandWidth = true;
            climberVlg.childForceExpandHeight = false;
            climberVlg.padding = new RectOffset(10, 10, 6, 6);

            if (_climberLevelText == null)
            {
                var go = EnsureChild(climberSection.transform, "ClimberLevelText");
                go.AddComponent<LayoutElement>().preferredHeight = 24f;
                _climberLevelText = EnsureComponent<TextMeshProUGUI>(go);
                _climberLevelText.fontSize = 18f;
                _climberLevelText.fontStyle = FontStyles.Bold;
                _climberLevelText.color = Color.white;
                _climberLevelText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var climberInfoRow = EnsureChild(climberSection.transform, "ClimberInfoRow");
            climberInfoRow.AddComponent<LayoutElement>().preferredHeight = 24f;
            var ciHlg = EnsureComponent<HorizontalLayoutGroup>(climberInfoRow);
            ciHlg.spacing = 10f;
            ciHlg.childForceExpandWidth = true;
            ciHlg.childForceExpandHeight = true;
            if (_climberCostText == null)
            {
                var go = EnsureChild(climberInfoRow.transform, "ClimberCostText");
                _climberCostText = EnsureComponent<TextMeshProUGUI>(go);
                _climberCostText.fontSize = 15f;
                _climberCostText.color = new Color(0.8f, 0.8f, 0.8f);
                _climberCostText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (_climberSlotsText == null)
            {
                var go = EnsureChild(climberInfoRow.transform, "ClimberSlotsText");
                _climberSlotsText = EnsureComponent<TextMeshProUGUI>(go);
                _climberSlotsText.fontSize = 15f;
                _climberSlotsText.color = new Color(0.8f, 0.8f, 0.8f);
                _climberSlotsText.alignment = TextAlignmentOptions.MidlineRight;
            }
            if (_btnClimberUpgrade == null)
            {
                var go = EnsureChild(climberSection.transform, "BtnClimberUpgrade");
                go.AddComponent<LayoutElement>().preferredHeight = 36f;
                var climberBtnImg = EnsureComponent<Image>(go);
                _btnClimberUpgrade = EnsureComponent<Button>(go);
                if (tm != null) tm.ApplyButtonFull(_btnClimberUpgrade);
                else climberBtnImg.color = new Color(0.25f, 0.4f, 0.3f, 0.9f);
                var lbl = EnsureChild(go.transform, "Label");
                SetStretch(lbl.GetComponent<RectTransform>());
                var lblTmp = EnsureComponent<TextMeshProUGUI>(lbl);
                lblTmp.text = "강화";
                lblTmp.fontSize = 16f;
                lblTmp.fontStyle = FontStyles.Bold;
                lblTmp.color = Color.white;
                lblTmp.alignment = TextAlignmentOptions.Center;
            }

            // 어빌리티 섹션
            if (_abilityListRoot == null)
            {
                var go = EnsureChild(relicContent.transform, "AbilityListRoot");
                go.AddComponent<LayoutElement>().flexibleHeight = 1f;
                var aVlg = EnsureComponent<VerticalLayoutGroup>(go);
                aVlg.spacing = 4f;
                aVlg.childForceExpandWidth = true;
                aVlg.childForceExpandHeight = false;
                aVlg.padding = new RectOffset(4, 4, 4, 4);
                _abilityListRoot = go.transform;
            }
            if (_abilityRowPrefab == null)
            {
                _abilityRowPrefab = EnsureChild(relicContent.transform, "AbilityRowPrefab");
                _abilityRowPrefab.SetActive(false);
            }
            if (_btnAbilityReroll == null)
            {
                var go = EnsureChild(relicContent.transform, "BtnAbilityReroll");
                go.AddComponent<LayoutElement>().preferredHeight = 36f;
                var rerollBtnImg = EnsureComponent<Image>(go);
                _btnAbilityReroll = EnsureComponent<Button>(go);
                if (tm != null) tm.ApplyButtonFull(_btnAbilityReroll);
                else rerollBtnImg.color = new Color(0.4f, 0.3f, 0.4f, 0.9f);
                var lbl = EnsureChild(go.transform, "Label");
                SetStretch(lbl.GetComponent<RectTransform>());
                var lblTmp = EnsureComponent<TextMeshProUGUI>(lbl);
                lblTmp.text = "리롤";
                lblTmp.fontSize = 16f;
                lblTmp.fontStyle = FontStyles.Bold;
                lblTmp.color = Color.white;
                lblTmp.alignment = TextAlignmentOptions.Center;
            }
        }

        private Button CreateStatButton(Transform parent, string label, float width)
        {
            var go = EnsureChild(parent, $"Btn{label}");
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            var btnImg = EnsureComponent<Image>(go);
            var btn = EnsureComponent<Button>(go);
            var tmBtn = UIThemeManager.Instance;
            if (tmBtn != null) tmBtn.ApplyButtonFull(btn);
            else btnImg.color = new Color(0.25f, 0.35f, 0.45f, 0.9f);
            var txtGo = EnsureChild(go.transform, "Label");
            SetStretch(txtGo.GetComponent<RectTransform>());
            var tmp = EnsureComponent<TextMeshProUGUI>(txtGo);
            tmp.text = label;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private Button CreateJobButton(Transform parent, string label, Color color)
        {
            var go = EnsureChild(parent, $"BtnJob{label}");
            EnsureComponent<Image>(go).color = color;
            var btn = EnsureComponent<Button>(go);
            var txtGo = EnsureChild(go.transform, "Label");
            SetStretch(txtGo.GetComponent<RectTransform>());
            var tmp = EnsureComponent<TextMeshProUGUI>(txtGo);
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        private GameObject EnsureChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void OnRecommendStatClicked()
        {
            if (RecommendationManager.Instance == null) return;

            var rec = RecommendationManager.Instance.GetRecommendedStatDistribution();
            if (_recommendStatDescText != null)
                _recommendStatDescText.text = rec.Description;

            int total = RecommendationManager.Instance.AutoAllocateStats();
            if (total > 0)
            {
                if (_recommendStatDescText != null)
                    _recommendStatDescText.text = $"{rec.Description}\n{total}포인트 분배 완료!";
                RefreshStatTab();
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }

    [Serializable]
    public struct StatRowUI
    {
        public TextMeshProUGUI nameText;
        public Image progressBar;
        public TextMeshProUGUI valueText;
        public Button btnPlus1;
        public Button btnPlus10;
        public Button btnMax;
    }
}
