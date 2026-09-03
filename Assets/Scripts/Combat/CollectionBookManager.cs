using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 도감 시스템. 몬스터/장비/무기/동료/유물/펫/코스튬 수집 현황을 추적하고,
    /// 마일스톤 달성 시 CombatStats modifier로 영구 보너스를 부여한다.
    /// SO 기반 마일스톤 데이터와 칭호 시스템을 지원한다.
    /// </summary>
    public class CollectionBookManager : MonoBehaviour
    {
        public static CollectionBookManager Instance { get; private set; }

        private const string SAVE_KEY = "CollectionBook";

        [SerializeField] private CollectionDataSO _collectionData;

        // 카테고리별 수집된 엔트리 ID
        private Dictionary<CollectionCategory, HashSet<string>> _collected = new();

        // 카테고리별 수령한 마일스톤 인덱스 (보상 수동 수령 추적)
        private Dictionary<CollectionCategory, HashSet<int>> _claimedMilestones = new();

        // 이전에 적용된 마일스톤 수 (중복 적용 방지)
        private Dictionary<CollectionCategory, int> _appliedMilestoneCount = new();

        // 현재 칭호
        private string _currentTitle = "";

        private CombatStats _playerStats;

        // 카테고리별 기본 전체 수 (SO 없을 때 폴백)
        private static readonly Dictionary<CollectionCategory, int> DefaultTotals = new()
        {
            { CollectionCategory.Monster, 50 },
            { CollectionCategory.Equipment, 30 },
            { CollectionCategory.Weapon, 30 },
            { CollectionCategory.Companion, 20 },
            // CollectionCategory.Relic: 2026-04-20 유물 시스템 완전 제거
            { CollectionCategory.Pet, 15 },
            { CollectionCategory.Costume, 30 }
        };

        // SO 없을 때 사용되는 기본 마일스톤 (하드코딩 폴백)
        private static readonly Dictionary<CollectionCategory, MilestoneEntry[]> DefaultMilestones = new()
        {
            {
                CollectionCategory.Monster, new[]
                {
                    new MilestoneEntry { Threshold = 10, StatType = StatType.MaxHp, FlatBonus = 100f },
                    new MilestoneEntry { Threshold = 25, StatType = StatType.MaxHp, FlatBonus = 300f },
                    new MilestoneEntry { Threshold = 50, StatType = StatType.MaxHp, FlatBonus = 800f }
                }
            },
            {
                CollectionCategory.Equipment, new[]
                {
                    new MilestoneEntry { Threshold = 5, StatType = StatType.Def, FlatBonus = 50f },
                    new MilestoneEntry { Threshold = 10, StatType = StatType.Def, FlatBonus = 150f },
                    new MilestoneEntry { Threshold = 20, StatType = StatType.Def, FlatBonus = 400f }
                }
            },
            {
                CollectionCategory.Weapon, new[]
                {
                    new MilestoneEntry { Threshold = 5, StatType = StatType.Atk, FlatBonus = 50f },
                    new MilestoneEntry { Threshold = 10, StatType = StatType.Atk, FlatBonus = 150f },
                    new MilestoneEntry { Threshold = 20, StatType = StatType.Atk, FlatBonus = 400f }
                }
            },
            {
                CollectionCategory.Companion, new[]
                {
                    new MilestoneEntry { Threshold = 5, StatType = StatType.AttackSpeed, PercentBonus = 0.05f },
                    new MilestoneEntry { Threshold = 10, StatType = StatType.AttackSpeed, PercentBonus = 0.10f }
                }
            },
            // CollectionCategory.Relic 마일스톤: 2026-04-20 유물 시스템 완전 제거
            {
                CollectionCategory.Pet, new[]
                {
                    new MilestoneEntry { Threshold = 5, StatType = StatType.ExpBonus, PercentBonus = 0.05f },
                    new MilestoneEntry { Threshold = 10, StatType = StatType.GoldBonus, PercentBonus = 0.10f }
                }
            },
            {
                CollectionCategory.Costume, new[]
                {
                    new MilestoneEntry { Threshold = 5, StatType = StatType.AllStats, PercentBonus = 0.01f },
                    new MilestoneEntry { Threshold = 10, StatType = StatType.AllStats, PercentBonus = 0.03f },
                    new MilestoneEntry { Threshold = 20, StatType = StatType.AllStats, PercentBonus = 0.05f }
                }
            }
        };

        private struct MilestoneEntry
        {
            public int Threshold;
            public StatType StatType;
            public float FlatBonus;
            public float PercentBonus;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (CollectionCategory cat in Enum.GetValues(typeof(CollectionCategory)))
            {
                _collected[cat] = new HashSet<string>();
                _claimedMilestones[cat] = new HashSet<int>();
                _appliedMilestoneCount[cat] = 0;
            }

            // _collectionData 미할당 시 Resources/Data/CollectionData 폴백 로드
            if (_collectionData == null)
            {
                _collectionData = Resources.Load<CollectionDataSO>("Data/CollectionData");
                if (_collectionData != null)
                    Debug.Log("[CollectionBookManager] Resources/Data/CollectionData 폴백 로드 성공");
                else
                    Debug.LogWarning("[CollectionBookManager] _collectionData 미할당 + Resources 폴백 없음. 'mkLike/Collection/Generate Default Data' 메뉴 실행 필요.");
            }
        }

        private void Start()
        {
            LoadState();
            FindPlayerAndApplyModifiers();

            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Subscribe(OnWeaponChanged);
            EventBus<CostumeObtainedEvent>.Subscribe(OnCostumeObtained);
            EventBus<LootDroppedEvent>.Subscribe(OnLootDropped);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Unsubscribe(OnWeaponChanged);
            EventBus<CostumeObtainedEvent>.Unsubscribe(OnCostumeObtained);
            EventBus<LootDroppedEvent>.Unsubscribe(OnLootDropped);
        }

        // ── Public API ──

        /// <summary>
        /// 도감에 엔트리를 등록한다. 최초 등록 시 이벤트 발행 + 마일스톤 체크.
        /// </summary>
        public void RegisterEntry(CollectionCategory category, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_collected.ContainsKey(category)) return;

            if (_collected[category].Add(id))
            {
                EventBus<CollectionEntryRegisteredEvent>.Publish(new CollectionEntryRegisteredEvent
                {
                    Category = category,
                    EntryId = id,
                    TotalCollected = GetTotalCollected()
                });

                CheckAndApplyMilestones(category);
                UpdateTitle();
                SaveState();

#if UNITY_EDITOR
                Debug.Log($"[CollectionBook] {category} 도감 등록: {id} ({_collected[category].Count}종)");
#endif
            }
        }

        /// <summary>
        /// 특정 엔트리가 수집되었는지 확인한다.
        /// </summary>
        public bool IsCollected(CollectionCategory category, string id)
        {
            return _collected.ContainsKey(category) && _collected[category].Contains(id);
        }

        /// <summary>
        /// 카테고리별 수집 진행률을 반환한다.
        /// </summary>
        public (int collected, int total) GetProgress(CollectionCategory category)
        {
            int collected = _collected.ContainsKey(category) ? _collected[category].Count : 0;
            int total = GetCategoryTotal(category);
            return (collected, total);
        }

        /// <summary>
        /// 전체 수집 수를 반환한다.
        /// </summary>
        public int GetTotalCollected()
        {
            int total = 0;
            foreach (var kvp in _collected)
                total += kvp.Value.Count;
            return total;
        }

        /// <summary>
        /// 전체 도감 완성도(0~1)를 반환한다.
        /// </summary>
        public float GetOverallCompletionRate()
        {
            int collected = GetTotalCollected();
            int total = 0;
            foreach (CollectionCategory cat in Enum.GetValues(typeof(CollectionCategory)))
                total += GetCategoryTotal(cat);
            return total > 0 ? (float)collected / total : 0f;
        }

        /// <summary>
        /// 카테고리의 마일스톤 정보를 반환한다. (threshold, statType, flatBonus, percentBonus, description, isClaimed, isAchieved)
        /// </summary>
        public List<MilestoneInfo> GetMilestoneInfos(CollectionCategory category)
        {
            var result = new List<MilestoneInfo>();
            int collected = _collected.ContainsKey(category) ? _collected[category].Count : 0;

            if (_collectionData != null)
            {
                var milestones = _collectionData.GetMilestones(category);
                if (milestones != null)
                {
                    for (int i = 0; i < milestones.Length; i++)
                    {
                        var m = milestones[i];
                        result.Add(new MilestoneInfo
                        {
                            Index = i,
                            Threshold = m.threshold,
                            StatType = m.statType,
                            FlatBonus = m.flatBonus,
                            PercentBonus = m.percentBonus,
                            Description = m.description,
                            IsAchieved = collected >= m.threshold,
                            IsClaimed = _claimedMilestones.ContainsKey(category) && _claimedMilestones[category].Contains(i)
                        });
                    }
                    return result;
                }
            }

            // 폴백: 기본 마일스톤
            if (DefaultMilestones.TryGetValue(category, out var defaults))
            {
                for (int i = 0; i < defaults.Length; i++)
                {
                    var d = defaults[i];
                    result.Add(new MilestoneInfo
                    {
                        Index = i,
                        Threshold = d.Threshold,
                        StatType = d.StatType,
                        FlatBonus = d.FlatBonus,
                        PercentBonus = d.PercentBonus,
                        Description = $"{d.StatType} +{(d.FlatBonus > 0 ? d.FlatBonus.ToString("F0") : (d.PercentBonus * 100f).ToString("F0") + "%")}",
                        IsAchieved = collected >= d.Threshold,
                        IsClaimed = _claimedMilestones.ContainsKey(category) && _claimedMilestones[category].Contains(i)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 마일스톤 보상을 수령한다. 달성했지만 아직 수령하지 않은 마일스톤만 수령 가능.
        /// </summary>
        public bool ClaimMilestoneReward(CollectionCategory category, int milestoneIndex)
        {
            if (!_claimedMilestones.ContainsKey(category)) return false;
            if (_claimedMilestones[category].Contains(milestoneIndex)) return false;

            int collected = _collected.ContainsKey(category) ? _collected[category].Count : 0;
            int threshold = GetMilestoneThreshold(category, milestoneIndex);
            if (threshold <= 0 || collected < threshold) return false;

            _claimedMilestones[category].Add(milestoneIndex);
            SaveState();

            EventBus<CollectionMilestoneClaimedEvent>.Publish(new CollectionMilestoneClaimedEvent
            {
                Category = category,
                MilestoneIndex = milestoneIndex,
                Threshold = threshold
            });

#if UNITY_EDITOR
            Debug.Log($"[CollectionBook] {category} 마일스톤 {threshold}종 보상 수령");
#endif
            return true;
        }

        /// <summary>
        /// 현재 칭호를 반환한다.
        /// </summary>
        public string GetCurrentTitle()
        {
            return _currentTitle;
        }

        /// <summary>
        /// 전체 칭호 목록과 달성 여부를 반환한다.
        /// </summary>
        public List<TitleInfo> GetTitleInfos()
        {
            var result = new List<TitleInfo>();
            int totalCollected = GetTotalCollected();

            if (_collectionData != null && _collectionData.titles != null)
            {
                for (int i = 0; i < _collectionData.titles.Length; i++)
                {
                    var t = _collectionData.titles[i];
                    result.Add(new TitleInfo
                    {
                        TitleName = t.titleName,
                        RequiredTotal = t.requiredTotal,
                        Description = t.description,
                        TitleColor = t.titleColor,
                        IsAchieved = totalCollected >= t.requiredTotal,
                        IsCurrent = t.titleName == _currentTitle
                    });
                }
            }
            else
            {
                // 폴백 칭호
                var defaultTitles = new[]
                {
                    ("초보 수집가", 10, "#AAAAAA"),
                    ("견습 수집가", 30, "#66CCFF"),
                    ("숙련 수집가", 60, "#66FF66"),
                    ("전문 수집가", 100, "#FFD700"),
                    ("탑의 박물관장", 150, "#FF6600")
                };

                for (int i = 0; i < defaultTitles.Length; i++)
                {
                    Color c;
                    ColorUtility.TryParseHtmlString(defaultTitles[i].Item3, out c);
                    result.Add(new TitleInfo
                    {
                        TitleName = defaultTitles[i].Item1,
                        RequiredTotal = defaultTitles[i].Item2,
                        Description = $"전체 {defaultTitles[i].Item2}종 수집",
                        TitleColor = c,
                        IsAchieved = totalCollected >= defaultTitles[i].Item2,
                        IsCurrent = defaultTitles[i].Item1 == _currentTitle
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 카테고리별 수집된 엔트리 ID 목록을 반환한다.
        /// </summary>
        public IReadOnlyCollection<string> GetCollectedEntries(CollectionCategory category)
        {
            return _collected.ContainsKey(category) ? _collected[category] : null;
        }

        // ── 내부 헬퍼 ──

        private int GetCategoryTotal(CollectionCategory category)
        {
            if (_collectionData != null) return _collectionData.GetTotal(category);
            return DefaultTotals.TryGetValue(category, out int total) ? total : 0;
        }

        private int GetMilestoneThreshold(CollectionCategory category, int index)
        {
            if (_collectionData != null)
            {
                var milestones = _collectionData.GetMilestones(category);
                if (milestones != null && index >= 0 && index < milestones.Length)
                    return milestones[index].threshold;
            }

            if (DefaultMilestones.TryGetValue(category, out var defaults) && index >= 0 && index < defaults.Length)
                return defaults[index].Threshold;

            return 0;
        }

        // ── 마일스톤 보너스 ──

        private void CheckAndApplyMilestones(CollectionCategory category)
        {
            if (_playerStats == null) FindPlayerStats();
            if (_playerStats == null) return;

            int collected = _collected[category].Count;
            int applied = _appliedMilestoneCount.ContainsKey(category) ? _appliedMilestoneCount[category] : 0;

            if (_collectionData != null)
            {
                var milestones = _collectionData.GetMilestones(category);
                if (milestones != null)
                {
                    for (int i = applied; i < milestones.Length; i++)
                    {
                        if (collected >= milestones[i].threshold)
                        {
                            var m = milestones[i];
                            string key = $"collection_{category}_{m.threshold}";
                            ApplyMilestoneModifier(key, m.statType, m.flatBonus, m.percentBonus);
                            _appliedMilestoneCount[category] = i + 1;
                        }
                        else break;
                    }
                    return;
                }
            }

            // 폴백: 기본 마일스톤
            if (!DefaultMilestones.TryGetValue(category, out var defaults)) return;

            for (int i = applied; i < defaults.Length; i++)
            {
                if (collected >= defaults[i].Threshold)
                {
                    var entry = defaults[i];
                    string key = $"collection_{category}_{entry.Threshold}";
                    ApplyMilestoneModifier(key, entry.StatType, entry.FlatBonus, entry.PercentBonus);
                    _appliedMilestoneCount[category] = i + 1;
                }
                else break;
            }
        }

        private void ApplyMilestoneModifier(string key, StatType statType, float flatBonus, float percentBonus)
        {
            _playerStats.SetCpReason("도감");
            if (statType == StatType.AllStats)
            {
                ApplyAllStatsModifier(key, percentBonus);
            }
            else
            {
                _playerStats.AddModifier(key, new StatModifier(
                    ModifierSource.Collection,
                    key,
                    statType,
                    flatBonus,
                    percentBonus
                ));
            }
        }

        private void ApplyAllStatsModifier(string keyPrefix, float percentBonus)
        {
            StatType[] allStatTypes = { StatType.Atk, StatType.Def, StatType.MaxHp, StatType.CritRate, StatType.CritDamage, StatType.AttackSpeed, StatType.MoveSpeed };
            for (int i = 0; i < allStatTypes.Length; i++)
            {
                _playerStats.AddModifier($"{keyPrefix}_{allStatTypes[i]}", new StatModifier(
                    ModifierSource.Collection,
                    keyPrefix,
                    allStatTypes[i],
                    0f,
                    percentBonus
                ));
            }
        }

        private void FindPlayerStats()
        {
            if (_playerStats != null) return;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
                _playerStats = player.GetComponent<CombatStats>();
        }

        private void FindPlayerAndApplyModifiers()
        {
            FindPlayerStats();
            if (_playerStats == null) return;

            foreach (CollectionCategory cat in Enum.GetValues(typeof(CollectionCategory)))
            {
                _appliedMilestoneCount[cat] = 0;
                CheckAndApplyMilestones(cat);
            }
        }

        // ── 칭호 시스템 ──

        private void UpdateTitle()
        {
            int totalCollected = GetTotalCollected();

            if (_collectionData != null)
            {
                var title = _collectionData.GetCurrentTitle(totalCollected);
                string newTitle = title != null ? title.titleName : "";
                if (newTitle != _currentTitle)
                {
                    string prevTitle = _currentTitle;
                    _currentTitle = newTitle;
                    EventBus<CollectionTitleChangedEvent>.Publish(new CollectionTitleChangedEvent
                    {
                        PreviousTitle = prevTitle,
                        NewTitle = _currentTitle,
                        TotalCollected = totalCollected
                    });
                }
                return;
            }

            // 폴백 칭호
            string fallbackTitle = "";
            if (totalCollected >= 150) fallbackTitle = "탑의 박물관장";
            else if (totalCollected >= 100) fallbackTitle = "전문 수집가";
            else if (totalCollected >= 60) fallbackTitle = "숙련 수집가";
            else if (totalCollected >= 30) fallbackTitle = "견습 수집가";
            else if (totalCollected >= 10) fallbackTitle = "초보 수집가";

            if (fallbackTitle != _currentTitle)
            {
                string prevTitle = _currentTitle;
                _currentTitle = fallbackTitle;
                EventBus<CollectionTitleChangedEvent>.Publish(new CollectionTitleChangedEvent
                {
                    PreviousTitle = prevTitle,
                    NewTitle = _currentTitle,
                    TotalCollected = totalCollected
                });
            }
        }

        // ── 이벤트 핸들러 ──

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (evt.Monster == null) return;
            // LeanPool.Spawn 시 "(Clone)" suffix가 붙음 → 원본 프리팹 이름만 키로 사용
            string entryId = evt.Monster.name;
            int cloneIdx = entryId.IndexOf("(Clone)");
            if (cloneIdx > 0) entryId = entryId.Substring(0, cloneIdx).TrimEnd();
            RegisterEntry(CollectionCategory.Monster, entryId);
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.EquipmentId))
                RegisterEntry(CollectionCategory.Equipment, evt.EquipmentId);
        }

        private void OnWeaponChanged(WeaponChangedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.WeaponId))
                RegisterEntry(CollectionCategory.Weapon, evt.WeaponId);
        }

        // OnRelicObtained: 2026-04-20 유물 시스템 완전 제거

        private void OnCostumeObtained(CostumeObtainedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.CostumeId))
                RegisterEntry(CollectionCategory.Costume, evt.CostumeId);
        }

        private void OnLootDropped(LootDroppedEvent evt)
        {
            // 특수 드롭(장비/무기)은 별도 이벤트로 처리됨. 여기서는 추가 로직 없음.
        }

        // ── 저장/불러오기 ──

        [Serializable]
        private class CollectionSaveData
        {
            public List<CategoryData> categories = new();
            public List<ClaimedData> claimed = new();
            public string currentTitle = "";
        }

        [Serializable]
        private class CategoryData
        {
            public string category;
            public List<string> entries = new();
        }

        [Serializable]
        private class ClaimedData
        {
            public string category;
            public List<int> milestoneIndices = new();
        }

        private void SaveState()
        {
            var data = new CollectionSaveData();
            data.currentTitle = _currentTitle;

            foreach (var kvp in _collected)
            {
                if (kvp.Value.Count == 0) continue;
                var catData = new CategoryData
                {
                    category = kvp.Key.ToString(),
                    entries = new List<string>(kvp.Value)
                };
                data.categories.Add(catData);
            }

            foreach (var kvp in _claimedMilestones)
            {
                if (kvp.Value.Count == 0) continue;
                var claimedData = new ClaimedData
                {
                    category = kvp.Key.ToString(),
                    milestoneIndices = new List<int>(kvp.Value)
                };
                data.claimed.Add(claimedData);
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SAVE_KEY, json);
        }

        private void LoadState()
        {
            string json = PlayerPrefs.GetString(SAVE_KEY, "");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<CollectionSaveData>(json);
            if (data == null) return;

            _currentTitle = data.currentTitle ?? "";

            if (data.categories != null)
            {
                for (int i = 0; i < data.categories.Count; i++)
                {
                    var catData = data.categories[i];
                    if (Enum.TryParse<CollectionCategory>(catData.category, out var cat))
                    {
                        if (!_collected.ContainsKey(cat))
                            _collected[cat] = new HashSet<string>();

                        for (int j = 0; j < catData.entries.Count; j++)
                            _collected[cat].Add(catData.entries[j]);
                    }
                }
            }

            if (data.claimed != null)
            {
                for (int i = 0; i < data.claimed.Count; i++)
                {
                    var claimedData = data.claimed[i];
                    if (Enum.TryParse<CollectionCategory>(claimedData.category, out var cat))
                    {
                        if (!_claimedMilestones.ContainsKey(cat))
                            _claimedMilestones[cat] = new HashSet<int>();

                        for (int j = 0; j < claimedData.milestoneIndices.Count; j++)
                            _claimedMilestones[cat].Add(claimedData.milestoneIndices[j]);
                    }
                }
            }
        }
    }

    // ── 공개 데이터 구조체 ──

    public struct MilestoneInfo
    {
        public int Index;
        public int Threshold;
        public StatType StatType;
        public float FlatBonus;
        public float PercentBonus;
        public string Description;
        public bool IsAchieved;
        public bool IsClaimed;
    }

    public struct TitleInfo
    {
        public string TitleName;
        public int RequiredTotal;
        public string Description;
        public Color TitleColor;
        public bool IsAchieved;
        public bool IsCurrent;
    }
}
