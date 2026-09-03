using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 아레나 라이벌 정의.
    /// </summary>
    [System.Serializable]
    public class ArenaRival
    {
        public string rivalId;
        public string displayName;
        [Tooltip("칭호 (예: 불꽃의 검성)")]
        public string title;
        [Tooltip("직업 ID (warrior/archer/mage)")]
        public string jobId;
        [Tooltip("전직 단계 (1~4)")]
        [Range(1, 4)]
        public int jobTier = 1;
        [Tooltip("이 라이벌이 출현하는 최소 ELO")]
        public int eloMin;
        [Tooltip("이 라이벌이 출현하는 최대 ELO")]
        public int eloMax = 9999;
        [Tooltip("플레이어 대비 스탯 배율 (0.85~1.15)")]
        [Range(0.85f, 1.15f)]
        public float statScale = 1f;
    }

    /// <summary>
    /// 아레나 라이벌 데이터 SO — 30명의 네임드 라이벌 정의.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaRivalData", menuName = "mkLike/Arena Rival Data")]
    public class ArenaRivalDataSO : ScriptableObject
    {
        [Header("라이벌 목록")]
        public ArenaRival[] rivals;

        /// <summary>
        /// 플레이어 ELO 범위 내 라이벌을 난이도별(쉬움/보통/어려움) 3명 반환.
        /// </summary>
        public ArenaRival[] GetMatchCandidates(int playerElo)
        {
            if (rivals == null || rivals.Length == 0) return System.Array.Empty<ArenaRival>();

            ArenaRival easy = null, normal = null, hard = null;
            float bestEasyDist = float.MaxValue;
            float bestNormalDist = float.MaxValue;
            float bestHardDist = float.MaxValue;

            for (int i = 0; i < rivals.Length; i++)
            {
                var r = rivals[i];

                // 쉬움: ELO - 150~200
                int easyTarget = playerElo - 175;
                float easyDist = Mathf.Abs(((r.eloMin + r.eloMax) / 2f) - easyTarget);
                if (easyDist < bestEasyDist)
                {
                    bestEasyDist = easyDist;
                    easy = r;
                }

                // 보통: ELO ± 50
                float normalDist = Mathf.Abs(((r.eloMin + r.eloMax) / 2f) - playerElo);
                if (normalDist < bestNormalDist)
                {
                    bestNormalDist = normalDist;
                    normal = r;
                }

                // 어려움: ELO + 150~200
                int hardTarget = playerElo + 175;
                float hardDist = Mathf.Abs(((r.eloMin + r.eloMax) / 2f) - hardTarget);
                if (hardDist < bestHardDist)
                {
                    bestHardDist = hardDist;
                    hard = r;
                }
            }

            // 중복 방지
            if (normal == easy) normal = FindAlternate(playerElo, easy, hard);
            if (hard == normal || hard == easy) hard = FindAlternate(playerElo + 175, easy, normal);

            return new[] { easy, normal, hard };
        }

        private ArenaRival FindAlternate(int targetElo, ArenaRival exclude1, ArenaRival exclude2)
        {
            ArenaRival best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < rivals.Length; i++)
            {
                var r = rivals[i];
                if (r == exclude1 || r == exclude2) continue;
                float dist = Mathf.Abs(((r.eloMin + r.eloMax) / 2f) - targetElo);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = r;
                }
            }
            return best ?? (rivals.Length > 0 ? rivals[0] : null);
        }

        /// <summary>ID로 라이벌 검색.</summary>
        public ArenaRival GetRival(string rivalId)
        {
            if (rivals == null) return null;
            for (int i = 0; i < rivals.Length; i++)
            {
                if (rivals[i].rivalId == rivalId) return rivals[i];
            }
            return null;
        }
    }
}
