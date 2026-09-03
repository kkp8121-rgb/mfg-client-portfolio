using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 퀘스트 데이터 ScriptableObject.
    /// 메인/일일/주간 퀘스트의 조건과 보상을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "mkLike/Quest Data")]
    public class QuestDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public QuestType questType;

        [Header("조건")]
        public QuestCondition condition;
        public int requiredAmount;

        [Header("보상")]
        public CurrencyType rewardType;
        public int rewardAmount;

        [Header("보너스 보상 (선택)")]
        public CurrencyType bonusRewardType;
        public int bonusRewardAmount;

        [Header("가이드 퀘스트 체인 (Guide 타입 전용)")]
        [Tooltip("완료 후 활성화될 다음 가이드 퀘스트 ID")]
        public string nextGuideQuestId;
        [Tooltip("가이드 퀘스트 체인 내 순서 (0부터 시작)")]
        public int guideChainIndex;

        [Header("순환 퀘스트 (Cycling 타입 전용)")]
        [Tooltip("순환 순서 (0부터 시작, 마지막 다음은 0으로 돌아감)")]
        public int cyclingOrder;
    }
}
