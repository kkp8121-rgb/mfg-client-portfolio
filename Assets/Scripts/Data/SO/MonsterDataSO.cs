using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 몬스터 정적 데이터를 정의하는 ScriptableObject.
    /// 스탯, 보상, 비주얼 정보를 포함한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMonster", menuName = "MkLike/Data/Monster")]
    public class MonsterDataSO : ScriptableObject
    {
        [Tooltip("고유 식별자")]
        public string id;

        [Tooltip("화면에 표시되는 이름")]
        public string displayName;

        [Header("스탯")]
        public int hp = 50;
        public int atk = 5;
        public int def = 3;
        public float attackSpeed = 1.0f;
        public float moveSpeed = 2.0f;

        [Header("보상")]
        public long goldReward = 10;
        public long expReward = 5;

        [Header("비주얼")]
        public Sprite icon;
        public GameObject prefab;

        [Tooltip("SPUM 프리팹 경로 (Assets/Folder_Assets/SPUM/Prefab/AnimationSample/ 하위)")]
        public string spumPrefabPath = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab";

        [Tooltip("몬스터 색상 틴트")]
        public Color visualTint = Color.white;

        [Tooltip("몬스터 비주얼 스케일")]
        public float visualScale = 1.0f;

        [Tooltip("공격 애니메이션 타입 (0=Normal, 1=Bow, 2=Magic)")]
        public int attackAnimType = 0;

        [Tooltip("보스 몬스터 여부")]
        public bool isBoss = false;
    }
}
