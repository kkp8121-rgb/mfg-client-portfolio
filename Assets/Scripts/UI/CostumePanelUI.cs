using System.Collections.Generic;
using MkLike.Costume;
using MkLike.Core;
using MkLike.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace MkLike.UI
{
    /// <summary>
    /// 코스튬 패널 UI 컨트롤러.
    /// 코스튬 목록 그리드 + 장착/해제 + 세트 정보.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CostumePanelUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _costumeGrid;
        private Label _emptyLabel;
        private Label _ownedCountLabel;
        private Label _setInfoLabel;
        private Button _synthesisBtn;

        // 서브탭
        private VisualElement _subtabAll;
        private VisualElement _subtabEquipped;
        private VisualElement _subtabSets;
        private int _activeSubtab; // 0=전체, 1=장착중, 2=세트

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50;

#if UNITY_EDITOR
            if (_doc.panelSettings == null)
            {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    "Assets/UI Toolkit/GamePanelSettings.asset");
                if (ps != null) _doc.panelSettings = ps;
            }

            if (_doc.visualTreeAsset == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("CostumePanel t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _doc.visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }
#endif
        }

        private void OnEnable()
        {
            _root = _doc.rootVisualElement;
            if (_root == null) return;

            PanelCloseHelper.BindCloseButton(_root, gameObject);

            _costumeGrid = _root.Q<VisualElement>("costume-grid");
            _emptyLabel = _root.Q<Label>("empty-label");
            _ownedCountLabel = _root.Q<Label>("owned-count");
            _setInfoLabel = _root.Q<Label>("set-info");
            _synthesisBtn = _root.Q<Button>("synthesis-btn");

            // 서브탭 바인딩
            _subtabAll = _root.Q<VisualElement>("subtab-all");
            _subtabEquipped = _root.Q<VisualElement>("subtab-equipped");
            _subtabSets = _root.Q<VisualElement>("subtab-sets");

            _subtabAll?.RegisterCallback<ClickEvent>(_ => SwitchSubtab(0));
            _subtabEquipped?.RegisterCallback<ClickEvent>(_ => SwitchSubtab(1));
            _subtabSets?.RegisterCallback<ClickEvent>(_ => SwitchSubtab(2));

            if (_synthesisBtn != null)
                _synthesisBtn.clicked += OnSynthesisClicked;

            // 이벤트 구독
            EventBus<CostumeObtainedEvent>.Subscribe(OnCostumeObtained);
            EventBus<CostumeEquippedEvent>.Subscribe(OnCostumeEquipped);
            EventBus<CostumeSetCompletedEvent>.Subscribe(OnCostumeSetCompleted);

            _activeSubtab = 0;
            RefreshAll();
        }

        private void OnDisable()
        {
            EventBus<CostumeObtainedEvent>.Unsubscribe(OnCostumeObtained);
            EventBus<CostumeEquippedEvent>.Unsubscribe(OnCostumeEquipped);
            EventBus<CostumeSetCompletedEvent>.Unsubscribe(OnCostumeSetCompleted);

            if (_synthesisBtn != null)
                _synthesisBtn.clicked -= OnSynthesisClicked;
        }

        private void OnCostumeObtained(CostumeObtainedEvent evt) => RefreshAll();
        private void OnCostumeEquipped(CostumeEquippedEvent evt) => RefreshAll();
        private void OnCostumeSetCompleted(CostumeSetCompletedEvent evt) => RefreshAll();

        private void SwitchSubtab(int index)
        {
            _activeSubtab = index;

            SetSubtabActive(_subtabAll, index == 0);
            SetSubtabActive(_subtabEquipped, index == 1);
            SetSubtabActive(_subtabSets, index == 2);

            RefreshGrid();
        }

        private void SetSubtabActive(VisualElement tab, bool isActive)
        {
            if (tab == null) return;
            if (isActive)
                tab.AddToClassList("costume-subtab--active");
            else
                tab.RemoveFromClassList("costume-subtab--active");
        }

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshGrid();
            RefreshFooter();
        }

        private void RefreshHeader()
        {
            if (CostumeManager.Instance == null)
            {
                if (_ownedCountLabel != null) _ownedCountLabel.text = "보유: 0";
                return;
            }

            if (_ownedCountLabel != null)
                _ownedCountLabel.text = $"보유: {CostumeManager.Instance.TotalOwned}";
        }

        private void RefreshGrid()
        {
            if (_costumeGrid == null) return;
            _costumeGrid.Clear();

            if (CostumeManager.Instance == null)
            {
                ShowEmpty(true);
                return;
            }

            var costumes = CostumeManager.Instance.OwnedCostumes;
            if (costumes.Count == 0)
            {
                ShowEmpty(true);
                return;
            }

            // 서브탭 필터링
            var filtered = new List<CostumeInstance>();
            foreach (var kvp in costumes)
            {
                var c = kvp.Value;
                switch (_activeSubtab)
                {
                    case 0: // 전체
                        filtered.Add(c);
                        break;
                    case 1: // 장착중
                        if (c.IsEquipped) filtered.Add(c);
                        break;
                    case 2: // 세트 (세트 ID가 있는 것만)
                        if (!string.IsNullOrEmpty(c.SetId)) filtered.Add(c);
                        break;
                }
            }

            if (filtered.Count == 0)
            {
                ShowEmpty(true);
                return;
            }

            ShowEmpty(false);

            foreach (var costume in filtered)
            {
                var card = CreateCostumeCard(costume);
                _costumeGrid.Add(card);
            }
        }

        private VisualElement CreateCostumeCard(CostumeInstance costume)
        {
            var card = new VisualElement();
            card.style.width = 150;
            card.style.height = 200;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.backgroundColor = new StyleColor(new Color(0.15f, 0.13f, 0.22f, 0.9f));
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;

            // 장착 중이면 테두리 강조
            if (costume.IsEquipped)
            {
                card.style.borderTopWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftWidth = 2;
                card.style.borderRightWidth = 2;
                var gold = new StyleColor(new Color(1f, 0.85f, 0.2f));
                card.style.borderTopColor = gold;
                card.style.borderBottomColor = gold;
                card.style.borderLeftColor = gold;
                card.style.borderRightColor = gold;
            }

            // 코스튬 아이콘 영역 (등급 색상 배경)
            Color gradeColor = CostumeManager.GetGradeColor(costume.Grade);
            var iconBg = new VisualElement();
            iconBg.style.width = 60;
            iconBg.style.height = 60;
            iconBg.style.borderTopLeftRadius = 8;
            iconBg.style.borderTopRightRadius = 8;
            iconBg.style.borderBottomLeftRadius = 8;
            iconBg.style.borderBottomRightRadius = 8;
            iconBg.style.alignSelf = Align.Center;
            iconBg.style.backgroundColor = new StyleColor(new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.3f));
            card.Add(iconBg);

            // 코스튬 이름
            var nameLabel = new Label(costume.DisplayName);
            nameLabel.style.fontSize = 14;
            nameLabel.style.color = new StyleColor(Color.white);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.marginTop = 6;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(nameLabel);

            // 등급
            var gradeLabel = new Label(CostumeManager.GetGradeLabel(costume.Grade));
            gradeLabel.style.fontSize = 12;
            gradeLabel.style.color = new StyleColor(gradeColor);
            gradeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            gradeLabel.style.marginTop = 2;
            card.Add(gradeLabel);

            // 세트 표시
            if (!string.IsNullOrEmpty(costume.SetId))
            {
                var setLabel = new Label($"[{costume.SetId}]");
                setLabel.style.fontSize = 11;
                setLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 1f));
                setLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                setLabel.style.marginTop = 2;
                card.Add(setLabel);
            }

            // 장착/해제 버튼
            var equipBtn = new Button(() => OnEquipClicked(costume.CostumeId));
            equipBtn.text = costume.IsEquipped ? "해제" : "장착";
            equipBtn.style.marginTop = 6;
            equipBtn.style.height = 28;
            equipBtn.style.fontSize = 13;
            equipBtn.style.backgroundColor = costume.IsEquipped
                ? new StyleColor(new Color(0.5f, 0.3f, 0.3f))
                : new StyleColor(new Color(0.2f, 0.5f, 0.8f));
            equipBtn.style.color = new StyleColor(Color.white);
            equipBtn.style.borderTopLeftRadius = 4;
            equipBtn.style.borderTopRightRadius = 4;
            equipBtn.style.borderBottomLeftRadius = 4;
            equipBtn.style.borderBottomRightRadius = 4;
            card.Add(equipBtn);

            // 장착 중 표시
            if (costume.IsEquipped)
            {
                var equippedBadge = new Label("장착 중");
                equippedBadge.style.fontSize = 10;
                equippedBadge.style.color = new StyleColor(new Color(1f, 0.85f, 0.2f));
                equippedBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                equippedBadge.style.marginTop = 2;
                card.Add(equippedBadge);
            }

            return card;
        }

        private void OnEquipClicked(string costumeId)
        {
            if (CostumeManager.Instance == null) return;

            var costume = CostumeManager.Instance.OwnedCostumes.GetValueOrDefault(costumeId);
            if (costume == null) return;

            if (costume.IsEquipped)
            {
                CostumeManager.Instance.UnequipCostume(costumeId);
            }
            else
            {
                CostumeManager.Instance.EquipCostume(costumeId);
            }
        }

        private void OnSynthesisClicked()
        {
            ToastUI.Show("합성 기능은 준비 중입니다.", "");
        }

        private void RefreshFooter()
        {
            if (CostumeManager.Instance == null) return;

            if (_setInfoLabel != null)
                _setInfoLabel.text = $"완성 세트: {CostumeManager.Instance.CompletedSets.Count}개";
        }

        private void ShowEmpty(bool show)
        {
            if (_emptyLabel != null)
                _emptyLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (_costumeGrid != null)
                _costumeGrid.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
