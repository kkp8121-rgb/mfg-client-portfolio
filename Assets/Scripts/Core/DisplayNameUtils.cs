namespace MkLike.Core
{
    /// <summary>
    /// 재화/등급 등의 영문 내부값을 한국어 표시명으로 변환하는 유틸리티.
    /// Core 어셈블리에 위치하여 모든 어셈블리에서 공통 사용 가능.
    /// </summary>
    public static class DisplayNameUtils
    {
        /// <summary>
        /// CurrencyType을 한국어 표시 이름으로 변환한다.
        /// </summary>
        public static string GetCurrencyDisplayName(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => "골드",
                CurrencyType.Ruby => "루비",
                CurrencyType.BlueDiamond => "블루 다이아",
                CurrencyType.WeaponTicket => "무기 소환권",
                CurrencyType.RuneFragment => "룬 조각",
                CurrencyType.StarCrystal => "별의 결정",
                CurrencyType.PotentialStone => "잠재의 수정",
                CurrencyType.SuperPotentialStone => "상급 잠재석",
                CurrencyType.ClimbToken => "등반의 증표",
                CurrencyType.HuntPoint => "사냥 포인트",
                CurrencyType.WeaponStone => "무기 강화석",
                _ => type.ToString()
            };
        }

        /// <summary>
        /// 영문 등급명을 한국어로 변환한다.
        /// </summary>
        public static string GetGradeDisplayName(string grade)
        {
            return grade switch
            {
                "Normal" => "일반",
                "Rare" => "희귀",
                "Epic" => "에픽",
                "Unique" => "유니크",
                "Legendary" => "전설",
                "Mythic" => "신화",
                _ => grade
            };
        }
    }
}
