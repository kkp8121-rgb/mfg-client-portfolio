namespace MkLike.Core
{
    /// <summary>
    /// 재화 종류 열거형 (10종).
    /// Core 어셈블리에 위치하여 모든 어셈블리에서 참조 가능.
    /// </summary>
    public enum CurrencyType
    {
        Gold,               // 기본 소프트 재화
        Ruby,               // 프리미엄 재화 (무한 충전, 가챠용)
        BlueDiamond,        // 프리미엄 재화 (유료 전용)
        WeaponTicket,       // 무기 소환권
        RuneFragment,       // 룬 조각 (주문서 강화)
        StarCrystal,        // 별의 결정 (성급 강화)
        PotentialStone,     // 잠재의 수정
        SuperPotentialStone,// 상급 잠재의 수정
        ClimbToken,         // 등반의 증표 (등반자의 힘)
        HuntPoint,          // 사냥 포인트 (정예 소환)
        WeaponStone,        // 무기 강화석
        QuickHuntTicket,    // 소탕권
        ArenaTicket,        // 아레나 입장권
    }
}
