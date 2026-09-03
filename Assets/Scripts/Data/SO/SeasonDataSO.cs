using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 시즌 데이터 ScriptableObject.
    /// 시즌별 기간, 최대 레벨, BXP 요구량 등을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Season_", menuName = "mkLike/Season Data")]
    public class SeasonDataSO : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _theme;
        [SerializeField] private int _durationWeeks = 6;
        [SerializeField] private int _maxLevel = 50;
        [SerializeField] private int _bxpPerLevel = 1000;

        /// <summary>시즌 고유 ID</summary>
        public string Id => _id;

        /// <summary>표시 이름</summary>
        public string DisplayName => _displayName;

        /// <summary>시즌 테마</summary>
        public string Theme => _theme;

        /// <summary>시즌 기간 (주)</summary>
        public int DurationWeeks => _durationWeeks;

        /// <summary>배틀패스 최대 레벨</summary>
        public int MaxLevel => _maxLevel;

        /// <summary>레벨당 필요 BXP (Battle Experience Point)</summary>
        public int BxpPerLevel => _bxpPerLevel;

        /// <summary>총 필요 BXP (최대 레벨 달성까지)</summary>
        public int TotalBxpRequired => _maxLevel * _bxpPerLevel;
    }
}
