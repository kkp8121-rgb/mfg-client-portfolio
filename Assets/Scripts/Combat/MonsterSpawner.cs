using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Lean.Pool;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 탑다운 아레나 가장자리 웨이브 스폰 시스템.
    /// _spawnInterval/batchSize 기반으로 몬스터를 주기적으로 스폰.
    /// LeanPool 기반 풀링.
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        #region 설정값

        [Header("스폰 설정")]
        [SerializeField] private float _spawnInterval = 3f;
        [SerializeField] private int _spawnBatchSize = 5;
        [SerializeField] private int _maxAliveCount = 40;

        [Header("몬스터 데이터")]
        [SerializeField] private MonsterDataSO _monsterData;

        [Header("보스 설정")]
        [SerializeField] private MonsterDataSO _bossData;
        [SerializeField] private float _bossScaleMultiplier = 1.5f;
        [SerializeField] private float _miniBossScaleMultiplier = 1.3f;

        [Header("미니보스 배율")]
        [SerializeField] private int _miniBossHpMultiplier = 10;
        [SerializeField] private float _miniBossAtkMultiplier = 1.3f;
        [SerializeField] private float _miniBossAtkSpdMultiplier = 0.8f;
        [SerializeField] private float _miniBossMoveSpdMultiplier = 0.7f;

        [Header("챕터보스 배율")]
        [SerializeField] private float _chapterBossAtkSpdMultiplier = 0.6f;
        [SerializeField] private float _chapterBossMoveSpdMultiplier = 0.5f;

        [Header("참조")]
        [SerializeField] private ArenaMap _arenaMap;

        [Header("스테이지 설정")]
        [SerializeField] private int _stagesPerChapter = 9;

        [Header("풀 설정")]
        [SerializeField] private int _poolInitialSize = 50;

        #endregion

        #region 상태

        private readonly List<GameObject> _aliveMonsters = new List<GameObject>();

        /// <summary>몬스터 프리팹 (엘리트 시스템에서 참조)</summary>
        public GameObject MonsterPrefab => _monsterData != null ? _monsterData.prefab : null;
        private int _currentChapter = 1;
        private int _currentStageIndex = 1;
        private bool _isSpawningEnabled;
        private GameObject _currentMiniBoss;
        private bool _poolInitialized;
        private Transform _playerTransform;
        private CombatStats _playerCombatStats;

        // LeanPool 풀 참조
        private LeanGameObjectPool _monsterPool;
        private LeanGameObjectPool _bossPool;
        private GameObject _bossPrefab;

        // 챕터별 몬스터 설정
        private ChapterMonsterConfigSO _chapterConfig;
        private ChapterMonsterConfigSO[] _allChapterConfigs;

        #endregion

        #region Unity 생명주기

        private void OnEnable()
        {
            EventBus.Subscribe<MonsterDiedEvent>(OnMonsterDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
        }

        private void Start()
        {
            CachePlayerReference();
            InitializePool();
        }

        #endregion

        #region 플레이어 참조

        private void CachePlayerReference()
        {
            if (_playerTransform != null) return;

            PlayerCharacter player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerCombatStats = player.GetComponent<CombatStats>();
            }
            else
            {
                Debug.LogWarning("[MonsterSpawner] PlayerCharacter를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 외부에서 플레이어 참조를 설정한다.
        /// </summary>
        public void SetPlayer(Transform playerTransform, CombatStats playerStats)
        {
            _playerTransform = playerTransform;
            _playerCombatStats = playerStats;
        }

        #endregion

        #region 풀 초기화

        private void InitializePool()
        {
            if (_poolInitialized) return;

            if (_monsterData == null || _monsterData.prefab == null)
            {
                Debug.LogError("[MonsterSpawner] MonsterDataSO 또는 prefab이 설정되지 않았습니다.");
                return;
            }

            // 몬스터 풀
            _monsterPool = CreateLeanPool($"Pool_Monster_{_monsterData.id}",
                _monsterData.prefab, _poolInitialSize, _maxAliveCount + 10);

            // 보스 풀
            _bossPrefab = _bossData != null ? _bossData.prefab : _monsterData.prefab;
            _bossPool = CreateLeanPool($"Pool_Boss_{(_bossData != null ? _bossData.id : _monsterData.id)}",
                _bossPrefab, 2, 5);

            _poolInitialized = true;
        }

        private LeanGameObjectPool CreateLeanPool(string name, GameObject prefab, int preload, int capacity)
        {
            var poolGo = new GameObject(name);
            poolGo.transform.SetParent(transform);
            var pool = poolGo.AddComponent<LeanGameObjectPool>();
            pool.Prefab = prefab;
            pool.Preload = preload;
            pool.Capacity = capacity;
            pool.Recycle = true;
            pool.Notification = LeanGameObjectPool.NotificationType.IPoolable;
            pool.Strategy = LeanGameObjectPool.StrategyType.DeactivateViaHierarchy;
            pool.PreloadAll();
            return pool;
        }

        #endregion

        #region 스테이지 시작

        public void StartStage(int chapter, int stageIndex)
        {
            if (!_poolInitialized) InitializePool();
            if (!_poolInitialized)
            {
                Debug.LogError("[MonsterSpawner] 풀 초기화 실패 — 스폰 불가");
                return;
            }

            // 플레이어 참조 안전 확인 (재도전 시 null일 수 있음)
            if (_playerTransform == null)
                CachePlayerReference();

            _currentChapter = chapter;
            _currentStageIndex = stageIndex;
            _currentMiniBoss = null;

            // 챕터별 몬스터 설정 로드
            LoadChapterConfig(chapter);

            ClearAllMonsters();
            StartSpawning();
        }

        private void LoadChapterConfig(int chapter)
        {
            if (_allChapterConfigs == null)
                _allChapterConfigs = Resources.LoadAll<ChapterMonsterConfigSO>("Data/Chapters");

            _chapterConfig = null;
            if (_allChapterConfigs != null)
            {
                for (int i = 0; i < _allChapterConfigs.Length; i++)
                {
                    if (_allChapterConfigs[i].ChapterNumber == chapter)
                    {
                        _chapterConfig = _allChapterConfigs[i];
                        break;
                    }
                }

                // 챕터 8 이후: 챕터 8(ModernPack) 순환
                if (_chapterConfig == null && _allChapterConfigs.Length > 0)
                {
                    int wrapped = ((chapter - 1) % _allChapterConfigs.Length) + 1;
                    for (int i = 0; i < _allChapterConfigs.Length; i++)
                    {
                        if (_allChapterConfigs[i].ChapterNumber == wrapped)
                        {
                            _chapterConfig = _allChapterConfigs[i];
                            break;
                        }
                    }
                }
            }

            if (_chapterConfig != null)
                Debug.Log($"[MonsterSpawner] 챕터 {chapter} 설정 로드: {_chapterConfig.ThemeName} ({_chapterConfig.GetMonsterPrefabs()?.Length ?? 0}개 프리팹)");
        }

        #endregion

        #region 웨이브 스폰 루프

        private void StartSpawning()
        {
            _isSpawningEnabled = true;
            SpawnLoopAsync().Forget();
        }

        private async UniTaskVoid SpawnLoopAsync()
        {
            // 최초 배치 즉시 스폰
            SpawnBatch();

            while (_isSpawningEnabled && this != null && gameObject.activeInHierarchy)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: destroyCancellationToken);

                if (!_isSpawningEnabled) break;

                SpawnBatch();
            }
        }

        private void SpawnBatch()
        {
            if (_arenaMap == null || !_poolInitialized) return;

            int toSpawn = Mathf.Min(_spawnBatchSize, _maxAliveCount - _aliveMonsters.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                SpawnMonster();
            }
        }

        private void SpawnMonster()
        {
            if (_aliveMonsters.Count >= _maxAliveCount) return;
            if (_monsterPool == null || _arenaMap == null) return;

            Vector2 spawnPos = _arenaMap.GetSpawnPosition();
            GameObject monster = LeanPool.Spawn(_monsterData.prefab, (Vector3)spawnPos, Quaternion.identity);
            if (monster == null) return;

            int floor = GetEffectiveFloor();

            CombatStats stats = monster.GetComponent<CombatStats>();
            if (stats != null)
            {
                stats.Initialize(
                    CombatFormula.MonsterHp(floor),
                    CombatFormula.MonsterAtk(floor),
                    CombatFormula.MonsterDef(floor),
                    0f,
                    _monsterData.attackSpeed,
                    _monsterData.moveSpeed
                );
            }

            MonsterController controller = monster.GetComponent<MonsterController>();
            if (controller != null)
            {
                controller.IsBoss = false;
                controller.ArenaMap = _arenaMap;

                if (_playerTransform != null)
                    controller.SetPlayer(_playerTransform, _playerCombatStats);
            }

            MonsterCombat combat = monster.GetComponent<MonsterCombat>();
            if (combat != null)
                combat.ResetCooldown();

            // 2026-04-20 SPUM 단일 원천 통일 — _chapterConfig 랜덤 오버라이드 제거.
            // 실제 스폰 프리팹(_monsterData.prefab)이 도감 아이콘과 동일 소스.
            // 챕터 테마(색/스케일)는 MonsterDataSO.visualTint/visualScale로 개별 지정.
            if (SpumCharacterManager.Instance != null && _monsterData != null)
            {
                SpumCharacterManager.Instance.ApplyMonsterVisual(monster.transform, _monsterData);
            }

            // 루트 스케일은 (1,1,1)로 리셋 (design.scale은 SPUMVisual 자식에 이미 적용됨)
            monster.transform.localScale = Vector3.one;
            _aliveMonsters.Add(monster);
        }

        #endregion

        #region 미니보스

        public void SpawnMiniBoss(int floor)
        {
            StopSpawning();

            if (_bossPool == null || _arenaMap == null) return;

            Vector2 spawnPos = _arenaMap.GetSpawnPosition();
            GameObject boss = LeanPool.Spawn(_bossPrefab, (Vector3)spawnPos, Quaternion.identity);
            if (boss == null)
            {
                Debug.LogError("[MonsterSpawner] 미니보스 스폰 실패!");
                return;
            }

            CombatStats stats = boss.GetComponent<CombatStats>();
            if (stats != null)
            {
                stats.Initialize(
                    Mathf.RoundToInt(CombatFormula.MonsterHp(floor) * _miniBossHpMultiplier),
                    Mathf.RoundToInt(CombatFormula.MonsterAtk(floor) * _miniBossAtkMultiplier),
                    CombatFormula.MonsterDef(floor),
                    0f,
                    _monsterData.attackSpeed * _miniBossAtkSpdMultiplier,
                    _monsterData.moveSpeed * _miniBossMoveSpdMultiplier
                );
            }

            MonsterController controller = boss.GetComponent<MonsterController>();
            if (controller != null)
            {
                controller.IsBoss = true;
                controller.ArenaMap = _arenaMap;

                if (_playerTransform != null)
                    controller.SetPlayer(_playerTransform, _playerCombatStats);
            }

            MonsterCombat combat = boss.GetComponent<MonsterCombat>();
            if (combat != null)
                combat.ResetCooldown();

            _aliveMonsters.Add(boss);
            _currentMiniBoss = boss;

            // 챕터별 미니보스 외형 적용
            if (_chapterConfig != null && SpumCharacterManager.Instance != null)
            {
                var prefab = _chapterConfig.GetRandomBossPrefab();
                if (prefab != null)
                {
                    SpumCharacterManager.Instance.ApplyMonsterVisual(
                        boss.transform, prefab,
                        _chapterConfig.MiniBossTint, _chapterConfig.MiniBossScale,
                        _chapterConfig.GetRandomAttackAnimType(), $"miniboss_ch{_currentChapter}");
                }
            }

            // 보스 스폰 비주얼 연출 (스케일 팝 + 오라)
            var visualEffect = GetCachedMonsterVisualEffect();
            if (visualEffect != null)
                visualEffect.PlayBossSpawn(boss.transform, false);
            else
                boss.transform.localScale = Vector3.one * _miniBossScaleMultiplier;

            // 미니보스는 패턴 컨트롤러 없음 (챕터 보스만 패턴 적용)

#if UNITY_EDITOR
            Debug.Log($"[MonsterSpawner] 미니보스 스폰! HP={CombatFormula.MonsterHp(floor) * _miniBossHpMultiplier}");
#endif
        }

        public void DespawnMiniBoss()
        {
            if (_currentMiniBoss != null && _currentMiniBoss.activeInHierarchy)
            {
                _aliveMonsters.Remove(_currentMiniBoss);
                LeanPool.Despawn(_currentMiniBoss);
                _currentMiniBoss = null;
            }
        }

        public void SpawnChapterBoss(int floor)
        {
            StopSpawning();
            ClearAllMonsters();

            if (_bossPool == null || _arenaMap == null) return;

            Vector2 spawnPos = _arenaMap.GetBossSpawnWorld();
            GameObject boss = LeanPool.Spawn(_bossPrefab, (Vector3)spawnPos, Quaternion.identity);
            if (boss == null)
            {
                Debug.LogError("[MonsterSpawner] 챕터 보스 스폰 실패!");
                return;
            }

            CombatStats stats = boss.GetComponent<CombatStats>();
            if (stats != null)
            {
                stats.Initialize(
                    CombatFormula.BossHp(floor),
                    CombatFormula.BossAtk(floor),
                    CombatFormula.BossDef(floor),
                    0f,
                    _monsterData.attackSpeed * _chapterBossAtkSpdMultiplier,
                    _monsterData.moveSpeed * _chapterBossMoveSpdMultiplier
                );
            }

            MonsterController controller = boss.GetComponent<MonsterController>();
            if (controller != null)
            {
                controller.IsBoss = true;
                controller.IsChapterBoss = true;
                controller.ArenaMap = _arenaMap;

                if (_playerTransform != null)
                    controller.SetPlayer(_playerTransform, _playerCombatStats);
            }

            MonsterCombat combat = boss.GetComponent<MonsterCombat>();
            if (combat != null)
                combat.ResetCooldown();

            _aliveMonsters.Add(boss);

            // 챕터별 보스 외형 적용
            if (_chapterConfig != null && SpumCharacterManager.Instance != null)
            {
                var prefab = _chapterConfig.GetRandomBossPrefab();
                if (prefab != null)
                {
                    SpumCharacterManager.Instance.ApplyMonsterVisual(
                        boss.transform, prefab,
                        _chapterConfig.BossTint, _chapterConfig.BossScale,
                        _chapterConfig.GetRandomAttackAnimType(), $"boss_ch{_currentChapter}");
                }
            }

            // 챕터 보스 스폰 비주얼 연출 (2.5배 스케일 + 오라 + 화면 흔들림)
            var visualEffect = GetCachedMonsterVisualEffect();
            if (visualEffect != null)
                visualEffect.PlayBossSpawn(boss.transform, true);
            else
                boss.transform.localScale = Vector3.one * _bossScaleMultiplier;

            // 보스 패턴 컨트롤러 부착
            AttachBossPattern(boss, controller);

#if UNITY_EDITOR
            Debug.Log($"[MonsterSpawner] 챕터 보스 스폰! HP={CombatFormula.BossHp(floor)}");
#endif
        }

        #endregion

        #region 스폰 제어

        public void StopSpawning()
        {
            _isSpawningEnabled = false;
        }

        public void ResumeSpawning()
        {
            if (!_isSpawningEnabled)
                StartSpawning();
        }

        /// <summary>
        /// 단일 몬스터를 스폰한다 (던전 전투용).
        /// HP/ATK 배율을 적용하여 강화된 몬스터를 생성.
        /// </summary>
        public GameObject SpawnSingleMonster(float hpMultiplier = 1f, float atkMultiplier = 1f)
        {
            if (!_poolInitialized) InitializePool();
            if (_arenaMap == null) return null;

            Vector2 spawnPos = _arenaMap.GetSpawnPosition();
            var monster = LeanPool.Spawn(_monsterData.prefab, (Vector3)spawnPos, Quaternion.identity);
            if (monster == null) return null;

            // 몬스터 기본 설정
            var controller = monster.GetComponent<MonsterController>();
            if (controller != null)
            {
                controller.IsBoss = false;
                controller.IsChapterBoss = false;
                if (_playerTransform != null)
                    controller.SetPlayer(_playerTransform, _playerCombatStats);
            }

            // HP/ATK 배율 적용
            var stats = monster.GetComponent<CombatStats>();
            if (stats != null)
            {
                if (hpMultiplier > 1f)
                {
                    stats.AddModifier("dungeon_hp",
                        new StatModifier(ModifierSource.Equipment, "dungeon",
                            Core.StatType.MaxHp, 0f, hpMultiplier - 1f));
                }
                if (atkMultiplier > 1f)
                {
                    stats.AddModifier("dungeon_atk",
                        new StatModifier(ModifierSource.Equipment, "dungeon",
                            Core.StatType.Atk, 0f, atkMultiplier - 1f));
                }
                stats.ResetHp();
            }

            // SPUM 비주얼
            if (SpumCharacterManager.Instance != null && _monsterData != null)
                SpumCharacterManager.Instance.ApplyMonsterVisual(monster.transform, _monsterData);

            _aliveMonsters.Add(monster);
            return monster;
        }

        public void ClearAllMonsters()
        {
            for (int i = _aliveMonsters.Count - 1; i >= 0; i--)
            {
                GameObject monster = _aliveMonsters[i];
                if (monster != null)
                    LeanPool.Despawn(monster);
            }
            _aliveMonsters.Clear();
            _currentMiniBoss = null;

            // 사망 애니메이션 재생 중인 몬스터도 정리 (aliveMonsters에서 이미 제거됨)
            var dyingMonsters = UnityEngine.Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            for (int i = 0; i < dyingMonsters.Length; i++)
            {
                if (dyingMonsters[i] != null && dyingMonsters[i].gameObject.activeInHierarchy)
                    LeanPool.Despawn(dyingMonsters[i].gameObject);
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _aliveMonsters.Remove(evt.Monster);
        }

        #endregion

        #region 캐시된 참조

        // 2026-04-23 캐시: 보스 스폰마다 FindFirstObjectByType 반복 호출 제거
        private MonsterVisualEffect _cachedVisualEffect;
        private MonsterVisualEffect GetCachedMonsterVisualEffect()
        {
            if (_cachedVisualEffect == null)
                _cachedVisualEffect = FindFirstObjectByType<MonsterVisualEffect>();
            return _cachedVisualEffect;
        }

        #endregion

        #region 유틸리티

        private int GetEffectiveFloor()
        {
            return (_currentChapter - 1) * (_stagesPerChapter + 1) + _currentStageIndex;
        }

        /// <summary>
        /// 보스 몬스터에 BossPatternController를 부착한다.
        /// 이미 부착된 경우 기존 컴포넌트를 재초기화한다.
        /// </summary>
        private void AttachBossPattern(GameObject boss, MonsterController controller)
        {
            if (boss == null || controller == null) return;

            // 이전 보스전에서 남은 BossPatternUI 정리 (오버레이 누적 방지)
            var existingPattern = Object.FindFirstObjectByType<BossPatternController>();
            if (existingPattern != null)
                Destroy(existingPattern.gameObject);

            // 별도 게임오브젝트에 생성 (Canvas를 보스 GO에 직접 넣으면 월드 스페이스 문제)
            var patternGo = new GameObject("BossPatternUI");
            var patternCtrl = patternGo.AddComponent<BossPatternController>();

            float timeLimit = controller.IsChapterBoss ? 60f : 30f;
            patternCtrl.Initialize(controller, timeLimit);

#if UNITY_EDITOR
            Debug.Log($"[MonsterSpawner] BossPatternController 부착 완료 (제한시간: {timeLimit}초)");
#endif
        }

        #endregion

        #region 에디터 설정용

        public void SetArenaMap(ArenaMap map) => _arenaMap = map;
        public void SetSpawnInterval(float interval) => _spawnInterval = interval;
        public void SetSpawnBatchSize(int size) => _spawnBatchSize = size;
        public void SetMaxAliveCount(int count) => _maxAliveCount = count;

        #endregion
    }
}
