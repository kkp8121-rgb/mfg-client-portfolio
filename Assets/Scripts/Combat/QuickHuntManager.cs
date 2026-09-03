using UnityEngine;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 소탕(Quick Hunt) 시스템.
    /// 소탕권을 소모하여 방치 전투 결과를 즉시 수령한다.
    /// 해금: 스테이지 75 (가이드 퀘스트로 유도).
    /// </summary>
    public class QuickHuntManager : MonoBehaviour
    {
        public static QuickHuntManager Instance { get; private set; }

        [Header("소탕 설정")]
        [SerializeField] private int _minutesPerTicket = 10;
        [SerializeField] private float _equipDropChance = 0.3f;

        /// <summary>소탕 해금 여부</summary>
        public bool IsUnlocked => _isUnlocked;
        private bool _isUnlocked;

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
            // 스테이지 기반 해금 체크
            CheckUnlock();
        }

        // 2026-04-23 QA 감사 P1: Start → OnEnable 이동 (Scene 재활성 시 먹통 방지)
        private void OnEnable()
        {
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
        }

        private void OnDisable()
        {
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            CheckUnlock();
        }

        private void CheckUnlock()
        {
            if (_isUnlocked) return;
            var stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager == null) return;

            int absoluteStage = (stageManager.CurrentChapter - 1) * 10 + stageManager.CurrentStageIndex;
            if (absoluteStage >= 75)
            {
                _isUnlocked = true;
                Debug.Log("[QuickHuntManager] 소탕 기능 해금!");
            }
        }

        /// <summary>
        /// 소탕을 실행한다.
        /// </summary>
        /// <param name="ticketCount">사용할 소탕권 수</param>
        /// <returns>성공 여부</returns>
        public bool ExecuteQuickHunt(int ticketCount)
        {
            if (!_isUnlocked)
            {
                Debug.LogWarning("[QuickHuntManager] 소탕 미해금");
                return false;
            }

            if (ticketCount <= 0) return false;

            var cm = CurrencyManager.Instance;
            if (cm == null) return false;

            if (!cm.HasEnough(CurrencyType.QuickHuntTicket, ticketCount))
            {
                Debug.LogWarning("[QuickHuntManager] 소탕권 부족");
                return false;
            }

            // 소탕권 소모
            cm.Spend(CurrencyType.QuickHuntTicket, ticketCount);

            // floor 기반 보상 계산 (CombatFormula 오프라인 공식 활용)
            var stageManager = Object.FindFirstObjectByType<StageManager>();
            int floor = 1;
            if (stageManager != null)
                floor = (stageManager.CurrentChapter - 1) * 10 + stageManager.CurrentStageIndex;

            // 1소탕권 = N분 오프라인 보상 (floor 기반 자동 스케일)
            long goldPerTicket = CombatFormula.OfflineGoldPerMin(floor) * _minutesPerTicket;
            long expPerTicket = CombatFormula.OfflineExpPerMin(floor) * _minutesPerTicket;

            long totalGold = goldPerTicket * ticketCount;
            long totalExp = expPerTicket * ticketCount;
            int totalEquipDrops = 0;

            for (int i = 0; i < ticketCount; i++)
            {
                if (Random.value < _equipDropChance)
                    totalEquipDrops++;
            }

            // 골드 지급
            cm.Add(CurrencyType.Gold, totalGold);

            // 경험치 지급 (이벤트 기반)
            EventBus.Publish(new ExpGainedEvent { Amount = totalExp });

            // 장비 드롭 (가챠 시스템 활용)
            if (totalEquipDrops > 0)
            {
                var gachaManager = Object.FindFirstObjectByType<Economy.GachaManager>();
                if (gachaManager != null)
                {
                    for (int i = 0; i < totalEquipDrops; i++)
                    {
                        gachaManager.Pull(Economy.GachaPoolType.Equipment);
                    }
                }
            }

            // 이벤트 발행
            EventBus.Publish(new QuickHuntCompletedEvent
            {
                TicketsUsed = ticketCount,
                GoldEarned = totalGold,
                ExpEarned = totalExp,
                EquipmentDrops = totalEquipDrops
            });

            // 2026-04-23 이슈 10: FeedbackBus로 결과 요약 Toast + 약한 쉐이크 + Player 위치 "+골드" 수치
            // 기존엔 Debug.Log만 있어 유저가 소탕 완료 여부조차 알 수 없었음
            string summary = $"소탕 {ticketCount}회 완료! +{totalGold:N0} 골드"
                + (totalEquipDrops > 0 ? $", 장비 {totalEquipDrops}개" : "");
            var pc = Object.FindFirstObjectByType<PlayerCharacter>();
            Vector3 feedbackPos = pc != null ? pc.transform.position : Vector3.zero;
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.QuickHunt,
                summary,
                feedbackPos,
                amountText: totalEquipDrops > 0 ? $"+{totalEquipDrops} 장비" : null,
                shakeIntensity: totalEquipDrops > 0 ? 2 : 1);

            Debug.Log($"[QuickHuntManager] 소탕 완료: {ticketCount}회, 골드 {totalGold}, 경험치 {totalExp}, 장비 {totalEquipDrops}개");
            return true;
        }

        /// <summary>
        /// 2026-04-23 이슈 10 지급 경로 정의: 스테이지 클리어 시 일정 확률로 소탕권 드롭.
        /// DungeonBattleController/QuestManager 등에서 호출 가능한 외부 지급 API.
        /// </summary>
        public void GrantTickets(int amount, string source = "system")
        {
            if (amount <= 0) return;
            var cm = CurrencyManager.Instance;
            if (cm == null) return;
            cm.Add(CurrencyType.QuickHuntTicket, amount);
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.QuickHunt,
                $"소탕권 +{amount} ({source})");
            Debug.Log($"[QuickHuntManager] 소탕권 지급 +{amount} from {source}");
        }

        /// <summary>현재 보유 소탕권 수</summary>
        public int GetTicketCount()
        {
            var bn = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.GetAmount(CurrencyType.QuickHuntTicket)
                : BigNumber.Zero;
            return bn.ToIntClamped();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
