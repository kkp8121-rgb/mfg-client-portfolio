using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using MkLike.Combat;
using MkLike.Data;

namespace MkLike.UI
{
    /// <summary>
    /// 도감 패널. 4개 카테고리(몬스터/장비/무기/유물)별 수집 현황,
    /// 마일스톤 보상 수령, 칭호 표시를 제공한다.
    /// </summary>
    public class CollectionBookPanel : BasePopup
    {
        [Header("카테고리 탭")]
        [SerializeField] private Button[] _categoryButtons;
        [SerializeField] private Image[] _categoryBgs;
        [SerializeField] private GameObject[] _categoryContents;

        [Header("진행도")]
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TMP_Text _totalText;
        [SerializeField] private TMP_Text _completionRateText;

        [Header("마일스톤")]
        [SerializeField] private TMP_Text _milestoneText;
        [SerializeField] private Button[] _milestoneClaimButtons;

        [Header("칭호")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _titleListText;

        [Header("색상")]
        [SerializeField] private Color _tabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        [SerializeField] private Color _tabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);

        // 2026-04-20 유물 시스템 완전 제거 — Relic 카테고리 제외
        private static readonly CollectionCategory[] Categories =
        {
            CollectionCategory.Monster,
            CollectionCategory.Equipment,
            CollectionCategory.Weapon
        };

        private static readonly string[] CategoryNames =
        {
            "몬스터", "장비", "무기"
        };

        private int _currentCategory = -1;

        private void OnEnable()
        {
            EventBus<CollectionEntryRegisteredEvent>.Subscribe(OnEntryRegistered);
            EventBus<CollectionMilestoneClaimedEvent>.Subscribe(OnMilestoneClaimed);
            EventBus<CollectionTitleChangedEvent>.Subscribe(OnTitleChanged);
        }

        private void OnDisable()
        {
            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnEntryRegistered);
            EventBus<CollectionMilestoneClaimedEvent>.Unsubscribe(OnMilestoneClaimed);
            EventBus<CollectionTitleChangedEvent>.Unsubscribe(OnTitleChanged);
        }

        private void Start()
        {
            EnsureComponents();

            if (_categoryButtons != null)
            {
                for (int i = 0; i < _categoryButtons.Length; i++)
                {
                    if (_categoryButtons[i] == null) continue;
                    int idx = i;
                    _categoryButtons[i].onClick.AddListener(() => SwitchCategory(idx));
                }
            }

            if (_milestoneClaimButtons != null)
            {
                for (int i = 0; i < _milestoneClaimButtons.Length; i++)
                {
                    if (_milestoneClaimButtons[i] == null) continue;
                    int idx = i;
                    _milestoneClaimButtons[i].onClick.AddListener(() => OnClaimMilestone(idx));
                }
            }
        }

        protected override void OnShow()
        {
            _currentCategory = -1;
            SwitchCategory(0);
            RefreshTotal();
            RefreshTitle();
        }

        /// <summary>
        /// 카테고리 탭을 전환한다.
        /// </summary>
        public void SwitchCategory(int index)
        {
            if (_categoryContents == null || index < 0 || index >= _categoryContents.Length)
                return;
            if (index == _currentCategory)
            {
                RefreshCategory();
                return;
            }

            // 테마 탭 스프라이트 로드
            Sprite tabNormalSprite = null, tabSelectedSprite = null;
            var theme = UIThemeManager.Instance;
            if (theme != null)
            {
                var (normal, selected) = theme.GetTabSprites();
                tabNormalSprite = normal;
                tabSelectedSprite = selected;
            }

            for (int i = 0; i < _categoryContents.Length; i++)
            {
                bool isActive = i == index;
                if (_categoryContents[i] != null)
                    _categoryContents[i].SetActive(isActive);

                if (_categoryBgs != null && i < _categoryBgs.Length && _categoryBgs[i] != null)
                {
                    var bg = _categoryBgs[i];
                    var sprite = isActive ? tabSelectedSprite : tabNormalSprite;
                    if (sprite != null)
                    {
                        bg.sprite = sprite;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;
                    }
                    else
                    {
                        DOTween.To(
                            () => bg.color,
                            c => bg.color = c,
                            isActive ? _tabSelected : _tabNormal, 0.15f)
                            .SetUpdate(true).SetLink(gameObject);
                    }
                }
            }

            // 페이드인 연출
            if (_categoryContents[index] != null)
            {
                var cg = _categoryContents[index].GetComponent<CanvasGroup>();
                if (cg == null) cg = _categoryContents[index].AddComponent<CanvasGroup>();
                cg.alpha = 0;
                DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, 0.2f).SetUpdate(true).SetLink(gameObject);
            }

            _currentCategory = index;
            RefreshCategory();
        }

        /// <summary>
        /// 현재 선택된 카테고리의 수집 진행도와 마일스톤을 갱신한다.
        /// </summary>
        private void RefreshCategory()
        {
            if (_currentCategory < 0) return;

            if (_currentCategory >= Categories.Length) return;
            if (CollectionBookManager.Instance == null) return;

            var category = Categories[_currentCategory];
            var (collected, total) = CollectionBookManager.Instance.GetProgress(category);

            if (_progressText != null)
                _progressText.text = $"{CategoryNames[_currentCategory]}: {collected} / {total}";

            if (_progressBar != null)
                _progressBar.fillAmount = total > 0 ? (float)collected / total : 0f;

            RefreshMilestoneDisplay(category);
        }

        private void RefreshMilestoneDisplay(CollectionCategory category)
        {
            if (CollectionBookManager.Instance == null) return;

            var milestones = CollectionBookManager.Instance.GetMilestoneInfos(category);
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < milestones.Count; i++)
            {
                var m = milestones[i];
                string color;
                string status;

                if (m.IsClaimed)
                {
                    color = "#FFD700";
                    status = " [수령완료]";
                }
                else if (m.IsAchieved)
                {
                    color = "#00FF00";
                    status = " [수령가능]";
                }
                else
                {
                    color = "#888888";
                    status = "";
                }

                string bonusText = !string.IsNullOrEmpty(m.Description) ? m.Description
                    : m.FlatBonus > 0 ? $"{m.StatType} +{m.FlatBonus:F0}"
                    : $"{m.StatType} +{m.PercentBonus * 100f:F0}%";

                sb.AppendLine($"<color={color}>{m.Threshold}종: {bonusText}{status}</color>");

                // 수령 버튼 상태 갱신
                if (_milestoneClaimButtons != null && i < _milestoneClaimButtons.Length && _milestoneClaimButtons[i] != null)
                {
                    _milestoneClaimButtons[i].gameObject.SetActive(m.IsAchieved && !m.IsClaimed);
                    _milestoneClaimButtons[i].interactable = m.IsAchieved && !m.IsClaimed;
                }
            }

            // 남은 버튼 숨기기
            if (_milestoneClaimButtons != null)
            {
                for (int i = milestones.Count; i < _milestoneClaimButtons.Length; i++)
                {
                    if (_milestoneClaimButtons[i] != null)
                        _milestoneClaimButtons[i].gameObject.SetActive(false);
                }
            }

            if (_milestoneText != null)
                _milestoneText.text = sb.ToString().TrimEnd();
        }

        private void OnClaimMilestone(int milestoneIndex)
        {
            if (_currentCategory < 0 || _currentCategory >= Categories.Length) return;
            if (CollectionBookManager.Instance == null) return;

            var category = Categories[_currentCategory];
            if (CollectionBookManager.Instance.ClaimMilestoneReward(category, milestoneIndex))
            {
                RefreshCategory();

                // 수령 연출
                if (_milestoneClaimButtons != null && milestoneIndex < _milestoneClaimButtons.Length
                    && _milestoneClaimButtons[milestoneIndex] != null)
                {
                    var rt = _milestoneClaimButtons[milestoneIndex].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.DOScale(1.2f, 0.1f).SetUpdate(true).SetLink(gameObject)
                            .OnComplete(() => rt.DOScale(1f, 0.1f).SetUpdate(true).SetLink(gameObject));
                    }
                }
            }
        }

        private void RefreshTotal()
        {
            if (CollectionBookManager.Instance == null) return;

            int totalCollected = CollectionBookManager.Instance.GetTotalCollected();
            if (_totalText != null)
                _totalText.text = $"전체 수집: {totalCollected}종";

            if (_completionRateText != null)
            {
                float rate = CollectionBookManager.Instance.GetOverallCompletionRate();
                _completionRateText.text = $"완성도: {rate * 100f:F1}%";
            }
        }

        private void RefreshTitle()
        {
            if (CollectionBookManager.Instance == null) return;

            string currentTitle = CollectionBookManager.Instance.GetCurrentTitle();
            if (_titleText != null)
            {
                _titleText.text = string.IsNullOrEmpty(currentTitle) ? "칭호 없음" : currentTitle;
            }

            if (_titleListText != null)
            {
                var titles = CollectionBookManager.Instance.GetTitleInfos();
                var sb = new System.Text.StringBuilder();

                for (int i = 0; i < titles.Count; i++)
                {
                    var t = titles[i];
                    string hexColor = ColorUtility.ToHtmlStringRGB(t.TitleColor);

                    if (t.IsCurrent)
                        sb.AppendLine($"<color=#{hexColor}><b>▶ {t.TitleName}</b> ({t.RequiredTotal}종)</color>");
                    else if (t.IsAchieved)
                        sb.AppendLine($"<color=#{hexColor}>{t.TitleName} ({t.RequiredTotal}종)</color>");
                    else
                        sb.AppendLine($"<color=#555555>??? ({t.RequiredTotal}종)</color>");
                }

                _titleListText.text = sb.ToString().TrimEnd();
            }
        }

        // ── 이벤트 핸들러 ──

        private void OnEntryRegistered(CollectionEntryRegisteredEvent evt)
        {
            if (!IsVisible) return;

            if (_currentCategory >= 0 && _currentCategory < Categories.Length
                && evt.Category == Categories[_currentCategory])
            {
                RefreshCategory();
            }

            RefreshTotal();
        }

        private void OnMilestoneClaimed(CollectionMilestoneClaimedEvent evt)
        {
            if (!IsVisible) return;

            if (_currentCategory >= 0 && _currentCategory < Categories.Length
                && evt.Category == Categories[_currentCategory])
            {
                RefreshCategory();
            }
        }

        private void OnTitleChanged(CollectionTitleChangedEvent evt)
        {
            if (!IsVisible) return;
            RefreshTitle();
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // ── Root: full-screen overlay background ──
            var rootRt = GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }
            var rootImg = GetComponent<Image>();
            if (rootImg == null) rootImg = gameObject.AddComponent<Image>();
            rootImg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            // ── Inner content panel: centered, ~90% ──
            var contentPanel = CreateUIChild("ContentPanel");
            var contentImg = contentPanel.AddComponent<Image>();
            var tm = UIThemeManager.Instance;
            if (tm != null) tm.ApplyPanelBackground(contentImg);
            else contentImg.color = new Color(0.1f, 0.1f, 0.18f, 0.95f);
            var contentRt = contentPanel.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.05f, 0.05f);
            contentRt.anchorMax = new Vector2(0.95f, 0.95f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var contentLayout = contentPanel.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 4;
            contentLayout.padding = new RectOffset(12, 12, 8, 8);

            // ── Title bar (title + close button) ──
            var titleBar = CreateUIChild("TitleBar");
            titleBar.transform.SetParent(contentPanel.transform, false);
            titleBar.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
            var titleBarLayout = titleBar.AddComponent<HorizontalLayoutGroup>();
            titleBarLayout.childAlignment = TextAnchor.MiddleCenter;
            titleBarLayout.childControlWidth = true;
            titleBarLayout.childControlHeight = true;
            titleBarLayout.childForceExpandWidth = true;
            titleBarLayout.childForceExpandHeight = true;
            titleBarLayout.padding = new RectOffset(40, 0, 0, 0);

            var titleGo = CreateUIChild("TitleText");
            titleGo.transform.SetParent(titleBar.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "도감";
            titleTmp.fontSize = 22f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            var titleLe = titleGo.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1;

            var closeGo = CreateUIChild("CloseBtn");
            closeGo.transform.SetParent(titleBar.transform, false);
            var closeBg = closeGo.AddComponent<Image>();
            var closeSpr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");
            if (closeSpr != null)
            {
                closeBg.sprite = closeSpr;
                closeBg.type = Image.Type.Simple;
                closeBg.preserveAspect = true;
                closeBg.color = Color.white;
            }
            else
            {
                closeBg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
            }
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => { if (UIManager.Instance != null) UIManager.Instance.ClosePopup(); else Hide(); });
            var closeLe = closeGo.AddComponent<LayoutElement>();
            closeLe.minWidth = 40; closeLe.minHeight = 40;
            closeLe.preferredWidth = 40; closeLe.preferredHeight = 40;
            if (closeSpr == null)
            {
                var closeTxtGo = CreateUIChild("X");
                closeTxtGo.transform.SetParent(closeGo.transform, false);
                var closeTxtRt = closeTxtGo.GetComponent<RectTransform>();
                closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
                closeTxtRt.offsetMin = Vector2.zero; closeTxtRt.offsetMax = Vector2.zero;
                var closeTmp = closeTxtGo.AddComponent<TextMeshProUGUI>();
                closeTmp.text = "X"; closeTmp.fontSize = 20f; closeTmp.fontStyle = FontStyles.Bold;
                closeTmp.color = Color.white; closeTmp.alignment = TextAlignmentOptions.Center;
            }

            // ── Summary row (total + completion rate + title) ──
            var summaryRow = CreateUIChild("SummaryRow");
            summaryRow.transform.SetParent(contentPanel.transform, false);
            summaryRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            var summaryLayout = summaryRow.AddComponent<HorizontalLayoutGroup>();
            summaryLayout.childAlignment = TextAnchor.MiddleCenter;
            summaryLayout.childControlWidth = true; summaryLayout.childControlHeight = true;
            summaryLayout.childForceExpandWidth = true; summaryLayout.childForceExpandHeight = true;
            summaryLayout.spacing = 12;

            if (_totalText == null)
            {
                var go = CreateUIChild("TotalText");
                go.transform.SetParent(summaryRow.transform, false);
                _totalText = go.AddComponent<TextMeshProUGUI>();
                _totalText.fontSize = 15f;
                _totalText.color = Color.white;
                _totalText.alignment = TextAlignmentOptions.Center;
            }
            if (_completionRateText == null)
            {
                var go = CreateUIChild("CompletionRateText");
                go.transform.SetParent(summaryRow.transform, false);
                _completionRateText = go.AddComponent<TextMeshProUGUI>();
                _completionRateText.fontSize = 15f;
                _completionRateText.color = new Color(0.6f, 0.8f, 1f);
                _completionRateText.alignment = TextAlignmentOptions.Center;
            }
            if (_titleText == null)
            {
                var go = CreateUIChild("TitleText");
                go.transform.SetParent(summaryRow.transform, false);
                _titleText = go.AddComponent<TextMeshProUGUI>();
                _titleText.fontSize = 15f;
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.color = new Color(1f, 0.85f, 0.1f);
                _titleText.alignment = TextAlignmentOptions.Center;
            }

            // ── Category tab row ──
            int catCount = Categories.Length;
            var tabRow = CreateUIChild("TabRow");
            tabRow.transform.SetParent(contentPanel.transform, false);
            tabRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            var tabRowLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.childAlignment = TextAnchor.MiddleCenter;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.spacing = 2;

            if (_categoryButtons == null || _categoryButtons.Length == 0)
            {
                _categoryButtons = new Button[catCount];
                _categoryBgs = new Image[catCount];
                for (int i = 0; i < catCount; i++)
                {
                    var tabGo = CreateUIChild($"CategoryBtn_{i}");
                    tabGo.transform.SetParent(tabRow.transform, false);
                    var tabBg = tabGo.AddComponent<Image>();
                    if (tm != null)
                    {
                        var (tabN, tabS) = tm.GetTabSprites();
                        UIThemeManager.ApplySpriteOrColor(tabBg, tabN, _tabNormal);
                    }
                    else tabBg.color = _tabNormal;
                    _categoryBgs[i] = tabBg;
                    _categoryButtons[i] = tabGo.AddComponent<Button>();

                    var tabLabel = CreateUIChild("Label");
                    tabLabel.transform.SetParent(tabGo.transform, false);
                    var tabLabelRt = tabLabel.GetComponent<RectTransform>();
                    tabLabelRt.anchorMin = Vector2.zero; tabLabelRt.anchorMax = Vector2.one;
                    tabLabelRt.offsetMin = Vector2.zero; tabLabelRt.offsetMax = Vector2.zero;
                    var tabTmp = tabLabel.AddComponent<TextMeshProUGUI>();
                    tabTmp.text = i < CategoryNames.Length ? CategoryNames[i] : $"Tab{i}";
                    tabTmp.fontSize = 12f;
                    tabTmp.color = Color.white;
                    tabTmp.alignment = TextAlignmentOptions.Center;
                }
            }
            else if (_categoryBgs == null || _categoryBgs.Length == 0)
            {
                _categoryBgs = new Image[catCount];
                for (int i = 0; i < catCount; i++)
                {
                    if (_categoryButtons[i] != null)
                        _categoryBgs[i] = _categoryButtons[i].GetComponent<Image>();
                }
            }

            // ── Progress bar area ──
            var progressArea = CreateUIChild("ProgressArea");
            progressArea.transform.SetParent(contentPanel.transform, false);
            progressArea.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 32);

            if (_progressText == null)
            {
                var go = CreateUIChild("ProgressText");
                go.transform.SetParent(progressArea.transform, false);
                var pRt = go.GetComponent<RectTransform>();
                pRt.anchorMin = Vector2.zero; pRt.anchorMax = Vector2.one;
                pRt.offsetMin = Vector2.zero; pRt.offsetMax = Vector2.zero;
                _progressText = go.AddComponent<TextMeshProUGUI>();
                _progressText.fontSize = 16f;
                _progressText.color = Color.white;
                _progressText.alignment = TextAlignmentOptions.Center;
            }
            if (_progressBar == null)
            {
                // Bar background
                var barBg = CreateUIChild("ProgressBarBg");
                barBg.transform.SetParent(progressArea.transform, false);
                var barBgRt = barBg.GetComponent<RectTransform>();
                barBgRt.anchorMin = new Vector2(0.05f, 0.1f);
                barBgRt.anchorMax = new Vector2(0.95f, 0.4f);
                barBgRt.offsetMin = Vector2.zero; barBgRt.offsetMax = Vector2.zero;
                var barBgImg = barBg.AddComponent<Image>();
                barBgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

                // Fill bar
                var barFill = CreateUIChild("ProgressBar");
                barFill.transform.SetParent(barBg.transform, false);
                var barFillRt = barFill.GetComponent<RectTransform>();
                barFillRt.anchorMin = Vector2.zero; barFillRt.anchorMax = Vector2.one;
                barFillRt.offsetMin = Vector2.zero; barFillRt.offsetMax = Vector2.zero;
                _progressBar = barFill.AddComponent<Image>();
                _progressBar.sprite = CreateWhiteSprite();
                _progressBar.color = new Color(0.3f, 0.6f, 0.3f);
                _progressBar.type = Image.Type.Filled;
                _progressBar.fillMethod = Image.FillMethod.Horizontal;
                _progressBar.fillAmount = 0f;
            }

            // ── Content area (holds category content panels) ──
            var contentArea = CreateUIChild("ContentArea");
            contentArea.transform.SetParent(contentPanel.transform, false);
            var contentAreaLe = contentArea.AddComponent<LayoutElement>();
            contentAreaLe.flexibleHeight = 1;
            contentAreaLe.flexibleWidth = 1;

            // Category content containers
            if (_categoryContents == null || _categoryContents.Length == 0)
            {
                _categoryContents = new GameObject[catCount];
                for (int i = 0; i < catCount; i++)
                {
                    var catContent = CreateUIChild($"CategoryContent_{i}");
                    catContent.transform.SetParent(contentArea.transform, false);
                    var ccRt = catContent.GetComponent<RectTransform>();
                    ccRt.anchorMin = Vector2.zero; ccRt.anchorMax = Vector2.one;
                    ccRt.offsetMin = Vector2.zero; ccRt.offsetMax = Vector2.zero;
                    _categoryContents[i] = catContent;
                    catContent.SetActive(false);

                    var ccLayout = catContent.AddComponent<VerticalLayoutGroup>();
                    ccLayout.childAlignment = TextAnchor.UpperCenter;
                    ccLayout.childControlWidth = true;
                    ccLayout.childControlHeight = false;
                    ccLayout.childForceExpandWidth = true;
                    ccLayout.childForceExpandHeight = false;
                    ccLayout.spacing = 6;
                    ccLayout.padding = new RectOffset(8, 8, 6, 6);
                }
            }

            // ── Milestone area (shared across normal category tabs) ──
            // Place milestone text and claim buttons inside each category content
            // We use a shared reference but parent them to the first normal category content,
            // then re-parent dynamically would be complex. Instead, put below contentArea.

            var milestoneArea = CreateUIChild("MilestoneArea");
            milestoneArea.transform.SetParent(contentPanel.transform, false);
            milestoneArea.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);
            var milestoneLe = milestoneArea.AddComponent<LayoutElement>();
            milestoneLe.flexibleHeight = 0.6f;
            var milestoneLayout = milestoneArea.AddComponent<VerticalLayoutGroup>();
            milestoneLayout.childControlWidth = true; milestoneLayout.childControlHeight = false;
            milestoneLayout.childForceExpandWidth = true; milestoneLayout.childForceExpandHeight = false;
            milestoneLayout.spacing = 4;
            milestoneLayout.padding = new RectOffset(8, 8, 4, 4);

            if (_milestoneText == null)
            {
                var go = CreateUIChild("MilestoneText");
                go.transform.SetParent(milestoneArea.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);
                var mlLe = go.AddComponent<LayoutElement>();
                mlLe.flexibleHeight = 1;
                _milestoneText = go.AddComponent<TextMeshProUGUI>();
                _milestoneText.fontSize = 13f;
                _milestoneText.color = Color.white;
                _milestoneText.richText = true;
                _milestoneText.alignment = TextAlignmentOptions.TopLeft;
            }

            if (_milestoneClaimButtons == null || _milestoneClaimButtons.Length == 0)
            {
                var claimRow = CreateUIChild("ClaimRow");
                claimRow.transform.SetParent(milestoneArea.transform, false);
                claimRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
                var claimLayout = claimRow.AddComponent<HorizontalLayoutGroup>();
                claimLayout.childAlignment = TextAnchor.MiddleCenter;
                claimLayout.childControlWidth = true; claimLayout.childControlHeight = true;
                claimLayout.childForceExpandWidth = true; claimLayout.childForceExpandHeight = true;
                claimLayout.spacing = 6;

                _milestoneClaimButtons = new Button[5];
                for (int i = 0; i < 5; i++)
                {
                    var go = CreateUIChild($"MilestoneClaimBtn_{i}");
                    go.transform.SetParent(claimRow.transform, false);
                    var bg = go.AddComponent<Image>();
                    _milestoneClaimButtons[i] = go.AddComponent<Button>();
                    if (tm != null) tm.ApplyConfirmButton(bg);
                    else bg.color = new Color(0.3f, 0.5f, 0.3f, 0.9f);
                    go.SetActive(false);

                    AddButtonLabel(go, "수령", 13f);
                }
            }

            // ── Title list area (bottom section) ──
            var titleArea = CreateUIChild("TitleArea");
            titleArea.transform.SetParent(contentPanel.transform, false);
            titleArea.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
            var titleAreaBg = titleArea.AddComponent<Image>();
            titleAreaBg.color = new Color(0.08f, 0.08f, 0.14f, 0.6f);

            if (_titleListText == null)
            {
                var go = CreateUIChild("TitleListText");
                go.transform.SetParent(titleArea.transform, false);
                var tlRt = go.GetComponent<RectTransform>();
                tlRt.anchorMin = Vector2.zero; tlRt.anchorMax = Vector2.one;
                tlRt.offsetMin = new Vector2(8, 4); tlRt.offsetMax = new Vector2(-8, -4);
                _titleListText = go.AddComponent<TextMeshProUGUI>();
                _titleListText.fontSize = 13f;
                _titleListText.color = Color.white;
                _titleListText.richText = true;
                _titleListText.alignment = TextAlignmentOptions.TopLeft;
            }

        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static void AddButtonLabel(GameObject parent, string text, float fontSize)
        {
            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(parent.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private new void OnDestroy()
        {
            base.OnDestroy();

            if (_categoryBgs != null)
            {
                for (int i = 0; i < _categoryBgs.Length; i++)
                {
                    if (_categoryBgs[i] != null)
                        DOTween.Kill(_categoryBgs[i]);
                }
            }

            if (_categoryContents != null)
            {
                for (int i = 0; i < _categoryContents.Length; i++)
                {
                    if (_categoryContents[i] != null)
                    {
                        var cg = _categoryContents[i].GetComponent<CanvasGroup>();
                        if (cg != null)
                            DOTween.Kill(cg);
                    }
                }
            }

            if (_milestoneClaimButtons != null)
            {
                for (int i = 0; i < _milestoneClaimButtons.Length; i++)
                {
                    if (_milestoneClaimButtons[i] != null)
                    {
                        var rt = _milestoneClaimButtons[i].GetComponent<RectTransform>();
                        if (rt != null)
                            DOTween.Kill(rt);
                    }
                }
            }
        }
    }
}
