using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 던전 전투 종료 결과 팝업.
    /// DungeonBattleEndedEvent 구독 → 성공/실패, 킬 수, 점수 표시.
    /// Canvas를 코드로 동적 생성한다. sortingOrder 350.
    /// 확인 버튼 또는 3초 후 자동 닫기.
    /// </summary>
    public class DungeonResultPopup : MonoBehaviour
    {
        // ── 설정 ──
        private const int SORTING_ORDER = 350;
        private const float AUTO_CLOSE_DELAY = 3f;
        private const float POPUP_WIDTH = 400f;
        private const float POPUP_HEIGHT = 300f;

        // ── 색상 ──
        private static readonly Color COLOR_SUCCESS = new(1f, 0.84f, 0f, 1f);   // 금색
        private static readonly Color COLOR_FAIL = new(0.6f, 0.6f, 0.6f, 1f);   // 회색
        private static readonly Color COLOR_WHITE = Color.white;
        private static readonly Color COLOR_OVERLAY = new(0f, 0f, 0f, 0.7f);
        private static readonly Color COLOR_PANEL_BG = new(0.12f, 0.12f, 0.18f, 0.95f);
        private static readonly Color COLOR_BUTTON = new(0.2f, 0.6f, 1f, 1f);
        private static readonly Color COLOR_BUTTON_TEXT = Color.white;

        // ── UI 참조 ──
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _popupPanel;
        private Image _overlayImage;
        private TMP_Text _dungeonNameText;
        private TMP_Text _resultText;
        private TMP_Text _killText;
        private TMP_Text _scoreText;
        private Button _confirmButton;

        // ── 상태 ──
        private bool _isShowing;
        private CancellationTokenSource _autoCloseCts;

        private void Awake()
        {
            BuildUI();
            HideImmediate();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);
        }

        private void OnDestroy()
        {
            _autoCloseCts?.Cancel();
            _autoCloseCts?.Dispose();
            transform.DOKill();
            if (_popupPanel != null)
                _popupPanel.DOKill();
            DOTween.Kill(this);
        }

        // ────────────────────────────────────────
        // 이벤트 핸들러
        // ────────────────────────────────────────

        private void OnDungeonBattleEnded(DungeonBattleEndedEvent evt)
        {
            Show(evt);
        }

        // ────────────────────────────────────────
        // 표시/숨김
        // ────────────────────────────────────────

        private void Show(DungeonBattleEndedEvent evt)
        {
            if (_isShowing) return;
            _isShowing = true;

            // 데이터 바인딩
            if (_dungeonNameText != null)
                _dungeonNameText.text = evt.DungeonName;

            bool isSuccess = evt.IsSuccess;

            if (_resultText != null)
            {
                _resultText.text = isSuccess ? "클리어 성공!" : "클리어 실패";
                _resultText.color = isSuccess ? COLOR_SUCCESS : COLOR_FAIL;
            }

            if (_killText != null)
                _killText.text = $"처치: {evt.KillCount} / {evt.TargetKills}";

            if (_scoreText != null)
            {
                int scorePercent = Mathf.RoundToInt(evt.Score * 100f);
                _scoreText.text = $"점수: {scorePercent}%";
            }

            // 보이기
            _canvas.enabled = true;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // 등장 애니메이션: 스케일 0 → 1.1 → 1.0 (OutBack)
            _popupPanel.localScale = Vector3.zero;
            DOTween.Kill(_popupPanel);
            DOTween.Sequence()
                .Append(
                    DOTween.To(
                        () => _popupPanel.localScale,
                        v => _popupPanel.localScale = v,
                        new Vector3(1.1f, 1.1f, 1f),
                        0.25f
                    ).SetEase(Ease.OutBack)
                )
                .Append(
                    DOTween.To(
                        () => _popupPanel.localScale,
                        v => _popupPanel.localScale = v,
                        Vector3.one,
                        0.1f
                    ).SetEase(Ease.InOutSine)
                )
                .SetLink(gameObject);

            // 오버레이 페이드인
            if (_overlayImage != null)
            {
                _overlayImage.color = new Color(0f, 0f, 0f, 0f);
                DOTween.To(
                    () => _overlayImage.color,
                    c => _overlayImage.color = c,
                    COLOR_OVERLAY,
                    0.2f
                ).SetLink(gameObject);
            }

            // 자동 닫기 타이머
            StartAutoClose().Forget();
        }

        private void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;

            _autoCloseCts?.Cancel();

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // 페이드아웃
            DOTween.To(
                () => _canvasGroup.alpha,
                a => _canvasGroup.alpha = a,
                0f,
                0.2f
            ).SetLink(gameObject)
            .OnComplete(() =>
            {
                _canvas.enabled = false;
            });
        }

        private void HideImmediate()
        {
            _isShowing = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_canvas != null)
                _canvas.enabled = false;
        }

        private async UniTaskVoid StartAutoClose()
        {
            _autoCloseCts?.Cancel();
            _autoCloseCts?.Dispose();
            _autoCloseCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            var ct = _autoCloseCts.Token;

            try
            {
                await UniTask.Delay(
                    (int)(AUTO_CLOSE_DELAY * 1000f),
                    cancellationToken: ct
                );
                Hide();
            }
            catch (System.OperationCanceledException)
            {
                // 수동 닫기 또는 오브젝트 파괴 — 무시
            }
        }

        private void OnConfirmClicked()
        {
            Hide();
        }

        // ────────────────────────────────────────
        // UI 동적 생성
        // ────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 오버레이 (반투명 검은 배경, 전체 화면)
            var overlayGo = CreateChild("Overlay", gameObject.transform);
            _overlayImage = overlayGo.AddComponent<Image>();
            _overlayImage.color = COLOR_OVERLAY;
            StretchFull(overlayGo.GetComponent<RectTransform>());

            // 팝업 패널
            var panelGo = CreateChild("PopupPanel", gameObject.transform);
            _popupPanel = panelGo.GetComponent<RectTransform>();
            _popupPanel.sizeDelta = new Vector2(POPUP_WIDTH, POPUP_HEIGHT);

            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = COLOR_PANEL_BG;

            // 수직 레이아웃 (수동 배치)
            float yOffset = POPUP_HEIGHT * 0.5f - 30f;

            // 던전 이름
            _dungeonNameText = CreateText("DungeonName", _popupPanel, 24, COLOR_WHITE);
            var nameRect = _dungeonNameText.GetComponent<RectTransform>();
            nameRect.anchoredPosition = new Vector2(0f, yOffset);
            nameRect.sizeDelta = new Vector2(POPUP_WIDTH - 40f, 36f);
            yOffset -= 50f;

            // 성공/실패
            _resultText = CreateText("Result", _popupPanel, 30, COLOR_SUCCESS);
            var resultRect = _resultText.GetComponent<RectTransform>();
            resultRect.anchoredPosition = new Vector2(0f, yOffset);
            resultRect.sizeDelta = new Vector2(POPUP_WIDTH - 40f, 40f);
            _resultText.fontStyle = FontStyles.Bold;
            yOffset -= 50f;

            // 킬 수
            _killText = CreateText("KillCount", _popupPanel, 22, COLOR_WHITE);
            var killRect = _killText.GetComponent<RectTransform>();
            killRect.anchoredPosition = new Vector2(0f, yOffset);
            killRect.sizeDelta = new Vector2(POPUP_WIDTH - 40f, 32f);
            yOffset -= 40f;

            // 점수
            _scoreText = CreateText("Score", _popupPanel, 22, COLOR_WHITE);
            var scoreRect = _scoreText.GetComponent<RectTransform>();
            scoreRect.anchoredPosition = new Vector2(0f, yOffset);
            scoreRect.sizeDelta = new Vector2(POPUP_WIDTH - 40f, 32f);
            yOffset -= 55f;

            // 확인 버튼
            var btnGo = CreateChild("ConfirmButton", _popupPanel);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0f, yOffset);
            btnRect.sizeDelta = new Vector2(200f, 50f);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = COLOR_BUTTON;

            _confirmButton = btnGo.AddComponent<Button>();
            _confirmButton.targetGraphic = btnImage;
            _confirmButton.onClick.AddListener(OnConfirmClicked);

            var btnText = CreateText("ButtonText", btnRect, 22, COLOR_BUTTON_TEXT);
            btnText.text = "확인";
            var btnTextRect = btnText.GetComponent<RectTransform>();
            StretchFull(btnTextRect);
        }

        // ────────────────────────────────────────
        // UI 유틸
        // ────────────────────────────────────────

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, int fontSize, Color color)
        {
            var go = CreateChild(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
