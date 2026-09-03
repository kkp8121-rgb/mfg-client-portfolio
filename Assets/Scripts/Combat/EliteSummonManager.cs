using UnityEngine;
using Lean.Pool;
using DG.Tweening; // 2026-04-23 이슈 14: 정예 소환 DOPunchScale 애니
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 엘리트 몬스터 소환 시스템.
    /// HuntPoint를 소비하여 정예 몬스터를 소환하고, 처치 시 등급별 장비를 드롭한다.
    /// 소환 레벨이 올라갈수록 고등급 장비가 해금된다.
    /// LeanPool 기반 풀링.
    /// </summary>
    public class EliteSummonManager : MonoBehaviour
    {
        public static EliteSummonManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private EliteSummonSO _config;

        [Header("참조")]
        [SerializeField] private MonsterSpawner _spawner;

        private const string SAVE_KEY_LEVEL = "elite_summon_level";
        private const string SAVE_KEY_TOTAL_SPENT = "elite_total_spent";
        private const string SAVE_KEY_KILL_COUNT = "elite_kill_count";

        private int _summonLevel = 1;
        private int _totalSpent;
        private int _killCount;
        private float _cooldownTimer;
        private bool _isEliteAlive;
        private GameObject _currentElite;

        private static readonly string[] GRADE_NAMES = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic" };

        /// <summary>현재 소환 레벨</summary>
        public int SummonLevel => _summonLevel;

        /// <summary>총 소비 HuntPoint</summary>
        public int TotalSpent => _totalSpent;

        /// <summary>엘리트 처치 수</summary>
        public int KillCount => _killCount;

        /// <summary>소환 가능 여부</summary>
        public bool CanSummon => !_isEliteAlive && _cooldownTimer <= 0f && HasEnoughHuntPoint();

        /// <summary>쿨다운 남은 시간</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        /// <summary>현재 소환 비용</summary>
        public int CurrentSummonCost => _config != null ? _config.GetSummonCost(_summonLevel) : 100;

        /// <summary>다음 레벨업 비용 (-1이면 최대 레벨)</summary>
        public int NextLevelUpCost => _config != null ? _config.GetLevelUpCost(_summonLevel) : -1;

        /// <summary>현재 해금된 최고 등급</summary>
        public string CurrentMaxGrade
        {
            get
            {
                var data = _config?.GetLevelData(_summonLevel);
                return data?.maxUnlockedGrade ?? "Normal";
            }
        }

        private void Awake()
        {
            Instance = this;
            LoadData();
        }

        private void OnEnable()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
        }

        /// <summary>
        /// 가이드 퀘스트 활성화 시 EliteSummonLevelReach 조건의 현재 상태를 재발행한다.
        /// </summary>
        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition != QuestCondition.EliteSummonLevelReach) return;
            if (_summonLevel <= 0) return;

            var data = _config?.GetLevelData(_summonLevel);
            string newMaxGrade = data?.maxUnlockedGrade ?? "Normal";

            EventBus.Publish(new EliteSummonLevelUpEvent
            {
                PreviousLevel = _summonLevel,
                NewLevel = _summonLevel,
                NewMaxGrade = newMaxGrade
            });
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ── 소환 ──

        /// <summary>엘리트 몬스터를 소환한다. HuntPoint 소비.</summary>
        public bool TrySummon()
        {
            if (!CanSummon) return false;
            if (_config == null || _spawner == null) return false;

            int cost = CurrentSummonCost;
            var cm = CurrencyManager.Instance;
            if (cm == null) return false;

            if (!cm.Spend(CurrencyType.HuntPoint, cost))
            {
                EventBus.Publish(new CurrencyShortageEvent
                {
                    Type = CurrencyType.HuntPoint,
                    Required = cost,
                    Current = cm.GetAmount(CurrencyType.HuntPoint)
                });
                return false;
            }

            _totalSpent += cost;
            SpawnEliteMonster();
            _cooldownTimer = _config.SummonCooldown;

            EventBus.Publish(new EliteSummonedEvent
            {
                SummonLevel = _summonLevel,
                CostPaid = cost,
                SpawnPosition = _currentElite != null ? _currentElite.transform.position : Vector3.zero
            });

            SaveData();
            return true;
        }

        // ── 레벨업 ──

        /// <summary>소환 레벨을 올린다. 누적 HuntPoint가 충분하면 자동.</summary>
        public bool TryLevelUp()
        {
            if (_config == null) return false;
            if (_summonLevel >= _config.MaxLevel) return false;

            int requiredTotal = _config.GetLevelUpCost(_summonLevel);
            if (requiredTotal < 0 || _totalSpent < requiredTotal) return false;

            int prevLevel = _summonLevel;
            _summonLevel++;

            var newData = _config.GetLevelData(_summonLevel);
            string newMaxGrade = newData?.maxUnlockedGrade ?? "Normal";

            EventBus.Publish(new EliteSummonLevelUpEvent
            {
                PreviousLevel = prevLevel,
                NewLevel = _summonLevel,
                NewMaxGrade = newMaxGrade
            });

            Debug.Log($"[EliteSummonManager] 소환 레벨업! Lv.{prevLevel} → Lv.{_summonLevel} (최고 등급: {newMaxGrade})");
            SaveData();
            return true;
        }

        // ── 스폰 ──

        private void SpawnEliteMonster()
        {
            var arenaMap = _spawner.GetComponentInChildren<ArenaMap>();
            if (arenaMap == null)
                arenaMap = Object.FindFirstObjectByType<ArenaMap>();

            Vector2 spawnPos = arenaMap != null ? arenaMap.GetSpawnPosition() : (Vector2)Vector3.zero;

            // 스포너의 몬스터 프리팹으로 스폰
            GameObject prefab = _spawner != null ? _spawner.MonsterPrefab : null;
            if (prefab == null)
            {
                Debug.LogWarning("[EliteSummonManager] 몬스터 프리팹을 찾을 수 없습니다.");
                return;
            }

            GameObject elite = LeanPool.Spawn(prefab, (Vector3)spawnPos, Quaternion.identity);
            if (elite == null)
            {
                Debug.LogWarning("[EliteSummonManager] 엘리트 스폰 실패");
                return;
            }

            var levelData = _config.GetLevelData(_summonLevel);
            int floor = GetCurrentFloor();

            // 스탯 강화
            var stats = elite.GetComponent<CombatStats>();
            if (stats != null)
            {
                float hpMult = levelData?.hpMultiplier ?? 3f;
                float atkMult = levelData?.atkMultiplier ?? 1.5f;

                stats.Initialize(
                    Mathf.RoundToInt(CombatFormula.MonsterHp(floor) * hpMult),
                    Mathf.RoundToInt(CombatFormula.MonsterAtk(floor) * atkMult),
                    CombatFormula.MonsterDef(floor),
                    0f,
                    1.0f * (_config.AttackSpeedMultiplier),
                    2.0f * (_config.MoveSpeedMultiplier)
                );
            }

            // 엘리트 플래그 설정
            var controller = elite.GetComponent<MonsterController>();
            if (controller != null)
            {
                controller.IsBoss = false;
                controller.IsChapterBoss = false;
                controller.IsElite = true;
                controller.EliteSummonLevel = _summonLevel;
                controller.ArenaMap = arenaMap;

                var player = Object.FindFirstObjectByType<CharacterCombat>();
                if (player != null)
                    controller.SetPlayer(player.transform, player.GetComponent<CombatStats>());
            }

            // 엘리트 비주얼: 약간 크고 붉은 틴트
            elite.transform.localScale = Vector3.one * 1.4f;
            var sr = elite.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                sr.color = new Color(1f, 0.5f, 0.5f, 1f);

            _currentElite = elite;
            _isEliteAlive = true;

            // 2026-04-23 이슈 14: 정예 소환 시 시각 피드백 강화
            //   - 기존: 스케일 1.4배 + 붉은 틴트만 (유저가 일반 몬스터와 구분 어려움)
            //   - 개선: VFX + FeedbackBus를 통한 3-tier 피드백 (Toast/Shake/수치)
            var visualEffect = Object.FindFirstObjectByType<MonsterVisualEffect>();
            if (visualEffect != null)
                visualEffect.PlayBossSpawn(elite.transform, isChapterBoss: false);
            elite.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 2, 0.5f);

            // FeedbackBus: Toast("정예 소환!") + ScreenShake(Medium=2) + DamageText("정예!")
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.EliteSummon,
                $"정예 소환! Lv.{_summonLevel}",
                elite.transform.position,
                amountText: "정예!",
                shakeIntensity: 2);

            Debug.Log($"[EliteSummonManager] 엘리트 소환! Lv.{_summonLevel}, HP x{levelData?.hpMultiplier:F1}, ATK x{levelData?.atkMultiplier:F1}");
        }

        // ── 처치 ──

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (!_isEliteAlive || _currentElite == null) return;
            if (evt.Monster != _currentElite) return;

            _isEliteAlive = false;
            _killCount++;

            // 등급별 장비 드롭
            DropEliteEquipment(evt.Position);

            // 레벨업 체크
            TryLevelUp();

            SaveData();
        }

        private void DropEliteEquipment(Vector3 position)
        {
            if (_config == null) return;

            // 등급 결정
            string grade = RollGrade();

            // EliteKilledEvent를 발행하면 EquipmentManager가 구독하여 인벤토리에 추가
            EventBus.Publish(new EliteKilledEvent
            {
                SummonLevel = _summonLevel,
                Position = position,
                DroppedEquipmentId = "",  // EquipmentManager가 카탈로그에서 랜덤 선택
                DroppedGrade = grade
            });

            // 드롭 비주얼 이벤트
            int gradeIdx = GradeToIndex(grade);
            EventBus.Publish(new LootDroppedEvent
            {
                Type = CurrencyType.Gold,
                Amount = 0,
                Position = position,
                Grade = gradeIdx
            });

            Debug.Log($"[EliteSummonManager] 엘리트 드롭! 등급: {grade}");
        }

        private string RollGrade()
        {
            float[] weights = _config.GetGradeWeights(_summonLevel);
            if (weights == null || weights.Length == 0)
                return "Normal";

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];

            float roll = Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < weights.Length && i < GRADE_NAMES.Length; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return GRADE_NAMES[i];
            }

            return GRADE_NAMES[0];
        }

        // ── 유틸 ──

        private bool HasEnoughHuntPoint()
        {
            var cm = CurrencyManager.Instance;
            return cm != null && cm.GetAmount(CurrencyType.HuntPoint) >= CurrentSummonCost;
        }

        private int GetCurrentFloor()
        {
            var stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager != null)
                return (stageManager.CurrentChapter - 1) * 10 + stageManager.CurrentStageIndex;
            return 1;
        }

        private static int GradeToIndex(string grade)
        {
            return grade switch
            {
                "Normal" => 0,
                "Rare" => 1,
                "Epic" => 2,
                "Unique" => 3,
                "Legendary" => 4,
                "Mythic" => 5,
                _ => 0
            };
        }

        // ── 저장/로드 ──

        private void SaveData()
        {
            PlayerPrefs.SetInt(SAVE_KEY_LEVEL, _summonLevel);
            PlayerPrefs.SetInt(SAVE_KEY_TOTAL_SPENT, _totalSpent);
            PlayerPrefs.SetInt(SAVE_KEY_KILL_COUNT, _killCount);
        }

        private void LoadData()
        {
            _summonLevel = Mathf.Max(1, PlayerPrefs.GetInt(SAVE_KEY_LEVEL, 1));
            _totalSpent = PlayerPrefs.GetInt(SAVE_KEY_TOTAL_SPENT, 0);
            _killCount = PlayerPrefs.GetInt(SAVE_KEY_KILL_COUNT, 0);
        }
    }
}
