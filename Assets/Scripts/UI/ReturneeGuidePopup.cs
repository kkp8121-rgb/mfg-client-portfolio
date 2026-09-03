using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 복귀 유저 가이드 팝업 (UX-22).
    /// ReturneeGuideEvent를 구독하여 자동으로 표시한다.
    /// 현재 상태 요약 + 추천 행동 + 복귀 보너스 수령 UI.
    /// </summary>
    public class ReturneeGuidePopup : BasePopup
    {
        [Header("상태 요약")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _floorText;
        [SerializeField] private TMP_Text _awayDaysText;

        [Header("오프라인 보상")]
        [SerializeField] private TMP_Text _offlineGoldText;
        [SerializeField] private TMP_Text _offlineExpText;

        [Header("추천 행동")]
        [SerializeField] private Transform _recommendationContainer;
        [SerializeField] private GameObject _recommendationItemPrefab;

        [Header("복귀 보너스")]
        [SerializeField] private TMP_Text _bonusTierText;
        [SerializeField] private TMP_Text _bonusRubyText;
        [SerializeField] private TMP_Text _bonusTicketText;
        [SerializeField] private TMP_Text _bonusKeyText;
        [SerializeField] private GameObject _bonusExpBuffLabel;
        [SerializeField] private GameObject _bonusCostumeLabel;
        [SerializeField] private GameObject _bonusWeaponLabel;
        [SerializeField] private GameObject _bonusEquipmentLabel;

        [Header("버튼")]
        [SerializeField] private Button _startButton;

        private ReturneeGuideEvent _cachedEvent;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
            EventBus<ReturneeGuideEvent>.Subscribe(OnReturneeGuide);

            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        protected override void OnDestroy()
        {
            DOTween.Kill(gameObject);
            base.OnDestroy();
            EventBus<ReturneeGuideEvent>.Unsubscribe(OnReturneeGuide);
        }

        private void OnReturneeGuide(ReturneeGuideEvent evt)
        {
            _cachedEvent = evt;
            PopulateUI(evt);
            Show();
        }

        private void PopulateUI(ReturneeGuideEvent evt)
        {
            int daysAway = evt.MinutesAway / (60 * 24);

            if (_titleText != null)
                _titleText.text = "돌아오셨군요, 헌터님!";

            if (_awayDaysText != null)
                _awayDaysText.text = $"미접속: {daysAway}일";

            if (_levelText != null)
                _levelText.text = $"Lv. {evt.PlayerLevel}";

            if (_floorText != null)
                _floorText.text = $"탑 최고층: {evt.MaxFloor}층";

            if (_offlineGoldText != null)
                _offlineGoldText.text = evt.OfflineGold.ToString("N0");

            if (_offlineExpText != null)
                _offlineExpText.text = evt.OfflineExp.ToString("N0");

            PopulateRecommendations(evt.Recommendations);
            PopulateBonus(evt.Bonus);
        }

        private void PopulateRecommendations(List<string> recommendations)
        {
            if (_recommendationContainer == null) return;

            // 기존 항목 제거
            for (int i = _recommendationContainer.childCount - 1; i >= 0; i--)
                Destroy(_recommendationContainer.GetChild(i).gameObject);

            if (recommendations == null) return;

            for (int i = 0; i < recommendations.Count; i++)
            {
                if (_recommendationItemPrefab != null)
                {
                    var item = Instantiate(_recommendationItemPrefab, _recommendationContainer);
                    var text = item.GetComponentInChildren<TMP_Text>();
                    if (text != null)
                        text.text = $"{i + 1}. {recommendations[i]}";
                }
            }
        }

        private void PopulateBonus(ReturneeBonus bonus)
        {
            if (_bonusTierText != null)
                _bonusTierText.text = bonus.TierName;

            if (_bonusRubyText != null)
                _bonusRubyText.text = $"루비 {bonus.Ruby}";

            if (_bonusTicketText != null)
                _bonusTicketText.text = $"무기소환권 x{bonus.WeaponTicket}";

            if (_bonusKeyText != null)
                _bonusKeyText.text = $"던전열쇠 x{bonus.DungeonKey}";

            if (_bonusExpBuffLabel != null)
                _bonusExpBuffLabel.SetActive(bonus.BonusExpDays > 0);

            if (_bonusCostumeLabel != null)
                _bonusCostumeLabel.SetActive(false);

            if (_bonusWeaponLabel != null)
                _bonusWeaponLabel.SetActive(bonus.HasWeaponBox);

            if (_bonusEquipmentLabel != null)
                _bonusEquipmentLabel.SetActive(bonus.HasEquipmentBox);
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Full-screen dimmed overlay
            var overlay = CreateUIChild("DimOverlay");
            var overlayRt = overlay.GetComponent<RectTransform>();
            SetStretchAll(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.7f);
            overlayImg.raycastTarget = true;

            // Centered popup panel
            var panel = CreateUIChild("Panel");
            panel.transform.SetParent(overlay.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            SetCentered(panelRt, 420f, 620f);
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = Color.white;
            var tm = UIThemeManager.Instance;
            if (tm != null) tm.ApplyPanelBackground(panelBg);
            else panelBg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);
            var panelVlg = panel.AddComponent<VerticalLayoutGroup>();
            panelVlg.childAlignment = TextAnchor.UpperCenter;
            panelVlg.childControlWidth = true;
            panelVlg.childControlHeight = false;
            panelVlg.childForceExpandWidth = true;
            panelVlg.spacing = 6f;
            panelVlg.padding = new RectOffset(20, 20, 16, 16);

            if (_titleText == null)
            {
                var go = CreateLayoutChild(panel.transform, "TitleText", 40f);
                _titleText = go.AddComponent<TextMeshProUGUI>();
                _titleText.fontSize = 24f;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = new Color(1f, 0.85f, 0.1f);
                _titleText.fontStyle = FontStyles.Bold;
            }

            if (_awayDaysText == null)
            {
                var go = CreateLayoutChild(panel.transform, "AwayDaysText", 28f);
                _awayDaysText = go.AddComponent<TextMeshProUGUI>();
                _awayDaysText.fontSize = 16f;
                _awayDaysText.alignment = TextAlignmentOptions.Center;
                _awayDaysText.color = new Color(0.7f, 0.7f, 0.7f);
            }

            // Status row (level + floor)
            var statusRow = CreateLayoutChild(panel.transform, "StatusRow", 30f);
            var statusHlg = statusRow.AddComponent<HorizontalLayoutGroup>();
            statusHlg.childAlignment = TextAnchor.MiddleCenter;
            statusHlg.childControlWidth = true;
            statusHlg.childForceExpandWidth = true;
            statusHlg.spacing = 16f;

            if (_levelText == null)
            {
                var go = CreateLayoutChild(statusRow.transform, "LevelText", 30f);
                _levelText = go.AddComponent<TextMeshProUGUI>();
                _levelText.fontSize = 18f;
                _levelText.alignment = TextAlignmentOptions.Center;
                _levelText.color = Color.white;
            }

            if (_floorText == null)
            {
                var go = CreateLayoutChild(statusRow.transform, "FloorText", 30f);
                _floorText = go.AddComponent<TextMeshProUGUI>();
                _floorText.fontSize = 18f;
                _floorText.alignment = TextAlignmentOptions.Center;
                _floorText.color = Color.white;
            }

            // Separator
            var sep1 = CreateLayoutChild(panel.transform, "Sep1", 2f);
            var sep1Img = sep1.AddComponent<Image>();
            sep1Img.color = new Color(1f, 1f, 1f, 0.15f);

            // Offline rewards header
            var offlineHeader = CreateLayoutChild(panel.transform, "OfflineHeader", 26f);
            var offlineHeaderTmp = offlineHeader.AddComponent<TextMeshProUGUI>();
            offlineHeaderTmp.text = "오프라인 보상";
            offlineHeaderTmp.fontSize = 16f;
            offlineHeaderTmp.alignment = TextAlignmentOptions.Center;
            offlineHeaderTmp.color = new Color(0.8f, 0.8f, 0.8f);

            var offlineRow = CreateLayoutChild(panel.transform, "OfflineRow", 32f);
            var offlineHlg = offlineRow.AddComponent<HorizontalLayoutGroup>();
            offlineHlg.childAlignment = TextAnchor.MiddleCenter;
            offlineHlg.childControlWidth = true;
            offlineHlg.childForceExpandWidth = true;
            offlineHlg.spacing = 16f;

            if (_offlineGoldText == null)
            {
                var go = CreateLayoutChild(offlineRow.transform, "OfflineGoldText", 32f);
                _offlineGoldText = go.AddComponent<TextMeshProUGUI>();
                _offlineGoldText.fontSize = 20f;
                _offlineGoldText.alignment = TextAlignmentOptions.Center;
                _offlineGoldText.color = new Color(1f, 0.85f, 0.1f);
            }

            if (_offlineExpText == null)
            {
                var go = CreateLayoutChild(offlineRow.transform, "OfflineExpText", 32f);
                _offlineExpText = go.AddComponent<TextMeshProUGUI>();
                _offlineExpText.fontSize = 20f;
                _offlineExpText.alignment = TextAlignmentOptions.Center;
                _offlineExpText.color = new Color(0.3f, 0.8f, 1f);
            }

            // Separator
            var sep2 = CreateLayoutChild(panel.transform, "Sep2", 2f);
            var sep2Img = sep2.AddComponent<Image>();
            sep2Img.color = new Color(1f, 1f, 1f, 0.15f);

            // Recommendations
            if (_recommendationContainer == null)
            {
                var go = CreateLayoutChild(panel.transform, "RecommendationContainer", 100f);
                _recommendationContainer = go.transform;
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 4f;
                vlg.padding = new RectOffset(8, 8, 4, 4);
            }

            // Bonus section
            if (_bonusTierText == null)
            {
                var go = CreateLayoutChild(panel.transform, "BonusTierText", 30f);
                _bonusTierText = go.AddComponent<TextMeshProUGUI>();
                _bonusTierText.fontSize = 20f;
                _bonusTierText.alignment = TextAlignmentOptions.Center;
                _bonusTierText.color = new Color(1f, 0.7f, 0.2f);
                _bonusTierText.fontStyle = FontStyles.Bold;
            }

            var bonusGrid = CreateLayoutChild(panel.transform, "BonusGrid", 60f);
            var bonusVlg = bonusGrid.AddComponent<VerticalLayoutGroup>();
            bonusVlg.childAlignment = TextAnchor.UpperLeft;
            bonusVlg.childControlWidth = true;
            bonusVlg.childControlHeight = false;
            bonusVlg.childForceExpandWidth = true;
            bonusVlg.spacing = 2f;
            bonusVlg.padding = new RectOffset(24, 24, 0, 0);

            if (_bonusRubyText == null)
            {
                var go = CreateLayoutChild(bonusGrid.transform, "BonusRubyText", 24f);
                _bonusRubyText = go.AddComponent<TextMeshProUGUI>();
                _bonusRubyText.fontSize = 16f;
                _bonusRubyText.alignment = TextAlignmentOptions.Left;
                _bonusRubyText.color = new Color(0.9f, 0.3f, 0.4f);
            }

            if (_bonusTicketText == null)
            {
                var go = CreateLayoutChild(bonusGrid.transform, "BonusTicketText", 24f);
                _bonusTicketText = go.AddComponent<TextMeshProUGUI>();
                _bonusTicketText.fontSize = 16f;
                _bonusTicketText.alignment = TextAlignmentOptions.Left;
                _bonusTicketText.color = Color.white;
            }

            if (_bonusKeyText == null)
            {
                var go = CreateLayoutChild(bonusGrid.transform, "BonusKeyText", 24f);
                _bonusKeyText = go.AddComponent<TextMeshProUGUI>();
                _bonusKeyText.fontSize = 16f;
                _bonusKeyText.alignment = TextAlignmentOptions.Left;
                _bonusKeyText.color = Color.white;
            }

            if (_startButton == null)
            {
                var go = CreateLayoutChild(panel.transform, "StartButton", 48f);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = Color.white;
                _startButton = go.AddComponent<Button>();
                var tmBtn = UIThemeManager.Instance;
                if (tmBtn != null) tmBtn.ApplyConfirmButton(btnBg);
                else btnBg.color = new Color(0.2f, 0.55f, 0.25f);
                var txt = new GameObject("Text", typeof(RectTransform));
                txt.transform.SetParent(go.transform, false);
                var txtRt = txt.GetComponent<RectTransform>();
                SetStretchAll(txtRt);
                var tmp = txt.AddComponent<TextMeshProUGUI>();
                tmp.text = "시작하기";
                tmp.fontSize = 20f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static GameObject CreateLayoutChild(Transform parent, string childName, float height)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return go;
        }

        private static void SetStretchAll(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetCentered(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private void OnStartClicked()
        {
            if (_startButton != null)
            {
                _startButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(Hide);
            }
            else
            {
                Hide();
            }
        }
    }
}
