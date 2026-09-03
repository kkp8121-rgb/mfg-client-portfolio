using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 엘리트 소환 레벨별 설정 데이터.
    /// 각 레벨에서 해금되는 장비 등급, 소환 비용, 레벨업 비용을 정의한다.
    /// </summary>
    [System.Serializable]
    public class EliteSummonLevelData
    {
        [Tooltip("이 레벨 도달에 필요한 누적 HuntPoint")]
        public int requiredTotalCost;

        [Tooltip("엘리트 1회 소환 비용 (HuntPoint)")]
        public int summonCost;

        [Tooltip("해금 장비 최고 등급 (Normal, Rare, Epic, Unique, Legendary, Mythic)")]
        public string maxUnlockedGrade = "Normal";

        [Tooltip("드롭 등급별 가중치 (Normal, Rare, Epic, Unique, Legendary, Mythic 순)")]
        public float[] gradeWeights = { 60f, 30f, 10f, 0f, 0f, 0f };

        [Tooltip("엘리트 HP 배율 (일반 몬스터 대비)")]
        public float hpMultiplier = 3f;

        [Tooltip("엘리트 ATK 배율")]
        public float atkMultiplier = 1.5f;
    }

    /// <summary>
    /// 엘리트 소환 시스템 설정 SO.
    /// HuntPoint를 소비하여 정예 몬스터를 소환하고, 처치 시 장비를 드롭한다.
    /// 소환 레벨이 올라갈수록 고등급 장비가 해금된다.
    /// </summary>
    [CreateAssetMenu(fileName = "EliteSummon", menuName = "mkLike/Elite Summon")]
    public class EliteSummonSO : ScriptableObject
    {
        [Header("소환 레벨 테이블")]
        [Tooltip("레벨 1부터 시작. 인덱스 0 = Lv.1")]
        [SerializeField] private EliteSummonLevelData[] _levels;

        [Header("기본 설정")]
        [Tooltip("최소 소환 쿨다운 (초)")]
        [SerializeField] private float _summonCooldown = 3f;

        [Tooltip("엘리트 이동속도 배율")]
        [SerializeField] private float _moveSpeedMultiplier = 0.7f;

        [Tooltip("엘리트 공격속도 배율")]
        [SerializeField] private float _attackSpeedMultiplier = 0.8f;

        public EliteSummonLevelData[] Levels => _levels;
        public float SummonCooldown => _summonCooldown;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float AttackSpeedMultiplier => _attackSpeedMultiplier;
        public int MaxLevel => _levels != null ? _levels.Length : 1;

        /// <summary>레벨 데이터 반환 (1-based index)</summary>
        public EliteSummonLevelData GetLevelData(int level)
        {
            if (_levels == null || _levels.Length == 0) return null;
            int idx = Mathf.Clamp(level - 1, 0, _levels.Length - 1);
            return _levels[idx];
        }

        /// <summary>해당 레벨의 소환 비용</summary>
        public int GetSummonCost(int level)
        {
            var data = GetLevelData(level);
            return data?.summonCost ?? 100;
        }

        /// <summary>다음 레벨업에 필요한 누적 비용</summary>
        public int GetLevelUpCost(int level)
        {
            if (_levels == null || level >= _levels.Length) return -1;
            return _levels[level].requiredTotalCost;
        }

        /// <summary>등급별 가중치 배열 반환</summary>
        public float[] GetGradeWeights(int level)
        {
            var data = GetLevelData(level);
            return data?.gradeWeights;
        }

        /// <summary>기본값 세팅 (에디터에서 생성 시 호출)</summary>
        public void SetupDefaults()
        {
            _summonCooldown = 3f;
            _moveSpeedMultiplier = 0.7f;
            _attackSpeedMultiplier = 0.8f;

            _levels = new EliteSummonLevelData[10];
            string[] grades = { "Normal", "Normal", "Rare", "Rare", "Epic", "Epic", "Unique", "Unique", "Legendary", "Legendary" };
            int[] costs = { 0, 200, 500, 1000, 2000, 4000, 7000, 12000, 20000, 32000 };
            int[] summonCosts = { 70, 90, 120, 160, 220, 300, 400, 550, 750, 1000 };

            for (int i = 0; i < 10; i++)
            {
                _levels[i] = new EliteSummonLevelData
                {
                    requiredTotalCost = costs[i],
                    summonCost = summonCosts[i],
                    maxUnlockedGrade = grades[i],
                    hpMultiplier = 3f + i * 0.5f,
                    atkMultiplier = 1.5f + i * 0.2f,
                    gradeWeights = BuildDefaultWeights(i)
                };
            }
        }

        private static float[] BuildDefaultWeights(int levelIndex)
        {
            // 레벨이 올라갈수록 고등급 비율 증가
            return levelIndex switch
            {
                0 => new[] { 70f, 25f, 5f, 0f, 0f, 0f },
                1 => new[] { 60f, 30f, 10f, 0f, 0f, 0f },
                2 => new[] { 40f, 35f, 20f, 5f, 0f, 0f },
                3 => new[] { 30f, 35f, 25f, 10f, 0f, 0f },
                4 => new[] { 20f, 30f, 30f, 15f, 5f, 0f },
                5 => new[] { 15f, 25f, 30f, 20f, 10f, 0f },
                6 => new[] { 10f, 20f, 30f, 25f, 13f, 2f },
                7 => new[] { 5f, 15f, 30f, 28f, 17f, 5f },
                8 => new[] { 3f, 10f, 25f, 30f, 22f, 10f },
                9 => new[] { 2f, 8f, 20f, 30f, 25f, 15f },
                _ => new[] { 50f, 30f, 15f, 4f, 0.9f, 0.1f }
            };
        }
    }
}
