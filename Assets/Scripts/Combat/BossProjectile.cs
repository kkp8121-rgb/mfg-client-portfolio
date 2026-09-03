using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 보스 직선 투사체. 지정 방향으로 이동하며 플레이어와 충돌 시 데미지.
    /// BossPatternController.SpawnProjectiles()에서 생성.
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private int _damage;
        private Transform _playerTransform;
        private CombatStats _playerStats;
        private float _lifetime = 3f;
        private float _timer;
        private bool _isInitialized;

        private const float COLLISION_RADIUS = 0.4f;

        public void Initialize(Vector2 direction, float speed, int damage,
            Transform playerTransform = null, CombatStats playerStats = null)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _playerTransform = playerTransform;
            _playerStats = playerStats;
            _isInitialized = true;
            _timer = 0f;

            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // 이동
            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);

            // 수명
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // 플레이어 충돌 판정
            if (_playerTransform == null) return;

            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist < COLLISION_RADIUS)
            {
                if (_playerStats != null && !_playerStats.IsDead)
                {
                    _playerStats.TakeDamage(_damage);
                    DamageTextManager.Instance?.Spawn(
                        _playerTransform.position, _damage, false, 0, true);
                }

                Destroy(gameObject);
            }
        }
    }
}
