using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Data;
using MkLike.Dungeon;
using MkLike.Equipment;
using MkLike.Quest;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 빨간 점(레드닷) 알림 카테고리.
    /// </summary>
    public enum RedDotCategory
    {
        Equipment,  // 현재보다 좋은 미장착 장비 보유
        Skill,      // 미사용 스탯/스킬 포인트 존재
        Quest,      // 완료 보상 미수령
        Dungeon,    // 열쇠 보유 중 (미사용)
        Shop        // 무료 아이템 수령 가능
    }

    /// <summary>
    /// 5개 카테고리별 빨간 점 상태를 관리한다.
    /// 이벤트 구독으로 자동 갱신하며, UI에서 HasRedDot()으로 조회한다.
    /// </summary>
    public class RedDotManager : MonoBehaviour
    {
        public static RedDotManager Instance { get; private set; }

        /// <summary>카테고리별 빨간 점 상태 변경 시 발행</summary>
        public event Action<RedDotCategory, bool> OnRedDotChanged;

        private readonly Dictionary<RedDotCategory, bool> _states = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 초기값 세팅
            foreach (RedDotCategory cat in Enum.GetValues(typeof(RedDotCategory)))
                _states[cat] = false;
        }

        private void OnEnable()
        {
            // Equipment
            EventBus<EquipmentInventoryChangedEvent>.Subscribe(OnEquipmentInventoryChanged);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);

            // Skill (스탯 포인트 = 레벨업 시 부여)
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<StatAllocatedEvent>.Subscribe(OnStatAllocated);

            // Quest
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<QuestRewardClaimedEvent>.Subscribe(OnQuestRewardClaimed);

            // Dungeon (열쇠 관련 — 던전 완료 시 갱신)
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<CurrencyChangedEvent>.Subscribe(OnCurrencyChanged);

            // Shop (무료 수령 — 출석/오프라인 이벤트로 간접 감지)
            EventBus<AttendanceCheckedEvent>.Subscribe(OnAttendanceChecked);
        }

        private void OnDisable()
        {
            EventBus<EquipmentInventoryChangedEvent>.Unsubscribe(OnEquipmentInventoryChanged);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<StatAllocatedEvent>.Unsubscribe(OnStatAllocated);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<QuestRewardClaimedEvent>.Unsubscribe(OnQuestRewardClaimed);
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<CurrencyChangedEvent>.Unsubscribe(OnCurrencyChanged);
            EventBus<AttendanceCheckedEvent>.Unsubscribe(OnAttendanceChecked);
        }

        /// <summary>
        /// 특정 카테고리에 빨간 점이 활성화되어 있는지 반환한다.
        /// </summary>
        public bool HasRedDot(RedDotCategory category)
        {
            return _states.TryGetValue(category, out bool v) && v;
        }

        /// <summary>
        /// 모든 카테고리의 빨간 점 상태를 강제 갱신한다.
        /// 초기화 또는 씬 전환 시 호출.
        /// </summary>
        public void RefreshAll()
        {
            RefreshEquipment();
            RefreshSkill();
            RefreshQuest();
            RefreshDungeon();
            RefreshShop();
        }

        // ── 카테고리별 갱신 로직 ──

        private void RefreshEquipment()
        {
            bool hasUpgrade = false;

            if (RecommendationManager.Instance != null)
            {
                var recs = RecommendationManager.Instance.GetRecommendedEquipment();
                hasUpgrade = recs != null && recs.Count > 0;
            }

            SetState(RedDotCategory.Equipment, hasUpgrade);
        }

        private void RefreshSkill()
        {
            bool hasPoints = false;

            // LevelSystem의 미사용 스탯 포인트 확인
            var levelSystem = UnityEngine.Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem != null)
            {
                hasPoints = levelSystem.AvailableStatPoints > 0;
            }

            SetState(RedDotCategory.Skill, hasPoints);
        }

        private void RefreshQuest()
        {
            bool hasUnclaimed = false;

            var qm = QuestManager.Instance;
            if (qm != null)
            {
                var activeQuests = qm.ActiveQuests;
                for (int i = 0; i < activeQuests.Count; i++)
                {
                    if (activeQuests[i].isCompleted && !activeQuests[i].isRewardClaimed)
                    {
                        hasUnclaimed = true;
                        break;
                    }
                }
            }

            SetState(RedDotCategory.Quest, hasUnclaimed);
        }

        private void RefreshDungeon()
        {
            bool hasKeys = false;

            var dm = DungeonManager.Instance;
            if (dm != null)
            {
                hasKeys = dm.CurrentKeys > 0;
            }

            SetState(RedDotCategory.Dungeon, hasKeys);
        }

        private void RefreshShop()
        {
            // 상점 무료 아이템은 별도 API가 없으므로
            // 출석 미완료 여부로 간접 판단 (출석 보상 = 매일 1회 무료)
            bool hasFree = false;

            var dcm = DailyChecklistManager.Instance;
            if (dcm != null)
            {
                var (completed, total) = dcm.GetDailyProgress();
                hasFree = total > 0 && completed < total;
            }

            SetState(RedDotCategory.Shop, hasFree);
        }

        // ── 이벤트 핸들러 ──

        private void OnEquipmentInventoryChanged(EquipmentInventoryChangedEvent evt) => RefreshEquipment();
        private void OnEquipmentChanged(EquipmentChangedEvent evt) => RefreshEquipment();
        private void OnLevelUp(LevelUpEvent evt) { RefreshSkill(); RefreshDungeon(); }
        private void OnStatAllocated(StatAllocatedEvent evt) => RefreshSkill();
        private void OnQuestCompleted(QuestCompletedEvent evt) { RefreshQuest(); RefreshShop(); }
        private void OnQuestRewardClaimed(QuestRewardClaimedEvent evt) => RefreshQuest();
        private void OnDungeonCompleted(DungeonCompletedEvent evt) => RefreshDungeon();
        private void OnCurrencyChanged(CurrencyChangedEvent evt) => RefreshDungeon();
        private void OnAttendanceChecked(AttendanceCheckedEvent evt) => RefreshShop();

        // ── 내부 유틸 ──

        private void SetState(RedDotCategory category, bool active)
        {
            if (_states.TryGetValue(category, out bool prev) && prev == active)
                return;

            _states[category] = active;
            OnRedDotChanged?.Invoke(category, active);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
