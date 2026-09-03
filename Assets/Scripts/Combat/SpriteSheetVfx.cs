using System.Collections.Generic;
using UnityEngine;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 스프라이트 시트 플립북 VFX 재생 컴포넌트.
    /// 오브젝트 풀링 내장. SpriteSheetVfxSO 기반 재생.
    /// desiredWorldSize로 스프라이트를 원하는 월드 크기에 맞춘다.
    /// </summary>
    public class SpriteSheetVfx : MonoBehaviour
    {
        private const int VFX_SORTING_ORDER = 100;
        // 2026-04-23 AoE 피크 대응: 40 몬스터 히트 VFX = key별 40+ 요청
        private const int MAX_POOL_PER_KEY = 40;

        private static readonly Dictionary<int, Queue<SpriteSheetVfx>> _pool = new();

        // 머터리얼 캐시 (매번 new Material 방지)
        private static Material _defaultMat;

        private SpriteRenderer _sr;
        private SpriteSheetVfxSO _data;
        private int _soId;
        private float _timer;
        private int _frameIndex;
        private bool _playing;
        private float _maxLifetime;
        private float _lifeTimer;

        #region 풀링 API

        /// <summary>
        /// 스프라이트 시트 VFX를 스폰한다 (풀링 적용).
        /// </summary>
        /// <param name="data">VFX 데이터 SO</param>
        /// <param name="position">월드 위치</param>
        /// <param name="desiredWorldSize">원하는 월드 크기 (유닛). 스프라이트를 이 크기에 맞춘다. 0이면 SO 기본 스케일 사용.</param>
        /// <param name="parent">부모 트랜스폼 (버프 오라 등)</param>
        /// <param name="maxLifetime">강제 수명 (0=자동)</param>
        /// <param name="rotation">Z축 회전 각도 (도)</param>
        /// <param name="flipX">X축 반전 (타겟이 왼쪽일 때)</param>
        public static SpriteSheetVfx Spawn(
            SpriteSheetVfxSO data,
            Vector3 position,
            float desiredWorldSize = 0f,
            Transform parent = null,
            float maxLifetime = 0f,
            float rotation = 0f,
            bool flipX = false)
        {
            if (data == null)
                return null;

            // frames가 비어있으면 프로시져럴 폴백 이펙트 사용
            if (data.frames == null || data.frames.Length == 0)
            {
                SpawnProceduralFallback(data, position, desiredWorldSize, rotation);
                return null;
            }

            position.z = 0f;
            int soId = data.GetInstanceID();
            SpriteSheetVfx instance = null;

            if (_pool.TryGetValue(soId, out var queue))
            {
                // 파괴된 오브젝트를 스킵하면서 유효한 것을 찾는다
                while (queue.Count > 0)
                {
                    var candidate = queue.Dequeue();
                    if (candidate != null && candidate.gameObject != null)
                    {
                        instance = candidate;
                        break;
                    }
                    // 파괴된 오브젝트 — 스킵
                }

                if (instance != null)
                {
                    instance.transform.position = position;
                    instance.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
                    instance.gameObject.SetActive(true);
                }
            }

            if (instance == null)
            {
                var go = new GameObject(data.name);
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

                instance = go.AddComponent<SpriteSheetVfx>();
                instance._sr = go.AddComponent<SpriteRenderer>();
                instance._soId = soId;
            }

            if (parent != null)
                instance.transform.SetParent(parent, true);
            else if (instance.transform.parent != null)
                instance.transform.SetParent(null);

            instance.Setup(data, desiredWorldSize, maxLifetime, flipX);
            return instance;
        }

        /// <summary>풀 전체를 비운다 (씬 전환 시).</summary>
        public static void ClearPool()
        {
            foreach (var queue in _pool.Values)
            {
                while (queue.Count > 0)
                {
                    var vfx = queue.Dequeue();
                    if (vfx != null) Destroy(vfx.gameObject);
                }
            }
            _pool.Clear();
        }

        #endregion

        #region 초기화/재생

        private void Setup(SpriteSheetVfxSO data, float desiredWorldSize, float maxLifetime, bool flipX = false)
        {
            _data = data;
            _frameIndex = 0;
            _timer = 0f;
            _playing = true;

            _sr.sprite = data.frames[0];
            _sr.sortingOrder = VFX_SORTING_ORDER;
            _sr.color = data.defaultTint;
            _sr.flipX = data.ignoreFlip ? false : flipX;
            _sr.flipY = data.flipY;

            if (_defaultMat == null)
                _defaultMat = new Material(Shader.Find("Sprites/Default"));
            _sr.sharedMaterial = _defaultMat;

            // 스케일: 스프라이트 실제 크기 → 원하는 월드 크기로 변환
            float finalScale;
            if (desiredWorldSize > 0f)
            {
                float spriteWorldSize = GetSpriteWorldSize(data.frames[0]);
                finalScale = (desiredWorldSize / spriteWorldSize) * data.defaultScale;
            }
            else
            {
                finalScale = data.defaultScale;
            }
            transform.localScale = Vector3.one * finalScale;

            // 수명
            if (maxLifetime > 0f)
                _maxLifetime = maxLifetime;
            else if (!data.loop)
                _maxLifetime = data.Duration;
            else
                _maxLifetime = 5f;

            _lifeTimer = _maxLifetime;
        }

        /// <summary>스프라이트의 월드 크기 (긴 축 기준).</summary>
        private static float GetSpriteWorldSize(Sprite sprite)
        {
            if (sprite == null) return 1f;
            var bounds = sprite.bounds;
            return Mathf.Max(bounds.size.x, bounds.size.y);
        }

        private void Update()
        {
            if (!_playing || _data == null) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }

            // 프레임 진행
            _timer += Time.deltaTime;
            float frameInterval = 1f / _data.fps;

            if (_timer >= frameInterval)
            {
                _timer -= frameInterval;
                _frameIndex++;

                if (_frameIndex >= _data.frames.Length)
                {
                    if (_data.loop)
                        _frameIndex = 0;
                    else
                    {
                        ReturnToPool();
                        return;
                    }
                }

                _sr.sprite = _data.frames[_frameIndex];
            }

            // 비루핑: 마지막 30%에서 페이드 아웃
            if (!_data.loop && _maxLifetime > 0f)
            {
                float ratio = _lifeTimer / _maxLifetime;
                if (ratio < 0.3f)
                {
                    float alpha = ratio / 0.3f;
                    Color c = _data.defaultTint;
                    _sr.color = new Color(c.r, c.g, c.b, c.a * alpha);
                }
            }
        }

        #endregion

        #region 풀 반환

        private void ReturnToPool()
        {
            _playing = false;
            transform.SetParent(null);

            if (!_pool.TryGetValue(_soId, out var queue))
            {
                queue = new Queue<SpriteSheetVfx>();
                _pool[_soId] = queue;
            }

            if (queue.Count < MAX_POOL_PER_KEY)
            {
                gameObject.SetActive(false);
                queue.Enqueue(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>즉시 풀에 반환한다 (버프 종료 시 외부 호출용).</summary>
        public void StopAndReturn()
        {
            ReturnToPool();
        }

        #endregion

        #region 프로시져럴 폴백

        /// <summary>
        /// frames가 비어있을 때 색상 원 확대→축소→페이드 프로시져럴 이펙트를 생성한다.
        /// SO의 fallbackColor가 설정되어 있으면 사용, 없으면 기본 흰색.
        /// </summary>
        private static void SpawnProceduralFallback(SpriteSheetVfxSO data, Vector3 position,
            float desiredWorldSize, float rotation)
        {
            position.z = 0f;
            Color color = data.HasFallbackColor ? data.fallbackColor : Color.white;
            float size = desiredWorldSize > 0f ? desiredWorldSize * 0.5f : 1.5f;
            float duration = data.loop ? 1.0f : (data.Duration > 0f ? data.Duration : 0.5f);

            // 원형 확대→페이드 이펙트
            SkillVfx.SpawnCircle(position, size, color, duration);

            // 링 이펙트 (약간 지연 + 큰 범위)
            SkillVfx.SpawnRing(position, size * 1.3f, color * 0.7f, duration * 1.2f);
        }

        #endregion
    }
}
