using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 가챠 소환 연출. GachaResultEvent를 구독하여 등급별 카드 연출을 재생한다.
    /// 10연차: 순차 표시 (0.2초 간격) + 마지막에 전체 결과 요약.
    /// </summary>
    public class GachaEffect : MonoBehaviour
    {
        [Header("연출 UI — 단일 카드")]
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private RectTransform _cardTransform;
        [SerializeField] private Image _cardImage;
        [SerializeField] private Image _glowImage;
        [SerializeField] private TMP_Text _gradeText;
        [SerializeField] private TMP_Text _itemNameText;

        [Header("NEW 뱃지 + CP 힌트")]
        [SerializeField] private TMP_Text _newBadgeText;
        [SerializeField] private TMP_Text _cpHintText;

        [Header("10연차 결과 요약")]
        [SerializeField] private RectTransform _summaryRoot;
        [SerializeField] private TMP_Text _summaryText;

        [Header("설정")]
        [SerializeField] private float _autoCloseDelay = 2f;
        [SerializeField] private float _multiPullInterval = 0.2f;
        [SerializeField] private float _queueCollectWindow = 0.15f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color RareColor = new Color(0.3f, 0.5f, 1f);
        private static readonly Color EpicColor = new Color(0.7f, 0.3f, 1f);
        private static readonly Color UniqueColor = new Color(1f, 0.4f, 0.4f);
        private static readonly Color LegendaryColor = new Color(1f, 0.8f, 0f);
        private static readonly Color MythicColor = new Color(1f, 0.2f, 0.2f);

        private readonly Queue<GachaResultEvent> _pendingResults = new();
        private bool _isPlaying;
        private bool _skipRequested;

        /// <summary>가챠 연출이 진행 중인지 여부. GachaCompareSystem에서 대기용으로 참조.</summary>
        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
        }

        private void OnDisable()
        {
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
        }

        private void OnGachaResult(GachaResultEvent e)
        {
            _pendingResults.Enqueue(e);

            if (!_isPlaying)
                ProcessQueue().Forget();
        }

        /// <summary>
        /// 큐에 쌓인 결과를 처리한다.
        /// 10연차는 동기적으로 큐에 10개가 쌓이므로, 짧은 대기 후 일괄 처리한다.
        /// </summary>
        private async UniTaskVoid ProcessQueue()
        {
            _isPlaying = true;
            var token = this.GetCancellationTokenOnDestroy();

            // 짧은 대기: PullTen이 동기 루프에서 10개를 발행하므로 다음 프레임까지 대기
            await UniTask.Delay(
                (int)(_queueCollectWindow * 1000f),
                ignoreTimeScale: true,
                cancellationToken: token);

            // 큐 스냅샷
            var batch = new List<GachaResultEvent>();
            while (_pendingResults.Count > 0)
                batch.Add(_pendingResults.Dequeue());

            if (batch.Count == 0)
            {
                _isPlaying = false;
                return;
            }

            bool isMultiPull = batch.Count > 1;

            // ── 소환 연출 (GachaSummonEffect) ──
            var summonEffect = GachaSummonEffect.Instance;
            if (summonEffect != null)
            {
                if (isMultiPull)
                {
                    // 10연차: 최고 등급 기준 짧은 연출
                    string highestGrade = GetHighestGrade(batch);
                    await summonEffect.PlaySummonBriefAsync(highestGrade, token);
                }
                else
                {
                    // 단일 뽑기: 풀 연출 (터치 대기 포함)
                    var single = batch[0];
                    string itemName = single.ItemId;
                    await summonEffect.PlaySummonAsync(single.Grade, itemName, token);
                }
            }

            // 오버레이 ON
            _overlay.alpha = 0f;
            _overlay.gameObject.SetActive(true);
            _overlay.blocksRaycasts = true;
            if (_summaryRoot != null)
                _summaryRoot.gameObject.SetActive(false);

            await DOTween.To(() => _overlay.alpha, x => _overlay.alpha = x, 0.7f, 0.3f)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: token);

            // 순차 카드 연출
            for (int i = 0; i < batch.Count; i++)
            {
                var result = batch[i];
                _skipRequested = false;

                await PlaySingleCard(result.Grade, result.ItemId, isMultiPull, token);

                // 10연차: 터치 스킵 또는 짧은 간격 대기
                if (isMultiPull && i < batch.Count - 1)
                {
                    float waited = 0f;
                    float waitTime = _multiPullInterval;
                    while (waited < waitTime && !_skipRequested)
                    {
                        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                        {
                            _skipRequested = true;
                            break;
                        }
                        waited += Time.unscaledDeltaTime;
                        await UniTask.Yield(cancellationToken: token);
                    }
                }
            }

            // 10연차: 전체 결과 요약 표시
            if (isMultiPull)
            {
                AudioManager.Instance?.PlaySfx(SfxType.GachaMultiResult);
                await ShowMultiPullSummary(batch, token);
            }

            // 단일 뽑기: 터치/클릭 대기 또는 자동 닫기
            if (!isMultiPull)
            {
                float elapsed = 0f;
                _skipRequested = false;
                while (elapsed < _autoCloseDelay && !_skipRequested)
                {
                    if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                    {
                        _skipRequested = true;
                        break;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    await UniTask.Yield(cancellationToken: token);
                }
            }

            // 오버레이 페이드아웃
            var closeSeq = DOTween.Sequence()
                .Join(DOTween.To(() => _overlay.alpha, x => _overlay.alpha = x, 0f, 0.25f))
                .Join(_cardTransform.DOScale(0f, 0.2f).SetEase(Ease.InBack))
                .Join(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0f, 0.2f))
                .SetUpdate(true)
                .SetLink(gameObject);
            await closeSeq.ToUniTask(cancellationToken: token);

            _overlay.blocksRaycasts = false;
            _overlay.gameObject.SetActive(false);
            if (_summaryRoot != null)
                _summaryRoot.gameObject.SetActive(false);
            _isPlaying = false;

            // 처리 중 새로 쌓인 이벤트가 있으면 재처리
            if (_pendingResults.Count > 0)
                ProcessQueue().Forget();
        }

        /// <summary>
        /// 단일 카드 연출을 재생한다.
        /// </summary>
        private async UniTask PlaySingleCard(string grade, string itemId, bool isMultiPull,
            System.Threading.CancellationToken token)
        {
            var gradeColor = GetGradeColor(grade);

            // 카드 초기 상태
            _cardTransform.localScale = Vector3.zero;
            _cardTransform.localRotation = Quaternion.identity;
            _glowImage.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0f);
            _gradeText.text = "";
            _itemNameText.text = "";
            if (_newBadgeText != null) _newBadgeText.gameObject.SetActive(false);
            if (_cpHintText != null) _cpHintText.gameObject.SetActive(false);

            // 등급별 SFX 재생
            AudioManager.Instance?.PlaySfx(GetGradeRevealSfx(grade));

            // 10연차는 축약 연출, 단일은 풀 연출
            if (isMultiPull)
            {
                await PlayCompactEffect(grade, gradeColor, token);
            }
            else
            {
                await PlayFullEffect(grade, gradeColor, token);
            }

            // 결과 텍스트 표시
            _gradeText.text = GetGradeLabel(grade);
            _gradeText.color = gradeColor;
            _itemNameText.text = itemId;

            _gradeText.transform.localScale = Vector3.zero;
            _itemNameText.transform.localScale = Vector3.zero;

            // NEW 뱃지 (에픽 이상)
            if (_newBadgeText != null)
            {
                bool isHighGrade = grade is "Epic" or "Unique" or "Legendary" or "Mythic";
                _newBadgeText.gameObject.SetActive(isHighGrade);
                if (isHighGrade)
                {
                    _newBadgeText.text = "NEW!";
                    _newBadgeText.color = new Color(1f, 0.85f, 0.1f);
                    _newBadgeText.transform.localScale = Vector3.zero;
                }
            }

            // CP 힌트 (에픽 이상)
            if (_cpHintText != null)
            {
                bool showHint = grade is "Epic" or "Unique" or "Legendary" or "Mythic";
                _cpHintText.gameObject.SetActive(showHint);
                if (showHint)
                {
                    _cpHintText.text = "장착하면 전투력 UP!";
                    _cpHintText.color = new Color(0.4f, 1f, 0.6f);
                    _cpHintText.transform.localScale = Vector3.zero;
                }
            }

            var textSeq = DOTween.Sequence()
                .Append(_gradeText.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack))
                .Append(_itemNameText.transform.DOScale(1f, 0.1f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetLink(gameObject);

            // NEW 뱃지 + CP 힌트 연출 (텍스트 뒤에 이어서)
            if (_newBadgeText != null && _newBadgeText.gameObject.activeSelf)
            {
                textSeq.Append(_newBadgeText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutElastic));
            }
            if (_cpHintText != null && _cpHintText.gameObject.activeSelf)
            {
                textSeq.Append(_cpHintText.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
            }

            await textSeq.ToUniTask(cancellationToken: token);
        }

        /// <summary>
        /// 10연차용 축약 연출. 등급에 따라 빠르게 재생.
        /// </summary>
        private async UniTask PlayCompactEffect(string grade, Color gradeColor,
            System.Threading.CancellationToken token)
        {
            float duration = grade switch
            {
                "Legendary" or "Mythic" => 0.6f,
                "Unique" => 0.5f,
                "Epic" => 0.4f,
                _ => 0.25f
            };

            _glowImage.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0f);

            var seq = DOTween.Sequence()
                .Join(_cardTransform.DORotate(new Vector3(0f, 360f, 0f), duration, RotateMode.FastBeyond360))
                .Join(_cardTransform.DOScale(1f, duration * 0.8f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, grade is "Normal" or "Rare" ? 0.3f : 0.7f, duration * 0.6f))
                .SetUpdate(true)
                .SetLink(gameObject);
            await seq.ToUniTask(cancellationToken: token);

            if (grade is "Legendary" or "Mythic" or "Unique")
            {
                await _cardTransform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 6)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .ToUniTask(cancellationToken: token);
            }
        }

        /// <summary>
        /// 단일 뽑기용 풀 연출.
        /// </summary>
        private async UniTask PlayFullEffect(string grade, Color gradeColor,
            System.Threading.CancellationToken token)
        {
            switch (grade)
            {
                case "Normal":
                    await PlayNormalEffect(token);
                    break;
                case "Rare":
                    await PlayRareEffect(gradeColor, token);
                    break;
                case "Epic":
                    await PlayEpicEffect(gradeColor, token);
                    break;
                case "Unique":
                    await PlayUniqueEffect(gradeColor, token);
                    break;
                case "Legendary":
                case "Mythic":
                    await PlayLegendaryEffect(gradeColor, token);
                    break;
                default:
                    await PlayNormalEffect(token);
                    break;
            }
        }

        /// <summary>
        /// 10연차 전체 결과 요약을 표시하고, 터치/클릭 또는 자동 닫기를 대기한다.
        /// </summary>
        private async UniTask ShowMultiPullSummary(List<GachaResultEvent> batch,
            System.Threading.CancellationToken token)
        {
            if (_summaryRoot == null || _summaryText == null)
                return;

            // 카드 숨기기
            await _cardTransform.DOScale(0f, 0.15f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: token);

            // 요약 텍스트 구성
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<size=120%><b>소환 결과 ({batch.Count}회)</b></size>");
            sb.AppendLine();

            // 등급별 카운트
            var gradeCounts = new Dictionary<string, int>();
            for (int i = 0; i < batch.Count; i++)
            {
                string g = batch[i].Grade;
                if (!gradeCounts.ContainsKey(g))
                    gradeCounts[g] = 0;
                gradeCounts[g]++;
            }

            string[] gradeOrder = { "Mythic", "Legendary", "Unique", "Epic", "Rare", "Normal" };
            for (int i = 0; i < gradeOrder.Length; i++)
            {
                string g = gradeOrder[i];
                if (gradeCounts.ContainsKey(g))
                {
                    var color = ColorUtility.ToHtmlStringRGB(GetGradeColor(g));
                    sb.AppendLine($"<color=#{color}>{GetGradeLabel(g)}</color> x{gradeCounts[g]}");
                }
            }

            _summaryText.text = sb.ToString();
            _summaryRoot.localScale = Vector3.zero;
            _summaryRoot.gameObject.SetActive(true);

            await _summaryRoot.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: token);

            // 터치/클릭 대기 또는 자동 닫기
            float elapsed = 0f;
            _skipRequested = false;
            while (elapsed < _autoCloseDelay && !_skipRequested)
            {
                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                {
                    _skipRequested = true;
                    break;
                }
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(cancellationToken: token);
            }
        }

        #region 단일 뽑기 등급별 연출

        private async UniTask PlayNormalEffect(System.Threading.CancellationToken token)
        {
            var seq = DOTween.Sequence()
                .Join(_cardTransform.DORotate(new Vector3(0f, 360f, 0f), 0.3f, RotateMode.FastBeyond360))
                .Join(_cardTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetLink(gameObject);
            await seq.ToUniTask(cancellationToken: token);
        }

        private async UniTask PlayRareEffect(Color glowColor, System.Threading.CancellationToken token)
        {
            _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            var seq = DOTween.Sequence()
                .Join(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.6f, 0.3f))
                .Join(_cardTransform.DORotate(new Vector3(0f, 360f, 0f), 0.5f, RotateMode.FastBeyond360))
                .Join(_cardTransform.DOScale(1f, 0.4f).SetEase(Ease.OutBack))
                .Append(_cardTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5))
                .SetUpdate(true)
                .SetLink(gameObject);
            await seq.ToUniTask(cancellationToken: token);
        }

        private async UniTask PlayEpicEffect(Color glowColor, System.Threading.CancellationToken token)
        {
            _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            var glowPulse = DOTween.Sequence()
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.8f, 0.25f))
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.3f, 0.25f))
                .SetLoops(2)
                .SetUpdate(true)
                .SetLink(gameObject);

            var cardAnim = DOTween.Sequence()
                .Join(_cardTransform.DORotate(new Vector3(0f, 720f, 0f), 1.0f, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutCubic))
                .Join(_cardTransform.DOScale(1f, 0.5f).SetEase(Ease.OutBack))
                .Append(_cardTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 6))
                .SetUpdate(true)
                .SetLink(gameObject);

            await UniTask.WhenAll(
                glowPulse.ToUniTask(cancellationToken: token),
                cardAnim.ToUniTask(cancellationToken: token)
            );
        }

        private async UniTask PlayUniqueEffect(Color glowColor, System.Threading.CancellationToken token)
        {
            _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            var glowPulse = DOTween.Sequence()
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 1f, 0.2f))
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.4f, 0.2f))
                .SetLoops(3)
                .SetUpdate(true)
                .SetLink(gameObject);

            var cardAnim = DOTween.Sequence()
                .Join(_cardTransform.DORotate(new Vector3(0f, 1080f, 0f), 1.2f, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutCubic))
                .Join(_cardTransform.DOScale(1f, 0.5f).SetEase(Ease.OutElastic))
                .Append(_cardTransform.DOPunchScale(Vector3.one * 0.25f, 0.4f, 8))
                .SetUpdate(true)
                .SetLink(gameObject);

            await UniTask.WhenAll(
                glowPulse.ToUniTask(cancellationToken: token),
                cardAnim.ToUniTask(cancellationToken: token)
            );
        }

        private async UniTask PlayLegendaryEffect(Color glowColor, System.Threading.CancellationToken token)
        {
            _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            var glowRect = _glowImage.rectTransform;
            var originalGlowScale = glowRect.localScale;

            glowRect.localScale = Vector3.one * 0.5f;
            var glowAnim = DOTween.Sequence()
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 1f, 0.15f))
                .Join(glowRect.DOScale(originalGlowScale * 1.5f, 0.4f).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.5f, 0.15f))
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 1f, 0.15f))
                .Append(DOTween.To(() => _glowImage.color.a, x => { var c = _glowImage.color; c.a = x; _glowImage.color = c; }, 0.6f, 0.15f))
                .SetUpdate(true)
                .SetLink(gameObject);

            var cardAnim = DOTween.Sequence()
                .Join(_cardTransform.DORotate(new Vector3(0f, 1080f, 0f), 1.5f, RotateMode.FastBeyond360)
                    .SetEase(Ease.InOutCubic))
                .Join(_cardTransform.DOScale(1f, 0.6f).SetEase(Ease.OutElastic))
                .SetUpdate(true)
                .SetLink(gameObject);

            var overlayRect = _overlay.GetComponent<RectTransform>();
            var shakeAnim = DOTween.Sequence()
                .AppendInterval(0.3f)
                .Append(overlayRect.DOShakePosition(0.4f, 15f, 20, 90f, false, true))
                .SetUpdate(true)
                .SetLink(gameObject);

            await UniTask.WhenAll(
                glowAnim.ToUniTask(cancellationToken: token),
                cardAnim.ToUniTask(cancellationToken: token),
                shakeAnim.ToUniTask(cancellationToken: token)
            );

            await _cardTransform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 10)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: token);

            glowRect.localScale = originalGlowScale;
        }

        #endregion

        private static string GetGradeRevealSfx(string grade)
        {
            return grade switch
            {
                "Mythic" => "sfx_gacha_reveal_mythic",
                "Legendary" => "sfx_gacha_reveal_legendary",
                "Unique" => "sfx_gacha_reveal_unique",
                "Epic" => "sfx_gacha_reveal_epic",
                "Rare" => "sfx_gacha_reveal_rare",
                _ => "sfx_gacha_reveal_normal"
            };
        }

        private static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "Normal" => NormalColor,
                "Rare" => RareColor,
                "Epic" => EpicColor,
                "Unique" => UniqueColor,
                "Legendary" => LegendaryColor,
                "Mythic" => MythicColor,
                _ => NormalColor
            };
        }

        private static string GetGradeLabel(string grade)
        {
            return grade switch
            {
                "Normal" => "일반",
                "Rare" => "희귀",
                "Epic" => "에픽",
                "Unique" => "유니크",
                "Legendary" => "전설",
                "Mythic" => "신화",
                _ => grade
            };
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 250;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // --- Overlay: full-screen dimmed background ---
            if (_overlay == null)
            {
                var overlayGo = CreateUIChild("Overlay");
                _overlay = overlayGo.AddComponent<CanvasGroup>();
                var overlayImg = overlayGo.AddComponent<Image>();
                overlayImg.color = new Color(0f, 0f, 0f, 0.7f);
                overlayImg.raycastTarget = true;
                var overlayRt = overlayGo.GetComponent<RectTransform>();
                overlayRt.anchorMin = Vector2.zero;
                overlayRt.anchorMax = Vector2.one;
                overlayRt.offsetMin = Vector2.zero;
                overlayRt.offsetMax = Vector2.zero;
                _overlay.alpha = 0f;
                _overlay.gameObject.SetActive(false);
            }

            // All card elements are children of overlay
            var overlayTransform = _overlay.transform;

            // --- Glow: behind card, centered ---
            if (_glowImage == null)
            {
                var go = new GameObject("Glow", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _glowImage = go.AddComponent<Image>();
                _glowImage.color = new Color(1f, 1f, 1f, 0f);
                _glowImage.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(340f, 420f);
            }

            // --- Card: centered display area ---
            if (_cardTransform == null)
            {
                var cardGo = new GameObject("Card", typeof(RectTransform));
                cardGo.transform.SetParent(overlayTransform, false);
                _cardTransform = cardGo.GetComponent<RectTransform>();
                _cardTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _cardTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _cardTransform.pivot = new Vector2(0.5f, 0.5f);
                _cardTransform.anchoredPosition = Vector2.zero;
                _cardTransform.sizeDelta = new Vector2(220f, 300f);
            }

            // --- Card background image ---
            if (_cardImage == null)
            {
                _cardImage = _cardTransform.GetComponent<Image>();
                if (_cardImage == null) _cardImage = _cardTransform.gameObject.AddComponent<Image>();
                _cardImage.color = Color.white;
                var tm = UIThemeManager.Instance;
                if (tm != null) tm.ApplyFrameBackground(_cardImage);
                else _cardImage.color = new Color(0.15f, 0.13f, 0.25f, 0.95f);
                _cardImage.raycastTarget = false;
            }

            // --- Grade text: above card center ---
            if (_gradeText == null)
            {
                var go = new GameObject("GradeText", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _gradeText = go.AddComponent<TextMeshProUGUI>();
                _gradeText.fontSize = 32f;
                _gradeText.fontStyle = FontStyles.Bold;
                _gradeText.alignment = TextAlignmentOptions.Center;
                _gradeText.color = Color.white;
                _gradeText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 190f);
                rt.sizeDelta = new Vector2(300f, 50f);
            }

            // --- Item name text: below card ---
            if (_itemNameText == null)
            {
                var go = new GameObject("ItemNameText", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _itemNameText = go.AddComponent<TextMeshProUGUI>();
                _itemNameText.fontSize = 22f;
                _itemNameText.alignment = TextAlignmentOptions.Center;
                _itemNameText.color = Color.white;
                _itemNameText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -180f);
                rt.sizeDelta = new Vector2(300f, 40f);
            }

            // --- NEW badge: top-right of card ---
            if (_newBadgeText == null)
            {
                var go = new GameObject("NewBadgeText", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _newBadgeText = go.AddComponent<TextMeshProUGUI>();
                _newBadgeText.fontSize = 24f;
                _newBadgeText.fontStyle = FontStyles.Bold;
                _newBadgeText.alignment = TextAlignmentOptions.Center;
                _newBadgeText.color = new Color(1f, 0.85f, 0.1f);
                _newBadgeText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(130f, 160f);
                rt.sizeDelta = new Vector2(80f, 40f);
                go.SetActive(false);
            }

            // --- CP hint text: below item name ---
            if (_cpHintText == null)
            {
                var go = new GameObject("CpHintText", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _cpHintText = go.AddComponent<TextMeshProUGUI>();
                _cpHintText.fontSize = 18f;
                _cpHintText.alignment = TextAlignmentOptions.Center;
                _cpHintText.color = new Color(0.4f, 1f, 0.6f);
                _cpHintText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -220f);
                rt.sizeDelta = new Vector2(300f, 36f);
                go.SetActive(false);
            }

            // --- Summary root: centered panel for 10-pull results ---
            if (_summaryRoot == null)
            {
                var go = new GameObject("SummaryRoot", typeof(RectTransform));
                go.transform.SetParent(overlayTransform, false);
                _summaryRoot = go.GetComponent<RectTransform>();
                _summaryRoot.anchorMin = new Vector2(0.5f, 0.5f);
                _summaryRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _summaryRoot.pivot = new Vector2(0.5f, 0.5f);
                _summaryRoot.anchoredPosition = Vector2.zero;
                _summaryRoot.sizeDelta = new Vector2(420f, 340f);

                var bg = go.AddComponent<Image>();
                bg.color = Color.white;
                var tm2 = UIThemeManager.Instance;
                if (tm2 != null) tm2.ApplyPanelBackground(bg);
                else bg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);
                bg.raycastTarget = false;

                go.SetActive(false);
            }

            // --- Summary text: fills summary root ---
            if (_summaryText == null)
            {
                var go = new GameObject("SummaryText", typeof(RectTransform));
                go.transform.SetParent(_summaryRoot, false);
                _summaryText = go.AddComponent<TextMeshProUGUI>();
                _summaryText.fontSize = 20f;
                _summaryText.alignment = TextAlignmentOptions.Center;
                _summaryText.color = Color.white;
                _summaryText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(20f, 20f);
                rt.offsetMax = new Vector2(-20f, -20f);
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        /// <summary>
        /// 배치에서 최고 등급을 반환한다. 10연차 소환 연출에 사용.
        /// </summary>
        private static string GetHighestGrade(List<GachaResultEvent> batch)
        {
            string[] gradeRank = { "Mythic", "Legendary", "Unique", "Epic", "Rare", "Normal" };
            string highest = "Normal";
            int highestIdx = gradeRank.Length - 1;

            for (int i = 0; i < batch.Count; i++)
            {
                for (int g = 0; g < gradeRank.Length; g++)
                {
                    if (batch[i].Grade == gradeRank[g] && g < highestIdx)
                    {
                        highestIdx = g;
                        highest = gradeRank[g];
                        break;
                    }
                }
                if (highestIdx == 0) break; // Mythic — 더 높은 등급 없음
            }

            return highest;
        }

        private void OnDestroy()
        {
            if (_cardTransform != null) DOTween.Kill(_cardTransform);
            if (_glowImage != null) DOTween.Kill(_glowImage.rectTransform);
            if (_overlay != null) DOTween.Kill(_overlay);
            if (_newBadgeText != null) _newBadgeText.transform.DOKill();
            if (_cpHintText != null) _cpHintText.transform.DOKill();
            var overlayRect = _overlay != null ? _overlay.GetComponent<RectTransform>() : null;
            if (overlayRect != null) DOTween.Kill(overlayRect);
            if (_summaryRoot != null)
                DOTween.Kill(_summaryRoot);
        }
    }
}
