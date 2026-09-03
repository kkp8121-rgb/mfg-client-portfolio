using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 로딩 화면 싱글톤. 씬 전환/던전 입장 시 Show/Hide로 제어.
    /// 가짜 진행 바 (0→0.9) + 랜덤 팁 + 회전 스피너.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _loadingBar;
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private RectTransform _spinnerImage;

        [SerializeField] private float _fadeDuration = 0.3f;

        private Tween _spinnerTween;
        private Tween _barTween;
        private Tween _fadeTween;

        // 2026-04-23 제거된 시스템 팁 삭제 (코스튬/등반/비밀방) + 현재 기획 반영
        private static readonly string[] Tips =
        {
            "매일 접속하면 출석 보상을 받을 수 있습니다",
            "장비 강화는 13성부터 파괴 리스크가 있습니다",
            "레벨이 오르면 자유 스탯 포인트를 획득합니다",
            "환생하면 영구 보너스를 획득합니다",
            "도감을 채우면 숨겨진 보너스가 있습니다",
            "4차 전직은 레벨 160 달성 시 자동 진행됩니다",
            "스킬 슬롯 4개를 순차 강화하면 전투력이 크게 증가합니다",
            "가이드 퀘스트를 따라가면 초반 진행이 빨라집니다",
            "정예 소환 포인트는 몬스터 처치로 쌓입니다",
            "배틀패스 보상은 매 시즌 리셋됩니다"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _canvasGroup.gameObject.SetActive(false);
            }
        }

        public void Show(string tip = null)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            // 페이드인 (0→1)
            _fadeTween?.Kill();
            _canvasGroup.alpha = 0f;
            _fadeTween = DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, _fadeDuration)
                .SetUpdate(true)
                .SetLink(gameObject);

            // 스피너 회전
            if (_spinnerImage != null)
            {
                _spinnerTween?.Kill();
                _spinnerImage.localRotation = Quaternion.identity;
                _spinnerTween = _spinnerImage
                    .DORotate(new Vector3(0f, 0f, -360f), 1f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 가짜 진행 바 (0→0.9, 2초) — 실제 로딩과 무관
            if (_loadingBar != null)
            {
                _barTween?.Kill();
                _loadingBar.fillAmount = 0f;
                _barTween = DOTween.To(() => _loadingBar.fillAmount, x => _loadingBar.fillAmount = x, 0.9f, 2f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 팁 텍스트
            if (_tipText != null)
                _tipText.text = tip ?? Tips[Random.Range(0, Tips.Length)];
        }

        public void Hide()
        {
            if (_canvasGroup == null) return;

            _barTween?.Kill();
            _fadeTween?.Kill();

            var seq = DOTween.Sequence();

            // 진행 바를 1로 완성 (0.2초)
            if (_loadingBar != null)
            {
                seq.Append(DOTween.To(() => _loadingBar.fillAmount, x => _loadingBar.fillAmount = x, 1f, 0.2f)
                    .SetEase(Ease.OutCubic));
            }

            // 페이드아웃 (1→0, 0.3초)
            seq.Append(DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, _fadeDuration)
                .SetEase(Ease.InCubic));

            seq.OnComplete(() =>
            {
                _spinnerTween?.Kill();
                _spinnerTween = null;
                if (_canvasGroup != null)
                {
                    _canvasGroup.blocksRaycasts = false;
                    _canvasGroup.interactable = false;
                    _canvasGroup.gameObject.SetActive(false);
                }
            });

            seq.SetUpdate(true).SetLink(gameObject);
        }

        public void SetProgress(float progress)
        {
            _barTween?.Kill();
            _barTween = null;
            if (_loadingBar != null)
            {
                DOTween.To(() => _loadingBar.fillAmount, x => _loadingBar.fillAmount = x, Mathf.Clamp01(progress), 0.2f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
            _spinnerTween?.Kill();
            _barTween?.Kill();
            if (_canvasGroup != null) DOTween.Kill(_canvasGroup);
            if (_loadingBar != null) DOTween.Kill(_loadingBar);
            if (_spinnerImage != null) DOTween.Kill(_spinnerImage);

            if (Instance == this) Instance = null;
        }
    }
}
