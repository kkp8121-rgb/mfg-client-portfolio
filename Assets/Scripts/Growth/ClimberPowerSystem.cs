using UnityEngine;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Growth
{
    /// <summary>
    /// 등반자의 힘 시스템.
    /// 등반의 증표(ClimbToken)를 소비하여 영구적 능력치 상승 + 어빌리티 슬롯 개방.
    /// 최대 100단계, 10단계마다 어빌리티 슬롯 1개 개방 (최대 10슬롯).
    /// </summary>
    public class ClimberPowerSystem : MonoBehaviour
    {
        public static ClimberPowerSystem Instance { get; private set; }

        private const int MAX_LEVEL = 100;
        private const int ATK_PER_LEVEL = 3;
        private const int HP_PER_LEVEL = 20;
        private const int DEF_PER_LEVEL = 1;
        private const int BASE_COST = 5;
        private const int COST_PER_LEVEL = 2;
        private const int LEVELS_PER_SLOT = 10;

        [SerializeField] private CombatStats _combatStats;

        private int _currentLevel;

        /// <summary>현재 강화 단계 (0~100)</summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>개방된 어빌리티 슬롯 수 (10단계마다 1슬롯, 최대 10)</summary>
        public int AbilitySlots => _currentLevel / LEVELS_PER_SLOT;

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 저장 데이터에서 레벨을 복원하고 CombatStats에 누적 보너스를 적용한다.
        /// </summary>
        /// <param name="savedLevel">저장된 강화 단계</param>
        public void Initialize(int savedLevel)
        {
            _currentLevel = Mathf.Clamp(savedLevel, 0, MAX_LEVEL);
            ApplyBonus();
        }

        /// <summary>
        /// 다음 강화에 필요한 ClimbToken 비용을 반환한다.
        /// </summary>
        public long GetUpgradeCost()
        {
            if (_currentLevel >= MAX_LEVEL) return -1;
            return BASE_COST + _currentLevel * COST_PER_LEVEL;
        }

        /// <summary>
        /// 강화 가능 여부를 반환한다.
        /// </summary>
        public bool CanUpgrade()
        {
            if (_currentLevel >= MAX_LEVEL) return false;
            if (CurrencyManager.Instance == null) return false;

            long cost = GetUpgradeCost();
            return CurrencyManager.Instance.HasEnough(CurrencyType.ClimbToken, cost);
        }

        /// <summary>
        /// 강화를 실행한다. ClimbToken을 소비하고 능력치 보너스를 적용한다.
        /// </summary>
        /// <returns>성공 시 true</returns>
        public bool Upgrade()
        {
            if (_currentLevel >= MAX_LEVEL) return false;
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[ClimberPowerSystem] CurrencyManager가 없습니다.");
                return false;
            }

            long cost = GetUpgradeCost();
            if (!CurrencyManager.Instance.Spend(CurrencyType.ClimbToken, cost))
            {
                return false;
            }

            // 이전 보너스 제거
            RemoveBonus();

            _currentLevel++;

            // 새 보너스 적용
            ApplyBonus();
            SaveState();

            EventBus.Publish(new ClimberPowerUpEvent { NewLevel = _currentLevel });

            return true;
        }

        /// <summary>
        /// SaveData에서 레벨을 로드한다.
        /// </summary>
        private void Start()
        {
            var saveData = Core.Save.SaveManager.Instance?.CurrentData;
            if (saveData != null)
                Initialize(saveData.climberPowerLevel);
        }

        private void SaveState()
        {
            var saveManager = Core.Save.SaveManager.Instance;
            if (saveManager?.CurrentData == null) return;

            saveManager.CurrentData.climberPowerLevel = _currentLevel;
            saveManager.Save();
        }

        /// <summary>
        /// 현재 레벨에 해당하는 누적 보너스를 CombatStats에 수정자로 적용한다.
        /// </summary>
        private void ApplyBonus()
        {
            if (_combatStats == null || _currentLevel <= 0) return;

            _combatStats.SetCpReason("등반자 파워");
            _combatStats.ClearModifiers(ModifierSource.Prestige);
            _combatStats.AddModifier("climberpower_hp", new StatModifier(
                ModifierSource.Prestige, "climberpower_hp", StatType.MaxHp, HP_PER_LEVEL * _currentLevel, 0f));
            _combatStats.AddModifier("climberpower_atk", new StatModifier(
                ModifierSource.Prestige, "climberpower_atk", StatType.Atk, ATK_PER_LEVEL * _currentLevel, 0f));
            _combatStats.AddModifier("climberpower_def", new StatModifier(
                ModifierSource.Prestige, "climberpower_def", StatType.Def, DEF_PER_LEVEL * _currentLevel, 0f));
        }

        /// <summary>
        /// 현재 레벨에 해당하는 누적 보너스를 CombatStats에서 제거한다.
        /// </summary>
        private void RemoveBonus()
        {
            if (_combatStats == null || _currentLevel <= 0) return;

            _combatStats.SetCpReason("등반자 파워");
            _combatStats.ClearModifiers(ModifierSource.Prestige);
        }
    }
}
