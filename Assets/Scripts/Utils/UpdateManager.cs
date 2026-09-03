using System;
using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Utils
{
    /// <summary>
    /// 중앙 Update 루프. 개별 MonoBehaviour.Update() 대신 여기서 일괄 호출하여
    /// 40+ 몬스터의 Update 오버헤드를 최소화한다.
    ///
    /// WebGL 안전: 파괴된 MonoBehaviour나 null 참조를 자동 제거하고,
    /// 개별 콜백 예외가 전체 루프를 멈추지 않도록 보호한다.
    /// </summary>
    /// <summary>
    /// DefaultExecutionOrder를 음수로 지정해 MonsterController/PlayerCharacter의 OnEnable보다
    /// 먼저 Awake가 실행되도록 보장한다 (Instance=null 타이밍 버그 방지).
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public class UpdateManager : MonoBehaviour
    {
        public static UpdateManager Instance { get; private set; }

        private readonly List<IUpdatable> _updatables = new(64);

        /// <summary>파괴된 엔트리 자동 정리 주기 (프레임 수)</summary>
        private const int CLEANUP_INTERVAL = 120;
        private int _cleanupCounter;

        /// <summary>
        /// Instance가 null일 때 씬에서 UpdateManager를 찾아 할당한다.
        /// Awake 스킵/Destroy 레이스 상황에서의 fallback.
        /// </summary>
        public static UpdateManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = UnityEngine.Object.FindFirstObjectByType<UpdateManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }
            var go = new GameObject("UpdateManager (Auto)");
            Instance = go.AddComponent<UpdateManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // Awake가 스킵된 경우에 대비 (시나리오: editor scene reload 중 특수 상황)
            if (Instance == null) Instance = this;
        }

        public void Register(IUpdatable updatable)
        {
            if (updatable == null) return;
            if (!_updatables.Contains(updatable))
                _updatables.Add(updatable);
        }

        public void Unregister(IUpdatable updatable)
        {
            _updatables.Remove(updatable);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _updatables.Count - 1; i >= 0; i--)
            {
                if (i >= _updatables.Count) continue;
                var u = _updatables[i];

                // null 또는 파괴된 Unity Object 체크
                if (u == null || (u is UnityEngine.Object obj && obj == null))
                {
                    _updatables.RemoveAt(i);
                    continue;
                }

                try
                {
                    u.OnUpdate(dt);
                }
                catch (MissingReferenceException)
                {
                    // 파괴된 오브젝트 — 자동 제거
                    _updatables.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    // 개별 오류가 전체 루프를 멈추지 않도록 보호
                    Debug.LogWarning($"[UpdateManager] OnUpdate 예외: {ex.Message}");
                    // 반복 예외 방지를 위해 제거
                    _updatables.RemoveAt(i);
                }
            }

            // 주기적 정리: null/파괴된 엔트리 스윕
            _cleanupCounter++;
            if (_cleanupCounter >= CLEANUP_INTERVAL)
            {
                _cleanupCounter = 0;
                CleanupDestroyedEntries();
            }
        }

        /// <summary>파괴된 엔트리를 리스트에서 제거한다.</summary>
        private void CleanupDestroyedEntries()
        {
            for (int i = _updatables.Count - 1; i >= 0; i--)
            {
                var u = _updatables[i];
                if (u == null || (u is UnityEngine.Object obj && obj == null))
                {
                    _updatables.RemoveAt(i);
                }
            }
        }
    }
}
