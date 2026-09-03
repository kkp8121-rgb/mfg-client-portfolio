using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Dungeon
{
    /// <summary>
    /// 보스 레이드 시스템.
    /// 주 3회 무료 도전, 난이도별(Easy/Normal/Hard) 보상 차등 지급.
    /// 즉시 결과 방식 (방치형).
    /// </summary>
    public class BossRaidSystem : MonoBehaviour
    {
        public static BossRaidSystem Instance { get; private set; }

        public enum Difficulty { Easy, Normal, Hard }

        private const int MAX_WEEKLY_ATTEMPTS = 3;

        [SerializeField] private int _weeklyAttemptsUsed;

        /// <summary>남은 주간 도전 횟수</summary>
        public int RemainingAttempts => MAX_WEEKLY_ATTEMPTS - _weeklyAttemptsUsed;

        /// <summary>최대 주간 도전 횟수</summary>
        public int MaxWeeklyAttempts => MAX_WEEKLY_ATTEMPTS;

        /// <summary>사용한 주간 도전 횟수</summary>
        public int WeeklyAttemptsUsed => _weeklyAttemptsUsed;

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

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            var data = SaveManager.Instance?.CurrentData?.dungeon;
            if (data == null) return;
            data.bossRaidWeeklyAttemptsUsed = _weeklyAttemptsUsed;
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var data = SaveManager.Instance?.CurrentData?.dungeon;
            if (data == null) return;
            Initialize(data.bossRaidWeeklyAttemptsUsed);
        }

        /// <summary>
        /// 난이도별 보상 배율을 반환한다.
        /// Easy: 1x, Normal: 2x, Hard: 4x
        /// </summary>
        private static float DifficultyMultiplier(Difficulty diff)
        {
            return diff switch
            {
                Difficulty.Easy => 1f,
                Difficulty.Normal => 2f,
                Difficulty.Hard => 4f,
                _ => 1f
            };
        }

        /// <summary>레이드 도전 가능 여부</summary>
        public bool CanRaid() => _weeklyAttemptsUsed < MAX_WEEKLY_ATTEMPTS;

        /// <summary>
        /// 보스 레이드를 실행한다 (즉시 결과).
        /// 전투력(ATK + HP/10) >= 난이도별 요구치이면 클리어.
        /// </summary>
        /// <param name="difficulty">난이도</param>
        /// <param name="playerAtk">플레이어 공격력</param>
        /// <param name="playerHp">플레이어 HP</param>
        /// <returns>레이드 결과</returns>
        public BossRaidResult ExecuteRaid(Difficulty difficulty, int playerAtk, int playerHp)
        {
            if (!CanRaid())
            {
                Debug.LogWarning("[BossRaidSystem] ExecuteRaid — 주간 도전 횟수 소진");
                return new BossRaidResult { Success = false };
            }

            _weeklyAttemptsUsed++;
            EventBus.Publish(new BossRaidEnteredEvent());
            float mult = DifficultyMultiplier(difficulty);

            // 승패 판정: 전투력 vs 난이도 요구치
            int requiredPower = (int)(1000 * mult);
            int playerPower = playerAtk + playerHp / 10;
            bool isSuccess = playerPower >= requiredPower;

            if (isSuccess)
            {
                int goldReward = (int)(5000 * mult);
                int rubyReward = (int)(30 * mult);
                int potentialStoneReward = (int)(2 * mult);

                CurrencyManager.Instance?.Add(CurrencyType.Gold, goldReward);
                CurrencyManager.Instance?.Add(CurrencyType.Ruby, rubyReward);
                CurrencyManager.Instance?.Add(CurrencyType.PotentialStone, potentialStoneReward);

                Debug.Log($"[BossRaidSystem] 레이드 클리어 — {difficulty}, Gold: {goldReward}, Ruby: {rubyReward}, PotentialStone: {potentialStoneReward}");

                return new BossRaidResult
                {
                    Success = true,
                    GoldReward = goldReward,
                    RubyReward = rubyReward
                };
            }

            Debug.Log($"[BossRaidSystem] 레이드 실패 — {difficulty}, 전투력: {playerPower}/{requiredPower}");
            return new BossRaidResult { Success = false };
        }

        /// <summary>
        /// 주간 도전 횟수를 초기화한다 (매주 월요일 리셋).
        /// </summary>
        public void ResetWeekly()
        {
            _weeklyAttemptsUsed = 0;
            Debug.Log("[BossRaidSystem] 주간 도전 횟수 초기화");
        }

        /// <summary>
        /// 세이브 데이터에서 상태를 복원한다.
        /// </summary>
        public void Initialize(int weeklyAttemptsUsed)
        {
            _weeklyAttemptsUsed = Mathf.Clamp(weeklyAttemptsUsed, 0, MAX_WEEKLY_ATTEMPTS);
            Debug.Log($"[BossRaidSystem] 초기화 — 사용 횟수: {_weeklyAttemptsUsed}/{MAX_WEEKLY_ATTEMPTS}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    /// <summary>보스 레이드 결과</summary>
    public struct BossRaidResult
    {
        public bool Success;
        public int GoldReward;
        public int RubyReward;
    }
}
