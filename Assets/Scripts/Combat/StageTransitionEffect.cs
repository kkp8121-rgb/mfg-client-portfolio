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
    /// 스테이지 클리어 / 챕터 전환 연출.
    /// StageChangedEvent를 구독하여 전환 연출을 재생한다.
    /// </summary>
    public class StageTransitionEffect : MonoBehaviour
    {
        [Header("스테이지 클리어 UI")]
        [SerializeField] private CanvasGroup _transitionOverlay;
        [SerializeField] private TMP_Text _stageClearText;
        [SerializeField] private TMP_Text _chapterText;

        [Header("와이프 전환")]
        [SerializeField] private RectTransform _wipeBar;

        [Header("연출 설정")]
        [SerializeField] private float _stageClearScaleDuration = 0.4f;
        [SerializeField] private float _stageClearDisplayTime = 1.0f;
        [SerializeField] private float _stageClearFadeOutDuration = 0.3f;
        [SerializeField] private float _wipeExpandDuration = 0.3f;
        [SerializeField] private float _chapterDisplayTime = 0.8f;
        [SerializeField] private float _wipeShrinkDuration = 0.3f;

        private Sequence _currentSequence;
        private int _lastChapter = -1;
        private bool _isFirstEvent = true;

        private void Awake()
        {
            EnsureComponents();
            HideAll();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            // 최초 이벤트(게임 시작)는 무시
            if (_isFirstEvent)
            {
                _isFirstEvent = false;
                _lastChapter = evt.Chapter;
                return;
            }

            bool isChapterChange = _lastChapter != evt.Chapter && _lastChapter > 0;
            _lastChapter = evt.Chapter;

            if (isChapterChange)
            {
                PlayChapterTransition(evt.Chapter).Forget();
            }
            else if (evt.StageIndex > 1 && !evt.DisplayName.Contains("BOSS"))
            {
                PlayStageClear().Forget();
            }
        }

        private async UniTaskVoid PlayStageClear()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            KillSequence();

            if (_stageClearText == null) return;

            // SFX
            AudioManager.Instance?.PlaySfx(SfxType.StageClear);

            // 초기 상태
            _stageClearText.gameObject.SetActive(true);
            _stageClearText.alpha = 0f;
            _stageClearText.transform.localScale = Vector3.zero;

            var isComplete = false;
            _currentSequence = DOTween.Sequence().SetLink(gameObject);

            // 1. "STAGE CLEAR" 등장: 스케일 0→1.2→1.0 + 페이드인
            _currentSequence.Append(
                _stageClearText.transform.DOScale(1.2f, _stageClearScaleDuration * 0.7f)
                    .SetEase(Ease.OutBack)
            );
            _currentSequence.Join(
                DOTween.To(() => _stageClearText.alpha, x => _stageClearText.alpha = x, 1f, _stageClearScaleDuration * 0.5f)
            );
            _currentSequence.Append(
                _stageClearText.transform.DOScale(1f, _stageClearScaleDuration * 0.3f)
                    .SetEase(Ease.InOutSine)
            );

            // 2. 축하 파티클 (범용 파티클 시스템 우선, fallback으로 로컬)
            _currentSequence.AppendCallback(() =>
            {
                if (ProceduralParticleSystem.Instance != null)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 center = cam.transform.position;
                        center.z = 0f;
                        ProceduralParticleSystem.Instance.SpawnParticles(ParticlePreset.Confetti, center, count: 12);
                    }
                }
                else
                {
                    SpawnCelebrationParticles();
                }
            });

            // 3. 화면 쉐이크
            _currentSequence.AppendCallback(() =>
            {
                var shaker = FindFirstObjectByType<ScreenShakeManager>();
                shaker?.ShakeLevelUp();
            });

            // 4. 잠시 표시
            _currentSequence.AppendInterval(_stageClearDisplayTime);

            // 5. 페이드아웃
            _currentSequence.Append(
                DOTween.To(() => _stageClearText.alpha, x => _stageClearText.alpha = x, 0f, _stageClearFadeOutDuration)
            );

            _currentSequence.OnComplete(() =>
            {
                _stageClearText.gameObject.SetActive(false);
                isComplete = true;
            });

            await UniTask.WaitUntil(() => isComplete, cancellationToken: ct);
        }

        /// <summary>
        /// 스테이지 클리어 시 축하 파티클 (별/스파크가 위에서 내려옴).
        /// </summary>
        private void SpawnCelebrationParticles()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            int particleCount = 12;
            for (int i = 0; i < particleCount; i++)
            {
                var go = new GameObject($"ClearParticle_{i}", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);

                var img = go.AddComponent<Image>();
                img.color = new Color(
                    Random.Range(0.8f, 1f),
                    Random.Range(0.7f, 0.95f),
                    Random.Range(0.1f, 0.4f),
                    1f
                );
                img.raycastTarget = false;

                var rt = go.GetComponent<RectTransform>();
                float size = Random.Range(6f, 14f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchorMin = new Vector2(0.5f, 0.6f);
                rt.anchorMax = new Vector2(0.5f, 0.6f);
                rt.anchoredPosition = new Vector2(Random.Range(-200f, 200f), Random.Range(20f, 80f));

                // 아래로 떨어지며 사라짐
                float fallDist = Random.Range(100f, 250f);
                float duration = Random.Range(0.6f, 1.2f);

                var seq = DOTween.Sequence();
                seq.Append(DOTween.To(
                    () => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    rt.anchoredPosition + new Vector2(Random.Range(-30f, 30f), -fallDist),
                    duration
                ).SetEase(Ease.InQuad));
                seq.Join(go.transform.DOScale(0f, duration * 0.8f).SetDelay(duration * 0.2f));
                seq.Join(DOTween.To(
                    () => img.color.a,
                    x => { var c = img.color; c.a = x; img.color = c; },
                    0f, duration * 0.6f
                ).SetDelay(duration * 0.4f));
                seq.OnComplete(() => Destroy(go));
                seq.SetUpdate(true);
            }
        }

        private async UniTaskVoid PlayChapterTransition(int chapter)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            KillSequence();

            var isComplete = false;
            _currentSequence = DOTween.Sequence().SetLink(gameObject);

            // 와이프 바 사용 가능 시
            if (_wipeBar != null)
            {
                _wipeBar.gameObject.SetActive(true);
                var parentRect = _wipeBar.parent as RectTransform;
                float screenHeight = parentRect != null ? parentRect.rect.height : Screen.height;

                // 초기: 높이 0
                _wipeBar.sizeDelta = new Vector2(_wipeBar.sizeDelta.x, 0f);

                // 1. 와이프 바 확장 (화면 커버)
                _currentSequence.Append(
                    DOTween.To(() => _wipeBar.sizeDelta, s => _wipeBar.sizeDelta = s,
                        new Vector2(_wipeBar.sizeDelta.x, screenHeight),
                        _wipeExpandDuration
                    ).SetEase(Ease.InQuad)
                );
            }

            // 2. 챕터 텍스트 표시
            if (_chapterText != null)
            {
                _chapterText.text = $"Chapter {chapter}";
                _chapterText.gameObject.SetActive(true);
                _chapterText.alpha = 0f;
                _chapterText.transform.localScale = Vector3.one * 0.8f;

                _currentSequence.Append(
                    DOTween.To(() => _chapterText.alpha, x => _chapterText.alpha = x, 1f, 0.2f)
                );
                _currentSequence.Join(
                    _chapterText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack)
                );
            }

            // 3. 대기
            _currentSequence.AppendInterval(_chapterDisplayTime);

            // 4. 챕터 텍스트 페이드아웃
            if (_chapterText != null)
            {
                _currentSequence.Append(
                    DOTween.To(() => _chapterText.alpha, x => _chapterText.alpha = x, 0f, 0.2f)
                );
            }

            // 5. 와이프 바 축소
            if (_wipeBar != null)
            {
                _currentSequence.Append(
                    DOTween.To(() => _wipeBar.sizeDelta, s => _wipeBar.sizeDelta = s,
                        new Vector2(_wipeBar.sizeDelta.x, 0f),
                        _wipeShrinkDuration
                    ).SetEase(Ease.OutQuad)
                );
            }

            _currentSequence.OnComplete(() =>
            {
                if (_wipeBar != null)
                    _wipeBar.gameObject.SetActive(false);
                if (_chapterText != null)
                    _chapterText.gameObject.SetActive(false);
                isComplete = true;
            });

            await UniTask.WaitUntil(() => isComplete, cancellationToken: ct);
        }

        private void HideAll()
        {
            if (_transitionOverlay != null)
            {
                _transitionOverlay.alpha = 0f;
                _transitionOverlay.gameObject.SetActive(false);
            }

            if (_stageClearText != null)
                _stageClearText.gameObject.SetActive(false);

            if (_chapterText != null)
                _chapterText.gameObject.SetActive(false);

            if (_wipeBar != null)
                _wipeBar.gameObject.SetActive(false);
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            if (_transitionOverlay == null)
            {
                var go = CreateUIChild("TransitionOverlay");
                _transitionOverlay = go.AddComponent<CanvasGroup>();
                var img = go.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 1f);
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                _transitionOverlay.alpha = 0f;
            }

            if (_stageClearText == null)
            {
                var go = CreateUIChild("StageClearText");
                _stageClearText = go.AddComponent<TextMeshProUGUI>();
                _stageClearText.fontSize = 36f;
                _stageClearText.alignment = TextAlignmentOptions.Center;
                _stageClearText.color = new Color(1f, 0.85f, 0.1f);
                _stageClearText.fontStyle = FontStyles.Bold;
                _stageClearText.text = "STAGE CLEAR";
                _stageClearText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(400f, 60f);
            }

            if (_chapterText == null)
            {
                var go = CreateUIChild("ChapterText");
                _chapterText = go.AddComponent<TextMeshProUGUI>();
                _chapterText.fontSize = 42f;
                _chapterText.alignment = TextAlignmentOptions.Center;
                _chapterText.color = Color.white;
                _chapterText.fontStyle = FontStyles.Bold;
                _chapterText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(400f, 70f);
            }

            if (_wipeBar == null)
            {
                var go = CreateUIChild("WipeBar");
                _wipeBar = go.GetComponent<RectTransform>();
                var img = go.AddComponent<Image>();
                img.color = new Color(0.05f, 0.05f, 0.1f, 1f);
                img.raycastTarget = false;
                _wipeBar.anchorMin = new Vector2(0f, 0.5f);
                _wipeBar.anchorMax = new Vector2(1f, 0.5f);
                _wipeBar.pivot = new Vector2(0.5f, 0.5f);
                _wipeBar.sizeDelta = new Vector2(0f, 0f);
            }
        }

        private GameObject CreateUIChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void KillSequence()
        {
            _currentSequence?.Kill();
            _currentSequence = null;
        }

        private void OnDestroy()
        {
            KillSequence();
            if (_wipeBar != null)
                DOTween.Kill(_wipeBar);
        }
    }
}
