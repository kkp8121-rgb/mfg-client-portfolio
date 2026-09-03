using UnityEngine;
using System.Collections.Generic;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;
using MkLike.Combat;

namespace MkLike.Growth
{
    /// <summary>
    /// 각인 시스템 매니저.
    /// 탑 클리어로 획득한 각인을 관리하고, 장착된 각인의 스탯 보너스를 계산한다.
    /// </summary>
    public class InscriptionManager : MonoBehaviour
    {
        public static InscriptionManager Instance { get; private set; }

        [SerializeField] private InscriptionDataSO[] _catalog;
        [SerializeField] private int _maxSlots = 4;

        private readonly List<string> _equippedInscriptions = new();
        private readonly List<string> _ownedInscriptions = new();
        private CombatStats _playerStats;

        public int MaxSlots => _maxSlots;
        public IReadOnlyList<string> EquippedInscriptions => _equippedInscriptions;
        public IReadOnlyList<string> OwnedInscriptions => _ownedInscriptions;

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
            LoadState();
            EventBus<TowerFloorReachedEvent>.Subscribe(OnTowerFloor);
        }

        private void OnDisable()
        {
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnTowerFloor);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 각인 획득 ──

        public void AddInscription(string inscriptionId)
        {
            if (string.IsNullOrEmpty(inscriptionId)) return;
            if (_ownedInscriptions.Contains(inscriptionId)) return;

            _ownedInscriptions.Add(inscriptionId);
            SaveState();
            Debug.Log($"[InscriptionManager] 각인 획득: {inscriptionId}");
        }

        // ── 장착/해제 ──

        public bool Equip(string inscriptionId)
        {
            if (_equippedInscriptions.Count >= _maxSlots) return false;
            if (_equippedInscriptions.Contains(inscriptionId)) return false;
            if (!_ownedInscriptions.Contains(inscriptionId)) return false;

            _equippedInscriptions.Add(inscriptionId);
            SaveState();
            ApplyInscriptionModifiers();

            EventBus<InscriptionChangedEvent>.Publish(new InscriptionChangedEvent
            {
                InscriptionId = inscriptionId,
                IsEquipped = true
            });

            return true;
        }

        public void Unequip(string inscriptionId)
        {
            if (!_equippedInscriptions.Remove(inscriptionId)) return;

            SaveState();
            ApplyInscriptionModifiers();

            EventBus<InscriptionChangedEvent>.Publish(new InscriptionChangedEvent
            {
                InscriptionId = inscriptionId,
                IsEquipped = false
            });
        }

        // ── 스탯 보너스 계산 ──

        /// <summary>
        /// 장착된 모든 각인에서 특정 스탯의 합산 보너스를 반환한다.
        /// </summary>
        public float GetStatBonus(StatType statType)
        {
            float total = 0f;
            for (int i = 0; i < _equippedInscriptions.Count; i++)
            {
                InscriptionDataSO data = GetData(_equippedInscriptions[i]);
                if (data?.Effects == null) continue;

                for (int j = 0; j < data.Effects.Length; j++)
                {
                    if (data.Effects[j].StatType == statType)
                        total += data.Effects[j].Value;
                }
            }
            return total;
        }

        /// <summary>
        /// 장착된 모든 각인에서 특정 스탯의 퍼센트 보너스만 합산하여 반환한다.
        /// </summary>
        public float GetStatPercentBonus(StatType statType)
        {
            float total = 0f;
            for (int i = 0; i < _equippedInscriptions.Count; i++)
            {
                InscriptionDataSO data = GetData(_equippedInscriptions[i]);
                if (data?.Effects == null) continue;

                for (int j = 0; j < data.Effects.Length; j++)
                {
                    InscriptionEffect effect = data.Effects[j];
                    if (effect.StatType == statType && effect.IsPercentage)
                        total += effect.Value;
                }
            }
            return total;
        }

        /// <summary>
        /// 장착된 모든 각인에서 특정 스탯의 고정 보너스만 합산하여 반환한다.
        /// </summary>
        public float GetStatFlatBonus(StatType statType)
        {
            float total = 0f;
            for (int i = 0; i < _equippedInscriptions.Count; i++)
            {
                InscriptionDataSO data = GetData(_equippedInscriptions[i]);
                if (data?.Effects == null) continue;

                for (int j = 0; j < data.Effects.Length; j++)
                {
                    InscriptionEffect effect = data.Effects[j];
                    if (effect.StatType == statType && !effect.IsPercentage)
                        total += effect.Value;
                }
            }
            return total;
        }

        /// <summary>
        /// 카탈로그에서 각인 데이터를 찾아 반환한다.
        /// </summary>
        public InscriptionDataSO GetData(string inscriptionId)
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i].InscriptionId == inscriptionId)
                    return _catalog[i];
            }
            return null;
        }

        // ── 슬롯 확장 ──

        public void ExpandSlots(int additionalSlots)
        {
            _maxSlots += additionalSlots;
            SaveState();
            Debug.Log($"[InscriptionManager] 슬롯 확장 → {_maxSlots}");
        }

        // ── 탑 이벤트 ──

        private void OnTowerFloor(TowerFloorReachedEvent e)
        {
            // 100층 마일스톤: 슬롯 +1
            if (e.Floor == 100 && e.IsNewHighest)
            {
                ExpandSlots(1);
            }
        }

        // ── CombatStats 연동 ──

        private void CachePlayerStats()
        {
            if (_playerStats == null)
            {
                var player = FindFirstObjectByType<PlayerCharacter>();
                if (player != null)
                    _playerStats = player.GetComponent<CombatStats>();
            }
        }

        private void ApplyInscriptionModifiers()
        {
            CachePlayerStats();
            if (_playerStats == null) return;

            _playerStats.SetCpReason("각인");
            _playerStats.ClearModifiers(ModifierSource.Inscription);

            for (int i = 0; i < _equippedInscriptions.Count; i++)
            {
                var data = GetData(_equippedInscriptions[i]);
                if (data?.Effects == null) continue;

                for (int j = 0; j < data.Effects.Length; j++)
                {
                    var effect = data.Effects[j];
                    string key = $"inscription_{_equippedInscriptions[i]}_{j}";

                    _playerStats.AddModifier(key, new StatModifier
                    {
                        source = ModifierSource.Inscription,
                        sourceId = _equippedInscriptions[i],
                        statType = effect.StatType,
                        flatBonus = effect.IsPercentage ? 0f : effect.Value,
                        percentBonus = effect.IsPercentage ? effect.Value / 100f : 0f
                    });
                }
            }
        }

        // ── 세이브/로드 ──

        private void LoadState()
        {
            var save = Core.Save.SaveManager.Instance?.CurrentData?.inscription;
            if (save == null) return;

            _ownedInscriptions.Clear();
            if (save.owned != null)
                _ownedInscriptions.AddRange(save.owned);

            _equippedInscriptions.Clear();
            if (save.equipped != null)
                _equippedInscriptions.AddRange(save.equipped);

            if (_equippedInscriptions.Count > 0)
                ApplyInscriptionModifiers();

            Debug.Log($"[InscriptionManager] 로드 완료 — 보유 {_ownedInscriptions.Count}, 장착 {_equippedInscriptions.Count}");
        }

        private void SaveState()
        {
            var saveManager = Core.Save.SaveManager.Instance;
            if (saveManager?.CurrentData == null) return;

            var save = saveManager.CurrentData.inscription;
            save.owned = new List<string>(_ownedInscriptions);
            save.equipped = new List<string>(_equippedInscriptions);
            saveManager.Save();
        }
    }
}
