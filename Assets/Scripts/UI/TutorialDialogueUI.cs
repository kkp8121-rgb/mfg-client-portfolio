using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    public class TutorialDialogueUI : MonoBehaviour, ITutorialDialogue
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _npcPortrait;
        [SerializeField] private TMP_Text _npcNameText;
        [SerializeField] private TMP_Text _dialogueText;
        [SerializeField] private Image _frameBackground;
        [SerializeField] private Image _frameBorder;
        [SerializeField] private GameObject _nextButton;
        [SerializeField] private GameObject _skipButton;
        [SerializeField] private float _typingSpeed = 0.03f;
        [SerializeField] private int _typingSfxInterval = 3;

        private bool _isShowing;
        private bool _isTyping;
        private bool _skipTyping;
        private bool _advanceRequested;

        private void Awake()
        {
            EnsureComponents();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public async UniTask ShowDialogue(string text, string npcName = null, string npcGrade = null, bool isSkippable = false, CancellationToken token = default)
        {
            gameObject.SetActive(true);
            _isShowing = true;
            _advanceRequested = false;

            // NPC 이름 + 등급 색상 적용
            if (_npcNameText != null)
            {
                _npcNameText.text = npcName ?? "";
                _npcNameText.gameObject.SetActive(!string.IsNullOrEmpty(npcName));

                if (!string.IsNullOrEmpty(npcGrade))
                {
                    var theme = UIThemeManager.Instance;
                    if (theme != null)
                        _npcNameText.color = theme.GetGradeColor(npcGrade);
                }
            }

            if (_skipButton != null)
                _skipButton.SetActive(isSkippable);
            if (_nextButton != null)
                _nextButton.SetActive(false);

            // 페이드인
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                await DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, 0.3f)
                    .SetUpdate(true).SetLink(gameObject)
                    .ToUniTask(cancellationToken: token);
            }

            // 타이핑 효과
            _isTyping = true;
            _skipTyping = false;
            if (_dialogueText != null)
                _dialogueText.text = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (_skipTyping || token.IsCancellationRequested)
                {
                    if (_dialogueText != null)
                        _dialogueText.text = text;
                    break;
                }

                if (_dialogueText != null)
                    _dialogueText.text = text.Substring(0, i + 1);

                // 타이핑 사운드 (N글자마다)
                if (i % _typingSfxInterval == 0)
                    AudioManager.Instance?.PlaySfx(SfxType.UiTap, 0.15f);

                await UniTask.Delay(
                    (int)(_typingSpeed * 1000),
                    ignoreTimeScale: true,
                    cancellationToken: token);
            }

            _isTyping = false;
            if (_dialogueText != null)
                _dialogueText.text = text;

            if (_nextButton != null)
                _nextButton.SetActive(true);

            // 다음 버튼 또는 화면 탭 대기
            _advanceRequested = false;
            await UniTask.WaitUntil(() => _advanceRequested, cancellationToken: token);
        }

        public async UniTask HideDialogue(CancellationToken token = default)
        {
            if (_canvasGroup != null)
            {
                await DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, 0.2f)
                    .SetUpdate(true).SetLink(gameObject)
                    .ToUniTask(cancellationToken: token);
            }

            _isShowing = false;
            gameObject.SetActive(false);
        }

        // UI 버튼에서 호출
        public void OnNextButtonClicked()
        {
            if (_isTyping)
            {
                _skipTyping = true;
            }
            else
            {
                _advanceRequested = true;
            }
        }

        public void OnSkipButtonClicked()
        {
            _skipTyping = true;
            _advanceRequested = true;
        }

        public void OnScreenTapped()
        {
            OnNextButtonClicked();
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // 대사창 프레임 배경
            if (_frameBackground == null)
            {
                var bgObj = new GameObject("DialogueFrame", typeof(RectTransform));
                bgObj.transform.SetParent(transform, false);
                _frameBackground = bgObj.AddComponent<Image>();
                var rt = bgObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.02f);
                rt.anchorMax = new Vector2(0.95f, 0.30f);
                rt.sizeDelta = Vector2.zero;

                var theme = UIThemeManager.Instance;
                if (theme != null)
                {
                    var popupBg = theme.GetPopupBackground();
                    UIThemeManager.ApplySpriteOrColor(_frameBackground, popupBg, theme.Theme.panelBackground);
                }
                else
                {
                    _frameBackground.color = new Color(0.10f, 0.10f, 0.18f, 0.95f);
                }
                _frameBackground.raycastTarget = true;
            }

            // 프레임 테두리
            if (_frameBorder == null && _frameBackground != null)
            {
                var borderObj = new GameObject("DialogueBorder", typeof(RectTransform));
                borderObj.transform.SetParent(_frameBackground.transform, false);
                _frameBorder = borderObj.AddComponent<Image>();
                var rt = borderObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = new Vector2(4f, 4f);
                rt.anchoredPosition = Vector2.zero;

                var theme = UIThemeManager.Instance;
                _frameBorder.color = theme != null ? theme.Theme.panelBorder : new Color(0.06f, 0.20f, 0.38f, 1f);
                _frameBorder.raycastTarget = false;

                // Outline으로 테두리 효과
                var outline = borderObj.AddComponent<Outline>();
                outline.effectColor = _frameBorder.color;
                outline.effectDistance = new Vector2(2f, 2f);
                _frameBorder.color = Color.clear;
            }

            // NPC 이름 텍스트
            if (_npcNameText == null && _frameBackground != null)
            {
                var nameObj = new GameObject("NpcName", typeof(RectTransform));
                nameObj.transform.SetParent(_frameBackground.transform, false);
                _npcNameText = nameObj.AddComponent<TextMeshProUGUI>();
                _npcNameText.fontSize = 18f;
                _npcNameText.fontStyle = FontStyles.Bold;
                _npcNameText.alignment = TextAlignmentOptions.Left;
                _npcNameText.raycastTarget = false;
                _npcNameText.color = new Color(1f, 0.84f, 0f);
                var rt = nameObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.75f);
                rt.anchorMax = new Vector2(0.5f, 1.0f);
                rt.sizeDelta = Vector2.zero;
            }

            // 대사 텍스트
            if (_dialogueText == null && _frameBackground != null)
            {
                var textObj = new GameObject("DialogueText", typeof(RectTransform));
                textObj.transform.SetParent(_frameBackground.transform, false);
                _dialogueText = textObj.AddComponent<TextMeshProUGUI>();
                _dialogueText.fontSize = 16f;
                _dialogueText.alignment = TextAlignmentOptions.TopLeft;
                _dialogueText.raycastTarget = false;
                _dialogueText.textWrappingMode = TextWrappingModes.Normal;

                var theme = UIThemeManager.Instance;
                _dialogueText.color = theme != null ? theme.Theme.textPrimary : new Color(0.88f, 0.88f, 0.88f);

                var rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.1f);
                rt.anchorMax = new Vector2(0.95f, 0.72f);
                rt.sizeDelta = Vector2.zero;
            }

            // 초상화
            if (_npcPortrait == null && _frameBackground != null)
            {
                var portraitObj = new GameObject("NpcPortrait", typeof(RectTransform));
                portraitObj.transform.SetParent(_frameBackground.transform, false);
                _npcPortrait = portraitObj.AddComponent<Image>();
                _npcPortrait.raycastTarget = false;
                _npcPortrait.color = new Color(1f, 1f, 1f, 0.3f);
                var rt = portraitObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.85f, 0.1f);
                rt.anchorMax = new Vector2(0.98f, 0.95f);
                rt.sizeDelta = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
