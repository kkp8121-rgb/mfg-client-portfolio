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
    /// 보스 등장 시 WARNING 연출.
    /// 화면 어둡게 + "WARNING!" 펄스 + 보스 이름 표시 후 전투 시작.
    /// </summary>
    public class BossIntroEffect : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private CanvasGroup _overlayGroup;
        [SerializeField] private TMP_Text _warningText;
        [SerializeField] private TMP_Text _bossNameText;

        private ScreenShakeManager _cachedShaker;
        [SerializeField] private Image _warningIcon;

        [Header("연출 설정")]
        [SerializeField] private float _overlayAlpha = 0.7f;
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private float _warningPulseDuration = 0.3f;
        [SerializeField] private int _warningPulseCount = 3;
        [SerializeField] private float _bossNameShowDelay = 0.8f;
        [SerializeField] private float _introDuration = 1.5f;

        private Sequence _introSequence;
        private bool _isPlaying;

        private void Awake()
        {
            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
                _overlayGroup.gameObject.SetActive(false);
            }
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
            if (evt.DisplayName.Contains("BOSS") && !_isPlaying)
            {
                string bossName = $"Chapter {evt.Chapter} Boss";
                PlayBossIntro(bossName).Forget();
            }
        }

        public async UniTaskVoid PlayBossIntro(string bossName)
        {
            if (_isPlaying || _overlayGroup == null) return;
            _isPlaying = true;

            var ct = this.GetCancellationTokenOnDestroy();

            KillSequence();

            // 초기 상태
            _overlayGroup.alpha = 0f;
            _overlayGroup.gameObject.SetActive(true);

            if (_warningText != null)
            {
                _warningText.alpha = 0f;
                _warningText.transform.localScale = Vector3.one;
            }

            if (_bossNameText != null)
            {
                _bossNameText.alpha = 0f;
                _bossNameText.text = bossName;
            }

            if (_warningIcon != null)
            {
                var iconColor = _warningIcon.color;
                iconColor.a = 0f;
                _warningIcon.color = iconColor;
            }

            // BGM 정지 (긴장감 연출)
            AudioManager.Instance?.StopBgm(0.5f);

            // SFX: 보스 등장 경고음
            AudioManager.Instance?.PlaySfx(SfxType.BossWarning);

            var isComplete = false;
            _introSequence = DOTween.Sequence().SetLink(gameObject);

            // 0. 카메라 줌인 (긴장감)
            var cam = Camera.main;
            float originalSize = 0f;
            if (cam != null)
            {
                originalSize = cam.orthographicSize;
                float zoomedSize = originalSize * 0.85f;
                _introSequence.Append(
                    DOTween.To(() => cam.orthographicSize, x => cam.orthographicSize = x,
                        zoomedSize, _fadeInDuration * 1.5f)
                    .SetEase(Ease.InOutSine)
                );
            }

            // 1. 오버레이 페이드인
            _introSequence.Join(
                DOTween.To(() => _overlayGroup.alpha, x => _overlayGroup.alpha = x, _overlayAlpha, _fadeInDuration)
            );

            // 2. WARNING 텍스트 펄스 (빨간 깜빡) + 화면 쉐이크
            if (_warningText != null)
            {
                _introSequence.AppendCallback(() =>
                {
                    _warningText.alpha = 1f;
                    // 경고 시 쉐이크
                    if (_cachedShaker == null)
                        _cachedShaker = FindFirstObjectByType<ScreenShakeManager>();
                    _cachedShaker?.ShakeBossHit();
                });

                for (int i = 0; i < _warningPulseCount; i++)
                {
                    _introSequence.Append(
                        _warningText.transform.DOPunchScale(
                            Vector3.one * 0.5f,
                            _warningPulseDuration,
                            vibrato: 0,
                            elasticity: 0f
                        )
                    );
                }
            }

            // 3. 경고 아이콘 표시
            if (_warningIcon != null)
            {
                _introSequence.Join(
                    DOTween.To(() => _warningIcon.color.a, x => { var c = _warningIcon.color; c.a = x; _warningIcon.color = c; }, 1f, _fadeInDuration)
                );
            }

            // 4. 보스 이름 표시 (슬라이드 인 + 페이드)
            if (_bossNameText != null)
            {
                _introSequence.AppendInterval(0.2f);

                // 이름 텍스트를 좌측에서 슬라이드 인
                var nameRt = _bossNameText.GetComponent<RectTransform>();
                if (nameRt != null)
                {
                    float origX = nameRt.anchoredPosition.x;
                    nameRt.anchoredPosition = new Vector2(origX - 200f, nameRt.anchoredPosition.y);

                    _introSequence.Append(
                        DOTween.To(() => _bossNameText.alpha, x => _bossNameText.alpha = x, 1f, _fadeInDuration)
                    );
                    _introSequence.Join(
                        DOTween.To(() => nameRt.anchoredPosition, v => nameRt.anchoredPosition = v,
                            new Vector2(origX, nameRt.anchoredPosition.y), _fadeInDuration * 1.5f)
                        .SetEase(Ease.OutCubic)
                    );
                }
                else
                {
                    _introSequence.Append(
                        DOTween.To(() => _bossNameText.alpha, x => _bossNameText.alpha = x, 1f, _fadeInDuration)
                    );
                }
            }

            // 5. 잠시 대기
            _introSequence.AppendInterval(_bossNameShowDelay);

            // 6. 오버레이 페이드아웃
            _introSequence.Append(
                DOTween.To(() => _overlayGroup.alpha, x => _overlayGroup.alpha = x, 0f, _fadeOutDuration)
            );

            if (_warningText != null)
                _introSequence.Join(
                    DOTween.To(() => _warningText.alpha, x => _warningText.alpha = x, 0f, _fadeOutDuration)
                );

            if (_bossNameText != null)
                _introSequence.Join(
                    DOTween.To(() => _bossNameText.alpha, x => _bossNameText.alpha = x, 0f, _fadeOutDuration)
                );

            if (_warningIcon != null)
                _introSequence.Join(
                    DOTween.To(() => _warningIcon.color.a, x => { var c = _warningIcon.color; c.a = x; _warningIcon.color = c; }, 0f, _fadeOutDuration)
                );

            // 카메라 줌아웃 복원
            if (cam != null && originalSize > 0f)
            {
                _introSequence.Append(
                    DOTween.To(() => cam.orthographicSize, x => cam.orthographicSize = x,
                        originalSize, _fadeOutDuration * 1.5f)
                    .SetEase(Ease.OutSine)
                );
            }

            _introSequence.OnComplete(() =>
            {
                _overlayGroup.gameObject.SetActive(false);
                _isPlaying = false;
                isComplete = true;

                // 보스 BGM 시작
                AudioManager.Instance?.PlayBgm(BgmType.Boss, 0.5f);
            });

            // 완료 대기
            await UniTask.WaitUntil(() => isComplete, cancellationToken: ct);
        }

        private void KillSequence()
        {
            _introSequence?.Kill();
            _introSequence = null;
        }

        private void OnDestroy()
        {
            KillSequence();
        }
    }
}
