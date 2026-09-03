using System;
using UnityEngine;
using MkLike.Core.Save;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// 시즌 관리 싱글톤 매니저.
    /// 현재 시즌의 시작/종료, BXP 트래킹, 시즌 리셋을 담당한다.
    /// </summary>
    public class SeasonManager : MonoBehaviour
    {
        public static SeasonManager Instance { get; private set; }

        /// <summary>현재 시즌 ID</summary>
        public string CurrentSeasonId { get; private set; }

        /// <summary>시즌 시작 시각</summary>
        public DateTime SeasonStartTime { get; private set; }

        /// <summary>시즌 종료 시각</summary>
        public DateTime SeasonEndTime { get; private set; }

        /// <summary>시즌이 활성 상태인지</summary>
        public bool IsSeasonActive => DateTime.UtcNow >= SeasonStartTime && DateTime.UtcNow < SeasonEndTime;

        /// <summary>시즌 남은 시간</summary>
        public TimeSpan RemainingTime => IsSeasonActive ? SeasonEndTime - DateTime.UtcNow : TimeSpan.Zero;

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

        /// <summary>
        /// 세이브 데이터에서 시즌 정보를 로드하여 초기화한다.
        /// </summary>
        public void Initialize(BattlePassSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SeasonManager] BattlePassSaveData가 null — 기본값으로 초기화");
                data = new BattlePassSaveData();
            }

            CurrentSeasonId = data.seasonId;

            if (!string.IsNullOrEmpty(data.seasonStartTime))
            {
                DateTime.TryParse(data.seasonStartTime, out var startTime);
                SeasonStartTime = startTime;
            }

            if (!string.IsNullOrEmpty(data.seasonEndTime))
            {
                DateTime.TryParse(data.seasonEndTime, out var endTime);
                SeasonEndTime = endTime;
            }

            Debug.Log($"[SeasonManager] 초기화 완료 — 시즌: {CurrentSeasonId}, 활성: {IsSeasonActive}");
        }

        /// <summary>
        /// 새 시즌을 시작한다.
        /// 기존 배틀패스 진행을 초기화하고 새 시즌 데이터를 설정한다.
        /// </summary>
        /// <param name="seasonId">시즌 고유 ID</param>
        /// <param name="durationWeeks">시즌 기간 (주)</param>
        public void StartNewSeason(string seasonId, int durationWeeks)
        {
            CurrentSeasonId = seasonId;
            SeasonStartTime = DateTime.UtcNow;
            SeasonEndTime = SeasonStartTime.AddDays(durationWeeks * 7);

            SyncToSaveData();

            // SFX: 시즌 시작
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx("sfx_season_start");

            EventBus.Publish(new SeasonStartedEvent
            {
                SeasonId = seasonId,
                DurationWeeks = durationWeeks
            });

            Debug.Log($"[SeasonManager] 새 시즌 시작 — {seasonId}, 기간: {durationWeeks}주");
        }

        /// <summary>
        /// 현재 시즌을 종료한다.
        /// </summary>
        public void EndCurrentSeason()
        {
            string endedSeasonId = CurrentSeasonId;
            CurrentSeasonId = "";
            SeasonStartTime = default;
            SeasonEndTime = default;

            SyncToSaveData();

            // SFX: 시즌 종료
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx("sfx_season_end");

            EventBus.Publish(new SeasonEndedEvent
            {
                SeasonId = endedSeasonId
            });

            Debug.Log($"[SeasonManager] 시즌 종료 — {endedSeasonId}");
        }

        /// <summary>
        /// 시즌 만료를 체크한다. 만료 시 자동 종료.
        /// </summary>
        public void CheckSeasonExpiry()
        {
            if (!string.IsNullOrEmpty(CurrentSeasonId) && !IsSeasonActive)
            {
                Debug.Log("[SeasonManager] 시즌 만료 감지 — 자동 종료");
                EndCurrentSeason();
            }
        }

        /// <summary>
        /// SaveManager에 시즌 데이터를 동기화한다.
        /// </summary>
        private void SyncToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            var bp = SaveManager.Instance.CurrentData.battlePass;
            bp.seasonId = CurrentSeasonId;
            bp.seasonStartTime = SeasonStartTime.ToString("o");
            bp.seasonEndTime = SeasonEndTime.ToString("o");
            SaveManager.Instance.Save();
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
