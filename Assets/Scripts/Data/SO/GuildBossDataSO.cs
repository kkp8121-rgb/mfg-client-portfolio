using UnityEngine;

namespace MkLike.Data
{
    /// <summary>보스 스킬 정의.</summary>
    [System.Serializable]
    public class BossSkillEntry
    {
        public string skillId;
        public string displayName;
        [Tooltip("데미지 배율 (ATK 기준)")]
        public float damageMultiplier = 1f;
        [Tooltip("쿨타임 (초)")]
        public float cooldown = 5f;
        [Tooltip("사용 가능 최소 페이즈 (1~3)")]
        public int minPhase = 1;
        [Tooltip("전체 공격 여부")]
        public bool isAoE;
        [Tooltip("스턴 지속 시간 (초, 0이면 스턴 없음)")]
        public float stunDuration;
        [Tooltip("특수 효과 설명")]
        public string description;
    }

    /// <summary>보스 페이즈 데이터.</summary>
    [System.Serializable]
    public class BossPhaseData
    {
        [Tooltip("이 페이즈 진입 HP 비율 (1.0→0.7→0.4)")]
        public float hpThreshold = 1f;
        public string phaseName;
        [Tooltip("공격력 배율")]
        public float atkMultiplier = 1f;
        [Tooltip("방어력 배율")]
        public float defMultiplier = 1f;
        [Tooltip("분노 게이지 증가 배율")]
        public float rageGainMultiplier = 1f;
    }

    /// <summary>분노 게이지 설정.</summary>
    [System.Serializable]
    public class RageGaugeConfig
    {
        public float maxRage = 100f;
        [Tooltip("일반 피격 시 분노 증가")]
        public float gainPerHit = 2f;
        [Tooltip("크리티컬 피격 시 분노 증가")]
        public float gainPerCrit = 5f;
        [Tooltip("비공격 턴 분노 감소")]
        public float decayPerIdleTurn = 3f;
        [Tooltip("전멸기 데미지 배율")]
        public float ultimateDamageMultiplier = 5f;
    }

    /// <summary>
    /// 길드 보스 데이터 SO.
    /// 3페이즈 보스 전투, 분노 게이지, 보스 스킬 6종 정의.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildBossData", menuName = "mkLike/Guild Boss Data")]
    public class GuildBossDataSO : ScriptableObject
    {
        [Header("보스 기본 정보")]
        public string bossName = "고대 수호자";
        public string bossDescription = "탑의 심층에 봉인된 수호자";

        [Header("전투 설정")]
        [Tooltip("전투 제한 시간 (초)")]
        public float battleTimeLimit = 120f;
        [Tooltip("턴 간격 (초)")]
        public float turnInterval = 0.5f;
        [Tooltip("보스 기본 ATK (길드 레벨 × 배율)")]
        public float bossAtkPerLevel = 500f;
        [Tooltip("보스 기본 DEF (길드 레벨 × 배율)")]
        public float bossDefPerLevel = 200f;

        [Header("페이즈 (3단계)")]
        public BossPhaseData[] phases = new[]
        {
            new BossPhaseData { hpThreshold = 1.0f, phaseName = "각성", atkMultiplier = 1f, defMultiplier = 1f, rageGainMultiplier = 1f },
            new BossPhaseData { hpThreshold = 0.7f, phaseName = "분노", atkMultiplier = 1.3f, defMultiplier = 1f, rageGainMultiplier = 1.3f },
            new BossPhaseData { hpThreshold = 0.4f, phaseName = "폭주", atkMultiplier = 1.8f, defMultiplier = 0.5f, rageGainMultiplier = 1.6f }
        };

        [Header("보스 스킬 (6종)")]
        public BossSkillEntry[] skills = new[]
        {
            new BossSkillEntry { skillId = "slash", displayName = "일격", damageMultiplier = 2f, cooldown = 5f, minPhase = 1 },
            new BossSkillEntry { skillId = "roar", displayName = "포효", damageMultiplier = 0.5f, cooldown = 8f, minPhase = 2, isAoE = true, stunDuration = 3f },
            new BossSkillEntry { skillId = "guard", displayName = "방어 강화", damageMultiplier = 0f, cooldown = 15f, minPhase = 1, description = "5초간 받는 데미지 -50%" },
            new BossSkillEntry { skillId = "wave", displayName = "광역 파동", damageMultiplier = 1.5f, cooldown = 12f, minPhase = 2, isAoE = true },
            new BossSkillEntry { skillId = "selfdestruct", displayName = "자폭 준비", damageMultiplier = 0f, cooldown = 999f, minPhase = 3, description = "30초 카운트다운" },
            new BossSkillEntry { skillId = "ultimate", displayName = "전멸기", damageMultiplier = 5f, cooldown = 999f, minPhase = 1, isAoE = true, description = "분노 MAX 시 발동" }
        };

        [Header("분노 게이지")]
        public RageGaugeConfig rageConfig = new();

        [Header("자폭 카운트다운")]
        [Tooltip("3페이즈 자폭 카운트다운 시간 (초)")]
        public float selfDestructCountdown = 30f;

        /// <summary>현재 HP 비율에 해당하는 페이즈 인덱스 반환 (0~2).</summary>
        public int GetPhaseIndex(float hpPercent)
        {
            if (phases == null || phases.Length == 0) return 0;
            for (int i = phases.Length - 1; i >= 0; i--)
            {
                if (hpPercent <= phases[i].hpThreshold)
                    return i;
            }
            return 0;
        }
    }
}
