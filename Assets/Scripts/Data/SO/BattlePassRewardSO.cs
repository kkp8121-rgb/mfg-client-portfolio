using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 배틀패스 보상 ScriptableObject.
    /// 각 레벨별 무료/프리미엄 보상을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BPReward_", menuName = "mkLike/BattlePass Reward")]
    public class BattlePassRewardSO : ScriptableObject
    {
        [SerializeField] private int _level;
        [SerializeField] private CurrencyType _freeRewardType;
        [SerializeField] private int _freeRewardAmount;
        [SerializeField] private CurrencyType _premiumRewardType;
        [SerializeField] private int _premiumRewardAmount;
        [SerializeField] private bool _isMilestone;

        /// <summary>보상 레벨 (1~50)</summary>
        public int Level => _level;

        /// <summary>무료 트랙 보상 재화 종류</summary>
        public CurrencyType FreeRewardType => _freeRewardType;

        /// <summary>무료 트랙 보상 수량</summary>
        public int FreeRewardAmount => _freeRewardAmount;

        /// <summary>프리미엄 트랙 보상 재화 종류</summary>
        public CurrencyType PremiumRewardType => _premiumRewardType;

        /// <summary>프리미엄 트랙 보상 수량</summary>
        public int PremiumRewardAmount => _premiumRewardAmount;

        /// <summary>마일스톤 보상 여부 (10, 20, 30, 40, 50 레벨)</summary>
        public bool IsMilestone => _isMilestone;
    }
}
