using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// HUD 요소의 부드러운 애니메이션을 관리.
    /// HP바 잔상, 경험치 트윈, 스킬 쿨타임, 재화 팝 등.
    /// </summary>
    public class HudAnimator : MonoBehaviour
    {
        [Header("HP바")]
        [SerializeField] private Image _hpFill;
        [SerializeField] private Image _hpTrailFill;
        [SerializeField] private float _trailDelay = 0.5f;
        [SerializeField] private float _trailSpeed = 0.3f;
        [SerializeField] private TMP_Text _hpWarningText;

        [Header("경험치바")]
        [SerializeField] private Image _expFill;
        [SerializeField] private float _expAnimDuration = 0.4f;

        [Header("스킬 쿨타임")]
        [SerializeField] private Image[] _skillCooldownFills;
        [SerializeField] private Image[] _skillReadyFlash;

        [Header("재화 변동")]
        [SerializeField] private TMP_Text _currencyPopText;

        private Tween _hpTrailTween;
        private Tween _hpBlinkTween;
        private Tween _expTween;
        private Tween _currencyPopTween;

        private static readonly Color ColorHpHigh = new Color(0.2f, 0.85f, 0.3f, 1f);
        private static readonly Color ColorHpMid = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color ColorHpLow = new Color(0.9f, 0.15f, 0.15f, 1f);
        private static readonly Color ColorExpFlash = new Color(1f, 0.85f, 0.1f, 1f);

        /// <summary>
        /// HP바 갱신. 즉시 감소 + 잔상 지연 추적 + 색상 변화 + 저HP 깜빡임.
        /// </summary>
        public void SetHP(float ratio01)
        {
            float ratio = Mathf.Clamp01(ratio01);

            // 즉시 감소 (빨간 바)
            if (_hpFill != null)
            {
                _hpFill.fillAmount = ratio;

                // 색상: >0.66 초록, >0.33 노랑, else 빨강
                if (ratio > 0.66f)
                    _hpFill.color = ColorHpHigh;
                else if (ratio > 0.33f)
                    _hpFill.color = ColorHpMid;
                else
                    _hpFill.color = ColorHpLow;
            }

            // 잔상 바 (흰색, 지연 후 천천히 추적)
            if (_hpTrailFill != null)
            {
                _hpTrailTween?.Kill();
                _hpTrailTween = DOTween.To(() => _hpTrailFill.fillAmount, x => _hpTrailFill.fillAmount = x, ratio, _trailSpeed)
                    .SetDelay(_trailDelay)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 저HP 깜빡임 (25% 이하)
            if (_hpFill != null)
            {
                if (ratio < 0.25f && ratio > 0f)
                {
                    if (_hpBlinkTween == null || !_hpBlinkTween.IsActive())
                    {
                        _hpBlinkTween = DOTween.To(() => _hpFill.color.a, x => { var c = _hpFill.color; c.a = x; _hpFill.color = c; }, 0.4f, 0.3f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetUpdate(true)
                            .SetLink(gameObject);
                    }
                }
                else
                {
                    if (_hpBlinkTween != null && _hpBlinkTween.IsActive())
                    {
                        _hpBlinkTween.Kill();
                        _hpBlinkTween = null;
                        var c = _hpFill.color;
                        c.a = 1f;
                        _hpFill.color = c;
                    }
                }
            }

            // 저HP 경고 텍스트
            if (_hpWarningText != null)
                _hpWarningText.gameObject.SetActive(ratio < 0.25f && ratio > 0f);
        }

        /// <summary>
        /// 경험치바 갱신. 부드러운 증가 + 레벨업 시 황금 플래시.
        /// </summary>
        public void SetExp(float ratio01)
        {
            float ratio = Mathf.Clamp01(ratio01);

            if (_expFill == null) return;

            _expTween?.Kill();

            // 레벨업 감지: ratio가 0으로 리셋되면 플래시
            bool isLevelUp = ratio < 0.01f && _expFill.fillAmount > 0.5f;

            _expTween = DOTween.To(() => _expFill.fillAmount, x => _expFill.fillAmount = x, ratio, _expAnimDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (isLevelUp)
            {
                // 황금 플래시: yellow → white (0.2초)
                var origColor = _expFill.color;
                DOTween.Sequence()
                    .Append(ColorTweenHelper.To(_expFill, ColorExpFlash, 0.1f))
                    .Append(ColorTweenHelper.To(_expFill, Color.white, 0.1f))
                    .Append(ColorTweenHelper.To(_expFill, origColor, 0.15f))
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 스킬 쿨타임 원형 표시 갱신.
        /// ratio01: 0=쿨타임 없음, 1=쿨타임 완료(사용 가능).
        /// </summary>
        public void UpdateSkillCooldown(int slot, float ratio01)
        {
            if (_skillCooldownFills == null || slot < 0 || slot >= _skillCooldownFills.Length)
                return;

            var fill = _skillCooldownFills[slot];
            if (fill == null) return;

            fill.fillAmount = Mathf.Clamp01(ratio01);

            // 쿨타임 완료 시 플래시
            if (ratio01 >= 1f && _skillReadyFlash != null && slot < _skillReadyFlash.Length)
            {
                var flash = _skillReadyFlash[slot];
                if (flash == null) return;

                // 이미 플래시 중이면 스킵
                if (flash.color.a > 0.01f) return;

                DOTween.Sequence()
                    .Append(DOTween.To(() => flash.color.a, x => { var c = flash.color; c.a = x; flash.color = c; }, 1f, 0.1f))
                    .Append(DOTween.To(() => flash.color.a, x => { var c = flash.color; c.a = x; flash.color = c; }, 0f, 0.2f))
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 재화 변동 팝 텍스트 표시.
        /// </summary>
        public void ShowCurrencyPop(string text, Color color)
        {
            if (_currencyPopText == null) return;

            _currencyPopTween?.Kill();

            _currencyPopText.text = text;
            _currencyPopText.color = new Color(color.r, color.g, color.b, 1f);
            _currencyPopText.transform.localScale = Vector3.zero;
            _currencyPopText.gameObject.SetActive(true);

            var cg = _currencyPopText.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = _currencyPopText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            _currencyPopTween = DOTween.Sequence()
                // 팝: 0 → 1.2 → 1.0
                .Append(_currencyPopText.transform.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack))
                .Append(_currencyPopText.transform.DOScale(1f, 0.1f).SetEase(Ease.InOutSine))
                // 유지
                .AppendInterval(0.3f)
                // 페이드아웃
                .Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.4f))
                .OnComplete(() => _currencyPopText.gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _hpTrailTween?.Kill();
            _hpBlinkTween?.Kill();
            _expTween?.Kill();
            _currencyPopTween?.Kill();

            if (_hpFill != null) DOTween.Kill(_hpFill);
            if (_hpTrailFill != null) DOTween.Kill(_hpTrailFill);
            if (_expFill != null) DOTween.Kill(_expFill);
            if (_currencyPopText != null) _currencyPopText.transform.DOKill();

            if (_skillCooldownFills != null)
            {
                for (int i = 0; i < _skillCooldownFills.Length; i++)
                {
                    if (_skillCooldownFills[i] != null)
                        DOTween.Kill(_skillCooldownFills[i]);
                }
            }

            if (_skillReadyFlash != null)
            {
                for (int i = 0; i < _skillReadyFlash.Length; i++)
                {
                    if (_skillReadyFlash[i] != null)
                        DOTween.Kill(_skillReadyFlash[i]);
                }
            }
        }
    }
}
