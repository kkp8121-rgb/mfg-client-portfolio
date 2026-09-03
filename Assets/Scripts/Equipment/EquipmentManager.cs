using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Combat;
using MkLike.Utils;
using MkLike.Economy;

namespace MkLike.Equipment
{
    /// <summary>
    /// 장비 인벤토리 + 장착/해제 관리.
    /// 가챠 결과를 받아 인벤토리에 추가하고,
    /// 장착 시 CombatStats에 보너스를 적용한다.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        [Header("장비 카탈로그 (자동 로드)")]
        private EquipmentDataSO[] _catalog;

        private readonly List<EquipmentInstance> _inventory = new();
        private readonly Dictionary<EquipmentSlot, EquipmentInstance> _equipped = new();
        private readonly Dictionary<EquipmentSlot, SlotEnhancementData> _slotEnhancements = new();

        private CombatStats _playerStats;

        public IReadOnlyList<EquipmentInstance> Inventory => _inventory;
        public IReadOnlyList<EquipmentDataSO> Catalog => _catalog;

        /// <summary>슬롯의 강화 데이터를 반환. 없으면 새로 생성.</summary>
        public SlotEnhancementData GetSlotEnhancement(EquipmentSlot slot)
        {
            if (!_slotEnhancements.TryGetValue(slot, out var data))
            {
                data = new SlotEnhancementData();
                _slotEnhancements[slot] = data;
            }
            return data;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Resources에서 카탈로그 자동 로드
            _catalog = Resources.LoadAll<EquipmentDataSO>("Data/Equipment");
            Debug.Log($"[EquipmentManager] 카탈로그 로드: {_catalog.Length}종");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GachaResultEvent>(OnGachaResult);
            EventBus<EliteKilledEvent>.Subscribe(OnEliteKilled);
            EventBus<OfflineEquipDropRequestEvent>.Subscribe(OnOfflineEquipDropRequest);
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GachaResultEvent>(OnGachaResult);
            EventBus<EliteKilledEvent>.Unsubscribe(OnEliteKilled);
            EventBus<OfflineEquipDropRequestEvent>.Unsubscribe(OnOfflineEquipDropRequest);
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
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

        /// <summary>
        /// 가이드 퀘스트 활성화 시 StarForceReach 조건의 현재 상태를 재발행한다.
        /// 슬롯 중 최대 스타포스 값을 기준으로 StarForceEvent(Success)를 발행.
        /// </summary>
        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition != QuestCondition.StarForceReach) return;

            int maxStarForce = 0;
            EquipmentSlot maxSlot = default;
            foreach (var kv in _slotEnhancements)
            {
                if (kv.Value != null && kv.Value.starForce > maxStarForce)
                {
                    maxStarForce = kv.Value.starForce;
                    maxSlot = kv.Key;
                }
            }

            if (maxStarForce <= 0) return;

            EventBus.Publish(new StarForceEvent
            {
                InstanceId = maxSlot.ToString(),
                IsSuccess = true,
                NewStarForce = maxStarForce,
                Result = StarForceResult.Success
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 카탈로그에서 장비 데이터를 찾는다.
        /// </summary>
        public EquipmentDataSO GetData(string equipmentId)
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] != null && _catalog[i].id == equipmentId)
                    return _catalog[i];
            }
            return null;
        }

        /// <summary>
        /// 카탈로그에서 랜덤 장비 ID를 반환한다. 오프라인 보상 장비 드롭용.
        /// </summary>
        public string GetRandomEquipmentId()
        {
            if (_catalog == null || _catalog.Length == 0) return null;
            int idx = UnityEngine.Random.Range(0, _catalog.Length);
            return _catalog[idx] != null ? _catalog[idx].id : null;
        }

        /// <summary>
        /// 인벤토리에서 인스턴스를 찾는다.
        /// </summary>
        public EquipmentInstance GetInstance(string instanceId)
        {
            for (int i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].instanceId == instanceId)
                    return _inventory[i];
            }
            return null;
        }

        /// <summary>
        /// 슬롯에 장착된 장비를 반환한다.
        /// </summary>
        public EquipmentInstance GetEquipped(EquipmentSlot slot)
        {
            return _equipped.TryGetValue(slot, out var inst) ? inst : null;
        }

        /// <summary>
        /// 장비가 장착 중인지 확인.
        /// </summary>
        public bool IsEquipped(string instanceId)
        {
            foreach (var kv in _equipped)
            {
                if (kv.Value != null && kv.Value.instanceId == instanceId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 인벤토리에 장비를 추가한다.
        /// </summary>
        public void AddToInventory(EquipmentInstance instance)
        {
            if (instance == null) return;
            _inventory.Add(instance);
            AutoSyncToSave();

            EventBus.Publish(new EquipmentInventoryChangedEvent
            {
                InstanceId = instance.instanceId,
                EquipmentId = instance.equipmentId,
                Grade = instance.grade,
                IsAdded = true
            });
        }

        /// <summary>
        /// 장비를 장착한다. 해당 슬롯에 이미 장비가 있으면 자동 해제.
        /// </summary>
        public bool Equip(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return false;

            var data = GetData(instance.equipmentId);
            if (data == null) return false;

            // 이미 다른 슬롯에 장착 중이면 먼저 해제
            if (IsEquipped(instanceId))
                Unequip(data.slot);

            // 해당 슬롯에 다른 장비가 있으면 해제
            if (_equipped.ContainsKey(data.slot))
                Unequip(data.slot);

            _equipped[data.slot] = instance;
            RecalculateAndApplyBonuses();
            AutoSyncToSave();

            EventBus.Publish(new EquipmentChangedEvent
            {
                Slot = data.slot,
                InstanceId = instanceId,
                EquipmentId = instance.equipmentId,
                Grade = instance.grade,
                IsEquipped = true
            });

            Debug.Log($"[EquipmentManager] 장착: {data.displayName} ({instance.grade}) → {data.slot}");
            return true;
        }

        /// <summary>
        /// 슬롯의 장비를 해제한다.
        /// </summary>
        public bool Unequip(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var instance))
                return false;

            _equipped.Remove(slot);
            RecalculateAndApplyBonuses();
            AutoSyncToSave();

            var data = GetData(instance.equipmentId);
            EventBus.Publish(new EquipmentChangedEvent
            {
                Slot = slot,
                InstanceId = instance.instanceId,
                EquipmentId = instance.equipmentId,
                Grade = instance.grade,
                IsEquipped = false
            });

            Debug.Log($"[EquipmentManager] 해제: {data?.displayName ?? instance.equipmentId} → {slot}");
            return true;
        }

        /// <summary>
        /// 인벤토리를 등급순(높은 것 먼저)으로 정렬하여 반환.
        /// </summary>
        public List<EquipmentInstance> GetSortedInventory()
        {
            return _inventory
                .OrderByDescending(e => GradeToInt(e.grade))
                .ThenBy(e => e.equipmentId)
                .ToList();
        }

        /// <summary>
        /// 장착된 장비 전체를 반환.
        /// </summary>
        public Dictionary<EquipmentSlot, EquipmentInstance> GetAllEquipped()
        {
            return new Dictionary<EquipmentSlot, EquipmentInstance>(_equipped);
        }

        // ── 스탯 적용 ──

        private void RecalculateAndApplyBonuses()
        {
            FindPlayerStats();
            if (_playerStats == null) return;

            // 기존 장비 수정자 일괄 제거
            _playerStats.ClearModifiers(ModifierSource.Equipment);

            // 장착된 장비별 수정자 등록
            int totalAtk = 0, totalHp = 0, totalDef = 0;
            float totalCritRate = 0f, totalAtkSpd = 0f;

            foreach (var kv in _equipped)
            {
                var data = GetData(kv.Value.equipmentId);
                if (data == null) continue;

                // 강화는 슬롯에 귀속 (장비 교체해도 강화 유지)
                var slotEnh = GetSlotEnhancement(kv.Key);
                float starMult = EquipmentDataSO.StarForceAtkMultiplier(slotEnh.starForce);
                int scrollBonus = EquipmentDataSO.ScrollBonusAtk(slotEnh.scrollLevel);

                float awakenMult = kv.Value.AwakeningMultiplier;
                totalAtk += Mathf.RoundToInt(data.GetAtk(kv.Value.grade) * starMult * awakenMult) + scrollBonus;
                totalHp += Mathf.RoundToInt(data.GetHp(kv.Value.grade) * starMult * awakenMult);
                totalDef += Mathf.RoundToInt(data.GetDef(kv.Value.grade) * starMult * awakenMult);
                totalCritRate += data.GetCritRate(kv.Value.grade);
                totalAtkSpd += data.GetAtkSpd(kv.Value.grade);
            }

            // 수정자 등록 (값이 0이 아닌 것만)
            _playerStats.SetCpReason("장비 장착");

            if (totalAtk != 0)
                _playerStats.AddModifier("equipment_atk", new StatModifier(ModifierSource.Equipment, "equipment", StatType.Atk, totalAtk, 0f));
            if (totalHp != 0)
                _playerStats.AddModifier("equipment_hp", new StatModifier(ModifierSource.Equipment, "equipment", StatType.MaxHp, totalHp, 0f));
            if (totalDef != 0)
                _playerStats.AddModifier("equipment_def", new StatModifier(ModifierSource.Equipment, "equipment", StatType.Def, totalDef, 0f));
            if (totalCritRate != 0f)
                _playerStats.AddModifier("equipment_critrate", new StatModifier(ModifierSource.Equipment, "equipment", StatType.CritRate, totalCritRate, 0f));
            if (totalAtkSpd != 0f)
                _playerStats.AddModifier("equipment_atkspd", new StatModifier(ModifierSource.Equipment, "equipment", StatType.AttackSpeed, totalAtkSpd, 0f));
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
            // 장비 가챠 결과만 처리
            if (evt.PoolName != "Equipment") return;

            Debug.Log($"[EquipmentManager] 가챠 장비 수신: {evt.ItemId} ({evt.Grade})");
            var instance = new EquipmentInstance(evt.ItemId, evt.Grade);
            AddToInventory(instance);
            Debug.Log($"[EquipmentManager] 인벤토리 추가 완료. 총 {_inventory.Count}개, 카탈로그={(_catalog != null ? _catalog.Length : 0)}종");
        }

        private void OnEliteKilled(EliteKilledEvent evt)
        {
            if (_catalog == null || _catalog.Length == 0) return;

            // 카탈로그에서 랜덤 장비 선택
            int idx = Random.Range(0, _catalog.Length);
            var equipData = _catalog[idx];
            if (equipData == null) return;

            var instance = new EquipmentInstance(equipData.id, evt.DroppedGrade);
            AddToInventory(instance);

            Debug.Log($"[EquipmentManager] 엘리트 드롭 수신: {equipData.displayName} ({evt.DroppedGrade})");
        }

        /// <summary>
        /// 오프라인 보상 장비 드롭 요청 처리 (Phase 13).
        /// Combat 어셈블리에서 이벤트로 요청, 여기서 실제 장비 생성.
        /// </summary>
        private void OnOfflineEquipDropRequest(OfflineEquipDropRequestEvent evt)
        {
            if (_catalog == null || _catalog.Length == 0) return;

            for (int i = 0; i < evt.Count; i++)
            {
                string equipId = GetRandomEquipmentId();
                if (string.IsNullOrEmpty(equipId)) continue;

                var instance = new EquipmentInstance(equipId, evt.MinGrade);
                AddToInventory(instance);
                Debug.Log($"[EquipmentManager] 오프라인 장비 드롭: {equipId} ({evt.MinGrade})");
            }
        }

        // ── 저장/로드 ──

        public void SyncToSaveData(Core.Save.SaveData saveData)
        {
            if (saveData.equipment == null)
                saveData.equipment = new Core.Save.EquipmentSaveData();

            saveData.equipment.inventory = new List<EquipmentInstance>(_inventory);
            saveData.equipment.equippedSlots = new List<Core.Save.EquippedSlotData>();

            foreach (var kv in _equipped)
            {
                saveData.equipment.equippedSlots.Add(new Core.Save.EquippedSlotData
                {
                    slot = kv.Key.ToString(),
                    instanceId = kv.Value.instanceId
                });
            }

            // 슬롯 강화 데이터 저장
            saveData.equipment.slotEnhancements = new List<Core.Save.SlotEnhancementSaveData>();
            foreach (var kv in _slotEnhancements)
            {
                saveData.equipment.slotEnhancements.Add(new Core.Save.SlotEnhancementSaveData
                {
                    slot = kv.Key.ToString(),
                    enhancement = kv.Value
                });
            }
        }

        public void SyncFromSaveData(Core.Save.SaveData saveData)
        {
            _inventory.Clear();
            _equipped.Clear();
            _slotEnhancements.Clear();

            if (saveData.equipment == null) return;

            // 인벤토리 복원
            if (saveData.equipment.inventory != null)
            {
                foreach (var inst in saveData.equipment.inventory)
                    _inventory.Add(inst);
            }

            // 장착 복원
            if (saveData.equipment.equippedSlots != null)
            {
                foreach (var slotData in saveData.equipment.equippedSlots)
                {
                    if (System.Enum.TryParse<EquipmentSlot>(slotData.slot, out var slot))
                    {
                        var instance = GetInstance(slotData.instanceId);
                        if (instance != null)
                            _equipped[slot] = instance;
                    }
                }
            }

            // 슬롯 강화 복원
            if (saveData.equipment.slotEnhancements != null)
            {
                foreach (var enhData in saveData.equipment.slotEnhancements)
                {
                    if (System.Enum.TryParse<EquipmentSlot>(enhData.slot, out var slot))
                        _slotEnhancements[slot] = enhData.enhancement ?? new SlotEnhancementData();
                }
            }

            RecalculateAndApplyBonuses();
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
                _ => 0
            };
        }

        // ═══════════════════════════════════
        //  각성 (중복 장비 겹치기)
        // ═══════════════════════════════════

        /// <summary>
        /// 같은 장비+등급의 재료를 소모하여 대상 장비를 각성한다.
        /// </summary>
        /// <returns>각성 성공 여부</returns>
        public bool TryAwaken(string targetInstanceId, string materialInstanceId)
        {
            var target = GetInstance(targetInstanceId);
            var material = GetInstance(materialInstanceId);
            if (target == null || material == null) return false;
            if (target.instanceId == material.instanceId) return false;

            // 같은 장비 ID + 같은 등급만 가능
            if (target.equipmentId != material.equipmentId) return false;
            if (target.grade != material.grade) return false;

            // 최대 각성 확인
            if (target.awakeningStars >= EquipmentInstance.MAX_AWAKENING) return false;

            // 재료가 장착 중이면 먼저 해제
            foreach (var kvp in _equipped)
            {
                if (kvp.Value.instanceId == materialInstanceId)
                {
                    Unequip(kvp.Key);
                    break;
                }
            }

            // 재료 소모
            _inventory.Remove(material);

            // 각성 단계 증가
            target.awakeningStars++;

            RecalculateAndApplyBonuses();
            AutoSyncToSave();

            EventBus.Publish(new EquipmentAwakenedEvent
            {
                InstanceId = target.instanceId,
                EquipmentId = target.equipmentId,
                Grade = target.grade,
                NewAwakeningStars = target.awakeningStars
            });

            Debug.Log($"[EquipmentManager] 각성: {target.equipmentId} ({target.grade}) → {target.awakeningStars}★");
            return true;
        }

        /// <summary>
        /// 대상 장비에 사용 가능한 각성 재료 목록을 반환한다.
        /// 같은 equipmentId + 같은 grade, 장착 중이 아닌 것.
        /// </summary>
        public System.Collections.Generic.List<EquipmentInstance> GetAwakeningMaterials(string targetInstanceId)
        {
            var target = GetInstance(targetInstanceId);
            if (target == null) return new();

            var result = new System.Collections.Generic.List<EquipmentInstance>();
            foreach (var inst in _inventory)
            {
                if (inst.instanceId == targetInstanceId) continue;
                if (inst.equipmentId != target.equipmentId) continue;
                if (inst.grade != target.grade) continue;
                result.Add(inst);
            }
            return result;
        }

        /// <summary>
        /// 특정 슬롯에 해당하는 인벤토리의 장비 목록을 반환한다.
        /// </summary>
        public System.Collections.Generic.List<EquipmentInstance> GetInventoryBySlot(EquipmentSlot slot)
        {
            var result = new System.Collections.Generic.List<EquipmentInstance>();
            foreach (var inst in _inventory)
            {
                var data = GetData(inst.equipmentId);
                if (data != null && data.slot == slot)
                    result.Add(inst);
            }
            return result;
        }

        // ═══════════════════════════════════
        //  분해 (Dismantle)
        // ═══════════════════════════════════

        private const long DISMANTLE_GOLD_PER_ITEM = 100;

        /// <summary>장비를 분해하여 골드를 획득한다.</summary>
        public bool Dismantle(string instanceId)
        {
            var inst = GetInstance(instanceId);
            if (inst == null) return false;

            // 장착 중이면 분해 불가
            foreach (var kvp in _equipped)
            {
                if (kvp.Value.instanceId == instanceId) return false;
            }

            _inventory.Remove(inst);
            long gold = DISMANTLE_GOLD_PER_ITEM * GradeToGoldMultiplier(inst.grade);
            CurrencyManager.Instance?.Add(CurrencyType.Gold, gold);

            AutoSyncToSave();
            EventBus.Publish(new EquipmentInventoryChangedEvent
            {
                InstanceId = inst.instanceId,
                EquipmentId = inst.equipmentId,
                Grade = inst.grade,
                IsAdded = false
            });

            return true;
        }

        /// <summary>등급 조건 이하 장비를 일괄 분해한다.</summary>
        public int BulkDismantle(string maxGrade)
        {
            int maxGradeInt = GradeToInt(maxGrade);
            var toRemove = new System.Collections.Generic.List<EquipmentInstance>();

            foreach (var inst in _inventory)
            {
                if (GradeToInt(inst.grade) > maxGradeInt) continue;

                // 장착 중이면 스킵
                bool isEquipped = false;
                foreach (var kvp in _equipped)
                {
                    if (kvp.Value.instanceId == inst.instanceId)
                    {
                        isEquipped = true;
                        break;
                    }
                }
                if (isEquipped) continue;

                toRemove.Add(inst);
            }

            long totalGold = 0;
            foreach (var inst in toRemove)
            {
                _inventory.Remove(inst);
                totalGold += DISMANTLE_GOLD_PER_ITEM * GradeToGoldMultiplier(inst.grade);
            }

            if (totalGold > 0)
                CurrencyManager.Instance?.Add(CurrencyType.Gold, totalGold);

            AutoSyncToSave();
            Debug.Log($"[EquipmentManager] 일괄 분해: {toRemove.Count}개, 골드 +{totalGold}");
            return toRemove.Count;
        }

        private static int GradeToGoldMultiplier(string grade)
        {
            return grade switch
            {
                "Normal" => 1,
                "Rare" => 3,
                "Epic" => 10,
                "Unique" => 30,
                "Legendary" => 100,
                "Mythic" => 500,
                _ => 1
            };
        }

        // ═══════════════════════════════════
        //  주문서 강화 (Scroll Enhancement) — 슬롯 기반
        // ═══════════════════════════════════

        private const int MAX_SCROLL_LEVEL = 10;
        private const long SCROLL_GOLD_COST = 5000;

        /// <summary>주문서 강화 성공 확률. 5번째에 +10%, 10번째에 +20%.</summary>
        public static float GetScrollSuccessRate(int currentScrollLevel)
        {
            float baseRate = 0.30f;
            if (currentScrollLevel == 4) return baseRate + 0.10f;
            if (currentScrollLevel == 9) return baseRate + 0.20f;
            return baseRate;
        }

        /// <summary>슬롯의 주문서 강화를 시도한다.</summary>
        public bool TryScrollEnhance(EquipmentSlot slot)
        {
            var slotEnh = GetSlotEnhancement(slot);
            if (slotEnh.scrollLevel >= MAX_SCROLL_LEVEL) return false;

            if (CurrencyManager.Instance == null) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, SCROLL_GOLD_COST))
                return false;

            float rate = GetScrollSuccessRate(slotEnh.scrollLevel);
            bool isSuccess = Random.value < rate;

            if (isSuccess)
            {
                slotEnh.scrollLevel++;
                RecalculateAndApplyBonuses();
            }

            AutoSyncToSave();

            EventBus.Publish(new ScrollEnhanceEvent
            {
                InstanceId = slot.ToString(),
                IsSuccess = isSuccess,
                NewScrollLevel = slotEnh.scrollLevel
            });

            AudioManager.Instance?.PlaySfx(isSuccess ? SfxType.EnhanceSuccess : SfxType.EnhanceFail);

            // 2026-04-23 이슈 15 FeedbackBus: 강화 결과 Toast + Shake (성공만 쉐이크 + 높은 레벨일수록 강함)
            if (isSuccess)
            {
                int intensity = slotEnh.scrollLevel >= 7 ? 2 : 1;
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.EquipEnhance,
                    $"강화 성공! {slot} +{slotEnh.scrollLevel}",
                    shakeIntensity: intensity);
            }
            else
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Negative,
                    $"강화 실패... {slot}");
            }
            return isSuccess;
        }

        // ═══════════════════════════════════
        //  스타포스 강화 (Star Force) — 슬롯 기반
        // ═══════════════════════════════════

        private const int MAX_STAR_FORCE = 25;
        private const long STARFORCE_BASE_COST = 1000L;
        private const long STARFORCE_COST_MULTIPLIER = 500;
        private const int SAFE_GUARD_MIN_STAR = 12;

        // 스타포스 성공 확률 테이블 (인덱스 = 구간: 0~4성, 5~9성, 10~14성, 15~19성, 20~25성)
        private static readonly float[] STARFORCE_SUCCESS_RATES = { 0.95f, 0.80f, 0.60f, 0.40f, 0.30f };

        // 스타포스 실패 시 하락 확률 테이블 (인덱스 = 구간: 0~14성, 15~19성, 20~25성)
        private static readonly float[] STARFORCE_DOWNGRADE_RATES = { 0f, 0.5f, 0.7f };

        // 스타포스 실패 시 파괴 확률 테이블 (인덱스 = 구간: 0~19성, 20~21성, 22~23성, 24~25성)
        private static readonly float[] STARFORCE_DESTROY_RATES = { 0f, 0.03f, 0.07f, 0.10f };

        /// <summary>스타포스 강화 비용 (골드).</summary>
        public static long GetStarForceCost(int currentStarForce)
        {
            return STARFORCE_BASE_COST + (long)(currentStarForce * currentStarForce * STARFORCE_COST_MULTIPLIER);
        }

        /// <summary>스타포스 성공 확률.</summary>
        public static float GetStarForceSuccessRate(int currentStarForce)
        {
            if (currentStarForce < 5) return STARFORCE_SUCCESS_RATES[0];
            if (currentStarForce < 10) return STARFORCE_SUCCESS_RATES[1];
            if (currentStarForce < 15) return STARFORCE_SUCCESS_RATES[2];
            if (currentStarForce < 20) return STARFORCE_SUCCESS_RATES[3];
            return STARFORCE_SUCCESS_RATES[4];
        }

        /// <summary>스타포스 실패 시 하락 확률 (15성 이상).</summary>
        public static float GetStarForceDowngradeRate(int currentStarForce)
        {
            if (currentStarForce < 15) return STARFORCE_DOWNGRADE_RATES[0];
            if (currentStarForce < 20) return STARFORCE_DOWNGRADE_RATES[1];
            return STARFORCE_DOWNGRADE_RATES[2];
        }

        /// <summary>스타포스 실패 시 파괴 확률 (20성 이상).</summary>
        public static float GetStarForceDestroyRate(int currentStarForce)
        {
            if (currentStarForce < 20) return STARFORCE_DESTROY_RATES[0];
            if (currentStarForce < 22) return STARFORCE_DESTROY_RATES[1];
            if (currentStarForce < 24) return STARFORCE_DESTROY_RATES[2];
            return STARFORCE_DESTROY_RATES[3];
        }

        /// <summary>슬롯의 스타포스 강화를 시도한다.</summary>
        public bool TryStarForceEnhance(EquipmentSlot slot)
        {
            var slotEnh = GetSlotEnhancement(slot);
            if (slotEnh.starForce >= MAX_STAR_FORCE) return false;

            long cost = GetStarForceCost(slotEnh.starForce);
            if (CurrencyManager.Instance == null) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, cost))
                return false;

            float rate = GetStarForceSuccessRate(slotEnh.starForce);
            float roll = Random.value;
            StarForceResult result;

            if (roll < rate)
            {
                // 성공
                slotEnh.starForce++;
                result = StarForceResult.Success;
                RecalculateAndApplyBonuses();
            }
            else
            {
                // 실패 — 파괴/하락/유지 판정
                float destroyRate = GetStarForceDestroyRate(slotEnh.starForce);
                float downgradeRate = GetStarForceDowngradeRate(slotEnh.starForce);
                float failRoll = Random.value;

                if (destroyRate > 0f && failRoll < destroyRate)
                {
                    // 파괴: 안전 강화 하한 성급으로 리셋
                    slotEnh.starForce = SAFE_GUARD_MIN_STAR;
                    result = StarForceResult.Destroy;
                    RecalculateAndApplyBonuses();
                    Debug.Log($"[EquipmentManager] 스타포스 파괴! {slot} → {SAFE_GUARD_MIN_STAR}성으로 리셋");
                }
                else if (downgradeRate > 0f && failRoll < destroyRate + downgradeRate)
                {
                    // 하락: -1
                    slotEnh.starForce = Mathf.Max(0, slotEnh.starForce - 1);
                    result = StarForceResult.Downgrade;
                    RecalculateAndApplyBonuses();
                }
                else
                {
                    // 단순 실패 (유지)
                    result = StarForceResult.Fail;
                }
            }

            AutoSyncToSave();

            EventBus.Publish(new StarForceEvent
            {
                InstanceId = slot.ToString(),
                IsSuccess = result == StarForceResult.Success,
                NewStarForce = slotEnh.starForce,
                Result = result
            });

            var sfx = result == StarForceResult.Success ? SfxType.StarforceUp
                    : result == StarForceResult.Destroy ? SfxType.EnhanceFail
                    : SfxType.EnhanceFail;
            AudioManager.Instance?.PlaySfx(sfx);

            // 2026-04-23 이슈 15 FeedbackBus: 스타포스 결과 Toast + 쉐이크 (결과 종류별)
            switch (result)
            {
                case StarForceResult.Success:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.EquipEnhance,
                        $"★ {slotEnh.starForce}성 달성! ({slot})",
                        shakeIntensity: slotEnh.starForce >= 15 ? 3 : 2);
                    break;
                case StarForceResult.Destroy:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.Negative,
                        $"💥 파괴! {slot} → {SAFE_GUARD_MIN_STAR}성 리셋",
                        shakeIntensity: 3);
                    break;
                case StarForceResult.Downgrade:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.Negative,
                        $"하락... {slot} {slotEnh.starForce}성");
                    break;
                case StarForceResult.Fail:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.Negative,
                        $"실패 {slot} {slotEnh.starForce}성 유지");
                    break;
            }
            return result == StarForceResult.Success;
        }

        // ═══════════════════════════════════
        //  서버/로컬 자동 분기 (Async)
        // ═══════════════════════════════════

        /// <summary>서버 모드 여부 (미인증 시 오프라인 fallback).</summary>
        public bool IsServerMode => MkLike.Core.Net.ApiClient.HasAuth;

        /// <summary>주문서 강화 (서버/로컬 자동 분기).</summary>
        /// <remarks>
        /// 서버 엔드포인트(/equipment/enhance)는 준비되어 있으나 클라 DTO가 아직 없음.
        /// HasAuth=true여도 현재는 로컬 경로로 fallback (재화 차감은 CurrencyManager.Spend → 서버 경로로 자동 위임됨).
        /// DTO 정의 + 서버 요청 구현 시 이 메서드 내부에서 ServerAsync를 우선 호출하도록 전환.
        /// </remarks>
        public async Cysharp.Threading.Tasks.UniTask<bool> TryScrollEnhanceAsync(
            EquipmentSlot slot,
            System.Threading.CancellationToken ct = default)
        {
            // TODO(P1-11): 클라 DTO/ServerAsync 구현 후 HasAuth 분기 추가.
            // if (IsServerMode) return await TryScrollEnhanceServerAsync(slot, ct);
            await Cysharp.Threading.Tasks.UniTask.Yield(ct);
            return TryScrollEnhance(slot);
        }

        /// <summary>스타포스 강화 (서버/로컬 자동 분기).</summary>
        public async Cysharp.Threading.Tasks.UniTask<bool> TryStarForceEnhanceAsync(
            EquipmentSlot slot,
            System.Threading.CancellationToken ct = default)
        {
            // TODO(P1-11): 서버 DTO 구현 후 HasAuth 분기 전환.
            await Cysharp.Threading.Tasks.UniTask.Yield(ct);
            return TryStarForceEnhance(slot);
        }
    }
}
