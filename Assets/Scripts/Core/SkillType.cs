namespace MkLike.Core
{
    /// <summary>
    /// 스킬 유형 열거형.
    /// Core 어셈블리에 위치하여 모든 어셈블리에서 참조 가능.
    /// </summary>
    public enum SkillType
    {
        Active,     // 기본공격 (쿨타임 없음)
        Passive,    // 습득 즉시 영구 적용
        Buff,       // 쿨타임마다 자동 시전, 일정 시간 유지
        Awakening   // 긴 쿨타임, 강력한 효과
    }

    /// <summary>
    /// 스킬 특수 효과 열거형.
    /// </summary>
    public enum SkillEffect
    {
        None,
        Knockback,
        Stun,
        Lifesteal,
        Revive,
        DefReduce,
        AllStatUp,
        Penetrate,
        MultiShot,
        Dodge,
        Burn,
        Freeze,
        Slow
    }
}
