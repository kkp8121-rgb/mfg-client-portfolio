using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Arena;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 아레나 PvP 팝업.
    /// 매칭 화면 → 상대 선택 → CP 비교 즉시 결과.
    /// 퀵메뉴 아레나 버튼에서 호출.
    /// </summary>
    public class ArenaPopup : BasePopup
    {
        [Header("UI 참조")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _tierText;
        [SerializeField] private TMP_Text _ratingText;
        [SerializeField] private TMP_Text _recordText;
        [SerializeField] private TMP_Text _entryText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _matchBtn;
        [SerializeField] private Button[] _candidateBtns;
        [SerializeField] private TMP_Text[] _candidateTexts;
        [SerializeField] private Button _closeBtn;

        // ── 패널 요소 (코드 생성) ──
        private Transform _mainPanel;
        private Transform _candidatePanel;
        private Transform _resultPanel;

        // ── 전적 패널 ──
        private Transform _recordPanel;
        private TMP_Text _recordSummaryText;
        private TMP_Text _recordListText;
        private TMP_Text _seasonInfoText;
        private Button _recordBackBtn;
        private Button _recordBtn;

        // ── 상태 ──
        private enum PopupState { Main, Candidates, Result, Records }
        private PopupState _state = PopupState.Main;

        private static readonly string[] TIER_NAMES = { "Bronze", "Silver", "Gold", "Diamond" };
        private static readonly Color[] TIER_COLORS =
        {
            new Color(0.8f, 0.5f, 0.2f),   // Bronze
            new Color(0.75f, 0.75f, 0.78f), // Silver
            new Color(1f, 0.84f, 0f),       // Gold
            new Color(0.5f, 0.8f, 1f)       // Diamond
        };

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
        }

        private void Start()
        {
            if (_matchBtn != null) _matchBtn.onClick.AddListener(OnMatchClicked);
            if (_closeBtn != null) _closeBtn.onClick.AddListener(() => Hide());

            _candidateBtns ??= new Button[3];
            for (int i = 0; i < _candidateBtns.Length; i++)
            {
                int idx = i;
                if (_candidateBtns[i] != null)
                    _candidateBtns[i].onClick.AddListener(() => OnCandidateSelected(idx));
            }
        }

        protected override void OnShow()
        {
            _state = PopupState.Main;
            RefreshMainUI();
            ShowPanel(PopupState.Main);
        }

        // ═══ UI 갱신 ═══

        private void RefreshMainUI()
        {
            var mgr = ArenaManager.Instance;
            if (mgr == null)
            {
                if (_titleText != null) _titleText.text = "아레나 (미연결)";
                return;
            }

            bool isUnlocked = mgr.IsUnlocked;

            if (_titleText != null)
                _titleText.text = isUnlocked ? "아레나" : "아레나 (미해금)";

            int tier = mgr.CurrentTier;
            string tierName = tier >= 0 && tier < TIER_NAMES.Length ? TIER_NAMES[tier] : "Bronze";
            Color tierColor = tier >= 0 && tier < TIER_COLORS.Length ? TIER_COLORS[tier] : Color.white;

            if (_tierText != null)
            {
                _tierText.text = tierName;
                _tierText.color = tierColor;
            }

            if (_ratingText != null)
                _ratingText.text = $"레이팅: {mgr.Rating}";

            if (_recordText != null)
            {
                float winRate = mgr.WinRate * 100f;
                _recordText.text = $"{mgr.TotalVictories}승 {mgr.TotalDefeats}패 (승률 {winRate:F0}%) | 연승: {mgr.CurrentWinStreak}";
            }

            if (_entryText != null)
            {
                int remaining = mgr.RemainingFreeEntries;
                int daily = mgr.DailyFreeEntries;
                BigNumber tickets = CurrencyManager.Instance != null
                    ? CurrencyManager.Instance.GetAmount(CurrencyType.ArenaTicket) : BigNumber.Zero;
                _entryText.text = $"무료 도전: {remaining}/{daily}  |  아레나 티켓: {NumberFormatter.FormatKorean(tickets)}";
            }

            if (_matchBtn != null)
                _matchBtn.interactable = isUnlocked && mgr.CanEnter();

            if (_resultText != null)
                _resultText.text = isUnlocked
                    ? "상대를 매칭하여 PvP 전투를 시작합니다."
                    : mgr.GetDenyReason();
        }

        // ═══ 매칭 ═══

        private void OnMatchClicked()
        {
            var mgr = ArenaManager.Instance;
            if (mgr == null || !mgr.CanEnter()) return;

            var candidates = mgr.GenerateRivalCandidates();
            ShowCandidates(candidates);
        }

        private static readonly string[] DIFFICULTY_LABELS = { "쉬움", "보통", "어려움" };
        private static readonly Color[] DIFFICULTY_COLORS =
        {
            new Color(0.4f, 0.8f, 0.4f),  // 녹색
            new Color(0.4f, 0.6f, 0.9f),  // 파란색
            new Color(0.9f, 0.3f, 0.3f),  // 빨간색
        };
        private static readonly float[] REWARD_MULTIPLIERS = { 0.8f, 1.0f, 1.5f };

        private void ShowCandidates(System.Collections.Generic.List<ArenaOpponent> candidates)
        {
            _state = PopupState.Candidates;
            ShowPanel(PopupState.Candidates);

            _candidateTexts ??= new TMP_Text[3];
            for (int i = 0; i < _candidateBtns.Length; i++)
            {
                if (i < candidates.Count)
                {
                    if (_candidateBtns[i] != null)
                    {
                        _candidateBtns[i].gameObject.SetActive(true);
                        var img = _candidateBtns[i].GetComponent<Image>();
                        if (img != null) img.color = DIFFICULTY_COLORS[i];
                    }
                    if (_candidateTexts[i] != null)
                    {
                        var c = candidates[i];
                        string diff = i < DIFFICULTY_LABELS.Length ? DIFFICULTY_LABELS[i] : "";
                        string reward = i < REWARD_MULTIPLIERS.Length ? $"보상 x{REWARD_MULTIPLIERS[i]}" : "";
                        string title = !string.IsNullOrEmpty(c.Title) ? $"<size=12>{c.Title}</size>\n" : "";
                        _candidateTexts[i].text = $"[{diff}] {title}{c.Name}\nLv.{c.Level}  |  CP: {c.PowerScore:N0}  |  {reward}";
                    }
                }
                else
                {
                    if (_candidateBtns[i] != null) _candidateBtns[i].gameObject.SetActive(false);
                }
            }
        }

        // ═══ 전투 (즉시 결과) ═══

        private void OnCandidateSelected(int index)
        {
            var mgr = ArenaManager.Instance;
            if (mgr == null) return;

            if (!mgr.StartMatch(index))
            {
                ToastUI.Show("도전 실패", "\u2718");
                return;
            }

            // CP 비교 즉시 결과
            var battleResult = mgr.SimulateBattleDetailed(index);
            ShowMatchResult(battleResult, mgr.CurrentCandidates[index]);
        }

        private void ShowMatchResult(ArenaBattleSimulator.BattleResult battleResult, ArenaOpponent opponent)
        {
            var mgr = ArenaManager.Instance;
            if (mgr == null) return;

            var matchResult = new ArenaMatchResult
            {
                IsVictory = battleResult.IsVictory,
                NewRating = mgr.Rating,
                NewTier = mgr.CurrentTier,
                OpponentName = opponent.Name,
                WinStreak = mgr.CurrentWinStreak,
                RewardGold = battleResult.IsVictory ? (mgr.CurrentTier + 1) * 5000 : 0,
                RewardRuby = battleResult.IsVictory && mgr.CurrentTier >= 1 ? mgr.CurrentTier * 5 : 0
            };
            ShowResult(matchResult);
        }

        private void ShowResult(ArenaMatchResult result)
        {
            _state = PopupState.Result;
            ShowPanel(PopupState.Result);

            if (_resultText != null)
            {
                string victoryText = result.IsVictory ? "<color=#FFD700>승리!</color>" : "<color=#FF4444>패배</color>";
                string ratingSign = result.RatingChange >= 0 ? "+" : "";
                string streakText = result.WinStreak > 1 ? $"\n연승: {result.WinStreak}" : "";
                string rewardText = result.IsVictory
                    ? $"\n보상: 골드 +{result.RewardGold:N0}" + (result.RewardRuby > 0 ? $", 루비 +{result.RewardRuby}" : "")
                    : "";

                int tier = result.NewTier;
                string tierName = tier >= 0 && tier < TIER_NAMES.Length ? TIER_NAMES[tier] : "Bronze";

                _resultText.text = $"vs {result.OpponentName}\n\n" +
                    $"{victoryText}\n" +
                    $"레이팅: {result.NewRating} ({ratingSign}{result.RatingChange})\n" +
                    $"티어: {tierName}" +
                    streakText + rewardText;
            }

            // 결과 확인 후 메인으로 복귀
            if (_matchBtn != null)
            {
                _matchBtn.onClick.RemoveAllListeners();
                _matchBtn.onClick.AddListener(OnBackToMain);
                _matchBtn.interactable = true;
                var label = _matchBtn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "확인";
            }
        }

        private void OnBackToMain()
        {
            if (_matchBtn != null)
            {
                _matchBtn.onClick.RemoveAllListeners();
                _matchBtn.onClick.AddListener(OnMatchClicked);
                var label = _matchBtn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "매칭";
            }

            _state = PopupState.Main;
            RefreshMainUI();
            ShowPanel(PopupState.Main);
        }

        // ═══ 전적 ═══

        private void OnRecordClicked()
        {
            var mgr = ArenaManager.Instance;
            if (mgr == null) return;

            _state = PopupState.Records;
            ShowPanel(PopupState.Records);

            // 요약
            float winRate = mgr.WinRate * 100f;
            int tier = mgr.CurrentTier;
            string tierName = tier >= 0 && tier < TIER_NAMES.Length ? TIER_NAMES[tier] : "Bronze";
            if (_recordSummaryText != null)
            {
                _recordSummaryText.text =
                    $"{mgr.TotalVictories}승 {mgr.TotalDefeats}패 (승률 {winRate:F1}%)\n" +
                    $"최고 연승: {mgr.BestWinStreak}  |  현재 연승: {mgr.CurrentWinStreak}";
            }

            // 시즌 정보
            if (_seasonInfoText != null)
            {
                _seasonInfoText.text =
                    $"시즌: {tierName} ({mgr.Rating} RP)\n" +
                    $"남은 도전: {mgr.RemainingFreeEntries}/{mgr.DailyFreeEntries}";
            }

            // 전적 목록
            if (_recordListText != null)
            {
                var records = mgr.Records;
                if (records.Count == 0)
                {
                    _recordListText.text = "전적이 없습니다.";
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    int start = System.Math.Max(0, records.Count - 20);
                    for (int i = records.Count - 1; i >= start; i--)
                    {
                        var r = records[i];
                        string resultStr = r.IsVictory
                            ? "<color=#4CAF50>승</color>"
                            : "<color=#F44336>패</color>";
                        string eloSign = r.EloChange >= 0 ? "+" : "";
                        sb.AppendLine($"{resultStr} vs {r.OpponentName} | {eloSign}{r.EloChange} RP | DMG {r.PlayerDamage:N0}");
                    }
                    _recordListText.text = sb.ToString();
                }
            }
        }

        // ═══ 패널 전환 ═══

        private void ShowPanel(PopupState state)
        {
            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(state == PopupState.Main || state == PopupState.Result);
            if (_candidatePanel != null)
                _candidatePanel.gameObject.SetActive(state == PopupState.Candidates);
            if (_recordPanel != null)
                _recordPanel.gameObject.SetActive(state == PopupState.Records);
        }

        // ═══ 코드 생성 UI ═══

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 딤 배경
            var bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.03f, 0.08f, 0.93f);
            }

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // ── 메인 패널 ──
            _mainPanel = EnsureChild(transform, "MainPanel", new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f),
                new Color(0.08f, 0.06f, 0.12f, 0.98f));
            var mainVlg = _mainPanel.gameObject.GetComponent<VerticalLayoutGroup>();
            if (mainVlg == null)
            {
                mainVlg = _mainPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                mainVlg.childAlignment = TextAnchor.UpperCenter;
                mainVlg.childControlWidth = true;
                mainVlg.childControlHeight = false;
                mainVlg.childForceExpandWidth = true;
                mainVlg.spacing = 10;
                mainVlg.padding = new RectOffset(20, 20, 20, 20);
            }

            // 타이틀
            if (_titleText == null)
            {
                var go = CreateText(_mainPanel, "Title", "아레나", 26, FontStyles.Bold, Color.white);
                _titleText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 38);
            }

            // 티어
            if (_tierText == null)
            {
                var go = CreateText(_mainPanel, "Tier", "Bronze", 22, FontStyles.Bold, TIER_COLORS[0]);
                _tierText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 32);
            }

            // 레이팅
            if (_ratingText == null)
            {
                var go = CreateText(_mainPanel, "Rating", "레이팅: 0", 18, FontStyles.Normal, new Color(1f, 0.85f, 0.1f));
                _ratingText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 26);
            }

            // 전적
            if (_recordText == null)
            {
                var go = CreateText(_mainPanel, "Record", "0승 0패", 15, FontStyles.Normal, new Color(0.7f, 0.75f, 0.8f));
                _recordText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
            }

            // 입장권
            if (_entryText == null)
            {
                var go = CreateText(_mainPanel, "Entry", "무료 도전: 5/5", 15, FontStyles.Normal, new Color(0.6f, 0.8f, 0.6f));
                _entryText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
            }

            // 결과/설명 텍스트
            if (_resultText == null)
            {
                var go = CreateText(_mainPanel, "Result", "", 16, FontStyles.Normal, new Color(0.85f, 0.9f, 0.95f));
                _resultText = go.GetComponent<TMP_Text>();
                _resultText.richText = true;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 120);
            }

            // 매칭 버튼
            if (_matchBtn == null)
            {
                _matchBtn = CreateButton(_mainPanel, "MatchBtn", "매칭", new Color(0.3f, 0.2f, 0.7f));
                _matchBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
            }

            // 전적 버튼
            if (_recordBtn == null)
            {
                _recordBtn = CreateButton(_mainPanel, "RecordBtn", "전적", new Color(0.25f, 0.35f, 0.5f));
                _recordBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
                _recordBtn.onClick.AddListener(OnRecordClicked);
            }

            // 닫기 버튼
            if (_closeBtn == null)
            {
                _closeBtn = CreateButton(_mainPanel, "CloseBtn", "닫기", new Color(0.4f, 0.25f, 0.25f));
                _closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
            }

            // ── 후보 패널 ──
            _candidatePanel = EnsureChild(transform, "CandidatePanel", new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.75f),
                new Color(0.06f, 0.08f, 0.14f, 0.98f));
            var candVlg = _candidatePanel.gameObject.GetComponent<VerticalLayoutGroup>();
            if (candVlg == null)
            {
                candVlg = _candidatePanel.gameObject.AddComponent<VerticalLayoutGroup>();
                candVlg.childAlignment = TextAnchor.MiddleCenter;
                candVlg.childControlWidth = true;
                candVlg.childControlHeight = false;
                candVlg.childForceExpandWidth = true;
                candVlg.spacing = 12;
                candVlg.padding = new RectOffset(20, 20, 20, 20);
            }

            // 후보 타이틀
            CreateText(_candidatePanel, "CandTitle", "상대를 선택하세요", 20, FontStyles.Bold, Color.white)
                .GetComponent<RectTransform>().sizeDelta = new Vector2(0, 34);

            _candidateBtns ??= new Button[3];
            _candidateTexts ??= new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                if (_candidateBtns[i] == null)
                {
                    float hue = 0.55f + i * 0.1f;
                    Color btnColor = Color.HSVToRGB(hue, 0.4f, 0.5f);
                    _candidateBtns[i] = CreateButton(_candidatePanel, $"Cand{i}Btn", $"후보 {i + 1}", btnColor);
                    _candidateBtns[i].GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
                    _candidateTexts[i] = _candidateBtns[i].GetComponentInChildren<TMP_Text>();
                }
            }

            _candidatePanel.gameObject.SetActive(false);

            // ── 전적 패널 ──
            _recordPanel = EnsureChild(transform, "RecordPanel", new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.9f),
                new Color(0.06f, 0.06f, 0.1f, 0.98f));
            var recVlg = _recordPanel.gameObject.GetComponent<VerticalLayoutGroup>();
            if (recVlg == null)
            {
                recVlg = _recordPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                recVlg.childAlignment = TextAnchor.UpperCenter;
                recVlg.childControlWidth = true;
                recVlg.childControlHeight = false;
                recVlg.childForceExpandWidth = true;
                recVlg.spacing = 6;
                recVlg.padding = new RectOffset(12, 12, 12, 12);
            }

            CreateText(_recordPanel, "RecTitle", "전적 기록", 22, FontStyles.Bold, Color.white)
                .GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);

            if (_recordSummaryText == null)
            {
                var go = CreateText(_recordPanel, "Summary", "", 15, FontStyles.Normal, new Color(0.9f, 0.85f, 0.5f));
                _recordSummaryText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
            }

            if (_seasonInfoText == null)
            {
                var go = CreateText(_recordPanel, "Season", "", 14, FontStyles.Normal, new Color(0.6f, 0.8f, 1f));
                _seasonInfoText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
            }

            if (_recordListText == null)
            {
                var go = CreateText(_recordPanel, "RecList", "", 12, FontStyles.Normal, new Color(0.8f, 0.85f, 0.9f));
                _recordListText = go.GetComponent<TMP_Text>();
                _recordListText.alignment = TextAlignmentOptions.TopLeft;
                _recordListText.richText = true;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 280);
            }

            if (_recordBackBtn == null)
            {
                _recordBackBtn = CreateButton(_recordPanel, "BackBtn", "돌아가기", new Color(0.3f, 0.3f, 0.4f));
                _recordBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
                _recordBackBtn.onClick.AddListener(() =>
                {
                    _state = PopupState.Main;
                    RefreshMainUI();
                    ShowPanel(PopupState.Main);
                });
            }

            _recordPanel.gameObject.SetActive(false);
        }

        private static Transform EnsureChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var child = parent.Find(name);
            if (child != null) return child;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var childRt = go.GetComponent<RectTransform>();
            childRt.anchorMin = anchorMin;
            childRt.anchorMax = anchorMax;
            childRt.offsetMin = Vector2.zero;
            childRt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            return go.transform;
        }

        private static GameObject CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = color;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return btn;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
