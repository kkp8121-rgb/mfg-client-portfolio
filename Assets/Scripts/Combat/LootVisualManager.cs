using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 재화별 드롭 연출 설정.
    /// </summary>
    [Serializable]
    public struct CurrencyVisual
    {
        public CurrencyType type;
        [Tooltip("스프라이트 프레임 (1장이면 정적, 여러 장이면 회전 애니메이션)")]
        public Sprite[] frames;
        [Tooltip("드롭 스프라이트 스케일")]
        public float scale;
        [Tooltip("바닥 체공 시간 (초)")]
        public float waitTime;
        [Tooltip("연출 없이 즉시 지급 (HuntPoint 등)")]
        public bool skipVisual;
    }

    /// <summary>
    /// 전리품 드롭 연출 매니저.
    /// GoldGainedEvent, LootDroppedEvent를 구독하여 시각적 드롭 오브젝트를 생성한다.
    /// 오브젝트 풀링으로 성능을 관리한다.
    /// </summary>
    public class LootVisualManager : MonoBehaviour
    {
        public static LootVisualManager Instance { get; private set; }

        [Header("재화별 연출 설정")]
        [SerializeField] private CurrencyVisual[] _visuals;

        [Header("풀링")]
        [SerializeField] private int _poolSize = 40;

        [Header("사운드")]
        [SerializeField] private AudioClip _coinSound;
        [SerializeField] private float _coinVolume = 0.3f;
        [SerializeField] private float _soundCooldown = 0.05f;

        [Header("참조")]
        [SerializeField] private Transform _playerTransform;

        private Dictionary<CurrencyType, CurrencyVisual> _visualMap;
        private Queue<LootDrop> _pool;
        private AudioSource _audioSource;
        private float _lastSoundTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D

            BuildVisualMap();
            InitPool();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Subscribe<LootDroppedEvent>(OnLootDropped);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Unsubscribe<LootDroppedEvent>(OnLootDropped);
        }

        /// <summary>
        /// 플레이어 참조 설정. Phase2SetupEditor에서 호출.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        private void BuildVisualMap()
        {
            _visualMap = new Dictionary<CurrencyType, CurrencyVisual>();
            if (_visuals == null) return;

            foreach (var v in _visuals)
            {
                _visualMap[v.type] = v;
            }
        }

        private void InitPool()
        {
            _pool = new Queue<LootDrop>();
            for (int i = 0; i < _poolSize; i++)
            {
                _pool.Enqueue(CreateDropObject());
            }
        }

        private LootDrop CreateDropObject()
        {
            var obj = new GameObject("LootDrop");
            obj.transform.SetParent(transform);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 50;
            sr.enabled = false;
            var drop = obj.AddComponent<LootDrop>();
            obj.SetActive(false);
            return drop;
        }

        private LootDrop GetFromPool()
        {
            LootDrop drop;
            if (_pool.Count > 0)
            {
                drop = _pool.Dequeue();
            }
            else
            {
                drop = CreateDropObject();
            }
            drop.gameObject.SetActive(true);
            return drop;
        }

        private void ReturnToPool(LootDrop drop)
        {
            drop.Deactivate();
            _pool.Enqueue(drop);
            _activeDropCount = Mathf.Max(0, _activeDropCount - 1);
        }

        // ── 이벤트 핸들러 ──

        private void OnGoldGained(GoldGainedEvent evt)
        {
            SpawnDrop(CurrencyType.Gold, evt.Position);
        }

        private void OnLootDropped(LootDroppedEvent evt)
        {
            SpawnDrop(evt.Type, evt.Position, evt.Grade);
        }

        private const int MAX_ACTIVE_DROPS = 30;
        private int _activeDropCount;

        private void SpawnDrop(CurrencyType type, Vector3 position, int grade = 0)
        {
            if (!_visualMap.TryGetValue(type, out var visual))
                return;

            if (visual.skipVisual)
                return;

            if (visual.frames == null || visual.frames.Length == 0)
                return;

            // 활성 드롭 상한 — 초과 시 시각 연출 생략
            if (_activeDropCount >= MAX_ACTIVE_DROPS)
                return;

            _activeDropCount++;
            var drop = GetFromPool();
            drop.Initialize(
                visual.frames,
                position,
                visual.waitTime,
                visual.scale,
                _playerTransform,
                ReturnToPool,
                grade
            );

            PlayCoinSound(type);
        }

        private void PlayCoinSound(CurrencyType type)
        {
            if (_coinSound == null || _audioSource == null) return;

            // 사운드 쿨다운 (동시에 너무 많은 소리 방지)
            if (Time.time - _lastSoundTime < _soundCooldown) return;
            _lastSoundTime = Time.time;

            // 골드는 작은 볼륨, 희귀 재화는 큰 볼륨
            float vol = type == CurrencyType.Gold ? _coinVolume : _coinVolume * 1.5f;
            _audioSource.PlayOneShot(_coinSound, vol);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
