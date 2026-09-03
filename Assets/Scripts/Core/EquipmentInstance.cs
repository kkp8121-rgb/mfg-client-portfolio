namespace MkLike.Core
{
    /// <summary>
    /// 장비 인스턴스. 가챠에서 획득한 개별 장비 아이템.
    /// 강화(주문서/스타포스/잠재능력)는 슬롯에 귀속 — SlotEnhancementData 참조.
    /// Core 어셈블리에 위치 (SaveData에서 참조하므로).
    /// </summary>
    [System.Serializable]
    public class EquipmentInstance
    {
        /// <summary>고유 인스턴스 ID (GUID)</summary>
        public string instanceId;
        /// <summary>EquipmentDataSO.id 참조</summary>
        public string equipmentId;
        /// <summary>등급 (Normal~Mythic)</summary>
        public string grade;
        /// <summary>각성 단계 (0~5). 같은 장비 중복 겹치기로 상승.</summary>
        public int awakeningStars;

        /// <summary>최대 각성 단계</summary>
        public const int MAX_AWAKENING = 5;

        /// <summary>각성 스탯 배율. 각성당 기본 스탯 +10%.</summary>
        public float AwakeningMultiplier => 1f + awakeningStars * 0.1f;

        public EquipmentInstance() { }

        public EquipmentInstance(string equipmentId, string grade)
        {
            this.instanceId = System.Guid.NewGuid().ToString("N");
            this.equipmentId = equipmentId;
            this.grade = grade;
        }
    }
}
