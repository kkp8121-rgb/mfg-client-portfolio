using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 아티팩트 정의.
    /// 등급별 고유 효과 + 세트 효과.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArtifact", menuName = "MkLike/Data/Artifact")]
    public class ArtifactDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        public string description;
        public Sprite icon;
        public string grade; // Common, Rare, Epic, Legendary, Mythic

        [Header("효과")]
        public StatType bonusStat;
        [Tooltip("퍼센트 보너스")]
        public float bonusPercent;
        [Tooltip("고정 보너스")]
        public float bonusFlat;

        [Header("세트")]
        [Tooltip("세트 ID (같은 ID끼리 세트 효과)")]
        public string setId;
        [Tooltip("세트 이름")]
        public string setName;
    }

    /// <summary>
    /// 아티팩트 세트 효과 정의.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArtifactSet", menuName = "MkLike/Data/Artifact Set")]
    public class ArtifactSetSO : ScriptableObject
    {
        public string setId;
        public string setName;

        [Header("2세트 효과")]
        public StatType twoSetStat;
        public float twoSetPercent;

        [Header("4세트 효과")]
        public StatType fourSetStat;
        public float fourSetPercent;

        /// <summary>장착된 세트 피스 수에 따른 보너스 계산</summary>
        public void GetBonuses(int pieceCount, out StatType stat2, out float percent2, out StatType stat4, out float percent4)
        {
            stat2 = twoSetStat;
            percent2 = pieceCount >= 2 ? twoSetPercent : 0f;
            stat4 = fourSetStat;
            percent4 = pieceCount >= 4 ? fourSetPercent : 0f;
        }
    }
}
