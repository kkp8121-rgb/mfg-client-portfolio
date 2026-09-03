using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 아티팩트 시스템.
    /// 최후반 영구 성장 — 아티팩트 수집 및 장착.
    /// 해금: 스테이지 347 (가이드 퀘스트로 유도).
    /// </summary>
    public class ArtifactManager : MonoBehaviour
    {
        public static ArtifactManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private ArtifactDataSO[] _artifactCatalog;
        [SerializeField] private ArtifactSetSO[] _setCatalog;
        [SerializeField] private int _maxSlots = 4;
        [SerializeField] private int _unlockStage = 347;

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked => _isUnlocked;
        private bool _isUnlocked;

        // 보유 아티팩트
        private readonly List<ArtifactInstanceData> _owned = new();
        // 장착 슬롯 (slotIndex → artifactId, 빈 슬롯은 "")
        private readonly List<string> _equippedSlots = new();

        public int MaxSlots => _maxSlots;
        public IReadOnlyList<ArtifactInstanceData> OwnedArtifacts => _owned;
        public IReadOnlyList<string> EquippedSlots => _equippedSlots;
        /// <summary>카탈로그 참조 (읽기 전용, 자동 테스트/에디터용)</summary>
        public ArtifactDataSO[] Catalog => _artifactCatalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < _maxSlots; i++)
                _equippedSlots.Add("");
        }

        private void Start()
        {
            CheckUnlock();
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
            if (absoluteStage >= _unlockStage)
            {
                _isUnlocked = true;
                Debug.Log("[ArtifactManager] 아티팩트 시스템 해금!");
            }
        }

        /// <summary>아티팩트 획득</summary>
        public void AddArtifact(string artifactId, string grade = "Common")
        {
            bool isNew = !_owned.Exists(a => a.artifactId == artifactId);
            _owned.Add(new ArtifactInstanceData
            {
                artifactId = artifactId,
                grade = grade,
                level = 1
            });

            EventBus.Publish(new ArtifactObtainedEvent
            {
                ArtifactId = artifactId,
                Grade = grade,
                IsNew = isNew
            });

            Debug.Log($"[ArtifactManager] 아티팩트 획득: {artifactId} ({grade})");
        }

        /// <summary>아티팩트 장착</summary>
        public bool EquipArtifact(string artifactId, int slotIndex)
        {
            if (!_isUnlocked) return false;
            if (slotIndex < 0 || slotIndex >= _maxSlots) return false;

            // 보유 확인
            if (!_owned.Exists(a => a.artifactId == artifactId)) return false;

            // 이미 다른 슬롯에 장착되어 있으면 해제
            for (int i = 0; i < _equippedSlots.Count; i++)
            {
                if (_equippedSlots[i] == artifactId)
                    _equippedSlots[i] = "";
            }

            _equippedSlots[slotIndex] = artifactId;
            RecalculateAndApplyBonuses();

            int totalEquipped = 0;
            for (int i = 0; i < _equippedSlots.Count; i++)
            {
                if (!string.IsNullOrEmpty(_equippedSlots[i]))
                    totalEquipped++;
            }

            EventBus.Publish(new ArtifactEquippedEvent
            {
                ArtifactId = artifactId,
                SlotIndex = slotIndex,
                TotalEquipped = totalEquipped
            });

            Debug.Log($"[ArtifactManager] 아티팩트 장착: {artifactId} → 슬롯 {slotIndex}");
            return true;
        }

        /// <summary>아티팩트 해제</summary>
        public void UnequipArtifact(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _maxSlots) return;
            _equippedSlots[slotIndex] = "";
            RecalculateAndApplyBonuses();
        }

        /// <summary>장착된 아티팩트 수</summary>
        public int EquippedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _equippedSlots.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_equippedSlots[i]))
                        count++;
                }
                return count;
            }
        }

        private void RecalculateAndApplyBonuses()
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            // 기존 아티팩트 보너스 클리어
            stats.ClearModifiers(ModifierSource.Artifact);

            // 개별 아티팩트 보너스
            for (int i = 0; i < _equippedSlots.Count; i++)
            {
                if (string.IsNullOrEmpty(_equippedSlots[i])) continue;
                var data = FindArtifactData(_equippedSlots[i]);
                if (data == null) continue;

                string modKey = $"artifact_{i}";
                stats.AddModifier(modKey, new StatModifier
                {
                    source = ModifierSource.Artifact,
                    sourceId = modKey,
                    statType = data.bonusStat,
                    flatBonus = data.bonusFlat,
                    percentBonus = data.bonusPercent / 100f
                });
            }

            // 세트 효과
            ApplySetBonuses(stats);
            stats.SetCpReason("아티팩트");
        }

        private void ApplySetBonuses(CombatStats stats)
        {
            if (_setCatalog == null) return;

            // 세트별 장착 피스 수 계산
            var setCounts = new Dictionary<string, int>();
            for (int i = 0; i < _equippedSlots.Count; i++)
            {
                if (string.IsNullOrEmpty(_equippedSlots[i])) continue;
                var data = FindArtifactData(_equippedSlots[i]);
                if (data == null || string.IsNullOrEmpty(data.setId)) continue;

                setCounts.TryGetValue(data.setId, out int count);
                setCounts[data.setId] = count + 1;
            }

            // 세트 보너스 적용
            foreach (var kvp in setCounts)
            {
                var setData = FindSetData(kvp.Key);
                if (setData == null) continue;

                setData.GetBonuses(kvp.Value, out var stat2, out float pct2, out var stat4, out float pct4);

                if (pct2 > 0f)
                {
                    stats.AddModifier($"artifact_set2_{kvp.Key}", new StatModifier
                    {
                        source = ModifierSource.Artifact,
                        sourceId = $"artifact_set2_{kvp.Key}",
                        statType = stat2,
                        percentBonus = pct2 / 100f
                    });
                }

                if (pct4 > 0f)
                {
                    stats.AddModifier($"artifact_set4_{kvp.Key}", new StatModifier
                    {
                        source = ModifierSource.Artifact,
                        sourceId = $"artifact_set4_{kvp.Key}",
                        statType = stat4,
                        percentBonus = pct4 / 100f
                    });
                }
            }
        }

        public ArtifactDataSO FindArtifactData(string artifactId)
        {
            if (_artifactCatalog == null) return null;
            for (int i = 0; i < _artifactCatalog.Length; i++)
            {
                if (_artifactCatalog[i] != null && _artifactCatalog[i].id == artifactId)
                    return _artifactCatalog[i];
            }
            return null;
        }

        private ArtifactSetSO FindSetData(string setId)
        {
            if (_setCatalog == null) return null;
            for (int i = 0; i < _setCatalog.Length; i++)
            {
                if (_setCatalog[i] != null && _setCatalog[i].setId == setId)
                    return _setCatalog[i];
            }
            return null;
        }

        /// <summary>세이브 데이터 로드</summary>
        public void LoadFromSave(ArtifactSaveData data)
        {
            if (data == null) return;
            _owned.Clear();
            _owned.AddRange(data.owned);

            for (int i = 0; i < _maxSlots && i < data.equippedSlots.Count; i++)
                _equippedSlots[i] = data.equippedSlots[i];

            RecalculateAndApplyBonuses();
        }

        /// <summary>세이브 데이터 생성</summary>
        public ArtifactSaveData ToSaveData()
        {
            var data = new ArtifactSaveData();
            data.owned.AddRange(_owned);
            data.equippedSlots.AddRange(_equippedSlots);
            return data;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
