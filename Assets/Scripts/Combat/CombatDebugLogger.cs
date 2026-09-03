#if UNITY_EDITOR
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 전투 관련 이벤트를 콘솔에 로깅하는 디버그 컴포넌트.
    /// 프로토타입 검증용.
    /// </summary>
    public class CombatDebugLogger : MonoBehaviour
    {
        [Header("로그 필터")]
        [SerializeField] private bool logPlayerState = true;
        [SerializeField] private bool logMonsterDeath = true;
        [SerializeField] private bool logLoot = true;
        [SerializeField] private bool logStage = true;
        [SerializeField] private bool logExp = true;
        [SerializeField] private bool logPeriodicStatus = true;

        [Header("상태 로그 간격 (초)")]
        [SerializeField] private float statusInterval = 3f;

        private PlayerCharacter _player;
        private CombatStats _playerStats;
        private LevelSystem _levelSystem;
        private StageManager _stageManager;

        private CharacterState _lastPlayerState;
        private float _statusTimer;
        private int _totalKills;
        private long _totalGoldEarned;
        private long _totalExpEarned;

        private void OnEnable()
        {
            EventBus.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Subscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Subscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Unsubscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Unsubscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerCharacter>();
            _stageManager = FindFirstObjectByType<StageManager>();

            if (_player != null)
            {
                _playerStats = _player.GetComponent<CombatStats>();
                _levelSystem = _player.GetComponent<LevelSystem>();
            }

            Debug.Log("<color=cyan>=== [CombatDebug] 전투 디버그 로거 시작 ===</color>");

            if (_player == null)
                Debug.LogWarning("[CombatDebug] PlayerCharacter를 찾을 수 없습니다!");
            else
                LogPlayerStatus();
        }

        private void Update()
        {
            if (_player == null) return;

            if (logPlayerState && _player.CurrentState != _lastPlayerState)
            {
                string targetInfo = "";
                if (_player.CurrentTarget != null)
                    targetInfo = $" | 타겟 HP: {_player.CurrentTarget.CurrentHp}/{_player.CurrentTarget.MaxHp}";

                Debug.Log($"<color=yellow>[Player] {_lastPlayerState} -> {_player.CurrentState}{targetInfo}</color>");
                _lastPlayerState = _player.CurrentState;
            }

            if (logPeriodicStatus)
            {
                _statusTimer += Time.deltaTime;
                if (_statusTimer >= statusInterval)
                {
                    _statusTimer = 0f;
                    LogPlayerStatus();
                }
            }
        }

        private void LogPlayerStatus()
        {
            if (_playerStats == null) return;

            string expInfo = "";
            if (_levelSystem != null)
                expInfo = $" | EXP: {_levelSystem.CurrentExp}/{_levelSystem.RequiredExp} ({_levelSystem.ExpRatio:P0})";

            string stageInfo = "";
            if (_stageManager != null)
                stageInfo = $" | {_stageManager.CurrentStageName} (처치: {_stageManager.KillCount})";

            Debug.Log($"<color=white>[Status] " +
                $"HP: {_playerStats.CurrentHp}/{_playerStats.MaxHp} | " +
                $"ATK: {_playerStats.Atk} DEF: {_playerStats.Def} | " +
                $"Lv.{(_levelSystem != null ? _levelSystem.CurrentLevel : 1)}{expInfo}{stageInfo} | " +
                $"총 처치: {_totalKills} | 총 골드: {_totalGoldEarned} | 총 EXP: {_totalExpEarned}" +
                $"</color>");
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _totalKills++;
            if (logMonsterDeath)
            {
                string bossTag = "";
                var ctrl = evt.Monster != null ? evt.Monster.GetComponent<MonsterController>() : null;
                if (ctrl != null && ctrl.IsBoss)
                    bossTag = " <color=red>[BOSS]</color>";

                Debug.Log($"<color=red>[Kill #{_totalKills}]{bossTag} 몬스터 처치 at ({evt.Position.x:F1}, {evt.Position.y:F1})</color>");
            }
        }

        private void OnGoldGained(GoldGainedEvent evt)
        {
            _totalGoldEarned += evt.Amount;
            if (logLoot)
                Debug.Log($"<color=#FFD700>[Gold] +{evt.Amount} (누적: {_totalGoldEarned})</color>");
        }

        private void OnExpGained(ExpGainedEvent evt)
        {
            _totalExpEarned += evt.Amount;
            if (logExp)
                Debug.Log($"<color=#00BFFF>[EXP] +{evt.Amount} (누적: {_totalExpEarned})</color>");
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            if (logStage)
                Debug.Log($"<color=green>========== [Stage] -> {evt.DisplayName} ==========</color>");
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            Debug.Log($"<color=magenta>★★★ [LEVEL UP!] Lv.{evt.PreviousLevel} -> Lv.{evt.CurrentLevel} ★★★</color>");
            LogPlayerStatus();
        }
    }
}
#endif
