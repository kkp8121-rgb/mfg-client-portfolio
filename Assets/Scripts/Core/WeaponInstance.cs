namespace MkLike.Core
{
    /// <summary>
    /// 무기 인스턴스. 가챠(뽑기)로 획득한 개별 무기 아이템.
    /// Core 어셈블리에 위치 (SaveData에서 참조하므로).
    /// </summary>
    // JSON 직렬화용 public 필드
    [System.Serializable]
    public class WeaponInstance
    {
        /// <summary>고유 인스턴스 ID (GUID)</summary>
        public string instanceId;
        /// <summary>WeaponDataSO.id 참조</summary>
        public string weaponId;
        /// <summary>등급 (Normal~Ancient)</summary>
        public string grade;
        /// <summary>티어 (0=레거시, 1=T1 최고, 4=T4 보통)</summary>
        public int tier;
        /// <summary>레벨 (무기강화석으로 상승)</summary>
        public int level = 1;
        /// <summary>각성 단계 (0~5, 같은 무기 합성)</summary>
        public int awakeningStars;
        /// <summary>장착 여부</summary>
        public bool isEquipped;

        public WeaponInstance() { }

        public WeaponInstance(string weaponId, string grade, int tier = 0)
        {
            this.instanceId = System.Guid.NewGuid().ToString("N");
            this.weaponId = weaponId;
            this.grade = grade;
            this.tier = tier;
            this.level = 1;
            this.awakeningStars = 0;
            this.isEquipped = false;
        }
    }
}
