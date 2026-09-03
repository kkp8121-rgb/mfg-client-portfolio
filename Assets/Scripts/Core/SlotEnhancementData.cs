namespace MkLike.Core
{
    /// <summary>
    /// 슬롯 강화 데이터. 장비가 아닌 "슬롯"을 강화하므로 장비 교체해도 강화 유지.
    /// </summary>
    [System.Serializable]
    public class SlotEnhancementData
    {
        /// <summary>주문서 강화 횟수 (0~10)</summary>
        public int scrollLevel;
        /// <summary>성급 ��화 단계 (0~25)</summary>
        public int starForce;
        /// <summary>잠재능력 등급</summary>
        public PotentialGrade potentialGrade = PotentialGrade.None;
        /// <summary>잠재능력 옵션 (최대 3줄)</summary>
        public System.Collections.Generic.List<string> potentialOptions = new();
        /// <summary>보조 잠재능력 등급 (12성 이상 개방)</summary>
        public PotentialGrade subPotentialGrade = PotentialGrade.None;
        /// <summary>보조 잠재능력 옵션 (최대 3줄)</summary>
        public System.Collections.Generic.List<string> subPotentialOptions = new();
    }
}
