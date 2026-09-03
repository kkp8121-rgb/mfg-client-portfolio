using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Net;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 오프라인 보상 시스템.
    /// 킬 기반 계산: 50마리/분 고정 킬 속도로 골드/EXP/아이템을 결정론적 계산 후 지급.
    /// - 앱 일시정지/종료 시 DateTime.UtcNow를 SaveData.progress.lastLogoutUtc에 기록.
    /// - 앱 복귀(Start 또는 OnApplicationPause(false)) 시 경과 시간을 계산하여 보상 지급.
    /// - 최대 오프라인 누적: 24시간.
    /// - 온라인 대비 60% 효율 (밸런스).
    /// - 아이템 드롭: 확률 × 킬 수 = 기대값 (소수부 확률 추가 드롭).
    /// - 보상 지급 시 OfflineRewardClaimedEvent를 발행하여 UI 팝업을 트리거한다.
    /// - Phase 13: 레벨별 배율, 부스터 연동, 장비 드롭 추가.
    /// </summary>
    public class OfflineRewardSystem : MonoBehaviour
    {
        public static OfflineRewardSystem Instance { get; private set; }

        private OfflineRewardConfigSO _config;

        /// <summary>최대 오프라인 누적 시간 (분). CombatFormula에서 정의.</summary>
        private static int MAX_OFFLINE_MINUTES => CombatFormula.MaxOfflineMinutes;

        /// <summary>최소 보상 지급 기준 (분)</summary>
        private const int MIN_REWARD_MINUTES = 1;

        /// <summary>Start에서 자동 보상 지급이 이미 실행되었는지 여부</summary>
        private bool _hasClaimedOnStart;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _config = Resources.Load<OfflineRewardConfigSO>("Data/OfflineRewardConfig");
        }

        private void Start()
        {
            TryClaimOnResume();
        }

        /// <summary>
        /// 보상을 미리보기한다 (적용하지 않음).
        /// SaveData의 lastLogoutUtc과 현재 시각을 비교하여 보상량을 계산한다.
        /// lastLogoutUtc가 없으면 lastLoginTime으로 폴백한다 (하위 호환).
        /// Phase 13: 레벨 배율 + 부스터 연동 + 장비 드롭 적용.
        /// </summary>
        public OfflineReward CalculateReward()
        {
            int minutesAway = GetMinutesAway();
            if (minutesAway < MIN_REWARD_MINUTES)
            {
                return default;
            }

            int floor = GetCurrentFloor();
            int totalKills = CombatFormula.OfflineTotalKills(minutesAway);

            // 킬 기반 골드/EXP 계산 (효율 배율 적용됨)
            long gold = CombatFormula.OfflineGoldPerMin(floor) * minutesAway;
            long exp = CombatFormula.OfflineExpPerMin(floor) * minutesAway;

            // Phase 13: 레벨 기반 배율 적용
            float levelMultiplier = GetLevelMultiplier();
            gold = (long)(gold * levelMultiplier);
            exp = (long)(exp * levelMultiplier);

            // Phase 13: 부스터 잔여 시간 배율 적용
            ApplyBoosterCarryover(minutesAway, ref gold, ref exp);

            // 결정론적 아이템 드롭: 확률 × 킬 수 = 기대값, 소수점은 절삭
            int huntPoint = totalKills * CombatFormula.OFFLINE_HUNT_POINT_PER_KILL;
            int runeFragment = DeterministicDrop(CombatFormula.OFFLINE_RUNE_FRAGMENT_RATE, totalKills);
            int starCrystal = DeterministicDrop(CombatFormula.OFFLINE_STAR_CRYSTAL_RATE, totalKills);
            int ruby = DeterministicDrop(CombatFormula.OFFLINE_RUBY_RATE, totalKills);

            // Phase 13: 장비 드롭 계산
            int equipDropCount = _config != null
                ? _config.CalculateEquipDropCount(minutesAway)
                : 0;

            return new OfflineReward
            {
                Gold = gold,
                Exp = exp,
                MinutesAway = minutesAway,
                TotalKills = totalKills,
                HuntPoint = huntPoint,
                RuneFragment = runeFragment,
                StarCrystal = starCrystal,
                Ruby = ruby,
                EquipDropCount = equipDropCount,
                LevelMultiplier = levelMultiplier
            };
        }

        /// <summary>
        /// 결정론적 드롭 계산. 정수부 확정 + 소수부를 확률로 추가 1개.
        /// </summary>
        private static int DeterministicDrop(float dropRate, int totalKills)
        {
            float expected = dropRate * totalKills;
            int guaranteed = (int)expected;
            float fractional = expected - guaranteed;

            // 소수부를 확률로 추가 드롭
            if (fractional > 0f && UnityEngine.Random.value < fractional)
            {
                guaranteed++;
            }

            return guaranteed;
        }

        /// <summary>
        /// 보상을 계산하고 실제로 적용한다.
        /// CurrencyManager에 골드를 추가하고 ExpGainedEvent를 발행한다.
        /// OfflineRewardClaimedEvent를 발행하여 UI 팝업을 트리거한다.
        /// lastLogoutUtc를 현재 시각으로 갱신한다.
        /// </summary>
        public OfflineReward ClaimReward()
        {
            OfflineReward reward = CalculateReward();

            if (reward.MinutesAway < MIN_REWARD_MINUTES)
            {
                SaveLogoutTime();
                return reward;
            }

            // 재화 지급
            CurrencyManager cm = CurrencyManager.Instance;
            if (cm != null)
            {
                if (reward.Gold > 0) cm.Add(CurrencyType.Gold, reward.Gold);
                if (reward.HuntPoint > 0) cm.Add(CurrencyType.HuntPoint, reward.HuntPoint);
                if (reward.RuneFragment > 0) cm.Add(CurrencyType.RuneFragment, reward.RuneFragment);
                if (reward.StarCrystal > 0) cm.Add(CurrencyType.StarCrystal, reward.StarCrystal);
                if (reward.Ruby > 0) cm.Add(CurrencyType.Ruby, reward.Ruby);
            }

            // EXP 지급
            if (reward.Exp > 0)
            {
                EventBus.Publish(new ExpGainedEvent { Amount = reward.Exp });
            }

            // Phase 13: 장비 드롭 — 이벤트 기반으로 EquipmentManager에 요청
            if (reward.EquipDropCount > 0)
            {
                string dropGrade = _config != null ? _config.MinDropGrade : "Rare";
                EventBus.Publish(new OfflineEquipDropRequestEvent
                {
                    Count = reward.EquipDropCount,
                    MinGrade = dropGrade
                });
            }

            // UI 팝업용 이벤트 발행
            EventBus.Publish(new OfflineRewardClaimedEvent
            {
                GoldAmount = reward.Gold,
                ExpAmount = reward.Exp,
                ElapsedMinutes = reward.MinutesAway,
                TotalKills = reward.TotalKills,
                HuntPoint = reward.HuntPoint,
                RuneFragment = reward.RuneFragment,
                StarCrystal = reward.StarCrystal,
                Ruby = reward.Ruby,
                EquipDropCount = reward.EquipDropCount,
                LevelMultiplier = reward.LevelMultiplier
            });

            // 로그아웃 시각 갱신
            SaveLogoutTime();

#if UNITY_EDITOR
            Debug.Log($"[OfflineRewardSystem] 오프라인 보상 지급 — {reward.MinutesAway}분, {reward.TotalKills}킬, 골드:{reward.Gold}, EXP:{reward.Exp}, 룬:{reward.RuneFragment}, 별결정:{reward.StarCrystal}, 루비:{reward.Ruby}");
#endif

            return reward;
        }

        /// <summary>
        /// 앱 복귀 시 자동으로 보상을 계산하고 지급한다.
        /// Start() 및 OnApplicationPause(false) 에서 호출된다.
        /// </summary>
        private void TryClaimOnResume()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            OfflineReward reward = ClaimReward();

            if (reward.MinutesAway >= MIN_REWARD_MINUTES)
            {
                _hasClaimedOnStart = true;
            }
        }

        /// <summary>
        /// 현재 시각을 lastLogoutUtc에 기록한다.
        /// 앱 종료, 자동 저장, 일시 정지 시 호출한다.
        /// 하위 호환을 위해 lastLoginTime도 동시에 갱신한다.
        /// </summary>
        public void SaveLogoutTime()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            string nowUtc = DateTime.UtcNow.ToString("o");
            SaveManager.Instance.CurrentData.progress.lastLogoutUtc = nowUtc;
            SaveManager.Instance.CurrentData.progress.lastLoginTime = nowUtc;
        }

        /// <summary>
        /// 하위 호환용. SaveLoginTime()은 SaveLogoutTime()으로 위임한다.
        /// </summary>
        public void SaveLoginTime()
        {
            SaveLogoutTime();
        }

        /// <summary>
        /// 경과 시간(분)을 계산한다. 최대 MAX_OFFLINE_MINUTES로 클램핑.
        /// lastLogoutUtc를 우선 사용하고, 없으면 lastLoginTime으로 폴백한다.
        /// </summary>
        private int GetMinutesAway()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return 0;
            }

            ProgressData progress = SaveManager.Instance.CurrentData.progress;

            // lastLogoutUtc 우선, 없으면 lastLoginTime 폴백 (하위 호환)
            string lastTimeStr = !string.IsNullOrEmpty(progress.lastLogoutUtc)
                ? progress.lastLogoutUtc
                : progress.lastLoginTime;

            if (string.IsNullOrEmpty(lastTimeStr))
            {
                return 0;
            }

            if (!DateTime.TryParse(lastTimeStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastTime))
            {
                Debug.LogWarning("[OfflineRewardSystem] 시각 파싱 실패: " + lastTimeStr);
                return 0;
            }

            double totalMinutes = (DateTime.UtcNow - lastTime.ToUniversalTime()).TotalMinutes;

            if (totalMinutes < MIN_REWARD_MINUTES)
            {
                return 0;
            }

            return Mathf.Min((int)totalMinutes, MAX_OFFLINE_MINUTES);
        }

        /// <summary>
        /// 현재 층수를 반환한다. 최소 1.
        /// </summary>
        private int GetCurrentFloor()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return 1;
            }

            return Mathf.Max(1, SaveManager.Instance.CurrentData.progress.currentFloor);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // 앱 백그라운드 진입 — 로그아웃 시각 기록 (로컬 + 서버)
                SaveLogoutTime();
                NotifyServerLogout();
            }
            else
            {
                // 앱 복귀 — 오프라인 보상 자동 지급
                TryClaimOnResume();
            }
        }

        private void OnApplicationQuit()
        {
            SaveLogoutTime();
            NotifyServerLogout();
        }

        /// <summary>
        /// 서버에 로그아웃 시각을 알린다 (fire-and-forget).
        /// 서버가 LastLogoutAt을 기록하여 오프라인 보상 검증에 사용한다.
        /// 실패해도 로컬 lastLogoutUtc가 폴백으로 동작한다.
        /// </summary>
        private void NotifyServerLogout()
        {
            if (!ApiClient.HasAuth) return;

            // fire-and-forget — 앱 종료/일시정지 시 await 불가
            ApiClient.PostAsync<LogoutResponse>("auth/logout", _emptyRequest).Forget();
        }

        [Serializable]
        private struct EmptyRequest { }
        private static readonly EmptyRequest _emptyRequest = new();

        /// <summary>
        /// 현재 플레이어 레벨에 따른 오프라인 보상 배율을 반환한다.
        /// ConfigSO가 없으면 1.0을 반환한다.
        /// </summary>
        private float GetLevelMultiplier()
        {
            if (_config == null) return 1f;

            var levelSystem = UnityEngine.Object.FindFirstObjectByType<LevelSystem>();
            int level = levelSystem != null ? levelSystem.CurrentLevel : 1;
            return _config.GetMultiplier(level);
        }

        /// <summary>
        /// 부스터 잔여 시간을 오프라인 보상에 적용한다.
        /// 로그아웃 시점에 활성화된 부스터의 남은 시간만큼 추가 배율을 적용한다.
        /// </summary>
        private void ApplyBoosterCarryover(int minutesAway, ref long gold, ref long exp)
        {
            if (_config == null || !_config.ApplyBoosterCarryover) return;

            var boosterManager = BoosterManager.Instance;
            if (boosterManager == null) return;

            // 부스터 잔여 시간 비율 계산
            float goldBoostMultiplier = boosterManager.GetMultiplier(BoosterType.GoldBoost);
            float expBoostMultiplier = boosterManager.GetMultiplier(BoosterType.ExpBoost);

            // 부스터가 활성이면 보상에 추가 배율 적용
            // 부스터 남은 시간은 온라인 시간 기준이므로 전체 오프라인 시간에는 비례 적용
            if (goldBoostMultiplier > 1f)
            {
                gold = (long)(gold * goldBoostMultiplier);
            }

            if (expBoostMultiplier > 1f)
            {
                exp = (long)(exp * expBoostMultiplier);
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

    /// <summary>
    /// 오프라인 보상 데이터. UI 팝업 표시용.
    /// </summary>
    public struct OfflineReward
    {
        /// <summary>획득 골드</summary>
        public long Gold;

        /// <summary>획득 경험치</summary>
        public long Exp;

        /// <summary>오프라인 경과 시간 (분)</summary>
        public int MinutesAway;

        /// <summary>총 킬 수</summary>
        public int TotalKills;

        /// <summary>사냥 포인트</summary>
        public int HuntPoint;

        /// <summary>룬 조각</summary>
        public int RuneFragment;

        /// <summary>별의 결정</summary>
        public int StarCrystal;

        /// <summary>루비</summary>
        public int Ruby;

        /// <summary>장비 드롭 수 (Phase 13)</summary>
        public int EquipDropCount;

        /// <summary>적용된 레벨 배율 (Phase 13)</summary>
        public float LevelMultiplier;
    }
}
