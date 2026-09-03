using DG.Tweening;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 피격 시 스프라이트 흰색 플래시 + 넉백 연출.
    /// 몬스터/플레이어에 부착하여 사용한다.
    /// </summary>
    public class HitFlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _flashDuration = 0.08f;
        [SerializeField] private float _knockbackStrength = 0.15f;

        private Color _originalColor;
        private bool _isFlashing;

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            if (_renderer != null)
                _originalColor = _renderer.color;
        }

        /// <summary>
        /// 피격 플래시 + 넉백 + 스케일 팝 연출을 재생한다.
        /// </summary>
        /// <param name="hitDirection">피격 방향 (공격자 → 피격자)</param>
        public void PlayHitFlash(Vector2 hitDirection)
        {
            if (_renderer == null) return;
            if (_isFlashing) return;

            _isFlashing = true;

            // 색상 플래시: 흰색으로 번쩍 → 원래 색상으로 복귀
            var seq = DOTween.Sequence();
            seq.Append(
                ColorTweenHelper.To(_renderer, Color.white, _flashDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
            );
            seq.Append(
                ColorTweenHelper.To(_renderer, _originalColor, _flashDuration * 0.7f)
                    .SetEase(Ease.InQuad)
            );

            // 스케일 팝: 살짝 커졌다 원래 크기로 (피격 임팩트)
            seq.Join(
                transform.DOPunchScale(Vector3.one * 0.12f, _flashDuration, 4, 0.5f)
            );

            seq.OnComplete(() => _isFlashing = false);
            seq.SetLink(gameObject);

            // 피격 밀림 제거됨 (유저 요청 — 몬스터 넉백 비활성화)
        }

        /// <summary>
        /// 원래 색상을 갱신한다. 스프라이트 색상이 런타임에 변경될 경우 호출.
        /// </summary>
        public void SetOriginalColor(Color color)
        {
            _originalColor = color;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
