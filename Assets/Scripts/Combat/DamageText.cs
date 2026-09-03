using UnityEngine;
using TMPro;
using DG.Tweening;
using Lean.Pool;
using MkLike.Core;

namespace MkLike.Combat
{
    public enum DamageTextType
    {
        Normal,
        Critical,
        Heal,
        Buff,
        Miss,
        Blocked,
        Combo,
        HeavyHit,
        Overkill,
        BossHit,
        Penetrating
    }

    /// <summary>
    /// 메이플스토리 스타일 데미지 텍스트.
    /// 위로 튀어오르며 페이드아웃. LeanPool 풀링 지원.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageText : MonoBehaviour, IPoolable
    {
        [Header("애니메이션 설정")]
        [SerializeField] private float _floatHeight = 0.4f;
        [SerializeField] private float _duration = 0.45f;
        [SerializeField] private float _popScale = 1.3f;
        [SerializeField] private float _popDuration = 0.1f;

        [Header("크리티컬 설정")]
        [SerializeField] private float _critScaleMultiplier = 1.5f;

        private TextMeshPro _tmp;
        private Sequence _sequence;

        // 유형별 색상
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color CritColor = new Color(1f, 0.843f, 0f, 1f);         // #FFD700
        private static readonly Color HealColor = new Color(0f, 1f, 0.533f, 1f);       // #00FF88
        private static readonly Color BuffColor = new Color(0.533f, 0.867f, 1f, 1f);    // #88DDFF
        private static readonly Color MissColor = new Color(0.6f, 0.6f, 0.6f, 1f);      // #999999
        private static readonly Color BlockedColor = new Color(1f, 0.867f, 0f, 1f);     // #FFDD00
        private static readonly Color ComboColorStart = new Color(1f, 0.55f, 0.1f, 1f); // 주황
        private static readonly Color ComboColorEnd = new Color(1f, 0.2f, 0.2f, 1f);    // 빨강
        private static readonly Color HeavyHitColor = new Color(1f, 0.85f, 0f, 1f);     // 금노랑
        private static readonly Color OverkillColor = new Color(1f, 0.84f, 0f, 1f);     // 골드
        private static readonly Color BossHitColor = new Color(1f, 0.267f, 0.267f, 1f); // #FF4444
        private static readonly Color PenetratingColor = new Color(0.667f, 0.267f, 1f, 1f); // #AA44FF

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.sortingOrder = 200;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>
        /// 데미지 텍스트를 초기화하고 애니메이션을 실행한다. (하위 호환)
        /// </summary>
        public void Play(int damage, bool isCritical, float yOffset)
        {
            var type = isCritical ? DamageTextType.Critical : DamageTextType.Normal;
            Setup(NumberFormatter.FormatKorean(damage), type, 0, yOffset);
        }

        /// <summary>
        /// 유형별 텍스트를 설정하고 애니메이션을 실행한다.
        /// </summary>
        public void Setup(string text, DamageTextType type, int comboCount = 0, float yOffset = 0f)
        {
            _tmp.text = text;

            float baseScale;
            float duration;
            Color color;

            switch (type)
            {
                case DamageTextType.Critical:
                    color = CritColor;
                    baseScale = _critScaleMultiplier;
                    duration = _duration;
                    break;
                case DamageTextType.Heal:
                    color = HealColor;
                    baseScale = 1f;
                    duration = _duration * 1.2f;
                    break;
                case DamageTextType.Buff:
                    color = BuffColor;
                    baseScale = 1f;
                    duration = _duration;
                    break;
                case DamageTextType.Miss:
                    color = MissColor;
                    baseScale = 0.7f;
                    duration = 0.3f;
                    break;
                case DamageTextType.Blocked:
                    color = BlockedColor;
                    baseScale = 1.1f;
                    duration = _duration;
                    break;
                case DamageTextType.Combo:
                    float t = Mathf.Clamp01(comboCount / 20f);
                    color = Color.Lerp(ComboColorStart, ComboColorEnd, t);
                    baseScale = 1f + Mathf.Min(comboCount * 0.02f, 0.3f);
                    duration = _duration;
                    break;
                case DamageTextType.HeavyHit:
                    color = HeavyHitColor;
                    baseScale = 1.1f;
                    duration = _duration;
                    break;
                case DamageTextType.Overkill:
                    color = OverkillColor;
                    baseScale = 1.2f;
                    duration = _duration * 1.2f;
                    break;
                case DamageTextType.BossHit:
                    color = BossHitColor;
                    baseScale = 2.0f;
                    duration = _duration * 1.2f;
                    break;
                case DamageTextType.Penetrating:
                    color = PenetratingColor;
                    baseScale = 1.1f;
                    duration = _duration;
                    break;
                default:
                    color = NormalColor;
                    baseScale = 1f;
                    duration = _duration;
                    break;
            }

            _tmp.color = color;
            transform.localScale = Vector3.one * baseScale;

            float randomX = Random.Range(-0.3f, 0.3f);
            Vector3 startPos = transform.position + new Vector3(randomX, yOffset, 0f);
            transform.position = startPos;

            _sequence?.Kill();
            _sequence = DOTween.Sequence();

            if (type == DamageTextType.Blocked)
            {
                // Blocked: 흔들림 연출
                _sequence.Append(
                    transform.DOShakePosition(0.2f, 0.15f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic)
                );
            }
            else if (type == DamageTextType.Critical)
            {
                // 크리티컬: 팝 스케일 + 쉐이크
                _sequence.Append(
                    transform.DOScale(baseScale * _popScale, _popDuration)
                        .SetEase(Ease.OutQuad)
                );
                _sequence.Append(
                    transform.DOScale(baseScale, _popDuration)
                        .SetEase(Ease.InQuad)
                );
                _sequence.Append(
                    transform.DOPunchPosition(new Vector3(0.08f, 0.08f, 0f), 0.15f, 14, 1f)
                );
            }
            else
            {
                // 팝 스케일 (살짝 커졌다 원래 크기로)
                _sequence.Append(
                    transform.DOScale(baseScale * _popScale, _popDuration)
                        .SetEase(Ease.OutQuad)
                );
                _sequence.Append(
                    transform.DOScale(baseScale, _popDuration)
                        .SetEase(Ease.InQuad)
                );
            }

            // 위로 떠오르기 (Heal은 부드러운 OutSine)
            Ease floatEase = type == DamageTextType.Heal ? Ease.OutSine : Ease.OutCubic;
            _sequence.Join(
                transform.DOMoveY(startPos.y + _floatHeight, duration)
                    .SetEase(floatEase)
            );

            // 페이드아웃 (후반부에 시작)
            _sequence.Insert(duration * 0.5f,
                DOTween.To(() => _tmp.alpha, x => _tmp.alpha = x, 0f, duration * 0.5f)
                    .SetEase(Ease.InQuad)
            );

            _sequence.SetLink(gameObject);
            _sequence.OnComplete(ReturnToPool);
        }

        private void ReturnToPool()
        {
            LeanPool.Despawn(gameObject);
        }

        // ── Lean.Pool.IPoolable ──

        public void OnSpawn()
        {
            _tmp = GetComponent<TextMeshPro>();
            _tmp.alpha = 1f;
            transform.localScale = Vector3.one;
        }

        public void OnDespawn()
        {
            _sequence?.Kill();
            _sequence = null;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }
    }
}
