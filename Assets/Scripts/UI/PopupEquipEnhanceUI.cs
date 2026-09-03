using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;
using MkLike.Data;
using MkLike.Equipment;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 장비 슬롯 강화 팝업 (주문서 / 스타포스 / 잠재옵션 3탭).
    /// 좌측: 장착 슬롯 목록, 우측: 선택된 탭의 강화 UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PopupEquipEnhanceUI : MonoBehaviour
    {
        public static PopupEquipEnhanceUI Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;

        // ── 캐시 ──
        private VisualElement _popupDim;
        private VisualElement _slotList;
        private Label _emptyHint;
        private Button _closeBtn;

        // 서브탭
        private Button _tabScroll;
        private Button _tabStarforce;
        private Button _tabPotential;
        private VisualElement _scrollTab;
        private VisualElement _starforceTab;
        private VisualElement _potentialTab;

        // 주문서 탭
        private VisualElement _scrollBar;
        private Label _scrollProgress;
        private Label _scrollRate;
        private Label _scrollCost;
        private Label _scrollBonus;
        private Button _scrollExecuteBtn;
        private Label _scrollResult;

        // 스타포스 탭
        private Label _starDisplay;
        private Label _starRate;
        private Label _starCost;
        private Button _starExecuteBtn;
        private Label _starResult;

        // 잠재옵션 탭
        private Label _potentialGrade;
        private Label[] _potentialOpts = new Label[3];
        private Label _potentialCostLabel;
        private Label _potentialPity;
        private Button _potentialExecuteBtn;
        private Label _potentialResult;

        // ── 상태 ──
        private int _activeTab; // 0=주문서, 1=스타포스, 2=잠재
        private EquipmentSlot _selectedSlot;
        private string _selectedInstanceId;
        private bool _hasSelection;

        // ── 강화 전 스냅샷 (Before/After 비교용) ──
        private int _preScrollLevel;
        private int _preStarForce;

        // ── 등급 색상 ──
        private static readonly System.Collections.Generic.Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.6f, 0.6f, 0.6f) },
            { "Rare", new Color(0.27f, 0.53f, 1f) },
            { "Epic", new Color(0.67f, 0.27f, 1f) },
            { "Unique", new Color(1f, 0.84f, 0f) },
            { "Legendary", new Color(1f, 0.4f, 0f) },
            { "Mythic", new Color(1f, 0.2f, 0.27f) },
        };

        private static readonly System.Collections.Generic.Dictionary<PotentialGrade, Color> PotentialGradeColors = new()
        {
            { PotentialGrade.None, new Color(0.6f, 0.6f, 0.6f) },
            { PotentialGrade.Rare, new Color(0.27f, 0.53f, 1f) },
            { PotentialGrade.Epic, new Color(0.67f, 0.27f, 1f) },
            { PotentialGrade.Unique, new Color(1f, 0.84f, 0f) },
            { PotentialGrade.Legendary, new Color(1f, 0.4f, 0f) },
            { PotentialGrade.Mythic, new Color(1f, 0.2f, 0.27f) },
        };

        // ═══════════════════════════════════
        //  라이프사이클
        // ═══════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 210; // 팝업이므로 높은 order

#if UNITY_EDITOR
            if (_doc.panelSettings == null)
            {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    "Assets/UI Toolkit/GamePanelSettings.asset");
                if (ps != null) _doc.panelSettings = ps;
            }

            if (_doc.visualTreeAsset == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("PopupEquipEnhance t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _doc.visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
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
                Debug.LogWarning("[PopupEquipEnhanceUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            CacheElements();
            BindButtons();

            EventBus.Subscribe<ScrollEnhanceEvent>(OnScrollEnhanceResult);
            EventBus.Subscribe<StarForceEvent>(OnStarForceResult);
            EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);

            Hide();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScrollEnhanceEvent>(OnScrollEnhanceResult);
            EventBus.Unsubscribe<StarForceEvent>(OnStarForceResult);
            EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ═══════════════════════════════════
        //  요소 캐싱
        // ═══════════════════════════════════

        private void CacheElements()
        {
            _popupDim = _root.Q<VisualElement>("popup-dim");
            _slotList = _root.Q<VisualElement>("slot-list");
            _emptyHint = _root.Q<Label>("empty-hint");
            _closeBtn = _root.Q<Button>("close-btn");

            // 서브탭
            _tabScroll = _root.Q<Button>("tab-scroll");
            _tabStarforce = _root.Q<Button>("tab-starforce");
            _tabPotential = _root.Q<Button>("tab-potential");
            _scrollTab = _root.Q<VisualElement>("scroll-tab");
            _starforceTab = _root.Q<VisualElement>("starforce-tab");
            _potentialTab = _root.Q<VisualElement>("potential-tab");

            // 주문서
            _scrollBar = _root.Q<VisualElement>("scroll-bar");
            _scrollProgress = _root.Q<Label>("scroll-progress");
            _scrollRate = _root.Q<Label>("scroll-rate");
            _scrollCost = _root.Q<Label>("scroll-cost");
            _scrollBonus = _root.Q<Label>("scroll-bonus");
            _scrollExecuteBtn = _root.Q<Button>("scroll-execute-btn");
            _scrollResult = _root.Q<Label>("scroll-result");

            // 스타포스
            _starDisplay = _root.Q<Label>("star-display");
            _starRate = _root.Q<Label>("star-rate");
            _starCost = _root.Q<Label>("star-cost");
            _starExecuteBtn = _root.Q<Button>("star-execute-btn");
            _starResult = _root.Q<Label>("star-result");

            // 잠재옵션
            _potentialGrade = _root.Q<Label>("potential-grade");
            _potentialOpts[0] = _root.Q<Label>("potential-opt-0");
            _potentialOpts[1] = _root.Q<Label>("potential-opt-1");
            _potentialOpts[2] = _root.Q<Label>("potential-opt-2");
            _potentialCostLabel = _root.Q<Label>("potential-cost");
            _potentialPity = _root.Q<Label>("potential-pity");
            _potentialExecuteBtn = _root.Q<Button>("potential-execute-btn");
            _potentialResult = _root.Q<Label>("potential-result");
        }

        private void BindButtons()
        {
            _closeBtn?.RegisterCallback<ClickEvent>(_ => Hide());
            _popupDim?.RegisterCallback<ClickEvent>(OnDimClicked);

            _tabScroll?.RegisterCallback<ClickEvent>(_ => SwitchEnhanceTab(0));
            _tabStarforce?.RegisterCallback<ClickEvent>(_ => SwitchEnhanceTab(1));
            _tabPotential?.RegisterCallback<ClickEvent>(_ => SwitchEnhanceTab(2));

            _scrollExecuteBtn?.RegisterCallback<ClickEvent>(_ => OnScrollExecute());
            _starExecuteBtn?.RegisterCallback<ClickEvent>(_ => OnStarForceExecute());
            _potentialExecuteBtn?.RegisterCallback<ClickEvent>(_ => OnPotentialExecute());
        }

        private void OnDimClicked(ClickEvent evt)
        {
            if (evt.target == _popupDim)
                Hide();
        }

        // ═══════════════════════════════════
        //  공개 API
        // ═══════════════════════════════════

        /// <summary>팝업을 연다. 장착된 슬롯 목록을 빌드한다.</summary>
        public void Show()
        {
            _hasSelection = false;
            _selectedInstanceId = null;

            if (_popupDim != null)
                _popupDim.style.display = DisplayStyle.Flex;

            BuildSlotList();
            ShowEmptyHint(true);
            SwitchEnhanceTab(0);
            ClearAllResults();

            Debug.Log("[PopupEquipEnhanceUI] Show");
        }

        /// <summary>특정 슬롯을 미리 선택한 상태로 팝업을 연다.</summary>
        public void Show(EquipmentSlot preselectedSlot)
        {
            Show();
            var equipped = EquipmentManager.Instance?.GetEquipped(preselectedSlot);
            if (equipped != null)
            {
                SelectSlot(preselectedSlot, equipped.instanceId);
            }
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            if (_popupDim != null)
                _popupDim.style.display = DisplayStyle.None;

            _hasSelection = false;
            _selectedInstanceId = null;
        }

        /// <summary>팝업 표시 중인지 여부.</summary>
        public bool IsVisible => _popupDim != null
            && _popupDim.resolvedStyle.display == DisplayStyle.Flex;

        // ═══════════════════════════════════
        //  좌측: 장착 슬롯 목록 빌드
        // ═══════════════════════════════════

        private void BuildSlotList()
        {
            if (_slotList == null) return;
            _slotList.Clear();

            var slots = System.Enum.GetValues(typeof(EquipmentSlot));
            foreach (EquipmentSlot slot in slots)
            {
                var card = CreateSlotCard(slot);
                _slotList.Add(card);
            }
        }

        private VisualElement CreateSlotCard(EquipmentSlot slot)
        {
            var equipped = EquipmentManager.Instance?.GetEquipped(slot);
            bool isEmpty = equipped == null;
            EquipmentDataSO data = null;
            if (!isEmpty && EquipmentManager.Instance != null)
                data = EquipmentManager.Instance.GetData(equipped.equipmentId);

            // 카드 컨테이너
            var card = new VisualElement();
            card.AddToClassList("enhance-slot-card");
            if (isEmpty)
                card.AddToClassList("enhance-slot-card--empty");
            if (_hasSelection && !isEmpty && equipped.instanceId == _selectedInstanceId)
                card.AddToClassList("enhance-slot-card--selected");

            EquipmentSlot capturedSlot = slot;
            string capturedId = isEmpty ? null : equipped.instanceId;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (!isEmpty)
                    SelectSlot(capturedSlot, capturedId);
            });

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("enhance-slot-card__icon");
            if (data != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);
            card.Add(icon);

            // 정보 영역
            var info = new VisualElement();
            info.AddToClassList("enhance-slot-card__info");

            // 이름
            string displayName = isEmpty ? GetSlotDisplayName(slot) : (data != null ? data.displayName : equipped.equipmentId);
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("enhance-slot-card__name");
            info.Add(nameLabel);

            if (!isEmpty)
            {
                // 등급
                Color gradeColor = GetGradeColor(equipped.grade);
                var gradeLabel = new Label(equipped.grade);
                gradeLabel.AddToClassList("enhance-slot-card__grade");
                gradeLabel.style.color = gradeColor;
                info.Add(gradeLabel);

                // 각성 별 + 스타포스 서브 라인
                var slotEnhData = EquipmentManager.Instance?.GetSlotEnhancement(slot);
                var sb = new StringBuilder();
                if (equipped.awakeningStars > 0)
                    sb.Append(BuildAwakenStars(equipped.awakeningStars));
                if (slotEnhData != null && slotEnhData.starForce > 0)
                    sb.Append($" \u2605{slotEnhData.starForce}");

                if (sb.Length > 0)
                {
                    var starsLabel = new Label(sb.ToString());
                    starsLabel.AddToClassList("enhance-slot-card__stars");
                    info.Add(starsLabel);
                }

                // "E" 뱃지 (항상 장착 중)
                var badge = new VisualElement();
                badge.AddToClassList("enhance-slot-card__badge");
                var badgeLabel = new Label("E");
                badge.Add(badgeLabel);
                card.Add(badge);
            }
            else
            {
                var subLabel = new Label("(빈 슬롯)");
                subLabel.AddToClassList("enhance-slot-card__sub");
                info.Add(subLabel);
            }

            card.Add(info);
            return card;
        }

        // ═══════════════════════════════════
        //  슬롯 선택
        // ═══════════════════════════════════

        private void SelectSlot(EquipmentSlot slot, string instanceId)
        {
            _selectedSlot = slot;
            _selectedInstanceId = instanceId;
            _hasSelection = true;

            // 슬롯 목록 하이라이트 갱신
            BuildSlotList();
            ShowEmptyHint(false);
            ClearAllResults();
            RefreshCurrentTab();

            Debug.Log($"[PopupEquipEnhanceUI] SelectSlot: {slot} ({instanceId})");
        }

        // ═══════════════════════════════════
        //  서브탭 전환
        // ═══════════════════════════════════

        /// <summary>서브탭 전환. 0=주문서, 1=스타포스, 2=잠재.</summary>
        public void SwitchEnhanceTab(int tabIndex)
        {
            _activeTab = tabIndex;

            // 탭 활성 상태
            SetSubtabActive(_tabScroll, tabIndex == 0);
            SetSubtabActive(_tabStarforce, tabIndex == 1);
            SetSubtabActive(_tabPotential, tabIndex == 2);

            // 콘텐츠 표시
            SetDisplay(_scrollTab, tabIndex == 0);
            SetDisplay(_starforceTab, tabIndex == 1);
            SetDisplay(_potentialTab, tabIndex == 2);

            ClearAllResults();
            RefreshCurrentTab();
        }

        private static void SetSubtabActive(Button btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive)
                btn.AddToClassList("enhance-subtab--active");
            else
                btn.RemoveFromClassList("enhance-subtab--active");
        }

        private static void SetDisplay(VisualElement el, bool isVisible)
        {
            if (el == null) return;
            el.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ═══════════════════════════════════
        //  탭 콘텐츠 갱신
        // ═══════════════════════════════════

        private void RefreshCurrentTab()
        {
            if (!_hasSelection) return;
            if (EquipmentManager.Instance == null) return;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);

            switch (_activeTab)
            {
                case 0: RefreshScrollTab(slotEnh); break;
                case 1: RefreshStarForceTab(slotEnh); break;
                case 2: RefreshPotentialTab(slotEnh); break;
            }
        }

        // ── 주문서 탭 ──
        private void RefreshScrollTab(SlotEnhancementData slotEnh)
        {
            // 슬롯 진행 바 (10칸)
            if (_scrollBar != null)
            {
                _scrollBar.Clear();
                for (int i = 0; i < 10; i++)
                {
                    var slot = new VisualElement();
                    slot.AddToClassList("enhance-scroll-slot");
                    slot.AddToClassList(i < slotEnh.scrollLevel
                        ? "enhance-scroll-slot--filled"
                        : "enhance-scroll-slot--empty");
                    _scrollBar.Add(slot);
                }
            }

            bool isMaxed = slotEnh.scrollLevel >= 10;
            float rate = EquipmentManager.GetScrollSuccessRate(slotEnh.scrollLevel);
            int nextBonus = EquipmentDataSO.ScrollBonusAtk(slotEnh.scrollLevel + 1)
                          - EquipmentDataSO.ScrollBonusAtk(slotEnh.scrollLevel);

            if (_scrollProgress != null)
                _scrollProgress.text = $"성공 {slotEnh.scrollLevel} / 전체 10";

            if (_scrollRate != null)
                _scrollRate.text = isMaxed ? "최대 강화 달성!" : $"성공 확률: {Mathf.RoundToInt(rate * 100f)}%";

            if (_scrollCost != null)
                _scrollCost.text = isMaxed ? "" : $"비용: {NumberFormatter.FormatKorean(5000)} 골드";

            if (_scrollBonus != null)
                _scrollBonus.text = isMaxed ? "" : $"예상 보너스: ATK +{nextBonus}";

            if (_scrollExecuteBtn != null)
                _scrollExecuteBtn.SetEnabled(!isMaxed);
        }

        // ── 스타포스 탭 ──
        private void RefreshStarForceTab(SlotEnhancementData slotEnh)
        {
            bool isMaxed = slotEnh.starForce >= 25;

            if (_starDisplay != null)
                _starDisplay.text = BuildStarForceDisplay(slotEnh.starForce, 25);

            float rate = EquipmentManager.GetStarForceSuccessRate(slotEnh.starForce);
            long cost = EquipmentManager.GetStarForceCost(slotEnh.starForce);
            float downgradeRate = EquipmentManager.GetStarForceDowngradeRate(slotEnh.starForce);
            float destroyRate = EquipmentManager.GetStarForceDestroyRate(slotEnh.starForce);

            if (_starRate != null)
            {
                if (isMaxed)
                {
                    _starRate.text = "최대 강화 달성!";
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"성공: {Mathf.RoundToInt(rate * 100f)}%");
                    if (downgradeRate > 0f) sb.Append($"  하락: {Mathf.RoundToInt(downgradeRate * 100f)}%");
                    if (destroyRate > 0f) sb.Append($"  <color=#FF4444>파괴: {Mathf.RoundToInt(destroyRate * 100f)}%</color>");
                    _starRate.text = sb.ToString();
                }
            }

            if (_starCost != null)
                _starCost.text = isMaxed ? "" : $"비용: {NumberFormatter.FormatKorean(cost)} 골드";

            if (_starExecuteBtn != null)
                _starExecuteBtn.SetEnabled(!isMaxed);
        }

        // ── 잠재옵션 탭 ──
        private void RefreshPotentialTab(SlotEnhancementData slotEnh)
        {
            PotentialGrade grade = slotEnh.potentialGrade;
            Color gradeColor = GetPotentialGradeColor(grade);

            if (_potentialGrade != null)
            {
                _potentialGrade.text = $"등급: {GetPotentialGradeName(grade)}";
                _potentialGrade.style.color = gradeColor;
            }

            // 옵션 3줄
            for (int i = 0; i < 3; i++)
            {
                if (_potentialOpts[i] == null) continue;
                if (slotEnh.potentialOptions != null && i < slotEnh.potentialOptions.Count)
                {
                    _potentialOpts[i].text = slotEnh.potentialOptions[i];
                    _potentialOpts[i].style.display = DisplayStyle.Flex;
                }
                else
                {
                    _potentialOpts[i].text = "";
                    _potentialOpts[i].style.display = grade == PotentialGrade.None
                        ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }

            // 비용 표시
            if (PotentialSystem.Instance != null)
            {
                long goldCost = PotentialSystem.Instance.GetPotentialChangeCost(slotEnh);
                if (_potentialCostLabel != null)
                    _potentialCostLabel.text = $"비용: {NumberFormatter.FormatKorean(goldCost)} 골드 + 잠재석 1개";

                int pityCount = PotentialSystem.Instance.GetPityCount(_selectedSlot);
                int pityCeiling = PotentialSystem.Instance.GetPityCeiling(grade);
                if (_potentialPity != null)
                    _potentialPity.text = $"천장: {pityCount} / {pityCeiling}";
            }

            bool canChange = grade < PotentialGrade.Mythic;
            if (_potentialExecuteBtn != null)
                _potentialExecuteBtn.SetEnabled(canChange);
        }

        // ═══════════════════════════════════
        //  실행 버튼 핸들러
        // ═══════════════════════════════════

        private void OnScrollExecute()
        {
            if (EquipmentManager.Instance == null || !_hasSelection)
                return;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
            if (slotEnh.scrollLevel >= 10) return;

            // Before 스냅샷
            _preScrollLevel = slotEnh.scrollLevel;

            // 서버/로컬 자동 분기 경로로 위임 — 결과는 ScrollEnhanceEvent로 수신
            DoScrollEnhanceAsync().Forget();
        }

        private async UniTaskVoid DoScrollEnhanceAsync()
        {
            if (_scrollExecuteBtn != null) _scrollExecuteBtn.SetEnabled(false);
            try
            {
                var ct = this.GetCancellationTokenOnDestroy();
                await EquipmentManager.Instance.TryScrollEnhanceAsync(_selectedSlot, ct);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PopupEquipEnhanceUI] 주문서 강화 실패: {ex.Message}");
            }
            finally
            {
                if (_scrollExecuteBtn != null) _scrollExecuteBtn.SetEnabled(true);
            }
        }

        private void OnStarForceExecute()
        {
            if (EquipmentManager.Instance == null || !_hasSelection)
                return;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
            if (slotEnh.starForce >= 25) return;

            // Before 스냅샷
            _preStarForce = slotEnh.starForce;

            DoStarForceEnhanceAsync().Forget();
        }

        private async UniTaskVoid DoStarForceEnhanceAsync()
        {
            if (_starExecuteBtn != null) _starExecuteBtn.SetEnabled(false);
            try
            {
                var ct = this.GetCancellationTokenOnDestroy();
                await EquipmentManager.Instance.TryStarForceEnhanceAsync(_selectedSlot, ct);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PopupEquipEnhanceUI] 스타포스 강화 실패: {ex.Message}");
            }
            finally
            {
                if (_starExecuteBtn != null) _starExecuteBtn.SetEnabled(true);
            }
        }

        private void OnPotentialExecute()
        {
            if (PotentialSystem.Instance == null || !_hasSelection)
                return;
            if (EquipmentManager.Instance == null) return;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
            if (slotEnh.potentialGrade >= PotentialGrade.Mythic) return;

            PotentialChangeResult result = PotentialSystem.Instance.ChangePotential(_selectedSlot);

            // 결과 표시
            if (_potentialResult != null)
            {
                _potentialResult.RemoveFromClassList("enhance-result--success");
                _potentialResult.RemoveFromClassList("enhance-result--fail");
                _potentialResult.RemoveFromClassList("enhance-result--upgrade");
                _potentialResult.RemoveFromClassList("enhance-result--reroll");

                switch (result)
                {
                    case PotentialChangeResult.Upgraded:
                        _potentialResult.text = "잠재능력 등급 승급!";
                        _potentialResult.AddToClassList("enhance-result--upgrade");
                        break;
                    case PotentialChangeResult.Rerolled:
                        _potentialResult.text = "옵션이 재설정되었습니다";
                        _potentialResult.AddToClassList("enhance-result--reroll");
                        break;
                    case PotentialChangeResult.Failed:
                        _potentialResult.text = "변화 없음...";
                        _potentialResult.AddToClassList("enhance-result--fail");
                        break;
                }
            }

            // 갱신된 슬롯 강화 데이터로 탭 리프레시
            slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
            RefreshPotentialTab(slotEnh);
            BuildSlotList(); // 좌측 목록 갱신
        }

        // ═══════════════════════════════════
        //  이벤트 핸들러
        // ═══════════════════════════════════

        private void OnScrollEnhanceResult(ScrollEnhanceEvent evt)
        {
            if (!IsVisible || !_hasSelection) return;
            if (evt.InstanceId != _selectedSlot.ToString()) return;

            if (_scrollResult != null)
            {
                _scrollResult.RemoveFromClassList("enhance-result--success");
                _scrollResult.RemoveFromClassList("enhance-result--fail");

                int beforePercent = _preScrollLevel * 5;
                int afterPercent = evt.NewScrollLevel * 5;

                if (evt.IsSuccess)
                {
                    _scrollResult.text = $"주문서 강화 성공! ({evt.NewScrollLevel}/10)\n보너스 +{beforePercent}% → +{afterPercent}%";
                    _scrollResult.AddToClassList("enhance-result--success");
                }
                else
                {
                    _scrollResult.text = $"실패... ({evt.NewScrollLevel}/10)";
                    _scrollResult.AddToClassList("enhance-result--fail");
                }
            }

            if (EquipmentManager.Instance != null)
            {
                var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
                RefreshScrollTab(slotEnh);
            }

            BuildSlotList();
        }

        private void OnStarForceResult(StarForceEvent evt)
        {
            if (!IsVisible || !_hasSelection) return;
            if (evt.InstanceId != _selectedSlot.ToString()) return;

            if (_starResult != null)
            {
                _starResult.RemoveFromClassList("enhance-result--success");
                _starResult.RemoveFromClassList("enhance-result--fail");
                _starResult.RemoveFromClassList("enhance-result--destroy");
                _starResult.RemoveFromClassList("enhance-result--downgrade");

                int beforePercent = _preStarForce * 3;
                int afterPercent = evt.NewStarForce * 3;

                switch (evt.Result)
                {
                    case StarForceResult.Success:
                        _starResult.text = $"스타포스 성공! ({evt.NewStarForce}\u2605)\n보너스 +{beforePercent}% → +{afterPercent}%";
                        _starResult.AddToClassList("enhance-result--success");
                        break;
                    case StarForceResult.Destroy:
                        _starResult.text = $"파괴!! 12\u2605로 리셋됨\n보너스 +{beforePercent}% → +{afterPercent}%";
                        _starResult.AddToClassList("enhance-result--destroy");
                        break;
                    case StarForceResult.Downgrade:
                        _starResult.text = $"하락... ({evt.NewStarForce}\u2605)\n보너스 +{beforePercent}% → +{afterPercent}%";
                        _starResult.AddToClassList("enhance-result--downgrade");
                        break;
                    default:
                        _starResult.text = $"실패... ({evt.NewStarForce}\u2605)";
                        _starResult.AddToClassList("enhance-result--fail");
                        break;
                }
            }

            if (EquipmentManager.Instance != null)
            {
                var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(_selectedSlot);
                RefreshStarForceTab(slotEnh);
            }

            BuildSlotList();
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (!IsVisible) return;
            BuildSlotList();
            RefreshCurrentTab();
        }

        // ═══════════════════════════════════
        //  유틸
        // ═══════════════════════════════════

        private void ShowEmptyHint(bool isVisible)
        {
            SetDisplay(_emptyHint, isVisible);
            SetDisplay(_scrollTab, !isVisible && _activeTab == 0);
            SetDisplay(_starforceTab, !isVisible && _activeTab == 1);
            SetDisplay(_potentialTab, !isVisible && _activeTab == 2);
        }

        private void ClearAllResults()
        {
            if (_scrollResult != null) _scrollResult.text = "";
            if (_starResult != null) _starResult.text = "";
            if (_potentialResult != null) _potentialResult.text = "";
        }

        private static Color GetGradeColor(string grade)
        {
            return GradeColors.TryGetValue(grade, out var color) ? color : Color.white;
        }

        private static Color GetPotentialGradeColor(PotentialGrade grade)
        {
            return PotentialGradeColors.TryGetValue(grade, out var color) ? color : Color.white;
        }

        private static string GetPotentialGradeName(PotentialGrade grade)
        {
            return grade switch
            {
                PotentialGrade.None => "없음",
                PotentialGrade.Rare => "레어",
                PotentialGrade.Epic => "에픽",
                PotentialGrade.Unique => "유니크",
                PotentialGrade.Legendary => "레전더리",
                PotentialGrade.Mythic => "미식",
                _ => "없음"
            };
        }

        private static string BuildAwakenStars(int stars)
        {
            var sb = new StringBuilder(EquipmentInstance.MAX_AWAKENING);
            for (int i = 0; i < EquipmentInstance.MAX_AWAKENING; i++)
            {
                sb.Append(i < stars ? '\u2605' : '\u2606'); // filled : empty
            }
            return sb.ToString();
        }

        private static string BuildStarForceDisplay(int current, int max)
        {
            var sb = new StringBuilder(max + 4);
            // 5개씩 그룹으로 표시
            for (int i = 0; i < max; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append(' ');
                sb.Append(i < current ? '\u2605' : '\u2606');
            }
            return sb.ToString();
        }

        private static string GetSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "무기",
                EquipmentSlot.Helmet => "투구",
                EquipmentSlot.Top => "상의",
                EquipmentSlot.Gloves => "장갑",
                EquipmentSlot.Boots => "신발",
                EquipmentSlot.Ring => "반지",
                EquipmentSlot.Necklace => "목걸이",
                EquipmentSlot.FaceAccessory => "얼굴장식",
                _ => slot.ToString()
            };
        }
    }
}
