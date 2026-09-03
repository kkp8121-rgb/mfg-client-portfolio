using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace MkLike.UI
{
    /// <summary>
    /// 화면 상단 토스트 알림 UI.
    /// ToastUI.Show("메시지") 정적 메서드로 호출한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ToastUI : MonoBehaviour
    {
        private static ToastUI _instance;

        [SerializeField] private float _displayDuration = 2.5f;
        [SerializeField] private float _fadeDuration = 0.4f;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _toastPanel;
        private Label _iconText;
        private Label _messageText;

        private CancellationTokenSource _currentCts;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ToastUI] 중복 인스턴스 제거");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            // 2026-04-23 Phase C FeedbackBus 시각 검증: sortingOrder 0 → 500.
            // HUD(0)/TabBar(10)/팝업(100) 위에 항상 오버레이. invariant ToastUIRuntimeHealth로 회귀 방지.
            _doc.sortingOrder = 500;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[ToastUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }

            // 토스트는 항상 활성이므로 클릭을 통과시켜야 함
            _root.pickingMode = PickingMode.Ignore;

            _toastPanel = _root.Q<VisualElement>("toast-panel");
            _iconText = _root.Q<Label>("toast-icon-text");
            _messageText = _root.Q<Label>("toast-message");

            // 초기 숨김
            if (_toastPanel != null)
            {
                _toastPanel.style.opacity = 0f;
            }
        }

        private void OnDisable()
        {
            CancelCurrent();
        }

        private void OnDestroy()
        {
            CancelCurrent();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 토스트 알림을 표시한다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="icon">아이콘 텍스트 (기본: "!")</param>
        public static void Show(string message, string icon = "!")
        {
            if (_instance == null)
            {
                Debug.LogWarning("[ToastUI] 인스턴스가 없습니다.");
                return;
            }

            _instance.ShowInternal(message, icon);
        }

        private void ShowInternal(string message, string icon)
        {
            if (_toastPanel == null)
            {
                Debug.LogWarning("[ToastUI] toast-panel을 찾을 수 없습니다.");
                return;
            }

            // 이전 토스트가 있으면 취소
            CancelCurrent();

            _messageText.text = message;
            _iconText.text = icon;

            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            ShowAsync(_currentCts.Token).Forget();
        }

        private async UniTaskVoid ShowAsync(CancellationToken ct)
        {
            // 즉시 표시 (opacity 1)
            _toastPanel.style.opacity = 1f;

            // displayDuration 동안 대기
            int displayMs = Mathf.RoundToInt(_displayDuration * 1000f);
            await UniTask.Delay(displayMs, cancellationToken: ct);

            // 페이드 아웃
            int fadeSteps = Mathf.Max(1, Mathf.RoundToInt(_fadeDuration / 0.016f));
            float opacityStep = 1f / fadeSteps;

            for (int i = 0; i < fadeSteps; i++)
            {
                ct.ThrowIfCancellationRequested();
                float opacity = 1f - (opacityStep * (i + 1));
                _toastPanel.style.opacity = Mathf.Max(0f, opacity);
                await UniTask.Delay(16, cancellationToken: ct);
            }

            _toastPanel.style.opacity = 0f;
        }

        private void CancelCurrent()
        {
            if (_currentCts != null)
            {
                _currentCts.Cancel();
                _currentCts.Dispose();
                _currentCts = null;
            }
        }
    }
}
