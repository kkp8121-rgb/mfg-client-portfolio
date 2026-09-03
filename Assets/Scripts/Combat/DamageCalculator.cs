using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 데미지 계산 결과를 담는 구조체.
    /// </summary>
    public struct DamageResult
    {
        public int damage;
        public bool isCritical;
        public bool isPenetrating;
    }

    /// <summary>
    /// 전투 데미지 계산을 담당하는 정적 유틸리티 클래스.
    /// 공식 (메이플 키우기 참고 보강):
    ///   effectiveDef = targetDef × (1 - defensePenetration)     [곱연산 체감]
    ///   baseDmg = ATK × (100 / (100 + effectiveDef))
    ///   finalDmg = baseDmg × skill × crit × variance × (1 + FinalDamage) × (1 + BossDamage)
    ///   피격 측: receivedDmg = finalDmg × (1 - DamageTakenDecrease)
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 최종 데미지를 계산하여 정수값으로 반환한다.
        /// </summary>
        public static int Calculate(int attackerAtk, int targetDef, float skillMultiplier,
            float critRate, float critDmg = 1.5f, float defensePenetration = 0f,
            float finalDamage = 0f, float bossDamage = 0f, float targetDmgReduction = 0f)
        {
            DamageResult result = CalculateWithResult(attackerAtk, targetDef, skillMultiplier,
                critRate, critDmg, defensePenetration, finalDamage, bossDamage, targetDmgReduction);
            return result.damage;
        }

        /// <summary>
        /// 하위 호환용 오버로드 (기존 7인자 호출 지원).
        /// </summary>
        public static int Calculate(int attackerAtk, int targetDef, float skillMultiplier,
            float critRate, float critDmg, float defensePenetration, float finalDamage)
        {
            return Calculate(attackerAtk, targetDef, skillMultiplier, critRate, critDmg,
                defensePenetration, finalDamage, 0f, 0f);
        }

        /// <summary>
        /// 최종 데미지를 계산하여 DamageResult로 반환한다.
        /// </summary>
        /// <param name="attackerAtk">공격자의 공격력</param>
        /// <param name="targetDef">대상의 방어력</param>
        /// <param name="skillMultiplier">스킬 배율 (기본 공격은 1.0)</param>
        /// <param name="critRate">치명타 확률 (0.0 ~ 1.0)</param>
        /// <param name="critDmg">치명타 데미지 배율 (기본 1.5)</param>
        /// <param name="defensePenetration">방어력 관통 비율 (0.0~1.0, 곱연산 체감 적용 후 값)</param>
        /// <param name="finalDamage">최종 데미지 배율 (곱연산 적용 후 값, 0.3 = +30%)</param>
        /// <param name="bossDamage">보스 추가 데미지 (0.3 = +30%, 보스 상대 시만 전달)</param>
        /// <param name="targetDmgReduction">피격 대상 피해 감소 (0.3 = -30%, 곱연산 체감 적용 후 값)</param>
        public static DamageResult CalculateWithResult(int attackerAtk, int targetDef,
            float skillMultiplier, float critRate, float critDmg = 1.5f,
            float defensePenetration = 0f, float finalDamage = 0f,
            float bossDamage = 0f, float targetDmgReduction = 0f)
        {
            // 방어력 관통 적용 (곱연산 체감 — CombatStats에서 이미 계산됨)
            float effectiveDef = targetDef * (1f - Mathf.Clamp01(defensePenetration));

            // 방어력 보정된 기본 데미지
            float baseDmg = attackerAtk * (100f / (100f + effectiveDef));

            // 치명타 판정
            bool isCritical = Random.value < critRate;
            float critMultiplier = isCritical ? critDmg : 1.0f;

            // 랜덤 편차 (±5%)
            float variance = Random.Range(0.95f, 1.05f);

            // 최종 데미지 = baseDmg × 스킬 × 크리 × 편차 × (1 + FinalDamage) × (1 + BossDamage)
            float finalDmgMultiplier = 1f + finalDamage;
            float bossDmgMultiplier = 1f + bossDamage;
            float rawDmg = baseDmg * skillMultiplier * critMultiplier * variance * finalDmgMultiplier * bossDmgMultiplier;

            // 피격 대상 피해 감소 적용
            float dmgAfterReduction = rawDmg * (1f - Mathf.Clamp(targetDmgReduction, 0f, 0.95f));

            int finalDmg = Mathf.Max(1, Mathf.RoundToInt(dmgAfterReduction));

            return new DamageResult
            {
                damage = finalDmg,
                isCritical = isCritical,
                isPenetrating = defensePenetration > 0f
            };
        }

        /// <summary>
        /// 하위 호환용 오버로드 (기존 7인자 호출 지원).
        /// </summary>
        public static DamageResult CalculateWithResult(int attackerAtk, int targetDef,
            float skillMultiplier, float critRate, float critDmg,
            float defensePenetration, float finalDamage)
        {
            return CalculateWithResult(attackerAtk, targetDef, skillMultiplier, critRate, critDmg,
                defensePenetration, finalDamage, 0f, 0f);
        }
    }
}
