using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;
using MkLike.Data;
using MkLike.Equipment;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 장비 슬롯 인벤토리 팝업.
    /// 슬롯 클릭 시 해당 슬롯의 보유 장비 그리드를 표시하고,
    /// 장비 선택 → 장착/각성 기능을 제공한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PopupEquipSlotInventory : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private EquipmentSlot _currentSlot;
        private string _selectedInstanceId;

        // ── UXML 요소 캐시 ──
        private VisualElement _popupDim;
        private VisualElement _popupPanel;
        private VisualElement _headerIcon;
        private Label _headerTitle;
        private Button _closeBtn;
        private VisualElement _equipGrid;
        private Label _emptyLabel;
        private VisualElement _detailPanel;
        private Label _detailName;
        private Label _detailGrade;
        private Label _detailAtk;
        private Label _detailHp;
        private Label _detailDef;
        private Label _detailAwakening;
        private Button _equipBtn;
        private Button _awakenBtn;

        // ── 등급 색상 ──
        private static readonly Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.6f, 0.6f, 0.6f) },
            { "Rare", new Color(0.27f, 0.53f, 1f) },
            { "Epic", new Color(0.67f, 0.27f, 1f) },
            { "Unique", new Color(1f, 0.84f, 0f) },
            { "Legendary", new Color(1f, 0.4f, 0f) },
            { "Mythic", new Color(1f, 0.2f, 0.27f) },
        };

        // ═══════════════════════════════════
        //  라이프사이클
        // ═══════════════════════════════════

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 200; // 팝업이므로 높은 order

            // PanelSettings / UXML 자동 할당 (인스펙터 미설정 시)
#if UNITY_EDITOR
            if (_doc.panelSettings == null)
            {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(
                    "Assets/UI Toolkit/GamePanelSettings.asset");
                if (ps != null) _doc.panelSettings = ps;
            }

            if (_doc.visualTreeAsset == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("PopupEquipSlotInventory t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _doc.visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path);
                }
            }
#else
            // 빌드에서는 다른 UIDocument의 PanelSettings 복사
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
                Debug.LogWarning("[PopupEquipSlotInventory] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            CacheElements();
            BindButtons();

            // 이벤트 구독 — 장비 변경 시 자동 갱신
            EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Subscribe<EquipmentAwakenedEvent>(OnEquipmentAwakened);
            EventBus.Subscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);

            // 초기 숨김
            Hide();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Unsubscribe<EquipmentAwakenedEvent>(OnEquipmentAwakened);
            EventBus.Unsubscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);
        }

        // ═══════════════════════════════════
        //  요소 캐싱
        // ═══════════════════════════════════

        private void CacheElements()
        {
            _popupDim = _root.Q<VisualElement>("popup-dim");
            _popupPanel = _root.Q<VisualElement>("popup-panel");
            _headerIcon = _root.Q<VisualElement>("header-icon");
            _headerTitle = _root.Q<Label>("header-title");
            _closeBtn = _root.Q<Button>("close-btn");
            _equipGrid = _root.Q<VisualElement>("equip-grid");
            _emptyLabel = _root.Q<Label>("empty-label");
            _detailPanel = _root.Q<VisualElement>("detail-panel");
            _detailName = _root.Q<Label>("detail-name");
            _detailGrade = _root.Q<Label>("detail-grade");
            _detailAtk = _root.Q<Label>("detail-atk");
            _detailHp = _root.Q<Label>("detail-hp");
            _detailDef = _root.Q<Label>("detail-def");
            _detailAwakening = _root.Q<Label>("detail-awakening");
            _equipBtn = _root.Q<Button>("equip-btn");
            _awakenBtn = _root.Q<Button>("awaken-btn");
        }

        private void BindButtons()
        {
            _closeBtn?.RegisterCallback<ClickEvent>(_ => Hide());
            _popupDim?.RegisterCallback<ClickEvent>(OnDimClicked);
            _equipBtn?.RegisterCallback<ClickEvent>(_ => OnEquipClicked());
            _awakenBtn?.RegisterCallback<ClickEvent>(_ => OnAwakenClicked());
        }

        /// <summary>딤 영역 클릭 시 닫기 (패널 내부 클릭은 무시)</summary>
        private void OnDimClicked(ClickEvent evt)
        {
            if (evt.target == _popupDim)
                Hide();
        }

        // ═══════════════════════════════════
        //  공개 API
        // ═══════════════════════════════════

        /// <summary>
        /// 특정 슬롯의 장비 인벤토리 팝업을 표시한다.
        /// </summary>
        public void Show(EquipmentSlot slot)
        {
            _currentSlot = slot;
            _selectedInstanceId = null;

            if (_popupDim != null)
                _popupDim.style.display = DisplayStyle.Flex;

            // 헤더 설정
            if (_headerTitle != null)
                _headerTitle.text = GetSlotDisplayName(slot);

            if (_headerIcon != null)
            {
                string iconPath = GetSlotIconPath(slot);
                var sprite = Resources.Load<Sprite>(iconPath);
                if (sprite != null)
                    _headerIcon.style.backgroundImage = new StyleBackground(sprite);
            }

            BuildGrid();
            HideDetail();

            Debug.Log($"[PopupEquipSlotInventory] Show: {slot}");
        }

        /// <summary>
        /// 팝업을 닫는다.
        /// </summary>
        public void Hide()
        {
            if (_popupDim != null)
                _popupDim.style.display = DisplayStyle.None;

            _selectedInstanceId = null;
        }

        /// <summary>팝업이 표시 중인지 여부</summary>
        public bool IsVisible => _popupDim != null
            && _popupDim.resolvedStyle.display == DisplayStyle.Flex;

        // ═══════════════════════════════════
        //  그리드 빌드
        // ═══════════════════════════════════

        private void BuildGrid()
        {
            if (_equipGrid == null) return;
            _equipGrid.Clear();

            if (EquipmentManager.Instance == null)
            {
                ShowEmpty(true);
                return;
            }

            var items = EquipmentManager.Instance.GetInventoryBySlot(_currentSlot);
            if (items == null || items.Count == 0)
            {
                ShowEmpty(true);
                return;
            }

            ShowEmpty(false);

            // 등급 높은 순 정렬
            items.Sort((a, b) =>
            {
                int cmp = GradeToInt(b.grade).CompareTo(GradeToInt(a.grade));
                if (cmp != 0) return cmp;
                return string.Compare(a.equipmentId, b.equipmentId, System.StringComparison.Ordinal);
            });

            for (int i = 0; i < items.Count; i++)
            {
                var card = CreateEquipCard(items[i]);
                _equipGrid.Add(card);
            }
        }

        private VisualElement CreateEquipCard(EquipmentInstance instance)
        {
            var data = EquipmentManager.Instance.GetData(instance.equipmentId);
            bool isEquipped = EquipmentManager.Instance.IsEquipped(instance.instanceId);
            Color gradeColor = GetGradeColor(instance.grade);
            string capturedId = instance.instanceId;

            // 카드 컨테이너
            var card = new VisualElement();
            card.AddToClassList("equip-card");
            card.style.borderTopColor = gradeColor;
            card.style.borderBottomColor = gradeColor;
            card.style.borderLeftColor = gradeColor;
            card.style.borderRightColor = gradeColor;

            if (capturedId == _selectedInstanceId)
                card.AddToClassList("equip-card--selected");

            // 클릭 이벤트
            card.RegisterCallback<ClickEvent>(_ => OnCardClicked(capturedId));

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("equip-card__icon");
            if (data != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);
            card.Add(icon);

            // 이름
            string displayName = data != null ? data.displayName : instance.equipmentId;
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("equip-card__name");
            card.Add(nameLabel);

            // 등급
            var gradeLabel = new Label(instance.grade);
            gradeLabel.AddToClassList("equip-card__grade");
            gradeLabel.style.color = gradeColor;
            card.Add(gradeLabel);

            // 각성 별
            if (instance.awakeningStars > 0)
            {
                var awakenLabel = new Label(BuildAwakenStars(instance.awakeningStars));
                awakenLabel.AddToClassList("equip-card__awakening");
                card.Add(awakenLabel);
            }

            // 장착 뱃지
            if (isEquipped)
            {
                var badge = new VisualElement();
                badge.AddToClassList("equip-card__badge");
                var badgeLabel = new Label("E");
                badge.Add(badgeLabel);
                card.Add(badge);
            }

            return card;
        }

        // ═══════════════════════════════════
        //  카드 선택 & 상세 표시
        // ═══════════════════════════════════

        private void OnCardClicked(string instanceId)
        {
            _selectedInstanceId = instanceId;

            if (EquipmentManager.Instance == null) return;
            var inst = EquipmentManager.Instance.GetInstance(instanceId);
            if (inst == null) return;

            ShowDetail(inst);
            RefreshGrid();
        }

        private void ShowDetail(EquipmentInstance inst)
        {
            if (_detailPanel == null) return;
            _detailPanel.style.display = DisplayStyle.Flex;

            var data = EquipmentManager.Instance?.GetData(inst.equipmentId);
            bool isEquipped = EquipmentManager.Instance != null
                && EquipmentManager.Instance.IsEquipped(inst.instanceId);
            Color gradeColor = GetGradeColor(inst.grade);

            // 이름 + 등급
            if (_detailName != null)
            {
                _detailName.text = data != null ? data.displayName : inst.equipmentId;
                _detailName.style.color = gradeColor;
            }

            if (_detailGrade != null)
            {
                _detailGrade.text = inst.grade;
                _detailGrade.style.color = gradeColor;
            }

            // 스탯 (각성 보너스 포함)
            if (data != null)
            {
                float awakenMult = inst.AwakeningMultiplier;
                int atk = Mathf.RoundToInt(data.GetAtk(inst.grade) * awakenMult);
                int hp = Mathf.RoundToInt(data.GetHp(inst.grade) * awakenMult);
                int def = Mathf.RoundToInt(data.GetDef(inst.grade) * awakenMult);

                if (_detailAtk != null) _detailAtk.text = $"ATK +{NumberFormatter.FormatKorean(atk)}";
                if (_detailHp != null) _detailHp.text = $"HP +{NumberFormatter.FormatKorean(hp)}";
                if (_detailDef != null) _detailDef.text = $"DEF +{NumberFormatter.FormatKorean(def)}";
            }

            // 전투력 변화 표시
            if (data != null && EquipmentManager.Instance != null)
            {
                var equipped = EquipmentManager.Instance.GetEquipped(_currentSlot);
                int cpDelta = 0;
                if (equipped != null && equipped.instanceId != inst.instanceId)
                {
                    var eqData = EquipmentManager.Instance.GetData(equipped.equipmentId);
                    if (eqData != null)
                    {
                        float aMult = inst.AwakeningMultiplier;
                        float eMult = equipped.AwakeningMultiplier;
                        int newAtk = Mathf.RoundToInt(data.GetAtk(inst.grade) * aMult);
                        int oldAtk = Mathf.RoundToInt(eqData.GetAtk(equipped.grade) * eMult);
                        cpDelta = newAtk - oldAtk;
                    }
                }
                else if (equipped == null)
                {
                    cpDelta = Mathf.RoundToInt(data.GetAtk(inst.grade) * inst.AwakeningMultiplier);
                }

                if (_detailDef != null && cpDelta != 0)
                {
                    string sign = cpDelta > 0 ? "+" : "";
                    Color deltaColor = cpDelta > 0 ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
                    _detailDef.text += $"  <color=#{ColorUtility.ToHtmlStringRGB(deltaColor)}>전투력 {sign}{cpDelta}</color>";
                }
            }

            // 각성
            if (_detailAwakening != null)
            {
                _detailAwakening.text = $"{inst.awakeningStars}/{EquipmentInstance.MAX_AWAKENING} {BuildAwakenStars(inst.awakeningStars)}";
            }

            // 장착 버튼 텍스트
            if (_equipBtn != null)
            {
                var btnLabel = _equipBtn.Q<Label>();
                if (btnLabel != null)
                    btnLabel.text = isEquipped ? "해제" : "장착";

                _equipBtn.RemoveFromClassList("eqslot-btn--equip");
                _equipBtn.RemoveFromClassList("eqslot-btn--unequip");
                _equipBtn.AddToClassList(isEquipped ? "eqslot-btn--unequip" : "eqslot-btn--equip");
            }

            // 각성 버튼 활성화 여부
            if (_awakenBtn != null)
            {
                bool canAwaken = false;
                if (EquipmentManager.Instance != null
                    && inst.awakeningStars < EquipmentInstance.MAX_AWAKENING)
                {
                    var materials = EquipmentManager.Instance.GetAwakeningMaterials(inst.instanceId);
                    canAwaken = materials != null && materials.Count > 0;
                }

                _awakenBtn.SetEnabled(canAwaken);
                _awakenBtn.RemoveFromClassList("eqslot-btn--awaken");
                _awakenBtn.RemoveFromClassList("eqslot-btn--disabled");
                _awakenBtn.AddToClassList(canAwaken ? "eqslot-btn--awaken" : "eqslot-btn--disabled");

                var awakenLabel = _awakenBtn.Q<Label>();
                if (awakenLabel != null)
                {
                    if (inst.awakeningStars >= EquipmentInstance.MAX_AWAKENING)
                        awakenLabel.text = "최대 각성";
                    else
                        awakenLabel.text = "각성";
                }
            }
        }

        private void HideDetail()
        {
            if (_detailPanel != null)
                _detailPanel.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════
        //  버튼 액션
        // ═══════════════════════════════════

        private void OnEquipClicked()
        {
            if (EquipmentManager.Instance == null || string.IsNullOrEmpty(_selectedInstanceId))
                return;

            bool isEquipped = EquipmentManager.Instance.IsEquipped(_selectedInstanceId);

            if (isEquipped)
            {
                EquipmentManager.Instance.Unequip(_currentSlot);
                Debug.Log($"[PopupEquipSlotInventory] 해제: {_selectedInstanceId}");
            }
            else
            {
                EquipmentManager.Instance.Equip(_selectedInstanceId);
                Debug.Log($"[PopupEquipSlotInventory] 장착: {_selectedInstanceId}");
            }

            // 갱신은 이벤트 핸들러에서 처리
        }

        private void OnAwakenClicked()
        {
            if (EquipmentManager.Instance == null || string.IsNullOrEmpty(_selectedInstanceId))
                return;

            var target = EquipmentManager.Instance.GetInstance(_selectedInstanceId);
            if (target == null) return;

            if (target.awakeningStars >= EquipmentInstance.MAX_AWAKENING)
            {
                Debug.Log("[PopupEquipSlotInventory] 이미 최대 각성 단계");
                return;
            }

            // 재료 자동 선택: 첫 번째 가용 재료 사용
            var materials = EquipmentManager.Instance.GetAwakeningMaterials(_selectedInstanceId);
            if (materials == null || materials.Count == 0)
            {
                Debug.Log("[PopupEquipSlotInventory] 각성 재료 부족");
                return;
            }

            string materialId = materials[0].instanceId;
            bool result = EquipmentManager.Instance.TryAwaken(_selectedInstanceId, materialId);
            if (result)
            {
                Debug.Log($"[PopupEquipSlotInventory] 각성 성공: {_selectedInstanceId} → {target.awakeningStars}★");
            }

            // 갱신은 이벤트 핸들러에서 처리
        }

        // ═══════════════════════════════════
        //  이벤트 핸들러
        // ═══════════════════════════════════

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (!IsVisible) return;
            RefreshGrid();

            // 선택된 장비 상세도 갱신
            if (!string.IsNullOrEmpty(_selectedInstanceId) && EquipmentManager.Instance != null)
            {
                var inst = EquipmentManager.Instance.GetInstance(_selectedInstanceId);
                if (inst != null)
                    ShowDetail(inst);
                else
                    HideDetail();
            }
        }

        private void OnEquipmentAwakened(EquipmentAwakenedEvent evt)
        {
            if (!IsVisible) return;
            RefreshGrid();

            if (evt.InstanceId == _selectedInstanceId && EquipmentManager.Instance != null)
            {
                var inst = EquipmentManager.Instance.GetInstance(_selectedInstanceId);
                if (inst != null)
                    ShowDetail(inst);
            }
        }

        private void OnInventoryChanged(EquipmentInventoryChangedEvent evt)
        {
            if (!IsVisible) return;
            RefreshGrid();
        }

        // ═══════════════════════════════════
        //  갱신
        // ═══════════════════════════════════

        private void RefreshGrid()
        {
            BuildGrid();
        }

        private void ShowEmpty(bool isVisible)
        {
            if (_emptyLabel != null)
                _emptyLabel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_equipGrid != null)
                _equipGrid.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ═══════════════════════════════════
        //  유틸
        // ═══════════════════════════════════

        private static Color GetGradeColor(string grade)
        {
            if (UIThemeManager.Instance != null)
                return UIThemeManager.Instance.GetGradeColor(grade);
            return GradeColors.TryGetValue(grade, out var color) ? color : Color.white;
        }

        private static int GradeToInt(string grade)
        {
            return grade switch
            {
                "Normal" => 0,
                "Rare" => 1,
                "Epic" => 2,
                "Unique" => 3,
                "Legendary" => 4,
                "Mythic" => 5,
                _ => 0
            };
        }

        private static string BuildAwakenStars(int stars)
        {
            var sb = new StringBuilder(EquipmentInstance.MAX_AWAKENING);
            for (int i = 0; i < EquipmentInstance.MAX_AWAKENING; i++)
            {
                sb.Append(i < stars ? '\u2605' : '\u2606'); // ★ : ☆
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

        private static string GetSlotIconPath(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "Icons/Stone/icon_sword",
                EquipmentSlot.Helmet => "Icons/Stone/icon_crown",
                EquipmentSlot.Top => "Icons/Stone/icon_shield",
                EquipmentSlot.Gloves => "Icons/Stone/icon_hammer",
                EquipmentSlot.Boots => "Icons/Stone/icon_horseshoes",
                EquipmentSlot.Ring => "Icons/Stone/icon_treasure",
                EquipmentSlot.Necklace => "Icons/Stone/icon_gem",
                _ => "Icons/Stone/icon_star"
            };
        }
    }
}
