using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 영웅의 힘 시스템.
    /// 전투력(CP) 마일스톤 달성 시 영구 패시브 보너스를 적용한다.
    /// 해금: 레벨 79 (가이드 퀘스트로 유도).
    /// </summary>
    public class HeroPowerManager : MonoBehaviour
    {
        public static HeroPowerManager Instance { get; private set; }

        [Header("영웅의 힘 설정")]
        [SerializeField] private HeroPowerDataSO _config;

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked => _isUnlocked;
        private bool _isUnlocked;

        /// <summary>현재 달성된 마일스톤 수</summary>
        public int UnlockedMilestones => _unlockedMilestones;
        private int _unlockedMilestones;

        /// <summary>설정 SO</summary>
        public HeroPowerDataSO Config => _config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CheckUnlock();
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<CpChangedEvent>.Subscribe(OnCpChanged);
            EventBus<QuestStateRefreshEvent>.Subscribe(OnQuestStateRefresh);
        }

        private void OnDisable()
        {
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<CpChangedEvent>.Unsubscribe(OnCpChanged);
            EventBus<QuestStateRefreshEvent>.Unsubscribe(OnQuestStateRefresh);
        }

        /// <summary>
        /// 가이드 퀘스트 활성화 시 HeroPowerMilestone 조건의 현재 상태를 재발행한다.
        /// 현재 달성된 마일스톤 수를 TotalMilestones로 통지.
        /// </summary>
        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition != QuestCondition.HeroPowerMilestone) return;
            if (_unlockedMilestones <= 0) return;
            if (_config == null) return;

            int lastIndex = _unlockedMilestones - 1;
            var milestone = _config.GetMilestone(lastIndex);

            EventBus.Publish(new HeroPowerMilestoneEvent
            {
                MilestoneIndex = lastIndex,
                RequiredCp = milestone.requiredCp,
                BonusStat = milestone.bonusStat,
                BonusValue = milestone.bonusPercent,
                TotalMilestones = _unlockedMilestones
            });
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            CheckUnlock();
        }

        private void CheckUnlock()
        {
            if (_isUnlocked) return;
            if (_config == null) return;

            var levelSystem = Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem == null) return;

            if (levelSystem.CurrentLevel >= _config.UnlockLevel)
            {
                _isUnlocked = true;
                Debug.Log("[HeroPowerManager] 영웅의 힘 해금!");
                // 해금 시 즉시 현재 CP로 마일스톤 체크
                CheckMilestones();
            }
        }

        private void OnCpChanged(CpChangedEvent evt)
        {
            if (!_isUnlocked) return;
            CheckMilestonesWithCp(evt.CurrentCp);
        }

        private void CheckMilestones()
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            CheckMilestonesWithCp(stats.PowerScore);
        }

        private void CheckMilestonesWithCp(long currentCp)
        {
            if (_config == null) return;

            int newCount = _config.CalculateUnlockedCount(currentCp);
            if (newCount <= _unlockedMilestones) return;

            // 새로 달성된 마일스톤 적용
            for (int i = _unlockedMilestones; i < newCount; i++)
            {
                var milestone = _config.GetMilestone(i);
                ApplyMilestoneBonus(i, milestone);

                EventBus.Publish(new HeroPowerMilestoneEvent
                {
                    MilestoneIndex = i,
                    RequiredCp = milestone.requiredCp,
                    BonusStat = milestone.bonusStat,
                    BonusValue = milestone.bonusPercent,
                    TotalMilestones = newCount
                });

                Debug.Log($"[HeroPowerManager] 마일스톤 #{i + 1} 달성! CP {milestone.requiredCp} — {milestone.bonusStat} +{milestone.bonusPercent}%");
            }

            _unlockedMilestones = newCount;
        }

        private void ApplyMilestoneBonus(int index, HeroPowerMilestone milestone)
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            string modKey = $"hero_power_{index}";
            stats.AddModifier(modKey, new StatModifier
            {
                source = ModifierSource.HeroPower,
                sourceId = modKey,
                statType = milestone.bonusStat,
                flatBonus = 0f,
                percentBonus = milestone.bonusPercent / 100f
            });

            stats.SetCpReason("영웅의 힘");
        }

        /// <summary>다음 마일스톤까지 필요한 CP</summary>
        public long GetNextMilestoneCp()
        {
            if (_config == null) return 0;
            return _config.GetNextMilestoneCp(_unlockedMilestones);
        }

        /// <summary>세이브 데이터 로드</summary>
        public void LoadFromSave(HeroPowerSaveData data)
        {
            if (data == null) return;
            _unlockedMilestones = 0;

            // 저장된 마일스톤 수만큼 보너스 재적용
            for (int i = 0; i < data.unlockedMilestones && i < _config.MilestoneCount; i++)
            {
                var milestone = _config.GetMilestone(i);
                ApplyMilestoneBonus(i, milestone);
            }
            _unlockedMilestones = data.unlockedMilestones;
        }

        /// <summary>세이브 데이터 생성</summary>
        public HeroPowerSaveData ToSaveData()
        {
            return new HeroPowerSaveData { unlockedMilestones = _unlockedMilestones };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
