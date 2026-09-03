using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// ScriptableObject 기반 정적 데이터를 Resources 폴더에서 로드하여 관리하는 매니저.
    /// 싱글톤 패턴으로 어디서든 접근 가능하며, DontDestroyOnLoad로 씬 전환 시에도 유지된다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        // 로드된 데이터 캐시
        private Dictionary<string, CharacterDataSO> _characterData;
        private Dictionary<string, MonsterDataSO> _monsterData;
        private Dictionary<string, StageDataSO> _stageData;
        private Dictionary<string, SkillDataSO> _skillData;
        private Dictionary<string, EquipmentDataSO> _equipmentData;
        private Dictionary<string, WeaponDataSO> _weaponData;
        private Dictionary<string, DungeonDataSO> _dungeonData;
        private Dictionary<string, QuestDataSO> _questData;
        // _relicData: 2026-04-20 유물 시스템 완전 제거

        /// <summary>
        /// 모든 데이터가 로드되었는지 여부.
        /// </summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        /// <summary>
        /// Resources 폴더에서 모든 ScriptableObject 데이터를 로드하여 딕셔너리에 캐시한다.
        /// </summary>
        public void Initialize()
        {
            LoadCharacterData();
            LoadMonsterData();
            LoadStageData();
            LoadSkillData();
            LoadEquipmentData();
            LoadWeaponData();
            LoadDungeonData();
            LoadQuestData();
            // LoadRelicData: 2026-04-20 유물 시스템 완전 제거

            IsInitialized = true;
            Debug.Log("[DataManager] 데이터 초기화 완료.");
        }

        /// <summary>
        /// 캐릭터 데이터를 로드한다.
        /// </summary>
        private void LoadCharacterData()
        {
            _characterData = new Dictionary<string, CharacterDataSO>();
            var characters = Resources.LoadAll<CharacterDataSO>("Data/Characters");
            foreach (var data in characters)
            {
                if (string.IsNullOrEmpty(data.id))
                {
                    Debug.LogWarning($"[DataManager] CharacterDataSO의 id가 비어있습니다: {data.name}");
                    continue;
                }

                if (!_characterData.TryAdd(data.id, data))
                {
                    Debug.LogWarning($"[DataManager] 중복된 캐릭터 ID: {data.id}");
                }
            }

            Debug.Log($"[DataManager] 캐릭터 데이터 {_characterData.Count}개 로드 완료.");
        }

        /// <summary>
        /// 몬스터 데이터를 로드한다.
        /// </summary>
        private void LoadMonsterData()
        {
            _monsterData = new Dictionary<string, MonsterDataSO>();
            var monsters = Resources.LoadAll<MonsterDataSO>("Data/Monsters");
            foreach (var data in monsters)
            {
                if (string.IsNullOrEmpty(data.id))
                {
                    Debug.LogWarning($"[DataManager] MonsterDataSO의 id가 비어있습니다: {data.name}");
                    continue;
                }

                if (!_monsterData.TryAdd(data.id, data))
                {
                    Debug.LogWarning($"[DataManager] 중복된 몬스터 ID: {data.id}");
                }
            }

            Debug.Log($"[DataManager] 몬스터 데이터 {_monsterData.Count}개 로드 완료.");
        }

        /// <summary>
        /// 스테이지 데이터를 로드한다.
        /// </summary>
        private void LoadStageData()
        {
            _stageData = new Dictionary<string, StageDataSO>();
            var stages = Resources.LoadAll<StageDataSO>("Data/Stages");
            foreach (var data in stages)
            {
                string key = $"{data.chapterId}-{data.stageIndex}";
                if (!_stageData.TryAdd(key, data))
                {
                    Debug.LogWarning($"[DataManager] 중복된 스테이지: {key}");
                }
            }

            Debug.Log($"[DataManager] 스테이지 데이터 {_stageData.Count}개 로드 완료.");
        }

        /// <summary>
        /// 스킬 데이터를 로드한다.
        /// </summary>
        private void LoadSkillData()
        {
            _skillData = new Dictionary<string, SkillDataSO>();
            var skills = Resources.LoadAll<SkillDataSO>("Data/Skills");
            foreach (var data in skills)
            {
                if (string.IsNullOrEmpty(data.id))
                {
                    Debug.LogWarning($"[DataManager] SkillDataSO의 id가 비어있습니다: {data.name}");
                    continue;
                }

                if (!_skillData.TryAdd(data.id, data))
                {
                    Debug.LogWarning($"[DataManager] 중복된 스킬 ID: {data.id}");
                }
            }

            Debug.Log($"[DataManager] 스킬 데이터 {_skillData.Count}개 로드 완료.");
        }

        private void LoadEquipmentData()
        {
            _equipmentData = new Dictionary<string, EquipmentDataSO>();
            var items = Resources.LoadAll<EquipmentDataSO>("Data/Equipment");
            foreach (var data in items)
            {
                if (string.IsNullOrEmpty(data.id)) continue;
                _equipmentData.TryAdd(data.id, data);
            }
            Debug.Log($"[DataManager] 장비 데이터 {_equipmentData.Count}개 로드 완료.");
        }

        private void LoadWeaponData()
        {
            _weaponData = new Dictionary<string, WeaponDataSO>();
            var items = Resources.LoadAll<WeaponDataSO>("Data/Weapon");
            foreach (var data in items)
            {
                if (string.IsNullOrEmpty(data.id)) continue;
                _weaponData.TryAdd(data.id, data);
            }
            Debug.Log($"[DataManager] 무기 데이터 {_weaponData.Count}개 로드 완료.");
        }

        private void LoadDungeonData()
        {
            _dungeonData = new Dictionary<string, DungeonDataSO>();
            var items = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");
            foreach (var data in items)
            {
                if (string.IsNullOrEmpty(data.id)) continue;
                _dungeonData.TryAdd(data.id, data);
            }
            Debug.Log($"[DataManager] 던전 데이터 {_dungeonData.Count}개 로드 완료.");
        }

        private void LoadQuestData()
        {
            _questData = new Dictionary<string, QuestDataSO>();
            var items = Resources.LoadAll<QuestDataSO>("Data/Quest");
            foreach (var data in items)
            {
                if (string.IsNullOrEmpty(data.id)) continue;
                _questData.TryAdd(data.id, data);
            }
            Debug.Log($"[DataManager] 퀘스트 데이터 {_questData.Count}개 로드 완료.");
        }

        // LoadRelicData: 2026-04-20 유물 시스템 완전 제거

        /// <summary>
        /// ID로 캐릭터 데이터를 조회한다.
        /// </summary>
        /// <param name="id">캐릭터 고유 ID</param>
        /// <returns>해당 캐릭터 데이터, 없으면 null</returns>
        public CharacterDataSO GetCharacterData(string id)
        {
            return _characterData.GetValueOrDefault(id);
        }

        /// <summary>
        /// ID로 몬스터 데이터를 조회한다.
        /// </summary>
        /// <param name="id">몬스터 고유 ID</param>
        /// <returns>해당 몬스터 데이터, 없으면 null</returns>
        public MonsterDataSO GetMonsterData(string id)
        {
            return _monsterData.GetValueOrDefault(id);
        }

        /// <summary>
        /// 챕터/스테이지 인덱스로 스테이지 데이터를 조회한다.
        /// </summary>
        public StageDataSO GetStageData(int chapter, int stageIndex)
        {
            string key = $"{chapter}-{stageIndex}";
            return _stageData.GetValueOrDefault(key);
        }

        /// <summary>
        /// ID로 스킬 데이터를 조회한다.
        /// </summary>
        /// <param name="id">스킬 고유 ID</param>
        /// <returns>해당 스킬 데이터, 없으면 null</returns>
        public SkillDataSO GetSkillData(string id)
        {
            return _skillData.GetValueOrDefault(id);
        }

        public EquipmentDataSO GetEquipmentData(string id)
        {
            return _equipmentData.GetValueOrDefault(id);
        }

        public WeaponDataSO GetWeaponData(string id)
        {
            return _weaponData.GetValueOrDefault(id);
        }

        public DungeonDataSO GetDungeonData(string id)
        {
            return _dungeonData.GetValueOrDefault(id);
        }

        public QuestDataSO GetQuestData(string id)
        {
            return _questData.GetValueOrDefault(id);
        }

        // GetRelicData / GetAllRelicData: 2026-04-20 유물 시스템 완전 제거

        /// <summary>
        /// 특정 타입의 모든 데이터를 반환한다.
        /// </summary>
        public IReadOnlyDictionary<string, EquipmentDataSO> GetAllEquipmentData() => _equipmentData;
        public IReadOnlyDictionary<string, WeaponDataSO> GetAllWeaponData() => _weaponData;
        public IReadOnlyDictionary<string, DungeonDataSO> GetAllDungeonData() => _dungeonData;
        public IReadOnlyDictionary<string, QuestDataSO> GetAllQuestData() => _questData;

    }
}
