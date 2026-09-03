using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Dungeon
{
    /// <summary>
    /// 던전 전투 세션 컨트롤러.
    /// 입장 → 맵 전환 → 타이머 시작 → 웨이브 스폰 → 시간 종료 → 성과 비례 보상.
    ///
    /// 기존 MonsterSpawner/StageManager를 활용하되, 던전 모드에서는
    /// 독자적인 스폰 규칙과 타이머/킬카운트를 관리한다.
    /// </summary>
    public class DungeonBattleController : MonoBehaviour
    {
        public static DungeonBattleController Instance { get; private set; }

        [Header("던전 전투 설정")]
        [SerializeField] private float _defaultDuration = 30f;
        [SerializeField] private float _spawnInterval = 1.5f;
        [SerializeField] private int _spawnBatchSize = 4;
        [SerializeField] private int _maxAliveMonsters = 30;
        [SerializeField] private float _difficultyHpMultiplier = 2f;
        [SerializeField] private float _difficultyAtkMultiplier = 1.2f;

        // 상태
        private bool _isActive;
        private float _remainingTime;
        private float _totalDuration;
        private int _killCount;
        private int _targetKills;
        private DungeonDataSO _activeDungeon;
        private float _spawnTimer;

        // 참조
        private MonsterSpawner _spawner;
        private StageManager _stageManager;
        private GameObject[] _dungeonPrefabs;

        /// <summary>던전 전투 진행 중 여부</summary>
        public bool IsActive => _isActive;
        /// <summary>남은 시간 (초)</summary>
        public float RemainingTime => _remainingTime;
        /// <summary>전체 시간 (초)</summary>
        public float TotalDuration => _totalDuration;
        /// <summary>현재 킬 수</summary>
        public int KillCount => _killCount;
        /// <summary>목표 킬 수</summary>
        public int TargetKills => _targetKills;
        /// <summary>진행률 (0~1)</summary>
        public float Progress => _targetKills > 0 ? Mathf.Clamp01((float)_killCount / _targetKills) : 0f;
        /// <summary>시간 진행률 (0~1, 1=시간 다 됨)</summary>
        public float TimeProgress => _totalDuration > 0f ? 1f - (_remainingTime / _totalDuration) : 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

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
            _spawner = FindFirstObjectByType<MonsterSpawner>();
            _stageManager = FindFirstObjectByType<StageManager>();
        }

        private void Update()
        {
            if (!_isActive) return;

            // 타이머 감소
            _remainingTime -= Time.deltaTime;

            // 던전 몬스터 스폰
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;
                SpawnDungeonWave();
            }

            // 시간 종료 또는 목표 달성
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                EndBattle();
            }
            else if (_killCount >= _targetKills)
            {
                // 목표 초과 달성 — 보너스 시간 계속 (추가 킬 = 추가 보상)
                // 시간이 끝날 때까지 계속 전투
            }
        }

        /// <summary>
        /// 던전 전투를 시작한다.
        /// DungeonPanelUI에서 호출. DungeonManager.EnterDungeon() 이후 호출되어야 한다.
        /// </summary>
        public void StartBattle(DungeonDataSO dungeon)
        {
            if (_isActive)
            {
                Debug.LogWarning("[DungeonBattleController] 이미 던전 전투 진행 중");
                return;
            }

            _activeDungeon = dungeon;
            _isActive = true;

            // 던전별 설정
            _totalDuration = dungeon.timeLimit > 0 ? dungeon.timeLimit : _defaultDuration;
            _remainingTime = _totalDuration;
            _targetKills = dungeon.monsterCount > 0 ? dungeon.monsterCount : 20;
            _killCount = 0;
            _spawnTimer = 0f; // 즉시 첫 웨이브

            // 일반 스테이지 스폰 중단
            if (_spawner != null)
            {
                _spawner.StopSpawning();
                _spawner.ClearAllMonsters();
            }

            // 던전 진입 이벤트 발행 (맵 전환 + 연출)
            EventBus.Publish(new DungeonBattleStartedEvent
            {
                DungeonId = dungeon.id,
                DungeonName = dungeon.displayName,
                Duration = _totalDuration,
                TargetKills = _targetKills
            });

            // 던전 전용 배경 전환
            if (ZoneVisualManager.Instance != null)
                ZoneVisualManager.Instance.EnterDungeon((int)dungeon.dungeonType);

            // 던전 전용 몬스터 프리팹 캐시
            _dungeonPrefabs = null;
            if (!string.IsNullOrEmpty(dungeon.monsterPrefabFolder))
                _dungeonPrefabs = Resources.LoadAll<GameObject>(dungeon.monsterPrefabFolder);

            Debug.Log($"[DungeonBattleController] 전투 시작: {dungeon.displayName}, " +
                      $"{_totalDuration}초, 목표: {_targetKills}킬, 프리팹: {_dungeonPrefabs?.Length ?? 0}개");
        }

        private void SpawnDungeonWave()
        {
            if (_spawner == null) return;

            // 현재 살아있는 몬스터 수 확인
            int aliveCount = GetAliveMonsterCount();
            if (aliveCount >= _maxAliveMonsters) return;

            int toSpawn = Mathf.Min(_spawnBatchSize, _maxAliveMonsters - aliveCount);

            // 던전 유형별 색상/스케일
            GetDungeonVisual(out Color tint, out float scale);

            for (int i = 0; i < toSpawn; i++)
            {
                GameObject monster = _spawner.SpawnSingleMonster(_difficultyHpMultiplier, _difficultyAtkMultiplier);
                if (monster == null) continue;

                // 던전 전용 SPUM 프리팹 적용
                if (_dungeonPrefabs != null && _dungeonPrefabs.Length > 0 && SpumCharacterManager.Instance != null)
                {
                    var prefab = _dungeonPrefabs[Random.Range(0, _dungeonPrefabs.Length)];
                    SpumCharacterManager.Instance.ApplyMonsterVisual(
                        monster.transform, prefab, tint, scale, Random.Range(0, 3), "dungeon");
                }
                else
                {
                    // fallback: 기존 방식 (틴트 + 스케일만)
                    monster.transform.localScale = Vector3.one * scale;
                    SpumCharacterManager.ApplyTintToExisting(monster.transform, tint);
                }
            }
        }

        /// <summary>
        /// 던전 유형에 따른 몬스터 색상 틴트와 스케일을 반환한다.
        /// </summary>
        private void GetDungeonVisual(out Color tint, out float scale)
        {
            if (_activeDungeon == null)
            {
                tint = Color.white;
                scale = 1f;
                return;
            }

            switch (_activeDungeon.dungeonType)
            {
                case DungeonType.Weapon:
                    tint = new Color(1f, 0.5f, 0.5f, 1f); // 붉은 틴트
                    scale = 1f;
                    break;
                case DungeonType.Experience:
                    tint = new Color(0.5f, 1f, 0.5f, 1f); // 녹색 틴트
                    scale = 0.8f;
                    break;
                case DungeonType.Equipment:
                    tint = new Color(0.5f, 0.7f, 1f, 1f); // 푸른 틴트
                    scale = 1.1f;
                    break;
                case DungeonType.Climber:
                    tint = new Color(0.8f, 0.4f, 1f, 1f); // 보라 틴트
                    scale = 1.3f;
                    break;
                case DungeonType.Enhancement:
                    tint = new Color(1f, 0.85f, 0.3f, 1f); // 금색 틴트
                    scale = 1f;
                    break;
                default:
                    tint = Color.white;
                    scale = 1f;
                    break;
            }
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (!_isActive) return;
            _killCount++;

            // 킬 진행 이벤트 (HUD 갱신용)
            EventBus.Publish(new DungeonBattleProgressEvent
            {
                KillCount = _killCount,
                TargetKills = _targetKills,
                RemainingTime = _remainingTime
            });
        }

        private void EndBattle()
        {
            if (!_isActive) return;
            _isActive = false;

            // 점수 계산: 킬 수 / 목표 킬 수 (초과 시 1.0 + 보너스)
            float score = _targetKills > 0 ? Mathf.Clamp01((float)_killCount / _targetKills) : 1f;

            // 남은 몬스터 제거
            if (_spawner != null)
                _spawner.ClearAllMonsters();

            // 던전 완료
            if (DungeonManager.Instance != null && _activeDungeon != null)
                DungeonManager.Instance.CompleteDungeon(_activeDungeon, score);

            // 결과 이벤트 (2026-04-23 QA: score >= 1f 부동소수점 오차 보정 → 0.9999)
            bool isSuccess = score >= 0.9999f;
            EventBus.Publish(new DungeonBattleEndedEvent
            {
                DungeonId = _activeDungeon?.id ?? "",
                DungeonName = _activeDungeon?.displayName ?? "",
                KillCount = _killCount,
                TargetKills = _targetKills,
                Score = score,
                IsSuccess = isSuccess
            });

            Debug.Log($"[DungeonBattleController] 전투 종료: {_killCount}/{_targetKills}킬, " +
                      $"점수: {score:F2}, 성공: {isSuccess}");

            // 일반 스테이지 스폰 재개
            if (_spawner != null)
                _spawner.ResumeSpawning();

            _activeDungeon = null;
        }

        /// <summary>던전 전투 강제 종료 (씬 전환 등)</summary>
        public void ForceEnd()
        {
            if (_isActive)
            {
                _remainingTime = 0f;
                EndBattle();
            }
        }

        private int GetAliveMonsterCount()
        {
            // 태그 또는 레이어로 카운트
            var monsters = FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < monsters.Length; i++)
            {
                if (monsters[i] == null) continue;
                var stats = monsters[i].GetComponent<CombatStats>();
                if (stats != null && !stats.IsDead)
                    count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
