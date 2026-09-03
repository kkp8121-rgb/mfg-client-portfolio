using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Combat;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Equipment
{
    /// <summary>
    /// 무기 인벤토리 + 장착/해제 관리.
    /// 가챠 결과("Weapon" 풀)를 받아 인벤토리에 추가하고,
    /// 장착 시 CombatStats에 보너스를 적용한다.
    /// 보유 효과: 보유한 모든 무기의 passiveAtkPercent 합산.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        public static WeaponManager Instance { get; private set; }

        [Header("무기 카탈로그")]
        [SerializeField] private WeaponDataSO[] _catalog;

        private readonly List<WeaponInstance> _inventory = new();
        private WeaponInstance _equippedWeapon;

        private const string WEAPON_EQUIP_KEY = "weapon_equip";
        private const string WEAPON_PASSIVE_KEY = "weapon_passive";

        private CombatStats _playerStats;

        public IReadOnlyList<WeaponInstance> Inventory => _inventory;
        public WeaponInstance EquippedWeapon => _equippedWeapon;

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
            EventBus.Subscribe<GachaResultEvent>(OnGachaResult);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GachaResultEvent>(OnGachaResult);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;
            SyncToSaveData(save);
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;
            SyncFromSaveData(save);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 카탈로그에서 무기 데이터를 찾는다.
        /// </summary>
        public WeaponDataSO GetData(string weaponId)
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] != null && _catalog[i].id == weaponId)
                    return _catalog[i];
            }
            return null;
        }

        /// <summary>
        /// 인벤토리에서 인스턴스를 찾는다.
        /// </summary>
        public WeaponInstance GetInstance(string instanceId)
        {
            for (int i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].instanceId == instanceId)
                    return _inventory[i];
            }
            return null;
        }

        /// <summary>
        /// 인벤토리에 무기를 추가한다.
        /// </summary>
        public void AddToInventory(WeaponInstance instance)
        {
            if (instance == null) return;
            _inventory.Add(instance);
            RecalculatePassiveBonuses();
            AutoSyncToSave();

            EventBus.Publish(new WeaponInventoryChangedEvent
            {
                InstanceId = instance.instanceId,
                WeaponId = instance.weaponId,
                Grade = instance.grade,
                IsAdded = true
            });
        }

        /// <summary>
        /// 무기를 장착한다. 이미 장착된 무기가 있으면 자동 해제.
        /// </summary>
        public bool Equip(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;

            var data = GetData(instance.weaponId);
            if (data == null) return false;

            // 이미 장착 중인 무기가 있으면 해제
            if (_equippedWeapon != null)
                Unequip();

            _equippedWeapon = instance;
            instance.isEquipped = true;
            RecalculateAndApplyBonuses();
            AutoSyncToSave();

            EventBus.Publish(new WeaponChangedEvent
            {
                InstanceId = instanceId,
                WeaponId = instance.weaponId,
                Grade = instance.grade,
                IsEquipped = true
            });

            Debug.Log($"[WeaponManager] 장착: {data.displayName} ({instance.grade}) Lv{instance.level} ★{instance.awakeningStars}");
            return true;
        }

        /// <summary>
        /// 장착된 무기를 해제한다.
        /// </summary>
        public bool Unequip()
        {
            if (_equippedWeapon == null) return false;

            var instance = _equippedWeapon;
            instance.isEquipped = false;
            _equippedWeapon = null;
            RecalculateAndApplyBonuses();
            AutoSyncToSave();

            var data = GetData(instance.weaponId);
            EventBus.Publish(new WeaponChangedEvent
            {
                InstanceId = instance.instanceId,
                WeaponId = instance.weaponId,
                Grade = instance.grade,
                IsEquipped = false
            });

            Debug.Log($"[WeaponManager] 해제: {data?.displayName ?? instance.weaponId}");
            return true;
        }

        /// <summary>
        /// 인벤토리를 등급순(높은 것 먼저)으로 정렬하여 반환.
        /// </summary>
        public List<WeaponInstance> GetSortedInventory()
        {
            return _inventory
                .OrderByDescending(w => GradeToInt(w.grade))
                .ThenByDescending(w => w.level)
                .ThenByDescending(w => w.awakeningStars)
                .ThenBy(w => w.weaponId)
                .ToList();
        }

        /// <summary>
        /// 보유 효과: 보유한 모든 무기의 passiveAtkPercent 합산.
        /// </summary>
        public float GetTotalPassiveAtkPercent()
        {
            float total = 0f;
            for (int i = 0; i < _inventory.Count; i++)
            {
                var data = GetData(_inventory[i].weaponId);
                if (data != null)
                    total += data.passiveAtkPercent;
            }
            return total;
        }

        // ── 무기 레벨업 ──

        private const int BASE_WEAPON_LEVEL_CAP = 50;
        private const int AWAKENING_LEVEL_CAP_BONUS = 20;
        private const int AWAKENING_MAX_STARS = 5;
        private const int AWAKENING_MATERIAL_COUNT = 4; // 본체 제외 같은 무기 4개 소비
        private const int PROMOTE_MATERIAL_COUNT = 5; // 5성 무기 5개 필요
        private const float PASSIVE_ATK_CONVERSION = 10f; // passiveAtkPercent → ATK 고정값 변환 계수

        /// <summary>
        /// 각성 단계에 따른 무기 레벨캡을 반환한다.
        /// 기본 50 + 각성 1회당 +20 = 최대 150.
        /// </summary>
        public static int GetWeaponLevelCap(int awakeningStars)
        {
            return BASE_WEAPON_LEVEL_CAP + awakeningStars * AWAKENING_LEVEL_CAP_BONUS;
        }

        /// <summary>
        /// 무기 레벨업 비용(골드)을 계산한다. CombatFormula 위임.
        /// </summary>
        public long GetLevelUpGoldCost(int currentLevel)
        {
            return CombatFormula.WeaponLevelGold(currentLevel);
        }

        /// <summary>
        /// 무기 레벨업 비용(무기강화석)을 계산한다. CombatFormula 위임.
        /// </summary>
        public long GetLevelUpStoneCost(int currentLevel)
        {
            return CombatFormula.WeaponLevelStone(currentLevel);
        }

        /// <summary>
        /// 무기 레벨업이 가능한지 확인한다.
        /// </summary>
        public bool CanLevelUp(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            int levelCap = GetWeaponLevelCap(instance.awakeningStars);
            if (instance.level >= levelCap) return false;

            if (CurrencyManager.Instance == null) return false;

            long goldCost = GetLevelUpGoldCost(instance.level);
            long stoneCost = GetLevelUpStoneCost(instance.level);

            return CurrencyManager.Instance.HasEnough(CurrencyType.Gold, goldCost)
                && CurrencyManager.Instance.HasEnough(CurrencyType.WeaponStone, stoneCost);
        }

        /// <summary>
        /// 무기를 레벨업한다. WeaponStone + Gold 소비.
        /// </summary>
        public bool LevelUpWeapon(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            int levelCap = GetWeaponLevelCap(instance.awakeningStars);
            if (instance.level >= levelCap) return false;

            if (CurrencyManager.Instance == null) return false;

            long goldCost = GetLevelUpGoldCost(instance.level);
            long stoneCost = GetLevelUpStoneCost(instance.level);

            bool spent = CurrencyManager.Instance.SpendMultiple(
                (CurrencyType.Gold, goldCost),
                (CurrencyType.WeaponStone, stoneCost)
            );
            if (!spent) return false;

            int oldLevel = instance.level;
            instance.level++;

            // 장착 중인 무기면 보너스 재계산
            if (_equippedWeapon != null && _equippedWeapon.instanceId == instanceId)
                RecalculateAndApplyBonuses();

            AutoSyncToSave();

            EventBus.Publish(new WeaponLevelUpEvent
            {
                InstanceId = instanceId,
                WeaponId = instance.weaponId,
                OldLevel = oldLevel,
                NewLevel = instance.level
            });

            Debug.Log($"[WeaponManager] 레벨업: {instance.weaponId} Lv{oldLevel} → Lv{instance.level} (Gold:{goldCost}, Stone:{stoneCost})");
            return true;
        }

        // ── 무기 각성 ──

        /// <summary>
        /// 각성에 사용할 수 있는 재료(같은 weaponId + grade, 본체 제외) 수를 반환한다.
        /// </summary>
        public int GetAwakeningMaterialCount(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return 0;

            int count = 0;
            for (int i = 0; i < _inventory.Count; i++)
            {
                var other = _inventory[i];
                if (other.instanceId == instanceId) continue;
                if (other.weaponId == instance.weaponId && other.grade == instance.grade)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 무기 각성이 가능한지 확인한다.
        /// 같은 weaponId + 같은 grade 무기 4개(본체 제외)가 필요하며, 최대 5성.
        /// </summary>
        public bool CanAwaken(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            if (instance.awakeningStars >= AWAKENING_MAX_STARS) return false;

            return GetAwakeningMaterialCount(instanceId) >= AWAKENING_MATERIAL_COUNT;
        }

        /// <summary>
        /// 무기를 각성한다. 같은 weaponId+grade 무기 4개를 소비하여 각성 단계 +1.
        /// </summary>
        public bool AwakenWeapon(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            if (instance.awakeningStars >= AWAKENING_MAX_STARS) return false;
            if (GetAwakeningMaterialCount(instanceId) < AWAKENING_MATERIAL_COUNT) return false;

            // 재료 무기 선택 및 제거 (장착되지 않은 것 우선)
            int consumed = 0;
            var toRemove = new List<WeaponInstance>();
            for (int i = 0; i < _inventory.Count && consumed < AWAKENING_MATERIAL_COUNT; i++)
            {
                var other = _inventory[i];
                if (other.instanceId == instanceId) continue;
                if (other.isEquipped) continue; // 장착 중인 무기는 건너뜀
                if (other.weaponId == instance.weaponId && other.grade == instance.grade)
                {
                    toRemove.Add(other);
                    consumed++;
                }
            }

            // 장착 중인 것도 포함해야 수가 맞는 경우
            if (consumed < AWAKENING_MATERIAL_COUNT)
            {
                for (int i = 0; i < _inventory.Count && consumed < AWAKENING_MATERIAL_COUNT; i++)
                {
                    var other = _inventory[i];
                    if (other.instanceId == instanceId) continue;
                    if (toRemove.Contains(other)) continue;
                    if (other.weaponId == instance.weaponId && other.grade == instance.grade)
                    {
                        // 장착 중인 재료 무기 해제
                        if (other.isEquipped && _equippedWeapon == other)
                            Unequip();
                        toRemove.Add(other);
                        consumed++;
                    }
                }
            }

            if (consumed < AWAKENING_MATERIAL_COUNT) return false;

            // 재료 무기 인벤토리에서 제거
            for (int i = 0; i < toRemove.Count; i++)
            {
                _inventory.Remove(toRemove[i]);
                EventBus.Publish(new WeaponInventoryChangedEvent
                {
                    InstanceId = toRemove[i].instanceId,
                    WeaponId = toRemove[i].weaponId,
                    Grade = toRemove[i].grade,
                    IsAdded = false
                });
            }

            int oldStars = instance.awakeningStars;
            instance.awakeningStars++;

            // 장착 중인 무기면 보너스 재계산
            if (_equippedWeapon != null && _equippedWeapon.instanceId == instanceId)
                RecalculateAndApplyBonuses();

            RecalculatePassiveBonuses();
            AutoSyncToSave();

            EventBus.Publish(new WeaponAwakeningEvent
            {
                InstanceId = instanceId,
                WeaponId = instance.weaponId,
                OldStars = oldStars,
                NewStars = instance.awakeningStars
            });

            // 2026-04-23 이슈 15 FeedbackBus: 무기 각성 Toast + 쉐이크 (별 많을수록 강하게)
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.EquipEnhance,
                $"★{oldStars} → ★{instance.awakeningStars} 각성! ({instance.weaponId})",
                shakeIntensity: instance.awakeningStars >= 3 ? 3 : 2);

            Debug.Log($"[WeaponManager] 각성: {instance.weaponId} ({instance.grade}) ★{oldStars} → ★{instance.awakeningStars} (재료 {consumed}개 소비)");
            return true;
        }

        // ── 무기 승급 ──

        /// <summary>
        /// 무기 승급이 가능한지 확인한다.
        /// 조건: 본체가 5성 + 같은 등급 5성 무기 4개 추가 보유 + Mythic 미만.
        /// </summary>
        public bool CanPromote(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            if (instance.awakeningStars < AWAKENING_MAX_STARS) return false;

            string nextGrade = GetNextGrade(instance.grade);
            if (nextGrade == null) return false; // Mythic은 더 이상 승급 불가

            // 같은 grade, 5성 무기 수 (본체 제외)
            int materialCount = 0;
            for (int i = 0; i < _inventory.Count; i++)
            {
                var other = _inventory[i];
                if (other.instanceId == instanceId) continue;
                if (other.grade == instance.grade && other.awakeningStars >= AWAKENING_MAX_STARS)
                    materialCount++;
            }

            return materialCount >= AWAKENING_MATERIAL_COUNT; // 본체 제외 4개 필요
        }

        /// <summary>
        /// 무기를 승급한다.
        /// 5성 무기 본체 + 같은 등급 5성 무기 4개를 소비하여 다음 등급으로 승급.
        /// 승급 후 awakeningStars=0, level=1로 초기화.
        /// </summary>
        public bool PromoteWeapon(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;
            if (instance.awakeningStars < AWAKENING_MAX_STARS) return false;

            string nextGrade = GetNextGrade(instance.grade);
            if (nextGrade == null) return false;

            // 재료 무기 선택 (본체 제외, 같은 등급, 5성, 비장착 우선)
            var toRemove = new List<WeaponInstance>();
            int consumed = 0;

            // 1차: 비장착 무기 먼저
            for (int i = 0; i < _inventory.Count && consumed < AWAKENING_MATERIAL_COUNT; i++)
            {
                var other = _inventory[i];
                if (other.instanceId == instanceId) continue;
                if (other.isEquipped) continue;
                if (other.grade == instance.grade && other.awakeningStars >= AWAKENING_MAX_STARS)
                {
                    toRemove.Add(other);
                    consumed++;
                }
            }

            // 2차: 장착 중인 무기도 포함
            if (consumed < AWAKENING_MATERIAL_COUNT)
            {
                for (int i = 0; i < _inventory.Count && consumed < AWAKENING_MATERIAL_COUNT; i++)
                {
                    var other = _inventory[i];
                    if (other.instanceId == instanceId) continue;
                    if (toRemove.Contains(other)) continue;
                    if (other.grade == instance.grade && other.awakeningStars >= AWAKENING_MAX_STARS)
                    {
                        if (other.isEquipped && _equippedWeapon == other)
                            Unequip();
                        toRemove.Add(other);
                        consumed++;
                    }
                }
            }

            if (consumed < AWAKENING_MATERIAL_COUNT) return false;

            // 재료 무기 인벤토리에서 제거
            for (int i = 0; i < toRemove.Count; i++)
            {
                _inventory.Remove(toRemove[i]);
                EventBus.Publish(new WeaponInventoryChangedEvent
                {
                    InstanceId = toRemove[i].instanceId,
                    WeaponId = toRemove[i].weaponId,
                    Grade = toRemove[i].grade,
                    IsAdded = false
                });
            }

            // 본체 승급
            string oldGrade = instance.grade;
            instance.grade = nextGrade;
            instance.awakeningStars = 0;
            instance.level = 1;

            // 장착 중인 무기면 보너스 재계산
            if (_equippedWeapon != null && _equippedWeapon.instanceId == instanceId)
                RecalculateAndApplyBonuses();

            RecalculatePassiveBonuses();
            AutoSyncToSave();

            EventBus.Publish(new WeaponPromoteEvent
            {
                InstanceId = instanceId,
                WeaponId = instance.weaponId,
                OldGrade = oldGrade,
                NewGrade = nextGrade
            });

            Debug.Log($"[WeaponManager] 승급: {instance.weaponId} {oldGrade} → {nextGrade} (재료 {consumed}개 소비)");
            return true;
        }

        /// <summary>
        /// 승급에 사용할 수 있는 재료(같은 등급 5성, 본체 제외) 수를 반환한다.
        /// </summary>
        public int GetPromoteMaterialCount(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return 0;

            int count = 0;
            for (int i = 0; i < _inventory.Count; i++)
            {
                var other = _inventory[i];
                if (other.instanceId == instanceId) continue;
                if (other.grade == instance.grade && other.awakeningStars >= AWAKENING_MAX_STARS)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 다음 승급 등급을 반환한다. Ancient이면 null (최대).
        /// </summary>
        public static string GetNextGrade(string currentGrade)
        {
            return currentGrade switch
            {
                "Normal" => "Rare",
                "Rare" => "Epic",
                "Epic" => "Unique",
                "Unique" => "Legendary",
                "Legendary" => "Mythic",
                "Mythic" => "Ancient",
                _ => null
            };
        }

        // ── 스탯 적용 ──

        private void RecalculateAndApplyBonuses()
        {
            FindPlayerStats();
            if (_playerStats == null) return;

            // 기존 장착 수정자 제거
            _playerStats.SetCpReason("무기 장착");
            _playerStats.RemoveModifier(WEAPON_EQUIP_KEY + "_atk");
            _playerStats.RemoveModifier(WEAPON_EQUIP_KEY + "_critrate");
            _playerStats.RemoveModifier(WEAPON_EQUIP_KEY + "_atkspd");

            if (_equippedWeapon != null)
            {
                var data = GetData(_equippedWeapon.weaponId);
                if (data != null)
                {
                    int bonusAtk = data.GetFinalAtk(
                        _equippedWeapon.grade,
                        _equippedWeapon.level,
                        _equippedWeapon.awakeningStars,
                        _equippedWeapon.tier
                    );
                    float bonusCritRate = data.GetCritRate(_equippedWeapon.grade, _equippedWeapon.tier) + data.equipCritRate;
                    float bonusAtkSpd = data.GetAtkSpeed(_equippedWeapon.grade, _equippedWeapon.tier);

                    _playerStats.SetCpReason("무기 장착");

                    if (bonusAtk != 0)
                        _playerStats.AddModifier(WEAPON_EQUIP_KEY + "_atk",
                            new StatModifier(ModifierSource.Equipment, WEAPON_EQUIP_KEY, StatType.Atk, bonusAtk, 0f));

                    if (bonusCritRate != 0f)
                        _playerStats.AddModifier(WEAPON_EQUIP_KEY + "_critrate",
                            new StatModifier(ModifierSource.Equipment, WEAPON_EQUIP_KEY, StatType.CritRate, bonusCritRate, 0f));

                    if (bonusAtkSpd != 0f)
                        _playerStats.AddModifier(WEAPON_EQUIP_KEY + "_atkspd",
                            new StatModifier(ModifierSource.Equipment, WEAPON_EQUIP_KEY, StatType.AttackSpeed, bonusAtkSpd, 0f));
                }
            }
        }

        /// <summary>
        /// 보유 효과 보너스를 재계산하여 CombatStats에 적용.
        /// 모든 보유 무기의 passiveAtkPercent 합산 → ATK 고정값으로 변환(합산값 * 10).
        /// 무기 획득/삭제/승급 시 호출.
        /// </summary>
        private void RecalculatePassiveBonuses()
        {
            FindPlayerStats();
            if (_playerStats == null) return;

            // 기존 패시브 수정자 제거
            _playerStats.SetCpReason("무기 보유 효과");
            _playerStats.RemoveModifier(WEAPON_PASSIVE_KEY + "_atk");

            // 모든 보유 무기의 passiveAtkPercent 합산
            float totalPercent = GetTotalPassiveAtkPercent();

            // ATK 고정값으로 변환
            int passiveBonusAtk = Mathf.RoundToInt(totalPercent * PASSIVE_ATK_CONVERSION);

            // 새 패시브 수정자 적용
            if (passiveBonusAtk != 0)
            {
                _playerStats.SetCpReason("무기 보유 효과");
                _playerStats.AddModifier(WEAPON_PASSIVE_KEY + "_atk",
                    new StatModifier(ModifierSource.Equipment, WEAPON_PASSIVE_KEY, StatType.Atk, passiveBonusAtk, 0f));
            }
        }

        private void FindPlayerStats()
        {
            if (_playerStats != null) return;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
                _playerStats = player.GetComponent<CombatStats>();
        }

        // ── 가챠 결과 처리 ──

        private void OnGachaResult(GachaResultEvent evt)
        {
            // 무기 가챠 결과만 처리
            if (evt.PoolName != "Weapon") return;

            string tierStr = evt.Tier > 0 ? $" T{evt.Tier}" : "";
            Debug.Log($"[WeaponManager] 가챠 무기 수신: {evt.ItemId} ({evt.Grade}{tierStr})");
            var instance = new WeaponInstance(evt.ItemId, evt.Grade, evt.Tier);
            AddToInventory(instance);
            Debug.Log($"[WeaponManager] 인벤토리 추가 완료. 총 {_inventory.Count}개");
        }

        // ── 저장/로드 ──

        /// <summary>
        /// 무기 데이터를 SaveData에 동기화한다.
        /// SaveData에 WeaponSaveData가 추가되면 연동.
        /// </summary>
        public void SyncToSaveData(SaveData saveData)
        {
            if (saveData.weapon == null)
                saveData.weapon = new WeaponSaveData();

            saveData.weapon.inventory = new List<WeaponInstance>(_inventory);
            saveData.weapon.equippedInstanceId = _equippedWeapon?.instanceId;
        }

        /// <summary>
        /// SaveData에서 무기 데이터를 복원한다.
        /// </summary>
        public void SyncFromSaveData(SaveData saveData)
        {
            _inventory.Clear();
            _equippedWeapon = null;

            if (saveData.weapon == null) return;

            // 인벤토리 복원
            if (saveData.weapon.inventory != null)
            {
                for (int i = 0; i < saveData.weapon.inventory.Count; i++)
                    _inventory.Add(saveData.weapon.inventory[i]);
            }

            // 장착 복원
            if (!string.IsNullOrEmpty(saveData.weapon.equippedInstanceId))
            {
                var instance = GetInstance(saveData.weapon.equippedInstanceId);
                if (instance != null)
                {
                    _equippedWeapon = instance;
                    instance.isEquipped = true;
                }
            }

            // 보너스 재계산
            RecalculateAndApplyBonuses();
            RecalculatePassiveBonuses();
        }

        private void AutoSyncToSave()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;
            SyncToSaveData(SaveManager.Instance.CurrentData);
        }

        // ── 유틸 ──

        private static int GradeToInt(string grade)
        {
            return grade switch
            {
                "Normal" => 0,
                "Rare" => 1,
                "Epic" => 2,
                "Unique" => 3,
                "Legendary" => 4,
                "Mythic" => 5,
                "Ancient" => 6,
                _ => 0
            };
        }
    }
}
