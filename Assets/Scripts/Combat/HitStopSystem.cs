using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace MkLike.Combat
{
    /// <summary>
    /// 강력한 스킬 히트 시 히트스톱(타임스케일 감소) + 카메라 줌인 연출.
    /// 배율이 높은 스킬일수록 강한 연출을 적용한다.
    /// </summary>
    public class HitStopSystem : MonoBehaviour
    {
        public static HitStopSystem Instance { get; private set; }

        [Header("히트스톱 설정")]
        [SerializeField, Tooltip("히트스톱 시 타임스케일")]
        private float _hitStopTimeScale = 0.1f;
        [SerializeField, Tooltip("히트스톱 최소 배율 임계값 (이 이상일 때 발동)")]
        private float _hitStopThreshold = 2f;
        [SerializeField, Tooltip("기본 히트스톱 지속시간 (실제 시간, 초)")]
        private float _baseHitStopDuration = 0.05f;
        [SerializeField, Tooltip("고배율 히트스톱 지속시간 (실제 시간, 초)")]
        private float _maxHitStopDuration = 0.15f;
        [SerializeField, Tooltip("히트스톱 최대 배율 (이 배율 이상이면 maxDuration 적용)")]
        private float _hitStopMaxMultiplier = 5f;

        [Header("카메라 줌인 설정")]
        [SerializeField, Tooltip("줌인 활성화 여부")]
        private bool _enableZoom = true;
        [SerializeField, Tooltip("배율 2.0일 때 줌인 비율 (0.05 = 5%)")]
        private float _minZoomRate = 0.05f;
        [SerializeField, Tooltip("배율 5.0일 때 줌인 비율 (0.15 = 15%)")]
        private float _maxZoomRate = 0.15f;
        [SerializeField, Tooltip("줌 복원 속도")]
        private float _zoomRestoreSpeed = 8f;

        private Camera _cachedCamera;
        private float _originalOrthoSize;
        private float _targetOrthoSize;
        private bool _isZooming;
        private bool _isHitStopping;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera != null)
                _originalOrthoSize = _cachedCamera.orthographicSize;
        }

        private void Update()
        {
            // 줌 복원
            if (_isZooming && _cachedCamera != null)
            {
                _cachedCamera.orthographicSize = Mathf.Lerp(
                    _cachedCamera.orthographicSize,
                    _targetOrthoSize,
                    _zoomRestoreSpeed * Time.unscaledDeltaTime
                );

                if (Mathf.Abs(_cachedCamera.orthographicSize - _targetOrthoSize) < 0.01f)
                {
                    _cachedCamera.orthographicSize = _targetOrthoSize;
                    if (_targetOrthoSize >= _originalOrthoSize)
                        _isZooming = false;
                }
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            // 타임스케일 복원
            if (_isHitStopping)
                Time.timeScale = 1f;

            // 카메라 줌 복원
            if (_cachedCamera != null && _originalOrthoSize > 0f)
                _cachedCamera.orthographicSize = _originalOrthoSize;

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 히트스톱 + 줌인 연출을 실행한다.
        /// 스킬 배율이 임계값 이상일 때만 발동한다.
        /// </summary>
        /// <param name="skillMultiplier">스킬 데미지 배율</param>
        public void TriggerHitStop(float skillMultiplier)
        {
            if (skillMultiplier < _hitStopThreshold) return;
            if (_isHitStopping) return;

            // 히트스톱 지속시간 보간 (배율에 비례)
            float t = Mathf.InverseLerp(_hitStopThreshold, _hitStopMaxMultiplier, skillMultiplier);
            float duration = Mathf.Lerp(_baseHitStopDuration, _maxHitStopDuration, t);

            // 카메라 줌
            if (_enableZoom && _cachedCamera != null)
            {
                float zoomRate = Mathf.Lerp(_minZoomRate, _maxZoomRate, t);
                float zoomedSize = _originalOrthoSize * (1f - zoomRate);
                _targetOrthoSize = zoomedSize;
                _isZooming = true;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            ExecuteHitStopAsync(duration, _cts.Token).Forget();
        }

        private async UniTaskVoid ExecuteHitStopAsync(float duration, CancellationToken ct)
        {
            _isHitStopping = true;
            Time.timeScale = _hitStopTimeScale;

            try
            {
                // 실제 시간 기준 대기 (unscaled)
                int delayMs = Mathf.RoundToInt(duration * 1000f);
                await UniTask.Delay(delayMs, ignoreTimeScale: true, cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소 시 즉시 복원
            }

            Time.timeScale = 1f;
            _isHitStopping = false;

            // 줌 복원
            if (_isZooming)
                _targetOrthoSize = _originalOrthoSize;
        }
    }
}
