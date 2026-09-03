using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 이벤트 콘텐츠 데이터 SO.
    /// 한정 던전, 특별 보스, 탑 침공 등 이벤트 설정을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EventContent_New", menuName = "MkLike/Event Content Data")]
    public class EventContentDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string _eventId;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private EventContentType _eventType;
        [SerializeField] private Sprite _icon;

        [Header("기간")]
        [SerializeField] private int _durationDays = 7;

        [Header("보상")]
        [SerializeField] private CurrencyType _rewardCurrency;
        [SerializeField] private int _rewardAmount = 100;
        [SerializeField] private string _rewardCostumeId;

        [Header("입장 제한")]
        [SerializeField] private int _dailyEntryLimit = 3; // 0 = 무제한

        public string EventId => _eventId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public EventContentType EventType => _eventType;
        public Sprite Icon => _icon;
        public int DurationDays => _durationDays;
        public CurrencyType RewardCurrency => _rewardCurrency;
        public int RewardAmount => _rewardAmount;
        public string RewardCostumeId => _rewardCostumeId;
        public int DailyEntryLimit => _dailyEntryLimit;
    }
}
