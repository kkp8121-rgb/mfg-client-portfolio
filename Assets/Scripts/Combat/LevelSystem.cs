using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Utils;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 경험치 누적 및 레벨업을 관리하는 컴포넌트.
    /// ExpGainedEvent를 구독하여 EXP를 누적하고, 필요 경험치 도달 시 레벨업을 처리한다.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        private const string LEVEL_KEY_PREFIX = "level_";

        [Header("참조")]
        [SerializeField] private CombatStats _combatStats;
        [SerializeField] private CharacterDataSO _characterData;

        [Header("레벨 상태")]
        [SerializeField] private int _currentLevel = 1;
        [SerializeField] private long _currentExp;

        // 누적 레벨업 보너스 추적 (수정자 갱신용)
        private int _totalLevelBonusHp;
        private int _totalLevelBonusAtk;
        private int _totalLevelBonusDef;

        /// <summary>레벨업 시 부여되는 자유 스탯 포인트</summary>
        [Header("스탯 포인트")]
        [SerializeField] private int _statPointsPerLevel = 3;
        private int _availableStatPoints;

        // ── 프로퍼티 ──

        /// <summary>현재 레벨</summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>현재 누적 경험치 (현재 레벨 구간 내)</summary>
        public long CurrentExp => _currentExp;

        /// <summary>현재 레벨에서 다음 레벨까지 필요한 총 경험치</summary>
        public long RequiredExp => CombatFormula.RequiredExp(_currentLevel);

        /// <summary>EXP 바 표시용 비율 (0~1)</summary>
        public float ExpRatio
        {
            get
            {
                long req = RequiredExp;
                if (req <= 0) return 1f;
                return Mathf.Clamp01((float)_currentExp / req);
            }
        }

        /// <summary>사용 가능한 자유 스탯 포인트</summary>
        public int AvailableStatPoints => _availableStatPoints;

        /// <summary>
        /// 스탯 포인트를 소비한다. 성공 시 true.
        /// </summary>
        public bool ConsumeStatPoints(int amount)
        {
            if (amount <= 0 || _availableStatPoints < amount) return false;
            _availableStatPoints -= amount;
            return true;
        }

        /// <summary>
        /// 스탯 포인트를 복원한다 (초기화 시).
        /// </summary>
        public void RestoreStatPoints(int amount)
        {
            if (amount > 0) _availableStatPoints += amount;
        }

        // ── 라이프사이클 ──

        private void OnEnable()
        {
            EventBus.Subscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Subscribe<ForceLevelUpEvent>(OnForceLevelUp);
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);

            // 시작 시 1회 복원
            SyncFromSaveData();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Unsubscribe<ForceLevelUpEvent>(OnForceLevelUp);
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
        }

        /// <summary>
        /// LoadCompletedEvent 시 SaveData에서 레벨/경험치를 재동기화한다.
        /// 이전 세션 런타임 값(Lv50)이 새 세이브(Lv1) 로드 후에도 남는 버그 방지.
        /// </summary>
        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            SyncFromSaveData();
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SyncToSaveData();

        private void SyncFromSaveData()
        {
            var data = SaveManager.Instance?.CurrentData;
            if (data == null) return;
            // level == 1이어도 강제 적용 (이전 세션 잔재 제거)
            Initialize(data.player.level, data.player.exp);
#if UNITY_EDITOR
            Debug.Log($"[LevelSystem] SaveData 동기화: Lv.{_currentLevel}, Exp={_currentExp}");
#endif
        }

        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition == QuestCondition.LevelUp && _currentLevel > 0)
                EventBus.Publish(new LevelUpEvent { CurrentLevel = _currentLevel });
        }

        // ── 공개 메서드 ──

        /// <summary>
        /// 레벨과 경험치를 지정하여 초기화한다. 세이브/로드 시 사용.
        /// </summary>
        public void Initialize(int level, long currentExp)
        {
            _currentLevel = Mathf.Max(1, level);
            _currentExp = System.Math.Max(0L, currentExp);
        }

        // ── 내부 로직 ──

        /// <summary>
        /// ExpGainedEvent 핸들러. 경험치를 누적하고 레벨업 조건을 검사한다.
        /// </summary>
        private void OnExpGained(ExpGainedEvent evt)
        {
            if (evt.Amount <= 0) return;

            _currentExp += evt.Amount;
            ProcessLevelUp();
            SyncToSaveData();
        }

        private void SyncToSaveData()
        {
            var data = SaveManager.Instance?.CurrentData;
            if (data == null) return;
            data.player.level = _currentLevel;
            data.player.exp = _currentExp;
        }

        private void OnForceLevelUp(ForceLevelUpEvent evt)
        {
            for (int i = 0; i < evt.Levels; i++)
            {
                long needed = RequiredExp - _currentExp;
                if (needed <= 0) needed = 1;
                _currentExp += needed;
                ProcessLevelUp();
            }
            SyncToSaveData();
        }

        /// <summary>
        /// 현재 EXP가 필요량 이상이면 레벨업을 처리한다.
        /// 초과 EXP가 다시 필요량 이상이면 연속 레벨업한다.
        /// </summary>
        private void ProcessLevelUp()
        {
            while (_currentExp >= RequiredExp)
            {
                _currentExp -= RequiredExp;

                int previousLevel = _currentLevel;
                _currentLevel++;

                // 스탯 포인트 부여
                _availableStatPoints += _statPointsPerLevel;

                // CombatStats에 레벨업 성장치 적용
                ApplyLevelUpBonus();

                // 레벨업 이벤트 발행
                int hpGain = _characterData != null ? _characterData.hpPerLevel : 0;
                int atkGain = _characterData != null ? _characterData.atkPerLevel : 0;
                int defGain = _characterData != null ? _characterData.defPerLevel : 0;

                EventBus.Publish(new LevelUpEvent
                {
                    PreviousLevel = previousLevel,
                    CurrentLevel = _currentLevel,
                    HpGain = hpGain,
                    AtkGain = atkGain,
                    DefGain = defGain,
                    StatPoints = _statPointsPerLevel
                });

                // 2026-04-23 이슈 15 FeedbackBus: 레벨업 Toast + 쉐이크 + Player 위치 "+N P" 수치
                var pc = Object.FindFirstObjectByType<PlayerCharacter>();
                Vector3 lvlPos = pc != null ? pc.transform.position : Vector3.zero;
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    $"Lv.{previousLevel} → Lv.{_currentLevel} 달성!",
                    lvlPos,
                    amountText: $"+{_statPointsPerLevel} P",
                    shakeIntensity: 2);
            }
        }

        /// <summary>
        /// CharacterDataSO의 레벨당 성장치를 CombatStats에 보너스로 추가한다.
        /// </summary>
        private void ApplyLevelUpBonus()
        {
            if (_combatStats == null || _characterData == null) return;

            _totalLevelBonusHp += _characterData.hpPerLevel;
            _totalLevelBonusAtk += _characterData.atkPerLevel;
            _totalLevelBonusDef += _characterData.defPerLevel;

            _combatStats.SetCpReason("레벨업");

            if (_totalLevelBonusHp != 0)
                _combatStats.AddModifier(LEVEL_KEY_PREFIX + "hp",
                    new StatModifier(ModifierSource.Level, LEVEL_KEY_PREFIX, StatType.MaxHp, _totalLevelBonusHp, 0f));

            if (_totalLevelBonusAtk != 0)
                _combatStats.AddModifier(LEVEL_KEY_PREFIX + "atk",
                    new StatModifier(ModifierSource.Level, LEVEL_KEY_PREFIX, StatType.Atk, _totalLevelBonusAtk, 0f));

            if (_totalLevelBonusDef != 0)
                _combatStats.AddModifier(LEVEL_KEY_PREFIX + "def",
                    new StatModifier(ModifierSource.Level, LEVEL_KEY_PREFIX, StatType.Def, _totalLevelBonusDef, 0f));
        }
    }
}
