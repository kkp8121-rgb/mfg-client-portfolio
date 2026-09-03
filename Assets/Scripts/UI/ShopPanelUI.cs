using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using System.Threading;
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Equipment;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 상점 패널 컨트롤러.
    /// 장비/무기 소환 2개 서브탭을 하나의 UXML/USS/C# 세트로 통합한다.
    /// GachaManager, CurrencyManager와 연동하여 가챠 수행 및 결과 표시.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanelUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 헤더 ──
        private Label _rubyText;

        // ── 서브탭 —— 2026-04-20 유물 시스템 완전 제거 (relic 탭 제외) ──
        private static readonly string[] TAB_KEYS = { "equipment", "weapon" };
        private static readonly GachaPoolType[] TAB_POOLS = { GachaPoolType.Equipment, GachaPoolType.Weapon };

        private VisualElement[] _tabElements;
        private VisualElement[] _contentElements;
        private int _currentTab = -1;

        // ── 각 탭 요소 캐싱 ──
        private Label[] _summonLevelLabels;
        private Label[] _cost1Labels;
        private Label[] _cost10Labels;
        private Button[] _pull1Buttons;
        private Button[] _pull10Buttons;
        private ScrollView[] _resultScrolls;
        private Label[] _rateLabels;

        // ── 루비 부족 플래시 ──
        private bool _isFlashingRuby;
        private CancellationTokenSource _flashCts;

        // ── 등급별 색상 ──
        private static readonly Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.7f, 0.7f, 0.7f) },
            { "Rare", new Color(0.3f, 0.6f, 1f) },
            { "Epic", new Color(0.7f, 0.3f, 0.9f) },
            { "Unique", new Color(1f, 0.85f, 0.1f) },
            { "Legendary", new Color(1f, 0.5f, 0.1f) },
            { "Mythic", new Color(1f, 0.2f, 0.3f) },
        };

        private static readonly Dictionary<string, string> GradeCssClasses = new()
        {
            { "Normal", "grade--normal" },
            { "Rare", "grade--rare" },
            { "Epic", "grade--epic" },
            { "Unique", "grade--unique" },
            { "Legendary", "grade--legendary" },
            { "Mythic", "grade--mythic" },
        };

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 56; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[ShopPanelUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindTabs();
            BindPullButtons();

            // 이벤트 구독 (중복 방지: Unsubscribe 먼저)
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<SummonLevelUpEvent>(OnSummonLevelUp);
            EventBus.Subscribe<SummonLevelUpEvent>(OnSummonLevelUp);

            // 기본 탭 선택
            SwitchTab(0);

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<SummonLevelUpEvent>(OnSummonLevelUp);

            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = null;
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ── 요소 캐싱 ──

        private void CacheElements()
        {
            _rubyText = _root.Q<Label>("ruby-text");

            int tabCount = TAB_KEYS.Length;
            _tabElements = new VisualElement[tabCount];
            _contentElements = new VisualElement[tabCount];
            _summonLevelLabels = new Label[tabCount];
            _rateLabels = new Label[tabCount];
            _cost1Labels = new Label[tabCount];
            _cost10Labels = new Label[tabCount];
            _pull1Buttons = new Button[tabCount];
            _pull10Buttons = new Button[tabCount];
            _resultScrolls = new ScrollView[tabCount];

            for (int i = 0; i < tabCount; i++)
            {
                string key = TAB_KEYS[i];
                _tabElements[i] = _root.Q<VisualElement>($"tab-{key}");
                _contentElements[i] = _root.Q<VisualElement>($"content-{key}");
                _summonLevelLabels[i] = _root.Q<Label>($"{key}-pity");
                _rateLabels[i] = _root.Q<Label>($"{key}-rates");
                _cost1Labels[i] = _root.Q<Label>($"{key}-cost1");
                _cost10Labels[i] = _root.Q<Label>($"{key}-cost10");
                _pull1Buttons[i] = _root.Q<Button>($"{key}-pull1");
                _pull10Buttons[i] = _root.Q<Button>($"{key}-pull10");
                _resultScrolls[i] = _root.Q<ScrollView>($"{key}-results");
            }
        }

        // ── 탭 바인딩 ──

        private void BindTabs()
        {
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                int idx = i;
                _tabElements[i]?.RegisterCallback<ClickEvent>(_ => SwitchTab(idx));
            }
        }

        private void SwitchTab(int index)
        {
            if (index == _currentTab) return;
            if (index < 0 || index >= TAB_KEYS.Length) return;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                bool isActive = i == index;

                // 서브탭 시각 전환
                if (_tabElements[i] != null)
                {
                    if (isActive)
                        _tabElements[i].AddToClassList("shop-subtab--active");
                    else
                        _tabElements[i].RemoveFromClassList("shop-subtab--active");
                }

                // 콘텐츠 display 토글
                if (_contentElements[i] != null)
                {
                    _contentElements[i].style.display = isActive
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }
            }

            _currentTab = index;
            RefreshAll();

            Debug.Log($"[ShopPanelUI] 탭 전환: {TAB_KEYS[index]}");
        }

        // ── 뽑기 버튼 바인딩 ──

        private void BindPullButtons()
        {
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                int idx = i;
                _pull1Buttons[i]?.RegisterCallback<ClickEvent>(_ => DoPull(TAB_POOLS[idx], false));
                _pull10Buttons[i]?.RegisterCallback<ClickEvent>(_ => DoPull(TAB_POOLS[idx], true));
            }
        }

        // ── 가챠 실행 ──

        private void DoPull(GachaPoolType poolType, bool isTenPull)
        {
            if (GachaManager.Instance == null)
            {
                Debug.LogWarning("[ShopPanelUI] GachaManager.Instance가 null");
                return;
            }

            DoPullAsync(poolType, isTenPull).Forget();
        }

        private async UniTaskVoid DoPullAsync(GachaPoolType poolType, bool isTenPull)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            int tabIndex = GetTabIndex(poolType);
            if (tabIndex < 0) return;

            var scroll = _resultScrolls[tabIndex];
            if (scroll != null)
                scroll.contentContainer.Clear();

            if (isTenPull)
            {
                var results = await GachaManager.Instance.PullTenAsync(poolType, ct);
                // 2026-04-23 post-await 가드: 팝업 Destroy 후 필드 접근 시 MissingReferenceException 방지
                if (this == null) return;
                if (results == null)
                {
                    FlashRubyInsufficient();
                    return;
                }

                for (int i = 0; i < results.Count; i++)
                {
                    AddResultCard(scroll, results[i], i);
                }
            }
            else
            {
                var result = await GachaManager.Instance.PullAsync(poolType, ct);
                if (this == null) return;
                if (result == null)
                {
                    FlashRubyInsufficient();
                    return;
                }

                AddResultCard(scroll, result, 0);
            }

            RefreshAll();
        }

        // ── 결과 카드 생성 ──

        private void AddResultCard(ScrollView scroll, GachaEntry entry, int index)
        {
            if (scroll == null || entry == null) return;

            string grade = entry.grade ?? "Normal";
            Color gradeColor = GetGradeColor(grade);
            bool isHighGrade = IsHighGrade(grade);
            string gradeCss = GradeCssClasses.TryGetValue(grade, out string css) ? css : "grade--normal";

            // 카드 루트
            var card = new VisualElement();
            card.AddToClassList("result-card");
            if (index % 2 == 1)
                card.AddToClassList("result-card--alt");
            if (isHighGrade)
                card.AddToClassList("result-card--high");

            // 등급 마커
            var marker = new VisualElement();
            marker.AddToClassList("result-card__marker");
            marker.style.backgroundColor = gradeColor;
            card.Add(marker);

            // 이름
            var nameLabel = new Label();
            nameLabel.AddToClassList("result-card__name");
            nameLabel.text = FormatItemName(entry.itemId);
            nameLabel.style.color = gradeColor;
            card.Add(nameLabel);

            // 등급 텍스트
            var gradeLabel = new Label();
            gradeLabel.AddToClassList("result-card__grade");
            gradeLabel.AddToClassList(gradeCss);
            gradeLabel.text = DisplayNameUtils.GetGradeDisplayName(grade);
            card.Add(gradeLabel);

            scroll.contentContainer.Add(card);
        }

        // ── 갱신 ──

        private void RefreshAll()
        {
            RefreshRuby();
            RefreshCosts();
            RefreshSummonLevel();
            RefreshRates();
            RefreshButtonStates();
        }

        private void RefreshRuby()
        {
            if (_rubyText == null) return;
            if (_isFlashingRuby) return;

            BigNumber ruby = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby)
                : BigNumber.Zero;
            _rubyText.text = HudPanel.FormatGold(ruby);
        }

        private void RefreshCosts()
        {
            if (_isFlashingRuby) return;
            if (GachaManager.Instance == null) return;

            int cost1 = GachaManager.Instance.SinglePullCost;
            int cost10 = GachaManager.Instance.TenPullCost;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                if (_cost1Labels[i] != null)
                    _cost1Labels[i].text = $"{cost1} 루비";
                if (_cost10Labels[i] != null)
                    _cost10Labels[i].text = $"{cost10} 루비";
            }
        }

        private void RefreshSummonLevel()
        {
            if (_isFlashingRuby) return;
            if (GachaManager.Instance == null) return;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                if (_summonLevelLabels[i] == null) continue;

                int level = GachaManager.Instance.GetSummonLevel(TAB_POOLS[i]);
                int maxLevel = GachaManager.Instance.GetMaxSummonLevel(TAB_POOLS[i]);
                int remaining = GachaManager.Instance.GetPullsToNextLevel(TAB_POOLS[i]);

                if (remaining >= 0)
                    _summonLevelLabels[i].text = $"소환 Lv.{level}/{maxLevel}  (다음 레벨까지 {remaining}회)";
                else
                    _summonLevelLabels[i].text = $"소환 Lv.{level}/{maxLevel}  (MAX)";
            }
        }

        /// <summary>
        /// 등급별 확률을 GachaPoolSO에서 계산하여 표시.
        /// 등급 순서: Normal → Rare → Epic → Unique → Legendary → Mythic
        /// </summary>
        private static readonly string[] GRADE_ORDER = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic", "Ancient" };

        private void RefreshRates()
        {
            if (GachaManager.Instance == null) return;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                if (_rateLabels[i] == null) continue;

                var pool = GachaManager.Instance.GetPool(TAB_POOLS[i]);
                if (pool == null || pool.entries == null || pool.entries.Length == 0)
                {
                    _rateLabels[i].text = "";
                    continue;
                }

                int summonLevel = GachaManager.Instance.GetSummonLevel(TAB_POOLS[i]);
                _rateLabels[i].text = BuildRateText(pool, summonLevel);
            }
        }

        /// <summary>
        /// 현재 소환 레벨에 맞는 등급별 확률 문자열 생성.
        /// 소환 레벨 데이터가 있으면 그걸 사용, 없으면 entries에서 합산.
        /// </summary>
        private static string BuildRateText(GachaPoolSO pool, int summonLevel)
        {
            float[] levelWeights = pool.GetGradeWeightsForLevel(summonLevel);

            if (levelWeights != null)
                return BuildRateTextFromWeights(levelWeights);

            return BuildRateTextFromEntries(pool);
        }

        /// <summary>소환 레벨 가중치 배열에서 확률 문자열 생성</summary>
        private static string BuildRateTextFromWeights(float[] weights)
        {
            float totalWeight = 0f;
            for (int i = 0; i < weights.Length; i++)
                totalWeight += weights[i];

            if (totalWeight <= 0f) return "";

            var sb = new StringBuilder();

            for (int i = 0; i < weights.Length && i < GRADE_ORDER.Length; i++)
            {
                if (weights[i] <= 0f) continue;

                float percent = weights[i] / totalWeight * 100f;

                if (sb.Length > 0)
                    sb.Append(" | ");

                string percentStr = percent % 1f == 0f
                    ? $"{percent:F0}%"
                    : $"{percent:F1}%";

                sb.Append($"{GRADE_ORDER[i]} {percentStr}");
            }

            return sb.ToString();
        }

        /// <summary>entries에서 등급별 가중치 합산 (소환 레벨 미설정 fallback)</summary>
        private static string BuildRateTextFromEntries(GachaPoolSO pool)
        {
            var gradeWeights = new Dictionary<string, float>();
            float totalWeight = 0f;

            for (int i = 0; i < pool.entries.Length; i++)
            {
                var entry = pool.entries[i];
                string grade = entry.grade ?? "Normal";
                float w = entry.weight;

                if (gradeWeights.ContainsKey(grade))
                    gradeWeights[grade] += w;
                else
                    gradeWeights[grade] = w;

                totalWeight += w;
            }

            if (totalWeight <= 0f) return "";

            var sb = new StringBuilder();

            for (int i = 0; i < GRADE_ORDER.Length; i++)
            {
                string grade = GRADE_ORDER[i];
                if (!gradeWeights.TryGetValue(grade, out float weight)) continue;
                if (weight <= 0f) continue;

                float percent = weight / totalWeight * 100f;

                if (sb.Length > 0)
                    sb.Append(" | ");

                string percentStr = percent % 1f == 0f
                    ? $"{percent:F0}%"
                    : $"{percent:F1}%";

                sb.Append($"{DisplayNameUtils.GetGradeDisplayName(grade)} {percentStr}");
            }

            return sb.ToString();
        }

        private void RefreshButtonStates()
        {
            if (_isFlashingRuby) return;

            BigNumber ruby = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby)
                : BigNumber.Zero;
            int cost1 = GachaManager.Instance != null ? GachaManager.Instance.SinglePullCost : 0;
            int cost10 = GachaManager.Instance != null ? GachaManager.Instance.TenPullCost : 0;

            bool canPull1 = ruby >= cost1;
            bool canPull10 = ruby >= cost10;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                ApplyButtonState(_pull1Buttons[i], canPull1);
                ApplyButtonState(_pull10Buttons[i], canPull10);
            }
        }

        private static void ApplyButtonState(Button btn, bool canAfford)
        {
            if (btn == null) return;
            btn.SetEnabled(canAfford);

            if (canAfford)
                btn.RemoveFromClassList("pull-btn--disabled");
            else
                btn.AddToClassList("pull-btn--disabled");
        }

        // ── 이벤트 핸들러 ──

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            if (evt.Type == CurrencyType.Ruby)
            {
                RefreshRuby();
                RefreshButtonStates();
            }
        }

        private void OnSummonLevelUp(SummonLevelUpEvent evt)
        {
            // 확률 및 레벨 표시 갱신
            RefreshSummonLevel();
            RefreshRates();

            // 레벨업 플래시 연출
            FlashSummonLevelUpAsync(evt).Forget();
        }

        private async UniTaskVoid FlashSummonLevelUpAsync(SummonLevelUpEvent evt)
        {
            // 현재 활성 탭의 레벨 라벨에 플래시
            if (_currentTab < 0 || _currentTab >= TAB_KEYS.Length) return;
            if (TAB_POOLS[_currentTab].ToString() != evt.PoolName) return;

            var label = _summonLevelLabels[_currentTab];
            if (label == null) return;

            var token = this.GetCancellationTokenOnDestroy();

            // 강조 색상 적용
            label.AddToClassList("summon-level--flash");
            label.style.color = new StyleColor(new Color(1f, 0.85f, 0.1f));

            string originalText = label.text;
            label.text = $"★ 소환 Lv.{evt.NewLevel} 달성! ★";

            try
            {
                await UniTask.Delay(1500, cancellationToken: token);
            }
            catch (System.OperationCanceledException) { }

            // 원래 상태 복원
            label.RemoveFromClassList("summon-level--flash");
            label.style.color = new StyleColor(StyleKeyword.Null);
            RefreshSummonLevel();
        }

        // ── 루비 부족 피드백 ──

        private void FlashRubyInsufficient()
        {
            Debug.Log("[ShopPanelUI] 루비 부족 — 플래시 피드백");

            if (!_isFlashingRuby)
                FlashRubyTextAsync().Forget();
        }

        private async UniTaskVoid FlashRubyTextAsync()
        {
            _isFlashingRuby = true;

            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            var token = _flashCts.Token;

            // 루비 텍스트 빨간 강조
            string originalText = _rubyText != null ? _rubyText.text : "";
            if (_rubyText != null)
            {
                BigNumber rubyHeld = CurrencyManager.Instance != null
                    ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby)
                    : BigNumber.Zero;
                _rubyText.text = $"부족! ({NumberFormatter.FormatKorean(rubyHeld)})";
                _rubyText.AddToClassList("ruby-flash");
            }

            // 현재 탭 천장 텍스트에도 표시
            if (_currentTab >= 0 && _currentTab < TAB_KEYS.Length && _summonLevelLabels[_currentTab] != null)
            {
                BigNumber rubyHeld = CurrencyManager.Instance != null
                    ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby)
                    : BigNumber.Zero;
                _summonLevelLabels[_currentTab].text = $"루비가 부족합니다! (보유: {NumberFormatter.FormatKorean(rubyHeld)})";
                _summonLevelLabels[_currentTab].style.color = new Color(1f, 0.2f, 0.2f);
            }

            try
            {
                await UniTask.Delay(2500, cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨 — 정상 흐름
            }

            // 원래 상태 복원
            if (_rubyText != null)
            {
                _rubyText.RemoveFromClassList("ruby-flash");
            }

            if (_currentTab >= 0 && _currentTab < TAB_KEYS.Length && _summonLevelLabels[_currentTab] != null)
            {
                _summonLevelLabels[_currentTab].style.color = new StyleColor(new Color(1f, 0.84f, 0f));
            }

            _isFlashingRuby = false;
            RefreshAll();
        }

        // ── 유틸리티 ──

        private static int GetTabIndex(GachaPoolType poolType)
        {
            return poolType switch
            {
                GachaPoolType.Equipment => 0,
                GachaPoolType.Weapon => 1,
                _ => -1
            };
        }

        private static Color GetGradeColor(string grade)
        {
            return GradeColors.TryGetValue(grade, out Color color) ? color : Color.white;
        }

        private static bool IsHighGrade(string grade)
        {
            return grade is "Epic" or "Unique" or "Legendary" or "Mythic";
        }

        private static string FormatItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;

            // 무기는 WeaponManager 우선 조회 (itemId "weapon_..." prefix)
            if (itemId.StartsWith("weapon_") && WeaponManager.Instance != null)
            {
                var wData = WeaponManager.Instance.GetData(itemId);
                if (wData != null && !string.IsNullOrEmpty(wData.displayName))
                    return wData.displayName;
            }

            // 장비 SO에서 한글 이름 조회
            if (EquipmentManager.Instance != null)
            {
                var equipData = EquipmentManager.Instance.GetData(itemId);
                if (equipData != null && !string.IsNullOrEmpty(equipData.displayName))
                    return equipData.displayName;
            }

            // 언더스코어를 공백으로 치환하고 첫 글자 대문자
            string formatted = itemId.Replace('_', ' ');
            if (formatted.Length > 0)
                formatted = char.ToUpper(formatted[0]) + formatted.Substring(1);
            return formatted;
        }
    }
}
