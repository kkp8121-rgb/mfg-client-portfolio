using System.Collections.Generic;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// 재화 획득처 정보. CurrencyType → 획득처 목록 매핑.
    /// CurrencyShortagePopup에서 "바로가기" 버튼을 생성할 때 사용한다.
    /// </summary>
    public struct CurrencySource
    {
        public string ContentName;
        public string Description;
        public string PanelAction;

        public CurrencySource(string contentName, string description, string panelAction)
        {
            ContentName = contentName;
            Description = description;
            PanelAction = panelAction;
        }
    }

    /// <summary>
    /// 재화별 획득처 매핑 정적 클래스.
    /// </summary>
    public static class CurrencySourceMap
    {
        private static readonly Dictionary<CurrencyType, List<CurrencySource>> _sources = new()
        {
            {
                CurrencyType.Gold, new List<CurrencySource>
                {
                    new("사냥", "몬스터 처치 시 획득", "combat"),
                    new("골드 던전", "골드 던전 입장", "dungeon_gold"),
                    new("상점", "루비로 골드 구매", "shop")
                }
            },
            {
                CurrencyType.Ruby, new List<CurrencySource>
                {
                    new("업적", "업적 완료 보상", "achievement"),
                    new("일일 퀘스트", "일일 퀘스트 보상", "quest_daily"),
                    new("배틀패스", "배틀패스 보상", "battlepass")
                }
            },
            {
                CurrencyType.BlueDiamond, new List<CurrencySource>
                {
                    new("상점", "유료 구매", "shop"),
                    new("배틀패스", "프리미엄 배틀패스 보상", "battlepass")
                }
            },
            {
                CurrencyType.WeaponTicket, new List<CurrencySource>
                {
                    new("퀘스트", "퀘스트 보상", "quest"),
                    new("배틀패스", "배틀패스 보상", "battlepass"),
                    new("상점", "루비로 구매", "shop")
                }
            },
            {
                CurrencyType.RuneFragment, new List<CurrencySource>
                {
                    new("사냥", "몬스터 드롭", "combat"),
                    new("던전", "던전 보상", "dungeon")
                }
            },
            {
                CurrencyType.StarCrystal, new List<CurrencySource>
                {
                    new("던전", "성급 던전", "dungeon_star"),
                    new("상점", "루비로 구매", "shop")
                }
            },
            {
                CurrencyType.PotentialStone, new List<CurrencySource>
                {
                    new("던전", "잠재능력 던전", "dungeon_potential"),
                    new("상점", "루비로 구매", "shop")
                }
            },
            {
                CurrencyType.SuperPotentialStone, new List<CurrencySource>
                {
                    new("탑", "탑 보상", "tower"),
                    new("상점", "루비로 구매", "shop")
                }
            },
            {
                CurrencyType.ClimbToken, new List<CurrencySource>
                {
                    new("환생", "프레스티지 보상", "tower"),
                    new("업적", "업적 완료 보상", "achievement")
                }
            },
            {
                CurrencyType.HuntPoint, new List<CurrencySource>
                {
                    new("사냥", "몬스터 처치 시 획득", "combat"),
                    new("퀘스트", "퀘스트 보상", "quest")
                }
            },
            {
                CurrencyType.WeaponStone, new List<CurrencySource>
                {
                    new("던전", "무기 던전", "dungeon_weapon"),
                    new("상점", "루비로 구매", "shop")
                }
            },
        };

        /// <summary>
        /// 재화의 획득처 목록을 반환한다. 최대 maxCount개.
        /// </summary>
        public static List<CurrencySource> GetSources(CurrencyType type, int maxCount = 3)
        {
            if (!_sources.TryGetValue(type, out var list))
                return new List<CurrencySource>();

            if (list.Count <= maxCount)
                return list;

            return list.GetRange(0, maxCount);
        }

        /// <summary>
        /// 재화의 한국어 표시명을 반환한다.
        /// </summary>
        public static string GetDisplayName(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => "골드",
                CurrencyType.Ruby => "루비",
                CurrencyType.BlueDiamond => "블루 다이아몬드",
                CurrencyType.WeaponTicket => "무기 소환권",
                CurrencyType.RuneFragment => "룬 조각",
                CurrencyType.StarCrystal => "별의 결정",
                CurrencyType.PotentialStone => "잠재의 수정",
                CurrencyType.SuperPotentialStone => "상급 잠재의 수정",
                CurrencyType.ClimbToken => "등반의 증표",
                CurrencyType.HuntPoint => "사냥 포인트",
                CurrencyType.WeaponStone => "무기 강화석",
                _ => type.ToString()
            };
        }
    }
}
