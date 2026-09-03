using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 복귀 유저 가이드 시스템 (UX-21~23).
    /// 72시간 이상 미접속 시 복귀 보너스 지급 + ReturneeGuideEvent 발행.
    /// OfflineRewardSystem과 연동하여 오프라인 보상 이후 복귀 팝업을 트리거한다.
    /// </summary>
    public class ReturneeGuideManager : MonoBehaviour
    {
        public static ReturneeGuideManager Instance { get; private set; }

        private const int RETURNEE_THRESHOLD_HOURS = 72;
        private const int RETURNEE_THRESHOLD_MINUTES = RETURNEE_THRESHOLD_HOURS * 60;
        private const int LONG_ABSENCE_DAYS = 30;
        private const int SHORT_ABSENCE_DAYS = 7;

        private bool _hasChecked;

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
            EventBus<OfflineRewardClaimedEvent>.Subscribe(OnOfflineRewardClaimed);
        }

        private void OnDisable()
        {
            EventBus<OfflineRewardClaimedEvent>.Unsubscribe(OnOfflineRewardClaimed);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnOfflineRewardClaimed(OfflineRewardClaimedEvent evt)
        {
            if (_hasChecked) return;
            _hasChecked = true;

            int minutesAway = GetTotalMinutesAway();
            if (minutesAway < RETURNEE_THRESHOLD_MINUTES) return;

            var bonus = CalculateReturneeBonus(minutesAway);
            ApplyBonus(bonus);

            var guideData = BuildGuideData(minutesAway, evt, bonus);
            EventBus.Publish(guideData);

#if UNITY_EDITOR
            Debug.Log($"[ReturneeGuideManager] 복귀 유저 감지 — {minutesAway / 60 / 24}일 미접속, 보너스 지급 완료");
#endif
        }

        /// <summary>
        /// 미접속 시간을 분 단위로 계산한다.
        /// OfflineRewardSystem은 최대 24시간으로 클램핑하지만 여기서는 원본 시간을 계산한다.
        /// </summary>
        private int GetTotalMinutesAway()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return 0;

            ProgressData progress = SaveManager.Instance.CurrentData.progress;
            string lastTimeStr = !string.IsNullOrEmpty(progress.lastLogoutUtc)
                ? progress.lastLogoutUtc
                : progress.lastLoginTime;

            if (string.IsNullOrEmpty(lastTimeStr))
                return 0;

            if (!DateTime.TryParse(lastTimeStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastTime))
                return 0;

            double totalMinutes = (DateTime.UtcNow - lastTime.ToUniversalTime()).TotalMinutes;
            return totalMinutes > 0 ? (int)totalMinutes : 0;
        }

        /// <summary>
        /// 미접속 기간에 따라 복귀 보너스를 계산한다.
        /// </summary>
        public static ReturneeBonus CalculateReturneeBonus(int minutesAway)
        {
            int daysAway = minutesAway / (60 * 24);

            if (daysAway >= LONG_ABSENCE_DAYS)
            {
                return new ReturneeBonus
                {
                    Ruby = 2000,
                    WeaponTicket = 20,
                    DungeonKey = 10,
                    BonusExpDays = 7,
                    TierName = "장기 복귀"
                };
            }
            if (daysAway >= 14)
            {
                return new ReturneeBonus
                {
                    Ruby = 1000,
                    WeaponTicket = 10,
                    DungeonKey = 10,
                    HasWeaponBox = true,
                    TierName = "2주 복귀"
                };
            }
            if (daysAway >= SHORT_ABSENCE_DAYS)
            {
                return new ReturneeBonus
                {
                    Ruby = 500,
                    WeaponTicket = 5,
                    DungeonKey = 5,
                    HasEquipmentBox = true,
                    TierName = "1주 복귀"
                };
            }
            // 3~6일
            return new ReturneeBonus
            {
                Ruby = 300,
                WeaponTicket = 3,
                DungeonKey = 3,
                TierName = "단기 복귀"
            };
        }

        /// <summary>
        /// 복귀 보너스를 적용한다.
        /// 루비/소환권은 CurrencyManager로 직접 지급.
        /// 던전 열쇠는 ReturneeKeyGrantEvent로 발행하여 DungeonManager가 구독 처리한다.
        /// </summary>
        private void ApplyBonus(ReturneeBonus bonus)
        {
            if (CurrencyManager.Instance != null)
            {
                if (bonus.Ruby > 0)
                    CurrencyManager.Instance.Add(CurrencyType.Ruby, bonus.Ruby);
                if (bonus.WeaponTicket > 0)
                    CurrencyManager.Instance.Add(CurrencyType.WeaponTicket, bonus.WeaponTicket);
            }

            // 던전 열쇠는 이벤트로 발행 (Combat→Dungeon 순환 참조 방지)
            if (bonus.DungeonKey > 0)
            {
                EventBus.Publish(new ReturneeKeyGrantEvent { KeyCount = bonus.DungeonKey });
            }
        }

        private ReturneeGuideEvent BuildGuideData(int minutesAway, OfflineRewardClaimedEvent offlineReward, ReturneeBonus bonus)
        {
            var save = SaveManager.Instance?.CurrentData;
            int level = save?.player?.level ?? 1;
            int maxFloor = save?.progress?.maxFloor ?? 1;
            int currentFloor = save?.progress?.currentFloor ?? 1;

            var recommendations = BuildRecommendations();

            return new ReturneeGuideEvent
            {
                MinutesAway = minutesAway,
                PlayerLevel = level,
                MaxFloor = maxFloor,
                CurrentFloor = currentFloor,
                OfflineGold = offlineReward.GoldAmount,
                OfflineExp = offlineReward.ExpAmount,
                Bonus = bonus,
                Recommendations = recommendations
            };
        }

        private List<string> BuildRecommendations()
        {
            var list = new List<string>(5);
            list.Add("출석 보상 수령");
            list.Add("던전 열쇠 소진");
            list.Add("가이드 퀘스트 이어서 진행");
            list.Add("장비 강화 (재료 확인!)");
            list.Add("탑 도전으로 최고층 갱신");
            return list;
        }
    }
}
