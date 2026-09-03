using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 3D 파티클 프리팹을 2D 탑다운에 맞게 자동 적응시키는 런타임 컴포넌트.
    /// 핵심: X=90° 강제 회전으로 3D 바닥면(XZ)을 2D 화면면(XY)으로 매핑.
    /// 오브젝트 풀링 내장: Spawn→ReturnToPool 사이클로 GC 최소화.
    /// </summary>
    public class ParticleVfx2DAdapter : MonoBehaviour
    {
        private const int VFX_BASE_SORTING_ORDER = 100;
        private const float AUTO_RETURN_MARGIN = 0.3f;
        private const int MAX_POOL_PER_PREFAB = 5;

        // 2D 탑다운: X=90°로 3D XZ 바닥 → 2D XY 화면 매핑
        private static readonly Quaternion TOP_DOWN_ROTATION = Quaternion.Euler(90f, 0f, 0f);

        // 오브젝트 풀: prefab instanceID → 비활성 인스턴스 큐
        private static readonly Dictionary<int, Queue<GameObject>> _pool = new();

        private int _prefabId;
        private float _returnTimer;
        private bool _timerActive;
        private ParticleSystem[] _cachedParticleSystems;
        private ParticleSystemRenderer[] _cachedRenderers;

        #region 풀링 API

        /// <summary>
        /// 파티클 프리팹을 2D 탑다운에 맞게 스폰한다 (풀링 적용).
        /// maxLifetime을 지정하면 루핑 파티클도 강제 회수된다.
        /// </summary>
        public static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            float scale = 1f,
            float? directionAngle = null,
            Transform parent = null,
            float maxLifetime = 0f)
        {
            if (prefab == null) return null;

            position.z = 0f;

            // 회전: 2D 탑다운 고정 + 방향
            var rotation = TOP_DOWN_ROTATION;
            if (directionAngle.HasValue)
            {
                rotation = TOP_DOWN_ROTATION * Quaternion.Euler(0f, -directionAngle.Value, 0f);
            }

            int prefabId = prefab.GetInstanceID();
            GameObject instance;
            ParticleVfx2DAdapter adapter;

            // 풀에서 꺼내기
            if (_pool.TryGetValue(prefabId, out var queue) && queue.Count > 0)
            {
                instance = queue.Dequeue();
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.transform.localScale = prefab.transform.localScale * scale;
                instance.SetActive(true);

                adapter = instance.GetComponent<ParticleVfx2DAdapter>();
                adapter.Restart(maxLifetime, parent);
            }
            else
            {
                // 새로 생성
                instance = Object.Instantiate(prefab, position, rotation);
                instance.transform.localScale = prefab.transform.localScale * scale;

                adapter = instance.AddComponent<ParticleVfx2DAdapter>();
                adapter._prefabId = prefabId;
                adapter.Setup(maxLifetime);
            }

            if (parent != null)
            {
                instance.transform.SetParent(parent, true);
            }

            return instance;
        }

        /// <summary>풀 전체를 비운다 (씬 전환 시 호출).</summary>
        public static void ClearPool()
        {
            foreach (var queue in _pool.Values)
            {
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj != null) Object.Destroy(obj);
                }
            }
            _pool.Clear();
        }

        #endregion

        #region 내부 초기화

        private void Setup(float maxLifetime)
        {
            CacheComponents();
            FixRenderers();
            FixParticleSystems();
            ScheduleReturn(maxLifetime);
        }

        private void CacheComponents()
        {
            _cachedParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _cachedRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        }

        private void Restart(float maxLifetime, Transform parent)
        {
            // 부모 해제 (이전 버프에서 붙어있었을 수 있음)
            if (parent == null && transform.parent != null)
                transform.SetParent(null);

            // 파티클 재시작
            if (_cachedParticleSystems == null) CacheComponents();
            foreach (var ps in _cachedParticleSystems)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            ScheduleReturn(maxLifetime);
        }

        private void FixRenderers()
        {
            int orderOffset = 0;

            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                var psr = _cachedRenderers[i];
                psr.sortingOrder = VFX_BASE_SORTING_ORDER + orderOffset;
                orderOffset++;

                psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psr.receiveShadows = false;
                psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                psr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                if (psr.renderMode == ParticleSystemRenderMode.Mesh)
                    psr.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }

        private void FixParticleSystems()
        {
            for (int i = 0; i < _cachedParticleSystems.Length; i++)
            {
                var main = _cachedParticleSystems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                if (transform.parent != null)
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        #endregion

        #region 자동 회수

        private void ScheduleReturn(float maxLifetime)
        {
            float duration;

            if (maxLifetime > 0f)
            {
                // 강제 수명 지정 (루핑 파티클도 이 시간 후 회수)
                duration = maxLifetime;
            }
            else
            {
                // 파티클 시스템 duration + startLifetime에서 최대값 산출
                duration = CalculateMaxDuration();
                if (duration <= 0f) duration = 3f; // 안전 폴백
            }

            _returnTimer = duration + AUTO_RETURN_MARGIN;
            _timerActive = true;
        }

        private float CalculateMaxDuration()
        {
            float maxDur = 0f;

            for (int i = 0; i < _cachedParticleSystems.Length; i++)
            {
                var main = _cachedParticleSystems[i].main;

                float lifetime = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                    ? main.startLifetime.constantMax
                    : main.startLifetime.constant;

                float delay = main.startDelay.mode == ParticleSystemCurveMode.TwoConstants
                    ? main.startDelay.constantMax
                    : main.startDelay.constant;

                float dur = main.duration + lifetime + delay;
                if (dur > maxDur) maxDur = dur;
            }

            return maxDur;
        }

        private void Update()
        {
            if (!_timerActive) return;

            _returnTimer -= Time.deltaTime;
            if (_returnTimer <= 0f)
            {
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            _timerActive = false;

            // 파티클 정지
            if (_cachedParticleSystems != null)
            {
                for (int i = 0; i < _cachedParticleSystems.Length; i++)
                    _cachedParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // 부모 해제
            transform.SetParent(null);

            // 풀에 반환
            if (!_pool.TryGetValue(_prefabId, out var queue))
            {
                queue = new Queue<GameObject>();
                _pool[_prefabId] = queue;
            }

            if (queue.Count < MAX_POOL_PER_PREFAB)
            {
                gameObject.SetActive(false);
                queue.Enqueue(gameObject);
            }
            else
            {
                // 풀 초과 시 파괴
                Destroy(gameObject);
            }
        }

        /// <summary>즉시 풀에 반환한다 (버프 종료 시 외부 호출용).</summary>
        public void StopAndReturn()
        {
            ReturnToPool();
        }

        #endregion

        private void OnDestroy()
        {
            DG.Tweening.DOTween.Kill(transform);
        }
    }
}
