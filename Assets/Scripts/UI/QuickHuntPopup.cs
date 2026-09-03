using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 소탕(Quick Hunt) 팝업.
    /// 소탕권 수량 선택 → 실행 → 결과 표시.
    /// </summary>
    public class QuickHuntPopup : BasePopup
    {
        [Header("UI 참조")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _ticketCountText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _hunt1Btn;
        [SerializeField] private Button _hunt5Btn;
        [SerializeField] private Button _hunt10Btn;
        [SerializeField] private Button _closeBtn;

        private bool _hasResult;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
        }

        private void Start()
        {
            if (_hunt1Btn != null) _hunt1Btn.onClick.AddListener(() => ExecuteHunt(1));
            if (_hunt5Btn != null) _hunt5Btn.onClick.AddListener(() => ExecuteHunt(5));
            if (_hunt10Btn != null) _hunt10Btn.onClick.AddListener(() => ExecuteHunt(10));
            if (_closeBtn != null) _closeBtn.onClick.AddListener(() => Hide());
        }

        private void OnEnable()
        {
            EventBus<QuickHuntCompletedEvent>.Subscribe(OnHuntCompleted);
        }

        private void OnDisable()
        {
            EventBus<QuickHuntCompletedEvent>.Unsubscribe(OnHuntCompleted);
        }

        protected override void OnShow()
        {
            _hasResult = false;
            RefreshUI();
        }

        private void RefreshUI()
        {
            int tickets = QuickHuntManager.Instance != null ? QuickHuntManager.Instance.GetTicketCount() : 0;
            bool isUnlocked = QuickHuntManager.Instance != null && QuickHuntManager.Instance.IsUnlocked;

            if (_titleText != null)
                _titleText.text = isUnlocked ? "소탕" : "소탕 (미해금)";

            if (_ticketCountText != null)
                _ticketCountText.text = $"소탕권: {tickets}장";

            if (_resultText != null && !_hasResult)
                _resultText.text = isUnlocked ? "소탕권을 사용하여 즉시 보상을 획득합니다." : "스테이지 75 이상에서 해금됩니다.";

            // 버튼 활성화
            bool canHunt = isUnlocked && tickets > 0;
            if (_hunt1Btn != null) _hunt1Btn.interactable = canHunt && tickets >= 1;
            if (_hunt5Btn != null) _hunt5Btn.interactable = canHunt && tickets >= 5;
            if (_hunt10Btn != null) _hunt10Btn.interactable = canHunt && tickets >= 10;
        }

        private void ExecuteHunt(int count)
        {
            if (QuickHuntManager.Instance == null) return;
            QuickHuntManager.Instance.ExecuteQuickHunt(count);
        }

        private void OnHuntCompleted(QuickHuntCompletedEvent evt)
        {
            _hasResult = true;

            if (_resultText != null)
            {
                _resultText.text = $"소탕 완료!\n" +
                    $"골드: +{evt.GoldEarned:N0}\n" +
                    $"경험치: +{evt.ExpEarned:N0}\n" +
                    (evt.EquipmentDrops > 0 ? $"장비: +{evt.EquipmentDrops}개" : "");
            }

            RefreshUI();
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 딤 배경
            var bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
            }

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 패널 컨테이너
            var panel = transform.Find("Panel");
            if (panel == null)
            {
                var panelGo = new GameObject("Panel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                var panelRt = panelGo.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.1f, 0.25f);
                panelRt.anchorMax = new Vector2(0.9f, 0.75f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
                var panelBg = panelGo.AddComponent<Image>();
                panelBg.color = new Color(0.12f, 0.1f, 0.08f, 0.98f);

                var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 12;
                vlg.padding = new RectOffset(20, 20, 20, 20);

                panel = panelGo.transform;
            }

            // 타이틀
            if (_titleText == null)
            {
                var go = CreateText(panel, "Title", "소탕", 24, FontStyles.Bold, Color.white);
                _titleText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            }

            // 소탕권 수
            if (_ticketCountText == null)
            {
                var go = CreateText(panel, "TicketCount", "소탕권: 0장", 18, FontStyles.Normal, new Color(1f, 0.85f, 0.1f));
                _ticketCountText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            }

            // 결과 텍스트
            if (_resultText == null)
            {
                var go = CreateText(panel, "Result", "", 16, FontStyles.Normal, new Color(0.8f, 0.85f, 0.9f));
                _resultText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 80);
            }

            // 버튼 영역
            var btnArea = panel.Find("BtnArea");
            if (btnArea == null)
            {
                var btnGo = new GameObject("BtnArea", typeof(RectTransform));
                btnGo.transform.SetParent(panel, false);
                btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
                var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.spacing = 12;
                btnArea = btnGo.transform;
            }

            if (_hunt1Btn == null)
                _hunt1Btn = CreateButton(btnArea, "Hunt1Btn", "1회", new Color(0.2f, 0.5f, 0.8f));
            if (_hunt5Btn == null)
                _hunt5Btn = CreateButton(btnArea, "Hunt5Btn", "5회", new Color(0.2f, 0.7f, 0.3f));
            if (_hunt10Btn == null)
                _hunt10Btn = CreateButton(btnArea, "Hunt10Btn", "10회", new Color(0.7f, 0.4f, 0.8f));

            // 닫기 버튼
            if (_closeBtn == null)
            {
                _closeBtn = CreateButton(panel, "CloseBtn", "닫기", new Color(0.5f, 0.3f, 0.3f));
                _closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
            }
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
