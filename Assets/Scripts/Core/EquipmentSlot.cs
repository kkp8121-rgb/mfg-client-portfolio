namespace MkLike.Core
{
    /// <summary>
    /// 장비 슬롯 종류 (7부위 + 무기).
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon,        // 무기
        Helmet,        // 투구
        Top,           // 상의
        Gloves,        // 장갑
        Boots,         // 신발
        Ring,          // 반지
        Necklace,      // 목걸이
        FaceAccessory  // 얼굴장식
    }

    /// <summary>
    /// 잠재능력 등급.
    /// </summary>
    public enum PotentialGrade
    {
        None,
        Rare,
        Epic,
        Unique,
        Legendary,
        Mythic
    }

    /// <summary>
    /// 장비 등급.
    /// </summary>
    public enum ItemGrade
    {
        Normal,
        Rare,
        Epic,
        Unique,
        Legendary,
        Mythic
    }
}
