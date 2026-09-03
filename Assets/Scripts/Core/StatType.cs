namespace MkLike.Core
{
    /// <summary>
    /// 스탯 유형 열거형.
    /// 각인, 유물, 동료 등 다양한 시스템에서 스탯 보너스를 지정할 때 사용한다.
    /// </summary>
    public enum StatType
    {
        Atk,
        Def,
        MaxHp,
        CritRate,
        CritDamage,
        AttackSpeed,
        MoveSpeed,
        GoldBonus,
        ExpBonus,
        DodgeRate,
        AllStats,
        AbyssResistance,
        FinalDamage,
        DefensePenetration,
        BossDamage,          // 보스 추가 데미지 (0.3 = +30%)
        DamageTakenDecrease  // 피해 감소 (0.3 = -30%, 곱연산 체감, 최대 95%)
    }
}
