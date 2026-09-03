using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Utils;

namespace MkLike.Costume
{
    /// <summary>
    /// 코스튬 런타임 인스턴스 데이터.
    /// </summary>
    [System.Serializable]
    public class CostumeInstance
    {
        public string CostumeId;
        public string DisplayName;
        public string Grade;
        public string SetId;
        public bool IsEquipped;
    }

    /// <summary>
    /// 코스튬 매니저. 코스튬 보유/장착/세트 효과를 관리한다.
    /// </summary>
    public class CostumeManager : MonoBehaviour
    {
        public static CostumeManager Instance { get; private set; }

        private static readonly string[] GRADE_ORDER =
        {
            "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic"
        };

        /// <summary>보유 코스튬 목록 (costumeId → CostumeInstance)</summary>
        private readonly Dictionary<string, CostumeInstance> _ownedCostumes = new();

        /// <summary>장착 중인 코스튬 ID 목록 (파츠별 장착)</summary>
        private readonly HashSet<string> _equippedIds = new();

        /// <summary>완성된 세트 목록</summary>
        private readonly HashSet<string> _completedSets = new();

        public IReadOnlyDictionary<string, CostumeInstance> OwnedCostumes => _ownedCostumes;
        public IReadOnlyCollection<string> EquippedIds => _equippedIds;
        public IReadOnlyCollection<string> CompletedSets => _completedSets;
        public int TotalOwned => _ownedCostumes.Count;

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
        }

        /// <summary>코스튬 획득</summary>
        public void AddCostume(string costumeId, string displayName, string grade, string setId = "")
        {
            bool isNew = !_ownedCostumes.ContainsKey(costumeId);

            if (isNew)
            {
                _ownedCostumes[costumeId] = new CostumeInstance
                {
                    CostumeId = costumeId,
                    DisplayName = displayName,
                    Grade = grade,
                    SetId = setId,
                    IsEquipped = false
                };
            }

            EventBus<CostumeObtainedEvent>.Publish(new CostumeObtainedEvent
            {
                CostumeId = costumeId,
                Grade = grade,
                IsNew = isNew
            });

            Debug.Log($"[CostumeManager] 코스튬 획득: {displayName} ({grade}){(isNew ? " [NEW]" : " [중복]")}");

            // 세트 체크
            if (!string.IsNullOrEmpty(setId))
                CheckSetComplete(setId);

            SaveState();
        }

        /// <summary>코스튬 장착</summary>
        public void EquipCostume(string costumeId)
        {
            if (!_ownedCostumes.TryGetValue(costumeId, out var costume)) return;

            costume.IsEquipped = true;
            _equippedIds.Add(costumeId);

            EventBus<CostumeEquippedEvent>.Publish(new CostumeEquippedEvent
            {
                CostumeId = costumeId,
                TotalEquipped = _equippedIds.Count
            });

            Debug.Log($"[CostumeManager] 코스튬 장착: {costume.DisplayName} (총 {_equippedIds.Count}개)");
            SaveState();
        }

        /// <summary>코스튬 해제</summary>
        public void UnequipCostume(string costumeId)
        {
            if (!_ownedCostumes.TryGetValue(costumeId, out var costume)) return;

            costume.IsEquipped = false;
            _equippedIds.Remove(costumeId);

            Debug.Log($"[CostumeManager] 코스튬 해제: {costume.DisplayName}");
            SaveState();
        }

        /// <summary>코스튬 분해 (제거)</summary>
        public void DisassembleCostume(string costumeId)
        {
            if (!_ownedCostumes.TryGetValue(costumeId, out var costume)) return;

            _equippedIds.Remove(costumeId);
            _ownedCostumes.Remove(costumeId);

            Debug.Log($"[CostumeManager] 코스튬 분해: {costume.DisplayName}");
            SaveState();
        }

        /// <summary>세트 완성 체크</summary>
        private void CheckSetComplete(string setId)
        {
            if (_completedSets.Contains(setId)) return;

            // 같은 세트의 보유 코스튬 수
            int count = _ownedCostumes.Values.Count(c => c.SetId == setId);

            // 세트 완성 기준: 3개 이상 (간단화)
            const int SET_THRESHOLD = 3;
            if (count >= SET_THRESHOLD)
            {
                _completedSets.Add(setId);
                EventBus<CostumeSetCompletedEvent>.Publish(new CostumeSetCompletedEvent
                {
                    SetId = setId,
                    TotalSets = _completedSets.Count
                });
                Debug.Log($"[CostumeManager] 세트 완성: {setId} (총 {_completedSets.Count}세트)");
            }
        }

        /// <summary>등급별 코스튬 목록</summary>
        public List<CostumeInstance> GetCostumesByGrade(string grade)
        {
            return _ownedCostumes.Values.Where(c => c.Grade == grade).ToList();
        }

        /// <summary>등급 한글 변환</summary>
        public static string GetGradeLabel(string grade) => grade switch
        {
            "Normal" => "일반",
            "Rare" => "희귀",
            "Epic" => "에픽",
            "Unique" => "유니크",
            "Legendary" => "전설",
            "Mythic" => "신화",
            _ => grade
        };

        /// <summary>등급 색상</summary>
        public static Color GetGradeColor(string grade) => grade switch
        {
            "Normal" => Color.white,
            "Rare" => new Color(0.3f, 0.5f, 1f),
            "Epic" => new Color(0.7f, 0.3f, 1f),
            "Unique" => new Color(1f, 0.4f, 0.4f),
            "Legendary" => new Color(1f, 0.8f, 0f),
            "Mythic" => new Color(1f, 0.2f, 0.2f),
            _ => Color.white
        };

        // ── 세이브/로드 ──

        private void LoadState()
        {
            var save = SaveManager.Instance?.CurrentData?.costume;
            if (save == null) return;

            _ownedCostumes.Clear();
            _equippedIds.Clear();
            _completedSets.Clear();

            if (save.owned != null)
            {
                foreach (var data in save.owned)
                {
                    var instance = new CostumeInstance
                    {
                        CostumeId = data.costumeId,
                        DisplayName = data.displayName,
                        Grade = data.grade,
                        SetId = data.setId,
                        IsEquipped = data.isEquipped
                    };
                    _ownedCostumes[data.costumeId] = instance;

                    if (data.isEquipped)
                        _equippedIds.Add(data.costumeId);
                }
            }

            if (save.completedSets != null)
            {
                foreach (var setId in save.completedSets)
                    _completedSets.Add(setId);
            }

            Debug.Log($"[CostumeManager] 로드 완료: {_ownedCostumes.Count}개 보유, {_equippedIds.Count}개 장착");
        }

        private void SaveState()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager?.CurrentData == null) return;

            var save = saveManager.CurrentData.costume;
            save.owned.Clear();
            save.equippedIds.Clear();
            save.completedSets.Clear();

            foreach (var kvp in _ownedCostumes)
            {
                var c = kvp.Value;
                save.owned.Add(new CostumeInstanceData
                {
                    costumeId = c.CostumeId,
                    displayName = c.DisplayName,
                    grade = c.Grade,
                    setId = c.SetId,
                    isEquipped = c.IsEquipped
                });
            }

            foreach (var id in _equippedIds)
                save.equippedIds.Add(id);

            foreach (var setId in _completedSets)
                save.completedSets.Add(setId);

            saveManager.Save();
        }
    }
}
