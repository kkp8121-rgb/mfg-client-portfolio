using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 몬스터 LOD (Level of Detail) 시스템.
    /// 화면 밖 몬스터 애니메이션 비활성화 + 먼 거리 애니메이션 속도 감소.
    /// 가까운 몬스터: 풀 애니메이션.
    /// 먼 몬스터: 애니메이션 속도 절반.
    /// 화면 밖: 애니메이션 완전 비활성화.
    /// 알파는 건드리지 않음 — 타겟팅 혼란 방지 (사망 페이드는 MonsterController가 담당).
    /// </summary>
    public class MonsterLOD : MonoBehaviour
    {
        [Header("LOD 거리 설정")]
        [SerializeField] private float _nearDistance = 8f;
        [SerializeField] private float _farDistance = 15f;
        [SerializeField] private float _farAnimSpeed = 0.5f;

        private Animator _animator;
        private CharacterAnimBridge _animBridge;
        private bool _isVisible = true;
        private LODLevel _currentLOD = LODLevel.Full;

        private Transform _playerTransform;
        private float _lodCheckTimer;

        private const float LOD_CHECK_INTERVAL = 0.3f;

        public enum LODLevel
        {
            Full,       // 풀 디테일
            Reduced,    // 간소화 (먼 거리)
            Hidden      // 화면 밖
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _animBridge = GetComponent<CharacterAnimBridge>();
        }

        private void OnBecameVisible()
        {
            _isVisible = true;
            if (_currentLOD == LODLevel.Hidden)
                SetLODLevel(LODLevel.Full);
        }

        private void OnBecameInvisible()
        {
            _isVisible = false;
            SetLODLevel(LODLevel.Hidden);
        }

        private void Update()
        {
            if (!_isVisible) return;

            _lodCheckTimer -= Time.deltaTime;
            if (_lodCheckTimer > 0f) return;
            _lodCheckTimer = LOD_CHECK_INTERVAL;

            UpdateLOD();
        }

        // 2026-04-23 정적 캐시: 스폰되는 모든 MonsterLOD가 공유 → 첫 1회만 Find
        private static Transform _sharedPlayerTransform;

        private void UpdateLOD()
        {
            if (_playerTransform == null)
            {
                if (_sharedPlayerTransform == null)
                {
                    var player = FindAnyObjectByType<PlayerCharacter>();
                    if (player != null) _sharedPlayerTransform = player.transform;
                }
                _playerTransform = _sharedPlayerTransform;
                if (_playerTransform == null) return;
            }

            float dist = Vector2.Distance(transform.position, _playerTransform.position);

            if (dist <= _nearDistance)
            {
                if (_currentLOD != LODLevel.Full)
                    SetLODLevel(LODLevel.Full);
            }
            else if (dist <= _farDistance)
            {
                if (_currentLOD != LODLevel.Reduced)
                    SetLODLevel(LODLevel.Reduced);
            }
            // farDistance 초과이지만 화면 안이면 Reduced 유지
            else if (_currentLOD != LODLevel.Reduced)
            {
                SetLODLevel(LODLevel.Reduced);
            }
        }

        private void SetLODLevel(LODLevel level)
        {
            _currentLOD = level;

            switch (level)
            {
                case LODLevel.Full:
                    if (_animator != null)
                    {
                        _animator.enabled = true;
                        _animator.speed = 1f;
                    }
                    break;

                case LODLevel.Reduced:
                    if (_animator != null)
                    {
                        _animator.enabled = true;
                        _animator.speed = _farAnimSpeed;
                    }
                    break;

                case LODLevel.Hidden:
                    if (_animator != null)
                        _animator.enabled = false;
                    break;
            }
        }

        /// <summary>
        /// 풀 리셋에서 호출 (OnSpawnFromPool 등).
        /// </summary>
        public void ResetLOD()
        {
            _currentLOD = LODLevel.Full;
            _isVisible = true;
            _lodCheckTimer = 0f;

            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.speed = 1f;
            }
        }

        /// <summary>
        /// 플레이어 참조를 외부에서 설정한다 (MonsterSpawner에서 호출 가능).
        /// </summary>
        public void SetPlayer(Transform player)
        {
            _playerTransform = player;
        }

        public bool IsVisible => _isVisible;
        public LODLevel CurrentLOD => _currentLOD;
    }
}
