using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 아레나 맵 경계 및 스폰 위치 관리.
    /// 직사각형 아레나 영역을 정의하고, 몬스터/보스/플레이어 스폰 위치와 경계 클램핑을 제공한다.
    /// </summary>
    public class ArenaMap : MonoBehaviour
    {
        [Header("아레나 영역")]
        [SerializeField] private Vector2 _arenaCenter = Vector2.zero;
        [SerializeField] private Vector2 _arenaSize = new Vector2(20f, 20f);

        [Header("스폰 설정")]
        [Tooltip("스폰 마진 — 아레나 경계에서 안쪽으로 이 거리만큼 떨어진 곳에서 스폰")]
        [SerializeField] private float _spawnMargin = 1f;

        [Header("플레이어 스폰")]
        [SerializeField] private Vector2 _playerSpawnOffset = Vector2.zero;

        [Header("보스 스폰")]
        [SerializeField] private Vector2 _bossSpawnOffset = new Vector2(0f, 5f);

        /// <summary>아레나 중심 좌표 반환.</summary>
        public Vector2 GetArenaCenter() => _arenaCenter;

        /// <summary>아레나 경계 Rect 반환.</summary>
        public Rect GetArenaBounds()
        {
            return new Rect(
                _arenaCenter.x - _arenaSize.x * 0.5f,
                _arenaCenter.y - _arenaSize.y * 0.5f,
                _arenaSize.x,
                _arenaSize.y
            );
        }

        /// <summary>위치를 아레나 경계 내로 클램핑.</summary>
        public Vector2 ClampPosition(Vector2 position)
        {
            Rect bounds = GetArenaBounds();
            float x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            float y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            return new Vector2(x, y);
        }

        /// <summary>아레나 가장자리에서 랜덤 스폰 위치 반환 (몬스터용).</summary>
        public Vector2 GetSpawnPosition()
        {
            Rect bounds = GetArenaBounds();

            // 4변 중 하나를 랜덤 선택
            int edge = Random.Range(0, 4);

            float x, y;
            switch (edge)
            {
                case 0: // 상단
                    x = Random.Range(bounds.xMin + _spawnMargin, bounds.xMax - _spawnMargin);
                    y = bounds.yMax - _spawnMargin;
                    break;
                case 1: // 하단
                    x = Random.Range(bounds.xMin + _spawnMargin, bounds.xMax - _spawnMargin);
                    y = bounds.yMin + _spawnMargin;
                    break;
                case 2: // 좌측
                    x = bounds.xMin + _spawnMargin;
                    y = Random.Range(bounds.yMin + _spawnMargin, bounds.yMax - _spawnMargin);
                    break;
                default: // 우측
                    x = bounds.xMax - _spawnMargin;
                    y = Random.Range(bounds.yMin + _spawnMargin, bounds.yMax - _spawnMargin);
                    break;
            }

            return new Vector2(x, y);
        }

        /// <summary>플레이어 스폰 월드 좌표 반환.</summary>
        public Vector2 GetPlayerSpawnWorld()
        {
            return _arenaCenter + _playerSpawnOffset;
        }

        /// <summary>보스 스폰 월드 좌표 반환.</summary>
        public Vector2 GetBossSpawnWorld()
        {
            return _arenaCenter + _bossSpawnOffset;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube((Vector3)_arenaCenter, (Vector3)_arenaSize);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere((Vector3)GetPlayerSpawnWorld(), 0.3f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere((Vector3)GetBossSpawnWorld(), 0.3f);
        }
#endif
    }
}
