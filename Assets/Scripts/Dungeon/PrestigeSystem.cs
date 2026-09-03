using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.Dungeon
{
    /// <summary>
    /// 프레스티지(환생) 시스템.
    /// 플레이어가 일정 조건 달성 후 리셋하면 영구 보너스를 얻는다.
    /// CombatStats modifier 시스템을 통해 전체 스탯에 percentBonus를 적용한다.
    /// </summary>
    public class PrestigeSystem : MonoBehaviour
    {
        public static PrestigeSystem Instance { get; private set; }

        [SerializeField] private int _minFloorForPrestige = 100;
        [SerializeField] private float _bonusPerPrestige = 0.05f; // 5% per prestige

        private int _prestigeCount;
        private float _totalBonus; // 누적 영구 보너스
        private CombatStats _playerStats;

        // modifier에 적용할 StatType 목록
        private static readonly StatType[] PrestigeStatTypes =
        {
            StatType.Atk,
            StatType.Def,
            StatType.MaxHp,
            StatType.CritRate,
            StatType.CritDamage,
            StatType.AttackSpeed,
            StatType.MoveSpeed
        };

        public int PrestigeCount => _prestigeCount;
        public float TotalBonus => _totalBonus;
        public bool CanPrestige => GetCurrentFloor() >= _minFloorForPrestige;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            LoadState();
            FindPlayerAndApplyModifiers();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 플레이어의 CombatStats를 찾아 캐싱하고, 기존 프레스티지 보너스가 있으면 modifier를 적용한다.
        /// </summary>
        private void FindPlayerAndApplyModifiers()
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[PrestigeSystem] PlayerCharacter를 찾을 수 없습니다.");
                return;
            }

            _playerStats = player.GetComponent<CombatStats>();
            if (_playerStats == null)
            {
                Debug.LogWarning("[PrestigeSystem] PlayerCharacter에 CombatStats가 없습니다.");
                return;
            }

            ApplyPrestigeModifiers();
        }

        /// <summary>
        /// 현재 _totalBonus 값을 기반으로 CombatStats에 프레스티지 modifier를 적용/갱신한다.
        /// _totalBonus가 0이면 기존 modifier를 제거한다.
        /// </summary>
        private void ApplyPrestigeModifiers()
        {
            if (_playerStats == null) return;

            _playerStats.SetCpReason("윤회");

            if (_totalBonus <= 0f)
            {
                _playerStats.ClearModifiers(ModifierSource.Prestige);
                return;
            }

            for (int i = 0; i < PrestigeStatTypes.Length; i++)
            {
                var statType = PrestigeStatTypes[i];
                string key = GetModifierKey(statType);
                var modifier = new StatModifier(
                    ModifierSource.Prestige,
                    key,
                    statType,
                    0f,
                    _totalBonus
                );
                _playerStats.AddModifier(key, modifier);
            }
        }

        /// <summary>
        /// StatType에 대응하는 프레스티지 modifier key를 반환한다.
        /// </summary>
        private static string GetModifierKey(StatType statType)
        {
            return statType switch
            {
                StatType.Atk => "prestige_atk",
                StatType.Def => "prestige_def",
                StatType.MaxHp => "prestige_hp",
                StatType.CritRate => "prestige_critrate",
                StatType.CritDamage => "prestige_critdmg",
                StatType.AttackSpeed => "prestige_atkspd",
                StatType.MoveSpeed => "prestige_movespd",
                _ => $"prestige_{statType}"
            };
        }

        /// <summary>
        /// 프레스티지 실행: 레벨/장비/재화 리셋, 영구 보너스 획득
        /// </summary>
        public bool ExecutePrestige()
        {
            if (!CanPrestige) return false;

            _prestigeCount++;
            _totalBonus = _prestigeCount * _bonusPerPrestige;

            // 프레스티지 보상 계산 (층수 기반)
            int floor = GetCurrentFloor();
            int prestigeTokens = CalculatePrestigeTokens(floor);

            // CombatStats modifier 갱신
            ApplyPrestigeModifiers();

            // 이벤트 발행
            EventBus<PrestigeExecutedEvent>.Publish(new PrestigeExecutedEvent
            {
                PrestigeCount = _prestigeCount,
                TotalBonus = _totalBonus,
                TokensEarned = prestigeTokens,
                FloorAtPrestige = floor
            });

            SaveState();

            Debug.Log($"[PrestigeSystem] 환생 #{_prestigeCount} 완료! 영구 보너스: {_totalBonus:P0}, 토큰: {prestigeTokens}");
            return true;
        }

        /// <summary>
        /// 프레스티지 토큰 계산 (층수 기반, 점진적 증가)
        /// </summary>
        private int CalculatePrestigeTokens(int floor)
        {
            // 100층: 10토큰, 200층: 30토큰, 300층: 60토큰
            return Mathf.FloorToInt(floor * floor / 1000f);
        }

        /// <summary>
        /// 현재 최고 층수 가져오기
        /// </summary>
        private int GetCurrentFloor()
        {
            // TowerManager가 있으면 그걸 사용, 없으면 SaveData에서
            var save = SaveManager.Instance?.CurrentData;
            return save?.progress?.maxFloor ?? 1;
        }

        /// <summary>
        /// 프레스티지 횟수에 따른 영구 스탯 배율
        /// </summary>
        public float GetStatMultiplier()
        {
            return 1f + _totalBonus;
        }

        /// <summary>
        /// 다음 프레스티지까지 남은 층수
        /// </summary>
        public int FloorsUntilPrestige()
        {
            int current = GetCurrentFloor();
            return Mathf.Max(0, _minFloorForPrestige - current);
        }

        // ── 세이브/로드 ──

        private void LoadState()
        {
            _prestigeCount = PlayerPrefs.GetInt("prestige_count", 0);
            _totalBonus = _prestigeCount * _bonusPerPrestige;
        }

        private void SaveState()
        {
            PlayerPrefs.SetInt("prestige_count", _prestigeCount);
            PlayerPrefs.Save();
        }
    }
}
