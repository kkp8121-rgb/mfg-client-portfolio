using System.Collections.Generic;
using MkLike.Core;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 챕터별 몬스터 외형 + 배경 설정.
    /// MonsterSpawner가 챕터 전환 시 이 SO를 참조하여
    /// 랜덤 SPUM 프리팹을 선택한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Chapter_", menuName = "MkLike/Chapter Monster Config")]
    public class ChapterMonsterConfigSO : ScriptableObject
    {
        [Header("챕터 정보")]
        [SerializeField] private int _chapterNumber;
        [SerializeField] private string _themeName;

        [Header("몬스터 SPUM 프리팹 풀 (Resources 경로)")]
        [Tooltip("Resources.LoadAll 경로. 예: Addons/MS_Orc/2_Prefab")]
        [SerializeField] private string _monsterPrefabFolder;

        [Header("보스/미니보스 프리팹 (Resources 경로)")]
        [Tooltip("보스 전용 프리팹 폴더. 비어있으면 일반 풀에서 선택")]
        [SerializeField] private string _bossPrefabFolder;

        [Header("몬스터 비주얼")]
        [SerializeField] private Color _monsterTint = Color.white;
        [SerializeField] private float _monsterScale = 1f;
        [SerializeField] private Color _bossTint = new Color(1f, 0.6f, 0.6f);
        [SerializeField] private float _bossScale = 1.5f;
        [SerializeField] private Color _miniBossTint = new Color(1f, 0.8f, 0.5f);
        [SerializeField] private float _miniBossScale = 1.3f;

        [Header("배경 색상 (하단 → 상단 그라데이션)")]
        [SerializeField] private Color _bgBottom = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] private Color _bgTop = new Color(0.2f, 0.2f, 0.2f);

        [Header("공격 애니메이션 분포")]
        [Tooltip("일반 몬스터 중 원거리(Bow/Magic) 비율 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _rangedRatio = 0.3f;

        // ── 프로퍼티 ──
        public int ChapterNumber => _chapterNumber;
        public string ThemeName => _themeName;
        public string MonsterPrefabFolder => _monsterPrefabFolder;
        public string BossPrefabFolder => _bossPrefabFolder;
        public Color MonsterTint => _monsterTint;
        public float MonsterScale => _monsterScale;
        public Color BossTint => _bossTint;
        public float BossScale => _bossScale;
        public Color MiniBossTint => _miniBossTint;
        public float MiniBossScale => _miniBossScale;
        public Color BgBottom => _bgBottom;
        public Color BgTop => _bgTop;
        public float RangedRatio => _rangedRatio;

        // ── 런타임 캐시 ──
        private GameObject[] _cachedMonsterPrefabs;
        private GameObject[] _cachedBossPrefabs;

        /// <summary>일반 몬스터 프리팹 풀 (런타임 캐시, 플레이어 외형 프리팹 제외)</summary>
        public GameObject[] GetMonsterPrefabs()
        {
            if (_cachedMonsterPrefabs == null || _cachedMonsterPrefabs.Length == 0)
            {
                var raw = Resources.LoadAll<GameObject>(_monsterPrefabFolder);
                _cachedMonsterPrefabs = FilterOutPlayerPrefabs(raw);
            }
            return _cachedMonsterPrefabs;
        }

        /// <summary>보스 프리팹 풀 (없으면 일반 풀 사용, 플레이어 외형 프리팹 제외)</summary>
        public GameObject[] GetBossPrefabs()
        {
            if (_cachedBossPrefabs == null || _cachedBossPrefabs.Length == 0)
            {
                if (!string.IsNullOrEmpty(_bossPrefabFolder))
                {
                    var raw = Resources.LoadAll<GameObject>(_bossPrefabFolder);
                    _cachedBossPrefabs = FilterOutPlayerPrefabs(raw);
                }

                if (_cachedBossPrefabs == null || _cachedBossPrefabs.Length == 0)
                    _cachedBossPrefabs = GetMonsterPrefabs();
            }
            return _cachedBossPrefabs;
        }

        // ── 플레이어 외형 프리팹 배제 (JobOutfitDatabase 기반) ──

        private static HashSet<GameObject> _playerExclusions;

        private static GameObject[] FilterOutPlayerPrefabs(GameObject[] source)
        {
            if (source == null || source.Length == 0) return source;
            EnsureExclusions();
            if (_playerExclusions == null || _playerExclusions.Count == 0) return source;

            var filtered = new List<GameObject>(source.Length);
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] != null && !_playerExclusions.Contains(source[i]))
                    filtered.Add(source[i]);
            }
            return filtered.ToArray();
        }

        private static void EnsureExclusions()
        {
            if (_playerExclusions != null) return;
            _playerExclusions = new HashSet<GameObject>();
            var db = JobOutfitDatabaseSO.Load();
            if (db == null) return;
            for (int jobIdx = 0; jobIdx < 3; ++jobIdx)
            {
                var job = (JobType)jobIdx;
                for (int tier = 0; tier <= 4; ++tier)
                {
                    var p = db.GetPrefab(job, tier);
                    if (p != null) _playerExclusions.Add(p);
                }
            }
        }

        /// <summary>랜덤 일반 몬스터 프리팹 선택</summary>
        public GameObject GetRandomMonsterPrefab()
        {
            var prefabs = GetMonsterPrefabs();
            if (prefabs == null || prefabs.Length == 0) return null;
            return prefabs[Random.Range(0, prefabs.Length)];
        }

        /// <summary>랜덤 보스 프리팹 선택</summary>
        public GameObject GetRandomBossPrefab()
        {
            var prefabs = GetBossPrefabs();
            if (prefabs == null || prefabs.Length == 0) return null;
            return prefabs[Random.Range(0, prefabs.Length)];
        }

        /// <summary>랜덤 공격 타입 (일반=0, 활=1, 마법=2)</summary>
        public int GetRandomAttackAnimType()
        {
            if (Random.value < _rangedRatio)
                return Random.value < 0.5f ? 1 : 2; // Bow or Magic
            return 0; // Normal (melee)
        }
    }
}
