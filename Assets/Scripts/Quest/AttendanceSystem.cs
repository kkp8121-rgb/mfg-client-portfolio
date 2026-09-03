using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Net;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 출석 보상 시스템.
    /// 매일 접속 시 출석 체크(일 1회), 7일 사이클 보상 지급.
    /// CurrencyManager를 통해 보상을 지급한다.
    /// </summary>
    public class AttendanceSystem : MonoBehaviour
    {
        public static AttendanceSystem Instance { get; private set; }

        /// <summary>7일 사이클 보상 테이블</summary>
        private static readonly (CurrencyType type, int amount)[] DailyRewards =
        {
            (CurrencyType.Gold, 1000),             // Day 1
            (CurrencyType.Ruby, 50),               // Day 2
            (CurrencyType.WeaponTicket, 1),        // Day 3
            (CurrencyType.Gold, 3000),             // Day 4
            (CurrencyType.WeaponTicket, 1),        // Day 5
            (CurrencyType.Ruby, 100),              // Day 6
            (CurrencyType.Ruby, 300),              // Day 7 (주간 완료 보너스)
        };

        /// <summary>연속 출석일수 (1~7 사이클)</summary>
        private int _consecutiveDays;

        /// <summary>마지막 출석 날짜 (yyyy-MM-dd, UTC)</summary>
        private string _lastCheckDate = "";

        /// <summary>연속 출석일수</summary>
        public int ConsecutiveDays => _consecutiveDays;

        /// <summary>오늘 출석 체크를 했는지 여부</summary>
        public bool HasCheckedToday
        {
            get
            {
                string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                return string.Equals(_lastCheckDate, today, StringComparison.Ordinal);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SyncToSaveData();

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var data = Core.Save.SaveManager.Instance?.CurrentData?.attendance;
            if (data == null)
            {
                Initialize(0, "");
                return;
            }
            Initialize(data.consecutiveDays, data.lastCheckDate);
        }

        /// <summary>
        /// 저장 데이터에서 출석 상태를 복원한다.
        /// </summary>
        public void Initialize(int consecutiveDays, string lastCheckDate)
        {
            _consecutiveDays = Mathf.Clamp(consecutiveDays, 0, 7);
            _lastCheckDate = lastCheckDate ?? "";

            Debug.Log($"[AttendanceSystem] 초기화 완료 — 연속 출석: {_consecutiveDays}일, 마지막 체크: {_lastCheckDate}");
        }

        /// <summary>
        /// 출석 체크를 수행하고 보상을 지급한다.
        /// 이미 오늘 출석했으면 false를 반환한다.
        /// </summary>
        public bool CheckAttendance()
        {
            if (HasCheckedToday)
            {
                Debug.Log("[AttendanceSystem] 이미 오늘 출석 체크 완료");
                return false;
            }

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // 연속 출석 판정: 어제 출석했으면 연속, 아니면 리셋
            if (IsConsecutive(today))
            {
                _consecutiveDays++;
                if (_consecutiveDays > 7)
                {
                    _consecutiveDays = 1; // 7일 사이클 완료 후 1일로 리셋
                }
            }
            else
            {
                _consecutiveDays = 1; // 연속 끊김 → 1일차부터
            }

            _lastCheckDate = today;

            // 보상 지급
            var (rewardType, rewardAmount) = GetTodayReward();

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(rewardType, rewardAmount);
            }

            // 부스터 보너스 지급 (Day 3: EXP, Day 5: Gold, Day 7: Drop)
            GrantBoosterBonus(_consecutiveDays);

            EventBus.Publish(new AttendanceCheckedEvent
            {
                Day = _consecutiveDays,
                RewardType = rewardType,
                RewardAmount = rewardAmount
            });

            // 2026-04-23 이슈 15 FeedbackBus: 출석 체크 Toast
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"출석 Day {_consecutiveDays} 보상 수령!");

            Core.Save.SaveManager.Instance?.Save();

            Debug.Log($"[AttendanceSystem] 출석 체크 완료 — Day {_consecutiveDays}: {rewardType} x{rewardAmount}");

            return true;
        }

        // ── 서버/로컬 자동 분기 ──

        /// <summary>출석 체크 (서버/로컬 자동 분기)</summary>
        public async UniTask<bool> CheckAttendanceAsync(CancellationToken ct)
        {
            if (!ApiClient.HasAuth)
                return CheckAttendance();

            if (HasCheckedToday)
            {
                Debug.Log("[AttendanceSystem] 이미 오늘 출석 체크 완료");
                return false;
            }

            var response = await ApiClient.PostAsync<AttendanceCheckResponse>("attendance/check", new { }, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[AttendanceSystem] 서버 출석 체크 실패: {response.error}");
                return false;
            }

            var data = response.data;
            _consecutiveDays = data.consecutiveDays;
            _lastCheckDate = data.serverDate;

            // 보상은 서버가 지급 — 로컬 재화 동기화
            if (CurrencyManager.Instance != null && data.reward != null)
            {
                if (Enum.TryParse<CurrencyType>(data.reward.type, out var rewardType))
                {
                    CurrencyManager.Instance.Add(rewardType, data.reward.amount);
                }
            }

            EventBus.Publish(new AttendanceCheckedEvent
            {
                Day = _consecutiveDays,
                RewardType = data.reward != null && Enum.TryParse<CurrencyType>(data.reward.type, out var rt) ? rt : CurrencyType.Gold,
                RewardAmount = data.reward?.amount ?? 0
            });

            Core.Save.SaveManager.Instance?.Save();
            Debug.Log($"[AttendanceSystem] 서버 출석 체크 완료 — Day {_consecutiveDays}: {data.reward?.type} x{data.reward?.amount}");
            return true;
        }

        private void SyncToSaveData()
        {
            var saveManager = Core.Save.SaveManager.Instance;
            if (saveManager?.CurrentData == null) return;

            saveManager.CurrentData.attendance.consecutiveDays = _consecutiveDays;
            saveManager.CurrentData.attendance.lastCheckDate = _lastCheckDate;
        }

        /// <summary>
        /// 오늘의 보상을 반환한다 (출석 체크 전에도 조회 가능).
        /// </summary>
        public (CurrencyType type, int amount) GetTodayReward()
        {
            // 아직 출석 전이면 다음 출석일 기준
            int day = HasCheckedToday ? _consecutiveDays : GetNextDay();
            int index = Mathf.Clamp(day - 1, 0, DailyRewards.Length - 1);
            return DailyRewards[index];
        }

        /// <summary>
        /// 특정 일차의 보상을 반환한다 (UI 미리보기용).
        /// </summary>
        public (CurrencyType type, int amount) GetRewardForDay(int day)
        {
            int index = Mathf.Clamp(day - 1, 0, DailyRewards.Length - 1);
            return DailyRewards[index];
        }

        /// <summary>
        /// 저장용 데이터를 반환한다.
        /// </summary>
        public (int consecutiveDays, string lastCheckDate) GetSaveData()
        {
            return (_consecutiveDays, _lastCheckDate);
        }

        /// <summary>
        /// 어제 출석했는지 판정하여 연속 여부를 반환한다.
        /// </summary>
        private bool IsConsecutive(string todayStr)
        {
            if (string.IsNullOrEmpty(_lastCheckDate)) return false;

            if (!DateTime.TryParse(_lastCheckDate, out DateTime lastDate)) return false;
            if (!DateTime.TryParse(todayStr, out DateTime today)) return false;

            int dayDiff = (today.Date - lastDate.Date).Days;
            return dayDiff == 1;
        }

        /// <summary>
        /// 다음 출석일(1~7)을 계산한다.
        /// </summary>
        private int GetNextDay()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (IsConsecutive(today))
            {
                int next = _consecutiveDays + 1;
                return next > 7 ? 1 : next;
            }

            return 1; // 연속 끊김 → 1일차
        }

        /// <summary>
        /// 출석 일차에 따라 부스터를 보너스로 지급한다.
        /// Day 3: 경험치 부스터 1개, Day 5: 골드 부스터 1개, Day 7: 전체 부스터 각 1개.
        /// </summary>
        private void GrantBoosterBonus(int day)
        {
            switch (day)
            {
                case 3:
                    EventBus.Publish(new BoosterStockAddEvent { Type = BoosterType.ExpBoost, Amount = 1 });
                    Debug.Log("[AttendanceSystem] Day 3 보너스: 경험치 부스터 1개");
                    break;
                case 5:
                    EventBus.Publish(new BoosterStockAddEvent { Type = BoosterType.GoldBoost, Amount = 1 });
                    Debug.Log("[AttendanceSystem] Day 5 보너스: 골드 부스터 1개");
                    break;
                case 7:
                    EventBus.Publish(new BoosterStockAddEvent { Type = BoosterType.ExpBoost, Amount = 1 });
                    EventBus.Publish(new BoosterStockAddEvent { Type = BoosterType.GoldBoost, Amount = 1 });
                    EventBus.Publish(new BoosterStockAddEvent { Type = BoosterType.DropRateBoost, Amount = 1 });
                    Debug.Log("[AttendanceSystem] Day 7 보너스: 전체 부스터 각 1개");
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
