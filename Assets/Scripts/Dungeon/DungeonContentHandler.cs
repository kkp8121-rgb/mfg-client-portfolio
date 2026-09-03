using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Dungeon
{
    /// <summary>
    /// 5종 던전의 구체적 보상/진행 로직을 관리한다.
    /// DungeonManager.CompleteDungeon() 후 발행되는 DungeonCompletedEvent를 구독하여
    /// 던전 타입별 추가 보상을 지급한다.
    ///
    /// C6-02 무기 던전: WeaponTicket + WeaponStone
    /// C6-03 경험치 던전: 대량 EXP 보너스 (기본 보상의 50%)
    /// C6-04 장비 던전: HuntPoint
    /// C6-05 등반자의 시련: ClimbToken (+ 향후 스택 버프)
    /// C6-06 강화 던전: RuneFragment + StarCrystal (+ 향후 보스 그로기)
    /// </summary>
    public class DungeonContentHandler : MonoBehaviour
    {
        public static DungeonContentHandler Instance { get; private set; }

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
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
        }

        private void OnDisable()
        {
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            switch (evt.DungeonType)
            {
                case "Weapon":
                    HandleWeaponDungeon(evt);
                    break;
                case "Experience":
                    HandleExpDungeon(evt);
                    break;
                case "Equipment":
                    HandleEquipmentDungeon(evt);
                    break;
                case "Climber":
                    HandleClimberDungeon(evt);
                    break;
                case "Enhancement":
                    HandleEnhancementDungeon(evt);
                    break;
                default:
                    Debug.LogWarning($"[DungeonContentHandler] 알 수 없는 던전 타입: {evt.DungeonType}");
                    break;
            }
        }

        /// <summary>
        /// C6-02 무기 던전: WeaponTicket 1~2개 + WeaponStone 1~3개 추가 보상.
        /// </summary>
        private void HandleWeaponDungeon(DungeonCompletedEvent evt)
        {
            int ticketBonus = Random.Range(1, 3);
            int stoneBonus = Random.Range(1, 4);

            CurrencyManager.Instance?.Add(CurrencyType.WeaponTicket, ticketBonus);
            CurrencyManager.Instance?.Add(CurrencyType.WeaponStone, stoneBonus);
        }

        /// <summary>
        /// C6-03 경험치 던전: 현재 floor 기반 EXP 보너스 (10분 오프라인 분량).
        /// </summary>
        private void HandleExpDungeon(DungeonCompletedEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            int floor = save != null ? Mathf.Max(1, save.progress.currentFloor) : 1;

            // 10분 오프라인 보상 분량 = MonsterExp × 50(kpm) × 0.6(eff) × 10(min)
            long bonusExp = ExpTable.MonsterExp(floor) * 300;
            if (bonusExp > 0)
            {
                EventBus<ExpGainedEvent>.Publish(new ExpGainedEvent { Amount = bonusExp });
            }
        }

        /// <summary>
        /// C6-04 장비 던전: HuntPoint 10~29 추가 보상.
        /// </summary>
        private void HandleEquipmentDungeon(DungeonCompletedEvent evt)
        {
            int bonus = Random.Range(10, 30);
            CurrencyManager.Instance?.Add(CurrencyType.HuntPoint, bonus);
        }

        /// <summary>
        /// C6-05 등반자의 시련: ClimbToken 5~14 추가 보상.
        /// 향후 스택 버프 시스템 연동 예정.
        /// </summary>
        private void HandleClimberDungeon(DungeonCompletedEvent evt)
        {
            int bonus = Random.Range(5, 15);
            CurrencyManager.Instance?.Add(CurrencyType.ClimbToken, bonus);
        }

        /// <summary>
        /// C6-06 강화 던전: RuneFragment 3~7 + StarCrystal 2~4 추가 보상.
        /// 향후 보스 그로기 시스템 연동 예정.
        /// </summary>
        private void HandleEnhancementDungeon(DungeonCompletedEvent evt)
        {
            CurrencyManager.Instance?.Add(CurrencyType.RuneFragment, Random.Range(3, 8));
            CurrencyManager.Instance?.Add(CurrencyType.StarCrystal, Random.Range(2, 5));
        }
    }
}
