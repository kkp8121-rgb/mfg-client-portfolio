using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// CP 마일스톤별 콘텐츠 해금 게이팅 시스템.
    /// CpChangedEvent를 구독하여 마일스톤 도달 시 콜백을 발행한다.
    /// </summary>
    public class CpGatingSystem : MonoBehaviour
    {
        public static CpGatingSystem Instance { get; private set; }

        /// <summary>마일스톤 도달 시 발행 (requiredCp, contentName)</summary>
        public event Action<long, string> OnMilestoneReached;

        private static readonly (long cp, string contentId, string displayName)[] Milestones =
        {
            (1_000,   "challenge",    "챌린지 모드"),
            (3_000,   "arena",        "아레나"),
            (5_000,   "guildboss",    "길드 보스"),
            (10_000,  "secretroom",   "탑 비밀 방"),
            (50_000,  "job4trial",    "4차 전직 시련"),
            (100_000, "prestige",     "윤회"),
        };

        // 이미 도달한 마일스톤 인덱스 추적
        private readonly HashSet<int> _reachedMilestones = new();
        private long _lastKnownCp;

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
            EventBus<CpChangedEvent>.Subscribe(OnCpChanged);
        }

        private void OnDisable()
        {
            EventBus<CpChangedEvent>.Unsubscribe(OnCpChanged);
        }

        /// <summary>
        /// 초기 CP를 설정하고, 이미 달성한 마일스톤을 마킹한다 (이벤트 발행 없음).
        /// 게임 로드 후 호출해야 한다.
        /// </summary>
        public void InitializeWithCurrentCp(long currentCp)
        {
            _lastKnownCp = currentCp;
            _reachedMilestones.Clear();

            for (int i = 0; i < Milestones.Length; i++)
            {
                if (currentCp >= Milestones[i].cp)
                    _reachedMilestones.Add(i);
            }
        }

        /// <summary>
        /// 해당 콘텐츠에 접근 가능한지 (현재 CP가 필요 CP 이상인지) 확인한다.
        /// </summary>
        public bool CanAccess(string contentId)
        {
            long required = GetRequiredCp(contentId);
            return required <= 0 || _lastKnownCp >= required;
        }

        /// <summary>
        /// 콘텐츠 접근에 필요한 최소 CP를 반환한다. 등록되지 않은 콘텐츠는 0.
        /// </summary>
        public long GetRequiredCp(string contentId)
        {
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (string.Equals(Milestones[i].contentId, contentId, StringComparison.Ordinal))
                    return Milestones[i].cp;
            }
            return 0;
        }

        /// <summary>
        /// 다음 도달할 마일스톤 정보를 반환한다.
        /// 모든 마일스톤을 달성했으면 (0, null).
        /// </summary>
        public (long requiredCp, string contentName) GetNextMilestone()
        {
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (!_reachedMilestones.Contains(i))
                    return (Milestones[i].cp, Milestones[i].displayName);
            }
            return (0, null);
        }

        /// <summary>
        /// 다음 마일스톤까지 남은 CP를 반환한다. 모든 달성 시 0.
        /// </summary>
        public long GetCpUntilNextMilestone()
        {
            var (requiredCp, _) = GetNextMilestone();
            if (requiredCp <= 0) return 0;
            long remaining = requiredCp - _lastKnownCp;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// 전체 마일스톤 목록을 읽기 전용으로 반환한다.
        /// </summary>
        public int MilestoneCount => Milestones.Length;

        public (long cp, string contentId, string displayName, bool isReached) GetMilestone(int index)
        {
            if (index < 0 || index >= Milestones.Length)
                return (0, null, null, false);

            var m = Milestones[index];
            return (m.cp, m.contentId, m.displayName, _reachedMilestones.Contains(index));
        }

        private void OnCpChanged(CpChangedEvent evt)
        {
            _lastKnownCp = evt.CurrentCp;

            for (int i = 0; i < Milestones.Length; i++)
            {
                if (_reachedMilestones.Contains(i)) continue;

                if (evt.CurrentCp >= Milestones[i].cp && evt.PreviousCp < Milestones[i].cp)
                {
                    _reachedMilestones.Add(i);
                    OnMilestoneReached?.Invoke(Milestones[i].cp, Milestones[i].displayName);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
