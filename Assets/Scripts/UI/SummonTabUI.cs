using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Equipment;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 소환 탭 UI 컨트롤러 (UI Toolkit).
    /// 세로(9:16) 레이아웃. 장비/무기 카테고리 탭 전환 + 소환 레벨 표시 + 1회/10연차 소환.
    /// GachaManager, CurrencyManager와 연동한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SummonTabUI : MonoBehaviour
    {
        // ── UI Document ──
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 헤더 재화 ──
        private Label _currencyRuby;
        private Label _currencyHeart;

        // ── 일러스트 ──
        private VisualElement _illustImage;
        private Label _illustDesc;

        // ── 소환 레벨 ──
        private Label _summonLevelValue;
        private Label _summonLevelPulls;
        private VisualElement _summonLevelFill;

        // ── 카테고리 탭 ──
        private Button _tabEquipment;
        private Button _tabWeapon;

        // ── 하단 버튼 ──
        private Button _btnTicket;
        private Button _btnAd;
        private Button _btnPull1;
        private Button _btnPull10;

        // ── 획득 정보 ──
        private Button _btnInfo;

        // ── 소환 결과 ──
        private VisualElement _resultOverlay;
        private VisualElement _resultGrid;
        private Button _resultCloseBtn;
        private readonly System.Collections.Generic.List<GachaResultEvent> _pendingResults = new();
        private int _expectedPullCount;

        // ── 상태 ──
        private enum SummonCategory { Equipment, Weapon }
        private SummonCategory _currentCategory = SummonCategory.Equipment;

        private static readonly string ACTIVE_TAB_CLASS = "summon__category-tab--active";
        private static readonly string DISABLED_BTN_CLASS = "summon__btn--disabled";

        // ═══════════════════════════════════════
        //  라이프사이클
        // ═══════════════════════════════════════

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 54; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리

#if UNITY_EDITOR
            if (_doc.panelSettings == null)
            {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    "Assets/UI Toolkit/GamePanelSettings.asset");
                if (ps != null) _doc.panelSettings = ps;
            }

            if (_doc.visualTreeAsset == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("SummonTab t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _doc.visualTreeAsset =
                        UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }
#else
            if (_doc.panelSettings == null)
            {
                foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    if (doc != _doc && doc.panelSettings != null)
                    {
                        _doc.panelSettings = doc.panelSettings;
                        break;
                    }
                }
            }
#endif
        }

        private void OnEnable()
        {
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[SummonTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindButtons();

            // 이벤트 구독
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<GachaResultEvent>(OnGachaResult);
            EventBus.Subscribe<SummonLevelUpEvent>(OnSummonLevelUp);

            // 초기 카테고리 선택 + 갱신
            SelectCategory(SummonCategory.Equipment);
            RefreshAll();
            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");

            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<GachaResultEvent>(OnGachaResult);
            EventBus.Unsubscribe<SummonLevelUpEvent>(OnSummonLevelUp);
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,
                this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ═══════════════════════════════════════
        //  요소 캐싱
        // ═══════════════════════════════════════

        private void CacheElements()
        {
            // 헤더 재화
            _currencyRuby = _root.Q<Label>("currency-ruby");
            _currencyHeart = _root.Q<Label>("currency-heart");

            // 일러스트
            _illustImage = _root.Q<VisualElement>("illust-image");
            _illustDesc = _root.Q<Label>("illust-desc");

            // 소환 레벨
            _summonLevelValue = _root.Q<Label>("summon-level-value");
            _summonLevelPulls = _root.Q<Label>("summon-level-pulls");
            _summonLevelFill = _root.Q<VisualElement>("summon-level-fill");

            // 카테고리 탭
            _tabEquipment = _root.Q<Button>("tab-equipment");
            _tabWeapon = _root.Q<Button>("tab-weapon");

            // 하단 버튼
            _btnTicket = _root.Q<Button>("btn-ticket");
            _btnAd = _root.Q<Button>("btn-ad");
            _btnPull1 = _root.Q<Button>("btn-pull1");
            _btnPull10 = _root.Q<Button>("btn-pull10");

            // 획득 정보
            _btnInfo = _root.Q<Button>("btn-info");

            // 소환 결과 오버레이
            _resultOverlay = _root.Q<VisualElement>("result-overlay");
            _resultGrid = _root.Q<VisualElement>("result-grid");
            _resultCloseBtn = _root.Q<Button>("result-close-btn");
        }

        // ═══════════════════════════════════════
        //  버튼 바인딩
        // ═══════════════════════════════════════

        private void BindButtons()
        {
            PanelCloseHelper.BindCloseButton(_root, gameObject);

            // 카테고리 탭
            _tabEquipment?.RegisterCallback<ClickEvent>(_ => SelectCategory(SummonCategory.Equipment));
            _tabWeapon?.RegisterCallback<ClickEvent>(_ => SelectCategory(SummonCategory.Weapon));

            // 소환 버튼
            _btnTicket?.RegisterCallback<ClickEvent>(_ => OnTicketPullClicked());
            _btnAd?.RegisterCallback<ClickEvent>(_ => OnAdPullClicked());
            _btnPull1?.RegisterCallback<ClickEvent>(_ => OnPull1Clicked());
            _btnPull10?.RegisterCallback<ClickEvent>(_ => OnPull10Clicked());

            // 획득 정보
            _btnInfo?.RegisterCallback<ClickEvent>(_ => OnInfoClicked());

            // 결과 닫기
            _resultCloseBtn?.RegisterCallback<ClickEvent>(_ => HideResultOverlay());
        }

        // ═══════════════════════════════════════
        //  카테고리 탭 전환
        // ═══════════════════════════════════════

        private void SelectCategory(SummonCategory category)
        {
            _currentCategory = category;

            // 탭 활성 상태
            SetTabActive(_tabEquipment, category == SummonCategory.Equipment);
            SetTabActive(_tabWeapon, category == SummonCategory.Weapon);

            // 설명 텍스트 + 일러스트 이미지 갱신
            if (_illustDesc != null)
            {
                _illustDesc.text = category switch
                {
                    SummonCategory.Equipment => "장비를 소환하여 캐릭터를 강화하세요",
                    SummonCategory.Weapon => "현재 직업의 무기를 소환할 수 있습니다",
                    _ => ""
                };
            }

            if (_illustImage != null)
            {
                string iconPath = category switch
                {
                    SummonCategory.Equipment => "Icons/Summon/summon_equipment",
                    SummonCategory.Weapon => "Icons/Summon/summon_weapon",
                    _ => "Icons/Summon/summon_equipment"
                };
                var sprite = Resources.Load<Sprite>(iconPath);
                if (sprite != null)
                    _illustImage.style.backgroundImage = new StyleBackground(sprite);
            }

            RefreshSummonLevel();
            RefreshButtons();
        }

        private static void SetTabActive(Button tab, bool isActive)
        {
            if (tab == null) return;
            if (isActive)
                tab.AddToClassList(ACTIVE_TAB_CLASS);
            else
                tab.RemoveFromClassList(ACTIVE_TAB_CLASS);
        }

        // ═══════════════════════════════════════
        //  이벤트 핸들러
        // ═══════════════════════════════════════

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            RefreshCurrencies();
            RefreshButtons();
        }

        private void OnGachaResult(GachaResultEvent evt)
        {
            _pendingResults.Add(evt);

            // 2026-04-23 이슈 2 안전장치: _expectedPullCount=0 (외부 PullAsync 직접 호출 등)
            // 이어도 첫 결과 수신 시 overlay 확실히 표시. 기존 `>= 0` 조건은 묵시적 작동이었으나
            // 의도 명시로 변경. 실 플레이 관찰로 연출 누락 보고 있어 safety net.
            int target = _expectedPullCount > 0 ? _expectedPullCount : 1;
            if (_pendingResults.Count >= target)
            {
                ShowResultOverlay();

                // 2026-04-23 Phase B FeedbackBus 적용: 가챠 결과 Toast + Shake
                // 등급 통계에서 최고 등급 추출 → Toast 메시지에 반영 (한글화)
                string highest = GetHighestGrade(_pendingResults);
                string highestKr = GetGradeDisplayName(highest);
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Gacha,
                    target > 1 ? $"{target}연속 소환! 최고 등급: {highestKr}" : $"소환! {highestKr}",
                    IsRareGrade(highest) ? 2 : 1);

                _pendingResults.Clear();
                _expectedPullCount = 0;
            }

            RefreshSummonLevel();
            RefreshCurrencies();
            RefreshButtons();
        }

        /// <summary>가챠 결과 리스트에서 가장 높은 등급을 찾는다. Mythic > Legendary > Unique > Epic > Rare > Normal.</summary>
        private static readonly string[] _gradeOrder = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic" };
        private static string GetHighestGrade(System.Collections.Generic.List<GachaResultEvent> results)
        {
            int highest = 0;
            foreach (var r in results)
            {
                for (int i = _gradeOrder.Length - 1; i >= 0; i--)
                {
                    if (r.Grade == _gradeOrder[i]) { if (i > highest) highest = i; break; }
                }
            }
            return _gradeOrder[highest];
        }
        private static bool IsRareGrade(string grade) => grade == "Epic" || grade == "Unique" || grade == "Legendary" || grade == "Mythic";

        private void OnSummonLevelUp(SummonLevelUpEvent evt)
        {
            RefreshSummonLevel();
            Debug.Log($"[SummonTabUI] 소환 레벨업! {evt.PoolName} Lv.{evt.NewLevel}");
        }

        // ═══════════════════════════════════════
        //  소환 버튼 액션
        // ═══════════════════════════════════════

        private void OnTicketPullClicked()
        {
            if (GachaManager.Instance == null) return;

            if (_currentCategory == SummonCategory.Weapon)
                OnTicketPullAsync().Forget();
        }

        private async UniTaskVoid OnTicketPullAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            var result = await GachaManager.Instance.PullWeaponAsync(ct);
            if (result == null)
            {
                Debug.Log("[SummonTabUI] 무기소환권 부족");
                return;
            }
            Debug.Log($"[SummonTabUI] 티켓 소환 결과: {result.itemId} ({result.grade})");
            RefreshAll();
        }

        private void OnAdPullClicked()
        {
            // 광고 소환은 추후 구현 (광고 시스템 연동 필요)
            Debug.Log("[SummonTabUI] 광고 소환은 추후 구현됩니다.");
        }

        // 2026-04-23 가챠 더블-풀 방지 플래그 (P1 — 루비 이중 소모 버그 차단)
        private bool _isPulling;

        private void OnPull1Clicked()
        {
            if (GachaManager.Instance == null) return;
            if (_isPulling) return;
            _expectedPullCount = 1;
            _pendingResults.Clear();
            OnPull1Async().Forget();
        }

        private async UniTaskVoid OnPull1Async()
        {
            _isPulling = true;
            SetPullButtonsEnabled(false);
            try
            {
                var ct = this.GetCancellationTokenOnDestroy();
                GachaPoolType poolType = GetCurrentPoolType();
                var result = await GachaManager.Instance.PullAsync(poolType, ct);
                if (this == null) return;
                if (result == null)
                {
                    Debug.Log("[SummonTabUI] 루비 부족 (1회 소환)");
                    return;
                }
                Debug.Log($"[SummonTabUI] 1회 소환 결과: {result.itemId} ({result.grade})");
                RefreshAll();
            }
            finally
            {
                if (this != null)
                {
                    _isPulling = false;
                    SetPullButtonsEnabled(true);
                }
            }
        }

        private void OnPull10Clicked()
        {
            if (GachaManager.Instance == null) return;
            if (_isPulling) return;
            _expectedPullCount = 10;
            _pendingResults.Clear();
            OnPull10Async().Forget();
        }

        private async UniTaskVoid OnPull10Async()
        {
            _isPulling = true;
            SetPullButtonsEnabled(false);
            try
            {
                var ct = this.GetCancellationTokenOnDestroy();
                GachaPoolType poolType = GetCurrentPoolType();
                var results = await GachaManager.Instance.PullTenAsync(poolType, ct);
                if (this == null) return;
                if (results == null)
                {
                    Debug.Log("[SummonTabUI] 루비 부족 (10연차)");
                    return;
                }
                Debug.Log($"[SummonTabUI] 10연차 소환 완료 — {results.Count}건");
                RefreshAll();
            }
            finally
            {
                if (this != null)
                {
                    _isPulling = false;
                    SetPullButtonsEnabled(true);
                }
            }
        }

        private void SetPullButtonsEnabled(bool enabled)
        {
            if (_btnPull1 != null) _btnPull1.SetEnabled(enabled);
            if (_btnPull10 != null) _btnPull10.SetEnabled(enabled);
        }

        private void OnInfoClicked()
        {
            // 확률표 팝업 (추후 구현)
            Debug.Log("[SummonTabUI] 획득 정보 팝업은 추후 구현됩니다.");
        }

        // ═══════════════════════════════════════
        //  갱신
        // ═══════════════════════════════════════

        private void RefreshAll()
        {
            RefreshCurrencies();
            RefreshSummonLevel();
            RefreshButtons();
        }

        /// <summary>헤더 재화 표시 갱신.</summary>
        private void RefreshCurrencies()
        {
            if (CurrencyManager.Instance == null) return;

            BigNumber ruby = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);
            BigNumber ticket = CurrencyManager.Instance.GetAmount(CurrencyType.WeaponTicket);

            if (_currencyRuby != null)
                _currencyRuby.text = $"\U0001F48E{NumberFormatter.FormatKorean(ruby)}";

            if (_currencyHeart != null)
                _currencyHeart.text = $"\u2764{NumberFormatter.FormatKorean(ticket)}";
        }

        /// <summary>소환 레벨 프로그레스바 갱신.</summary>
        private void RefreshSummonLevel()
        {
            if (GachaManager.Instance == null) return;

            GachaPoolType poolType = GetCurrentPoolType();
            int level = GachaManager.Instance.GetSummonLevel(poolType);
            int maxLevel = GachaManager.Instance.GetMaxSummonLevel(poolType);
            int totalPulls = GachaManager.Instance.GetTotalPulls(poolType);
            int pullsToNext = GachaManager.Instance.GetPullsToNextLevel(poolType);

            // 레벨 표시
            if (_summonLevelValue != null)
                _summonLevelValue.text = $"Lv.{level}";

            // 풀 카운트 표시
            if (_summonLevelPulls != null)
            {
                if (pullsToNext < 0)
                {
                    // 최대 레벨
                    _summonLevelPulls.text = "MAX";
                }
                else
                {
                    // 현재 레벨에서의 진행도 계산
                    var pool = GachaManager.Instance.GetPool(poolType);
                    if (pool != null && pool.summonLevels != null && level < pool.summonLevels.Length)
                    {
                        int nextRequired = pool.summonLevels[level].requiredPulls;
                        int currentRequired = level > 1 ? pool.summonLevels[level - 1].requiredPulls : 0;
                        int progressCurrent = totalPulls - currentRequired;
                        int progressMax = nextRequired - currentRequired;
                        _summonLevelPulls.text = $"{progressCurrent}/{progressMax}";
                    }
                    else
                    {
                        _summonLevelPulls.text = $"{totalPulls}";
                    }
                }
            }

            // 프로그레스바 fill
            if (_summonLevelFill != null)
            {
                float fillPercent = 0f;
                if (pullsToNext < 0)
                {
                    fillPercent = 100f;
                }
                else
                {
                    var pool = GachaManager.Instance.GetPool(poolType);
                    if (pool != null && pool.summonLevels != null && level < pool.summonLevels.Length)
                    {
                        int nextRequired = pool.summonLevels[level].requiredPulls;
                        int currentRequired = level > 1 ? pool.summonLevels[level - 1].requiredPulls : 0;
                        int progressMax = nextRequired - currentRequired;
                        int progressCurrent = totalPulls - currentRequired;
                        fillPercent = progressMax > 0 ? (float)progressCurrent / progressMax * 100f : 0f;
                    }
                }
                _summonLevelFill.style.width = new Length(Mathf.Clamp(fillPercent, 0f, 100f), LengthUnit.Percent);
            }
        }

        /// <summary>하단 버튼 상태 갱신 (비용 표시 + 활성/비활성).</summary>
        private void RefreshButtons()
        {
            if (GachaManager.Instance == null || CurrencyManager.Instance == null) return;

            BigNumber ruby = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);
            BigNumber ticket = CurrencyManager.Instance.GetAmount(CurrencyType.WeaponTicket);

            int singleCost = GachaManager.Instance.SinglePullCost;
            int tenCost = GachaManager.Instance.TenPullCost;

            // 티켓 버튼
            if (_btnTicket != null)
            {
                bool hasTicket = ticket >= 1 && _currentCategory == SummonCategory.Weapon;
                _btnTicket.text = $"티켓\n소환권 {NumberFormatter.FormatKorean(ticket)}";
                SetButtonEnabled(_btnTicket, hasTicket, "summon__btn--ticket");
            }

            // 광고 버튼 (항상 비활성 — 추후 구현)
            if (_btnAd != null)
            {
                _btnAd.text = "광고\n무료";
                SetButtonEnabled(_btnAd, false, "summon__btn--ad");
            }

            // 1회 소환 버튼
            if (_btnPull1 != null)
            {
                bool canPull1 = ruby >= singleCost;
                _btnPull1.text = $"1회\n\u2764{NumberFormatter.FormatKorean(singleCost)}";
                SetButtonEnabled(_btnPull1, canPull1, "summon__btn--pull1");
            }

            // 일괄 소환 (10연차) 버튼
            if (_btnPull10 != null)
            {
                bool canPull10 = ruby >= tenCost;
                _btnPull10.text = $"일괄\n\u2764{NumberFormatter.FormatKorean(tenCost)}";
                SetButtonEnabled(_btnPull10, canPull10, "summon__btn--pull10");
            }
        }

        // ═══════════════════════════════════════
        //  유틸
        // ═══════════════════════════════════════

        /// <summary>현재 선택된 카테고리에 해당하는 GachaPoolType 반환.</summary>
        private GachaPoolType GetCurrentPoolType()
        {
            return _currentCategory switch
            {
                SummonCategory.Equipment => GachaPoolType.Equipment,
                SummonCategory.Weapon => GachaPoolType.Weapon,
                _ => GachaPoolType.Equipment
            };
        }

        /// <summary>버튼 활성/비활성 스타일 전환.</summary>
        private static void SetButtonEnabled(Button btn, bool isEnabled, string activeClass)
        {
            if (btn == null) return;

            btn.SetEnabled(isEnabled);
            if (isEnabled)
            {
                btn.RemoveFromClassList(DISABLED_BTN_CLASS);
                btn.AddToClassList(activeClass);
            }
            else
            {
                btn.RemoveFromClassList(activeClass);
                btn.AddToClassList(DISABLED_BTN_CLASS);
            }
        }

        // ═══════════════════════════════════════
        //  소환 결과 오버레이
        // ═══════════════════════════════════════

        private void ShowResultOverlay()
        {
            if (_resultOverlay == null || _resultGrid == null) return;

            _resultGrid.Clear();

            foreach (var evt in _pendingResults)
            {
                var card = new VisualElement();
                card.AddToClassList("summon__result-card");

                // 등급 border 색상
                Color gradeColor = GetGradeColor(evt.Grade);
                card.style.borderTopColor = gradeColor;
                card.style.borderBottomColor = gradeColor;
                card.style.borderLeftColor = gradeColor;
                card.style.borderRightColor = gradeColor;

                var nameLabel = new Label(ResolveItemDisplayName(evt.ItemId, evt.PoolName));
                nameLabel.AddToClassList("summon__result-card__name");

                var gradeLabel = new Label(GetGradeDisplayName(evt.Grade));
                gradeLabel.AddToClassList("summon__result-card__count");
                gradeLabel.style.color = gradeColor;

                card.Add(nameLabel);
                card.Add(gradeLabel);
                _resultGrid.Add(card);
            }

            _resultOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideResultOverlay()
        {
            if (_resultOverlay != null)
                _resultOverlay.style.display = DisplayStyle.None;
        }

        private static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "Normal" => new Color(0.6f, 0.6f, 0.6f),
                "Rare" => new Color(0.27f, 0.53f, 1f),
                "Epic" => new Color(0.67f, 0.27f, 1f),
                "Unique" => new Color(1f, 0.84f, 0f),
                "Legendary" => new Color(1f, 0.4f, 0f),
                "Mythic" => new Color(1f, 0.2f, 0.27f),
                _ => Color.white
            };
        }

        private static string GetGradeDisplayName(string grade) => grade switch
        {
            "Normal" => "일반",
            "Rare" => "희귀",
            "Epic" => "에픽",
            "Unique" => "유니크",
            "Legendary" => "전설",
            "Mythic" => "신화",
            _ => grade
        };

        /// <summary>
        /// 가챠 결과 ItemId를 SO displayName으로 해석. Pool 종류에 따라 Weapon/Equipment SO 조회.
        /// </summary>
        private static string ResolveItemDisplayName(string itemId, string poolName)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;

            // 무기 풀은 itemId가 "weapon_"로 시작
            if (itemId.StartsWith("weapon_"))
            {
                var wm = WeaponManager.Instance;
                if (wm != null)
                {
                    var data = wm.GetData(itemId);
                    if (data != null && !string.IsNullOrEmpty(data.displayName))
                        return data.displayName;
                }
            }

            // 장비 풀 (top_, bottom_, shoes_, weapon_ 외 전부)
            var em = EquipmentManager.Instance;
            if (em != null)
            {
                var data = em.GetData(itemId);
                if (data != null && !string.IsNullOrEmpty(data.displayName))
                    return data.displayName;
            }

            return itemId; // fallback
        }
    }
}
