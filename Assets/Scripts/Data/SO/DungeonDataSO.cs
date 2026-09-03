using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 던전 종류.
    /// </summary>
    public enum DungeonType
    {
        Weapon,       // 무기 던전 (무기소환권 + 강화석)
        Experience,   // 경험치 던전 (시간 내 처치)
        Equipment,    // 장비 던전 (사냥 포인트)
        Climber,      // 등반자의 시련 (등반의 증표)
        Enhancement   // 강화 던전 (룬 조각 + 주문서)
    }

    /// <summary>
    /// 던전 정적 데이터를 정의하는 ScriptableObject.
    /// 입장 조건, 보상, 전투 설정 등을 포함한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Dungeon_", menuName = "mkLike/Dungeon Data")]
    public class DungeonDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("고유 식별자 (예: dungeon_weapon, dungeon_exp)")]
        public string id;

        [Tooltip("화면에 표시되는 이름")]
        public string displayName;

        [TextArea]
        public string description;

        public DungeonType dungeonType;

        [Tooltip("던전 아이콘")]
        public Sprite icon;

        [Header("입장 조건")]
        [Tooltip("입장에 필요한 열쇠 수")]
        public int requiredKeys = 1;

        [Tooltip("입장에 필요한 최소 층수")]
        public int requiredFloor = 1;

        [Tooltip("입장에 필요한 최소 전투력(CP). 0이면 제한 없음.")]
        public long requiredCp;

        [Header("보상")]
        [Tooltip("주 보상 재화 타입")]
        public CurrencyType mainRewardType;

        [Tooltip("기본 보상량")]
        public int baseRewardAmount;

        [Tooltip("부 보상 재화 타입 (없으면 Gold)")]
        public CurrencyType bonusRewardType;

        [Tooltip("부 보상량")]
        public int bonusRewardAmount;

        [Header("전투 설정")]
        [Tooltip("시간 제한 (초)")]
        public float timeLimit = 60f;

        [Tooltip("등장 몬스터 수")]
        public int monsterCount = 30;

        [Tooltip("난이도 배율")]
        public float difficultyMultiplier = 1f;

        [Header("던전 비주얼")]
        [Tooltip("몬스터 SPUM 프리팹 폴더 (Resources 경로). 비어있으면 현재 챕터 설정 사용")]
        public string monsterPrefabFolder;

        [Tooltip("몬스터 색상 틴트")]
        public Color monsterTint = Color.white;

        [Tooltip("배경색 하단")]
        public Color bgBottom = new Color(0.05f, 0.05f, 0.08f);

        [Tooltip("배경색 상단")]
        public Color bgTop = new Color(0.10f, 0.08f, 0.15f);
    }
}
