using UnityEngine;
using DG.Tweening;

namespace MkLike.Combat
{
    /// <summary>
    /// 화면 흔들림을 통합 관리한다.
    /// 강도별 프리셋(Light/Medium/Heavy)과 커스텀 Shake를 제공한다.
    /// 중복 방지: 현재 흔들림보다 강도가 높을 때만 교체한다.
    /// </summary>
    public class ScreenShakeManager : MonoBehaviour
    {
        public static ScreenShakeManager Instance { get; private set; }

        private bool _isShaking;
        private float _currentIntensity;
        private Tween _shakeTween;
        // 2026-04-23 쿨다운: 연속 쉐이크 호출 시 0.1s 최소 간격 (의도적 강도 상승만 허용)
        private float _nextAllowedAt;
        private const float MIN_SHAKE_INTERVAL = 0.1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            _shakeTween?.Kill();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 커스텀 강도/지속시간으로 화면을 흔든다.
        /// 이미 흔들림 중이면 강도가 더 높을 때만 교체한다.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (_isShaking && intensity <= _currentIntensity) return;
            // 0.1s 쿨다운 (연속 이벤트 spam 방지). 강도 상승은 통과.
            if (Time.unscaledTime < _nextAllowedAt && intensity <= _currentIntensity) return;

            var cam = Camera.main;
            if (cam == null) return;

            _shakeTween?.Kill();
            _isShaking = true;
            _currentIntensity = intensity;
            _nextAllowedAt = Time.unscaledTime + MIN_SHAKE_INTERVAL;

            _shakeTween = cam.transform.DOShakePosition(duration, intensity, 10, 90f, false)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _isShaking = false;
                    _currentIntensity = 0f;
                })
                .SetLink(cam.gameObject);
        }

        /// <summary>
        /// 약한 흔들림. 일반 히트용.
        /// </summary>
        public void ShakeLight()
        {
            Shake(0.08f, 0.06f);
        }

        /// <summary>
        /// 중간 흔들림. 크리티컬 히트용.
        /// </summary>
        public void ShakeMedium()
        {
            Shake(0.18f, 0.1f);
        }

        /// <summary>
        /// 강한 흔들림. 보스 등장, 궁극기용.
        /// </summary>
        public void ShakeHeavy()
        {
            Shake(0.4f, 0.2f);
        }

        /// <summary>
        /// 보스 히트 흔들림. 보스가 공격당할 때.
        /// </summary>
        public void ShakeBossHit()
        {
            Shake(0.25f, 0.15f);
        }

        /// <summary>
        /// 원킬/오버킬 흔들림. 강렬한 일격 연출.
        /// </summary>
        public void ShakeOverkill()
        {
            Shake(0.5f, 0.25f);
        }

        /// <summary>
        /// 궁극기 발동 흔들림. 긴 지속 + 높은 강도.
        /// </summary>
        public void ShakeUltimate()
        {
            Shake(0.6f, 0.35f);
        }

        /// <summary>
        /// 레벨업 흔들림. 짧고 가벼운 축하 진동.
        /// </summary>
        public void ShakeLevelUp()
        {
            Shake(0.12f, 0.15f);
        }

        /// <summary>
        /// 전직 흔들림. 강하고 긴 연출.
        /// </summary>
        public void ShakeJobAdvance()
        {
            Shake(0.45f, 0.4f);
        }
    }
}
