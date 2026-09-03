namespace MkLike.Core
{
    /// <summary>
    /// 매일 접속 시 부여되는 축복 유형.
    /// DailyBlessingSystem에서 가중 랜덤으로 선택된다.
    /// </summary>
    public enum BlessingType
    {
        /// <summary>행운의 축복: 드롭률(GoldBonus) +50%</summary>
        Luck,
        /// <summary>전사의 축복: 공격력 +30%</summary>
        Warrior,
        /// <summary>수호의 축복: 방어력 +30%</summary>
        Guardian,
        /// <summary>탐험가의 축복: 경험치(ExpBonus) +50%</summary>
        Explorer,
        /// <summary>축복받은 자: 전체 스탯 +20%</summary>
        Blessed
    }
}
