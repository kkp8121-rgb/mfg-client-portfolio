using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 던전 입장/퇴장 시 극적인 화면 전환 연출.
    /// 화면 플래시 + 와이프 + 텍스트 연출 + 화면 흔들림으로 던전 진입감을 강화한다.
    /// DungeonEnteredEvent/DungeonCompletedEvent를 구독한다.
    /// </summary>
    public class DungeonTransitionEffect : MonoBehaviour
    {
        [Header("연출 설정")]
        [SerializeField] private float _flashDuration = 0.15f;
        [SerializeField] private float _wipeInDuration = 0.4f;
        [SerializeField] private float _textDisplayTime = 1.2f;
        [SerializeField] private float _wipeOutDuration = 0.4f;
        [SerializeField] private Color _flashColor = new(0.8f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color _wipeColor = new(0.03f, 0.02f, 0.06f, 1f);

        private Canvas _canvas;
        private Image _flashOverlay;
        private Image _wipeOverlay;
        private TMP_Text _dungeonNameText;
        private TMP_Text _dungeonSubText;
        private Sequence _currentSequence;

        private void Awake()
        {
            EnsureUI();
            HideAll();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DungeonEnteredEvent>(OnDungeonEntered);
            EventBus.Subscribe<DungeonCompletedEvent>(OnDungeonCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DungeonEnteredEvent>(OnDungeonEntered);
            EventBus.Unsubscribe<DungeonCompletedEvent>(OnDungeonCompleted);
        }

        private void OnDungeonEntered(DungeonEnteredEvent evt)
        {
            PlayEnterEffect(evt.DungeonName).Forget();
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            PlayExitEffect().Forget();
        }

        /// <summary>
        /// 던전 입장 연출: 플래시 → 와이프인 → 텍스트 등장 → 와이프아웃
        /// </summary>
        private async UniTaskVoid PlayEnterEffect(string dungeonName)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            KillSequence();

            bool isComplete = false;
            _currentSequence = DOTween.Sequence().SetLink(gameObject);

            // 1. 화면 플래시
            _flashOverlay.gameObject.SetActive(true);
            _flashOverlay.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);

            _currentSequence.Append(
                DOTween.To(
                    () => _flashOverlay.color,
                    c => _flashOverlay.color = c,
                    _flashColor,
                    _flashDuration * 0.3f
                ).SetEase(Ease.OutQuad)
            );
            _currentSequence.Append(
                DOTween.To(
                    () => _flashOverlay.color,
                    c => _flashOverlay.color = c,
                    new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f),
                    _flashDuration * 0.7f
                ).SetEase(Ease.InQuad)
            );

            // 2. 화면 흔들림
            _currentSequence.AppendCallback(() =>
            {
                var shaker = FindFirstObjectByType<ScreenShakeManager>();
                shaker?.ShakeLevelUp();
            });

            // 3. 와이프 인 (어둠이 화면을 덮음)
            _wipeOverlay.gameObject.SetActive(true);
            _wipeOverlay.color = _wipeColor;

            var wipeRt = _wipeOverlay.GetComponent<RectTransform>();
            _currentSequence.Append(
                DOTween.To(
                    () => wipeRt.anchorMax,
                    v => { wipeRt.anchorMin = new Vector2(0f, 0f); wipeRt.anchorMax = v; },
                    Vector2.one,
                    _wipeInDuration
                ).SetEase(Ease.InQuad)
            );

            // 4. 던전 이름 텍스트 등장
            _dungeonNameText.gameObject.SetActive(true);
            _dungeonSubText.gameObject.SetActive(true);
            _dungeonNameText.text = dungeonName ?? "DUNGEON";
            _dungeonSubText.text = "- 입장 -";
            _dungeonNameText.alpha = 0f;
            _dungeonSubText.alpha = 0f;
            _dungeonNameText.transform.localScale = Vector3.one * 0.6f;

            _currentSequence.Append(
                DOTween.To(
                    () => _dungeonNameText.alpha,
                    a => _dungeonNameText.alpha = a,
                    1f, 0.25f
                )
            );
            _currentSequence.Join(
                _dungeonNameText.transform.DOScale(1.1f, 0.3f).SetEase(Ease.OutBack)
            );
            _currentSequence.Join(
                DOTween.To(
                    () => _dungeonSubText.alpha,
                    a => _dungeonSubText.alpha = a,
                    0.7f, 0.3f
                ).SetDelay(0.1f)
            );

            // 5. 잠시 표시
            _currentSequence.AppendInterval(_textDisplayTime);

            // 6. 텍스트 페이드아웃
            _currentSequence.Append(
                DOTween.To(
                    () => _dungeonNameText.alpha,
                    a => _dungeonNameText.alpha = a,
                    0f, 0.2f
                )
            );
            _currentSequence.Join(
                DOTween.To(
                    () => _dungeonSubText.alpha,
                    a => _dungeonSubText.alpha = a,
                    0f, 0.2f
                )
            );

            // 7. 와이프 아웃 (위에서 아래로 걷어냄)
            _currentSequence.Append(
                DOTween.To(
                    () => wipeRt.anchorMin,
                    v => { wipeRt.anchorMin = v; wipeRt.anchorMax = Vector2.one; },
                    Vector2.one,
                    _wipeOutDuration
                ).SetEase(Ease.OutQuad)
            );

            _currentSequence.OnComplete(() =>
            {
                HideAll();
                isComplete = true;
            });

            await UniTask.WaitUntil(() => isComplete, cancellationToken: ct);
        }

        /// <summary>
        /// 던전 퇴장 연출: 간단한 페이드 아웃/인
        /// </summary>
        private async UniTaskVoid PlayExitEffect()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            KillSequence();

            bool isComplete = false;
            _currentSequence = DOTween.Sequence().SetLink(gameObject);

            // 플래시 효과로 빠져나감
            _flashOverlay.gameObject.SetActive(true);
            _flashOverlay.color = Color.clear;

            _currentSequence.Append(
                DOTween.To(
                    () => _flashOverlay.color,
                    c => _flashOverlay.color = c,
                    new Color(1f, 1f, 1f, 0.6f),
                    0.2f
                ).SetEase(Ease.OutQuad)
            );
            _currentSequence.AppendInterval(0.15f);
            _currentSequence.Append(
                DOTween.To(
                    () => _flashOverlay.color,
                    c => _flashOverlay.color = c,
                    Color.clear,
                    0.4f
                ).SetEase(Ease.InQuad)
            );

            _currentSequence.OnComplete(() =>
            {
                HideAll();
                isComplete = true;
            });

            await UniTask.WaitUntil(() => isComplete, cancellationToken: ct);
        }

        #region UI 생성

        private void EnsureUI()
        {
            // Canvas 확보
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 250; // StageTransitionEffect(200)보다 위
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 플래시 오버레이
            if (_flashOverlay == null)
            {
                var go = CreateUIChild("FlashOverlay");
                _flashOverlay = go.AddComponent<Image>();
                _flashOverlay.color = Color.clear;
                _flashOverlay.raycastTarget = false;
                StretchFull(go.GetComponent<RectTransform>());
            }

            // 와이프 오버레이
            if (_wipeOverlay == null)
            {
                var go = CreateUIChild("WipeOverlay");
                _wipeOverlay = go.AddComponent<Image>();
                _wipeOverlay.color = _wipeColor;
                _wipeOverlay.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(1f, 0f); // 초기: 높이 0
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 던전 이름 텍스트
            if (_dungeonNameText == null)
            {
                var go = CreateUIChild("DungeonNameText");
                _dungeonNameText = go.AddComponent<TextMeshProUGUI>();
                _dungeonNameText.fontSize = 48f;
                _dungeonNameText.alignment = TextAlignmentOptions.Center;
                _dungeonNameText.color = new Color(0.9f, 0.75f, 1f);
                _dungeonNameText.fontStyle = FontStyles.Bold;
                _dungeonNameText.text = "DUNGEON";
                _dungeonNameText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.55f);
                rt.anchorMax = new Vector2(0.5f, 0.55f);
                rt.sizeDelta = new Vector2(600f, 80f);
            }

            // 던전 서브 텍스트
            if (_dungeonSubText == null)
            {
                var go = CreateUIChild("DungeonSubText");
                _dungeonSubText = go.AddComponent<TextMeshProUGUI>();
                _dungeonSubText.fontSize = 24f;
                _dungeonSubText.alignment = TextAlignmentOptions.Center;
                _dungeonSubText.color = new Color(0.7f, 0.6f, 0.8f);
                _dungeonSubText.text = "- 입장 -";
                _dungeonSubText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.45f);
                rt.anchorMax = new Vector2(0.5f, 0.45f);
                rt.sizeDelta = new Vector2(400f, 50f);
            }
        }

        private GameObject CreateUIChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void HideAll()
        {
            if (_flashOverlay != null)
                _flashOverlay.gameObject.SetActive(false);
            if (_wipeOverlay != null)
            {
                _wipeOverlay.gameObject.SetActive(false);
                var rt = _wipeOverlay.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(1f, 0f);
            }
            if (_dungeonNameText != null)
                _dungeonNameText.gameObject.SetActive(false);
            if (_dungeonSubText != null)
                _dungeonSubText.gameObject.SetActive(false);
        }

        #endregion

        private void KillSequence()
        {
            _currentSequence?.Kill();
            _currentSequence = null;
        }

        private void OnDestroy()
        {
            KillSequence();
        }
    }
}
