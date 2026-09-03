using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.Combat
{
    /// <summary>
    /// 전직 시 빛 기둥 + 텍스트 연출을 재생한다.
    /// EventBus&lt;JobChangedEvent&gt;를 구독하여 자동 트리거된다.
    /// </summary>
    public class JobAdvanceEffect : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _overlayGroup;
        [SerializeField] private RectTransform _lightPillar;
        [SerializeField] private TMP_Text _advanceText;

        private Image _lightPillarImage;

        /// <summary>전직 티어별 보너스 비율 (JobSystem과 동일)</summary>
        private static readonly float[] TierBonusPercents = { 0.05f, 0.08f, 0.12f, 0.15f };

        /// <summary>보너스 텍스트 (월드 스페이스 TMP, 동적 생성)</summary>
        private TextMeshPro _bonusTmp;

        private void Awake()
        {
            if (_lightPillar != null)
                _lightPillarImage = _lightPillar.GetComponent<Image>();

            if (_overlayGroup != null)
                _overlayGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            EventBus<JobChangedEvent>.Subscribe(OnJobChanged);
        }

        private void OnDisable()
        {
            EventBus<JobChangedEvent>.Unsubscribe(OnJobChanged);
        }

        private void OnJobChanged(JobChangedEvent e)
        {
            PlayAdvanceEffect(e).Forget();
        }

        private async UniTaskVoid PlayAdvanceEffect(JobChangedEvent e)
        {
            if (_overlayGroup == null) return;

            var ct = destroyCancellationToken;

            // 이전/현재 직업 표시 이름
            string prevName = GetJobDisplayName(e.PreviousJobId, e.PreviousTier);
            string currName = GetJobDisplayName(e.CurrentJobId, e.CurrentTier);

            // 4차 전직 직업별 색상
            Color pillarColor = Color.white;
            if (e.CurrentTier == 4)
            {
                pillarColor = GetTier4Color(e.CurrentJobId);
            }

            if (_lightPillarImage != null)
                _lightPillarImage.color = new Color(pillarColor.r, pillarColor.g, pillarColor.b, 0f);

            // SFX
            AudioManager.Instance?.PlaySfx(SfxType.JobAdvance);

            // 1) 오버레이 페이드인
            _overlayGroup.gameObject.SetActive(true);
            var fadeInComplete = false;
            DOTween.To(() => _overlayGroup.alpha, x => _overlayGroup.alpha = x, 0.6f, 0.3f)
                .SetLink(gameObject)
                .OnComplete(() => fadeInComplete = true);
            await UniTask.WaitUntil(() => fadeInComplete, cancellationToken: ct);

            // 2) 빛 기둥 스케일 + 페이드
            if (_lightPillar != null)
            {
                _lightPillar.localScale = new Vector3(0.3f, 3f, 1f);
                if (_lightPillarImage != null)
                    _lightPillarImage.color = new Color(pillarColor.r, pillarColor.g, pillarColor.b, 0f);

                var pillarComplete = false;
                var seq = DOTween.Sequence()
                    .Join(_lightPillar.DOScale(new Vector3(3f, 30f, 1f), 0.5f).SetEase(Ease.OutBack))
                    .Join(_lightPillarImage != null
                        ? DOTween.To(() => _lightPillarImage.color.a, x => { var c = _lightPillarImage.color; c.a = x; _lightPillarImage.color = c; }, 1f, 0.5f)
                        : DOTween.Sequence())
                    .SetLink(gameObject)
                    .OnComplete(() => pillarComplete = true);

                await UniTask.WaitUntil(() => pillarComplete, cancellationToken: ct);
            }

            // 3) 텍스트 표시
            if (_advanceText != null)
            {
                _advanceText.text = $"{prevName} → {currName}";
                _advanceText.transform.localScale = Vector3.zero;
                _advanceText.gameObject.SetActive(true);

                var scaleUpComplete = false;
                _advanceText.transform.DOScale(1.2f, 0.3f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject)
                    .OnComplete(() => scaleUpComplete = true);
                await UniTask.WaitUntil(() => scaleUpComplete, cancellationToken: ct);

                var scaleDownComplete = false;
                _advanceText.transform.DOScale(1f, 0.15f)
                    .SetLink(gameObject)
                    .OnComplete(() => scaleDownComplete = true);
                await UniTask.WaitUntil(() => scaleDownComplete, cancellationToken: ct);
            }

            // 4) 전직 보너스 텍스트 표시
            await ShowBonusText(e.CurrentTier, ct);

            // 5) 잠시 유지
            await UniTask.Delay(1200, cancellationToken: ct);

            // 6) 전체 페이드아웃
            var fadeOutComplete = false;
            var fadeOut = DOTween.Sequence()
                .Join(DOTween.To(() => _overlayGroup.alpha, x => _overlayGroup.alpha = x, 0f, 0.4f));

            if (_lightPillarImage != null)
                fadeOut.Join(DOTween.To(() => _lightPillarImage.color.a, x => { var c = _lightPillarImage.color; c.a = x; _lightPillarImage.color = c; }, 0f, 0.4f));

            if (_advanceText != null)
                fadeOut.Join(DOTween.To(() => _advanceText.alpha, x => _advanceText.alpha = x, 0f, 0.4f));

            if (_bonusTmp != null && _bonusTmp.gameObject.activeSelf)
                fadeOut.Join(DOTween.To(() => _bonusTmp.alpha, x => _bonusTmp.alpha = x, 0f, 0.4f));

            fadeOut.SetLink(gameObject)
                .OnComplete(() => fadeOutComplete = true);
            await UniTask.WaitUntil(() => fadeOutComplete, cancellationToken: ct);

            _overlayGroup.gameObject.SetActive(false);
            if (_advanceText != null)
                _advanceText.gameObject.SetActive(false);
            if (_bonusTmp != null)
                _bonusTmp.gameObject.SetActive(false);
        }

        private async UniTask ShowBonusText(int tier, System.Threading.CancellationToken ct)
        {
            if (tier <= 0 || tier > TierBonusPercents.Length) return;

            float bonusPercent = TierBonusPercents[tier - 1] * 100f;

            EnsureBonusTmp();
            if (_bonusTmp == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 bonusPos = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.4f, 10f));
            bonusPos.z = 0f;
            _bonusTmp.transform.position = bonusPos;
            _bonusTmp.text = $"<color=#FFD700>ATK / DEF / HP +{bonusPercent:F0}%!</color>";
            _bonusTmp.alpha = 0f;
            _bonusTmp.transform.localScale = Vector3.one * 0.8f;
            _bonusTmp.gameObject.SetActive(true);

            // 페이드인 + 펀치스케일
            var complete = false;
            var seq = DOTween.Sequence()
                .Append(DOTween.To(() => _bonusTmp.alpha, a => _bonusTmp.alpha = a, 1f, 0.25f))
                .Join(_bonusTmp.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.3f))
                .SetLink(gameObject)
                .OnComplete(() => complete = true);

            await UniTask.WaitUntil(() => complete, cancellationToken: ct);
        }

        private void EnsureBonusTmp()
        {
            if (_bonusTmp != null) return;

            var obj = new GameObject("JobAdvanceBonusTMP");
            obj.transform.SetParent(transform);
            _bonusTmp = obj.AddComponent<TextMeshPro>();
            _bonusTmp.fontSize = 3.5f;
            _bonusTmp.fontStyle = FontStyles.Bold;
            _bonusTmp.alignment = TextAlignmentOptions.Center;
            _bonusTmp.textWrappingMode = TextWrappingModes.NoWrap;
            _bonusTmp.sortingOrder = 210;
            _bonusTmp.rectTransform.sizeDelta = new Vector2(10f, 1.5f);
            _bonusTmp.outlineWidth = 0.2f;
            _bonusTmp.outlineColor = new Color32(0, 0, 0, 180);

            var font = TMP_Settings.defaultFontAsset;
            if (font != null) _bonusTmp.font = font;

            obj.SetActive(false);
        }

        // 직업별 티어별 표시 이름 (JobSystem과 동일, 어셈블리 의존성 회피)
        private static readonly string[,] JobDisplayNames =
        {
            { "견습 전사", "나이트", "워로드", "타이탄", "드래곤 슬레이어" },
            { "견습 궁수", "스카우트", "윈드워커", "호크아이", "스톰브링어" },
            { "견습 마법사", "소서러", "세이지", "룬마스터", "아크메이지" }
        };

        private static string GetJobDisplayName(string jobId, int tier)
        {
            int jobIndex;
            switch (jobId)
            {
                case "warrior": jobIndex = 0; break;
                case "archer": jobIndex = 1; break;
                case "mage": jobIndex = 2; break;
                default: return jobId;
            }

            int clampedTier = Mathf.Clamp(tier, 0, 4);
            return JobDisplayNames[jobIndex, clampedTier];
        }

        private static Color GetTier4Color(string jobId)
        {
            switch (jobId)
            {
                case "warrior": return new Color(1f, 0.3f, 0.3f); // 빨강
                case "archer": return new Color(0.3f, 1f, 0.3f);  // 초록
                case "mage": return new Color(0.6f, 0.3f, 1f);    // 보라
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (_lightPillar != null) DOTween.Kill(_lightPillar);
            if (_advanceText != null) _advanceText.transform.DOKill();
            if (_bonusTmp != null) _bonusTmp.transform.DOKill();
        }
    }
}
