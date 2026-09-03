using System;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// SPUM 캐릭터 부위별 디자인 데이터.
    /// JSON으로 직렬화되어 관리된다 (향후 CSV/CDN 전환 대비).
    /// 스프라이트 경로는 SPUM 리소스 기준: "Items/{category}/{filename}" 또는 "Packages/{pkg}/{category}/{filename}"
    /// </summary>
    [Serializable]
    public class CharacterDesignData
    {
        [Tooltip("고유 식별자")]
        public string id;

        [Tooltip("표시 이름")]
        public string displayName;

        [Header("종족/바디")]
        [Tooltip("종족 경로 (예: 0_Human/Human_1)")]
        public string species = "0_Human/Human_1";

        [Tooltip("눈 경로 (예: 0_Human/Eye/Eye7)")]
        public string eye = "0_Human/Eye/Eye7";

        [Header("장비 - 스프라이트 경로 (Items/ 하위)")]
        [Tooltip("머리카락 (예: 0_Hair/Hair_3)")]
        public string hair = "";

        [Tooltip("수염 (예: 1_FaceHair/FaceHair_1)")]
        public string faceHair = "";

        [Tooltip("투구 (예: 4_Helmet/Helmet_1)")]
        public string helmet = "";

        [Tooltip("상의 (예: 2_Cloth/Cloth_1) - 멀티스프라이트")]
        public string cloth = "";

        [Tooltip("하의 (예: 3_Pant/Foot_1) - 멀티스프라이트")]
        public string pants = "";

        [Tooltip("갑옷 (예: 5_Armor/Armor_1) - 멀티스프라이트")]
        public string armor = "";

        [Tooltip("등 장비 (예: 7_Back/Back_1)")]
        public string back = "";

        [Tooltip("오른손 무기 (예: 6_Weapons/Sword_1)")]
        public string weaponRight = "";

        [Tooltip("왼손 무기/방패 (예: 6_Weapons/Shield_1)")]
        public string weaponLeft = "";

        [Header("색상 (hex)")]
        [Tooltip("머리카락 색상 (#RRGGBB)")]
        public string hairColor = "#FFFFFF";

        [Tooltip("눈 색상 (#RRGGBB)")]
        public string eyeColor = "#FFFFFF";

        [Header("애니메이션")]
        [Tooltip("공격 타입 (0=Normal, 1=Bow, 2=Magic)")]
        public int attackAnimType = 0;

        [Header("비주얼 보정")]
        [Tooltip("전체 스케일")]
        public float scale = 1.0f;

        [Tooltip("색상 틴트 (#RRGGBB, 기본 흰색)")]
        public string tintColor = "#FFFFFF";
    }

    /// <summary>
    /// 여러 캐릭터 디자인을 담는 컨테이너 (JSON 배열 래퍼).
    /// </summary>
    [Serializable]
    public class CharacterDesignCollection
    {
        public CharacterDesignData[] designs;
    }
}
