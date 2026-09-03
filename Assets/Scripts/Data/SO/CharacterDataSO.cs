using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 캐릭터 정적 데이터를 정의하는 ScriptableObject.
    /// 기본 스탯, 레벨당 성장치, 전직 정보 등을 포함한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "MkLike/Data/Character")]
    public class CharacterDataSO : ScriptableObject
    {
        [Tooltip("고유 식별자 (예: warrior, archer, mage)")]
        public string id;

        [Tooltip("화면에 표시되는 이름 (예: 전사, 궁수, 마법사)")]
        public string displayName;

        [TextArea]
        public string description;

        [Header("기본 스탯")]
        public int baseHp = 150;
        public int baseAtk = 12;
        public int baseDef = 8;
        public float baseCritRate = 0.05f;
        public float attackSpeed = 1.0f;
        public float moveSpeed = 3.0f;

        [Header("레벨당 성장")]
        public int hpPerLevel = 15;
        public int atkPerLevel = 2;
        public int defPerLevel = 2;

        [Header("전직 정보")]
        [Tooltip("0: 초기, 1: 1차, 2: 2차, 3: 3차")]
        public int jobTier = 0;

        [Header("비주얼")]
        [Tooltip("SPUM 프리팹 경로 (Assets/Folder_Assets/SPUM/Prefab/AnimationSample/ 하위)")]
        public string spumPrefabPath;

        [Tooltip("공격/스킬 애니메이션 타입 (0=Normal, 1=Bow, 2=Magic)")]
        public int attackAnimType = 0;

        [Tooltip("캐릭터 색상 틴트 (기본: 흰색)")]
        public Color visualTint = Color.white;
    }
}
