using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 일일 체크리스트 11항목의 전체 완료 보너스 + 랜덤 2배 보상 관리.
    /// QuestManager의 QuestType.Daily 퀘스트를 감시하여 전부 완료 시 보너스를 지급한다.
    /// </summary>
    public class DailyChecklistManager : MonoBehaviour
    {
        public static DailyChecklistManager Instance { get; private set; }

        [Header("전체 완료 보너스")]
        [SerializeField] private int _allClearRuby = 100;
        [SerializeField] private int _allClearWeaponTicket = 2;
        [SerializeField] private int _allClearGold = 10000;

        /// <summary>오늘 랜덤 2배 보상 대상 퀘스트 인덱스 (0~10)</summary>
        private int _doubleRewardIndex;

        /// <summary>전체 완료 보너스가 이미 지급되었는지</summary>
        private bool _allClearClaimed;

        /// <summary>오늘 날짜 (리셋 감지용)</summary>
        private string _lastCheckDate;

        /// <summary>랜덤 2배 보상 대상 퀘스트 인덱스</summary>
        public int DoubleRewardIndex => _doubleRewardIndex;

        /// <summary>전체 완료 보너스 지급 완료 여부</summary>
        public bool IsAllClearClaimed => _allClearClaimed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetIfNewDay();
        }

        private void OnEnable()
        {
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
        }

        private void OnDisable()
        {
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
        }

        /// <summary>
        /// 날짜가 바뀌었으면 상태를 리셋한다.
        /// </summary>
        public void ResetIfNewDay()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.Equals(_lastCheckDate, today, StringComparison.Ordinal))
                return;

            _lastCheckDate = today;
            _allClearClaimed = false;

            // 시드 기반 랜덤 2배 항목 (매일 동일한 결과)
            int seed = today.GetHashCode();
            _doubleRewardIndex = Mathf.Abs(seed) % 11;

            Debug.Log($"[DailyChecklist] 리셋 — 2배 보상 인덱스: {_doubleRewardIndex}");
        }

        /// <summary>
        /// 특정 퀘스트가 2배 보상 대상인지 확인한다.
        /// </summary>
        public bool IsDoubleRewardQuest(string questId)
        {
            var qm = QuestManager.Instance;
            if (qm == null) return false;

            int dailyIndex = 0;
            var activeQuests = qm.ActiveQuests;
            for (int i = 0; i < activeQuests.Count; i++)
            {
                QuestDataSO data = qm.GetQuestData(activeQuests[i].questId);
                if (data == null || data.questType != QuestType.Daily) continue;

                if (data.id == questId)
                    return dailyIndex == _doubleRewardIndex;

                dailyIndex++;
            }

            return false;
        }

        /// <summary>
        /// 일일 퀘스트 완료 상황을 확인하고 전체 완료 보너스를 지급한다.
        /// </summary>
        public (int completed, int total) GetDailyProgress()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return (0, 0);

            int total = 0;
            int completed = 0;

            var activeQuests = qm.ActiveQuests;
            for (int i = 0; i < activeQuests.Count; i++)
            {
                QuestDataSO data = qm.GetQuestData(activeQuests[i].questId);
                if (data == null || data.questType != QuestType.Daily) continue;

                total++;
                if (activeQuests[i].isCompleted)
                    completed++;
            }

            return (completed, total);
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            if (evt.QuestType != QuestType.Daily) return;

            ResetIfNewDay();
            CheckAllCleared();
        }

        private void CheckAllCleared()
        {
            if (_allClearClaimed) return;

            var (completed, total) = GetDailyProgress();
            if (total <= 0 || completed < total) return;

            _allClearClaimed = true;

            // 보너스 지급
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(CurrencyType.Ruby, _allClearRuby);
                CurrencyManager.Instance.Add(CurrencyType.WeaponTicket, _allClearWeaponTicket);
                CurrencyManager.Instance.Add(CurrencyType.Gold, _allClearGold);
            }

            EventBus.Publish(new DailyChecklistAllClearEvent
            {
                RubyReward = _allClearRuby,
                TicketReward = _allClearWeaponTicket,
                GoldReward = _allClearGold
            });

            Debug.Log($"[DailyChecklist] 전체 완료! 보너스: 루비 {_allClearRuby} + 무기소환권 {_allClearWeaponTicket} + 골드 {_allClearGold}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
