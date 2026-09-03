using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 부스터 아이템 정의.
    /// 각 부스터의 타입, 배율, 지속시간을 설정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBooster", menuName = "MkLike/Data/Booster")]
    public class BoosterDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        public string description;
        public Sprite icon;

        [Header("효과")]
        public BoosterType boosterType;
        [Tooltip("배율 (2.0 = x2)")]
        public float multiplier = 2f;

        [Header("지속시간 (초)")]
        [Tooltip("부스터 지속 시간 (초). 1800=30분, 3600=1시간, 7200=2시간")]
        public float durationSeconds = 1800f;

        /// <summary>지속시간을 분 단위로 반환</summary>
        public int DurationMinutes => Mathf.RoundToInt(durationSeconds / 60f);
    }
}
