using UnityEngine;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 캐릭터의 JSON 디자인 ID를 저장하고, 런타임에 attackAnimType을 적용하는 컴포넌트.
    /// 스프라이트는 에디터 타임에 프리팹에 베이킹되므로, 런타임에서는 애니메이션 타입만 설정.
    /// 향후 CDN/Addressables 전환 시 런타임 스프라이트 로딩 추가 예정.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        [Tooltip("CharacterDesigns JSON의 id")]
        [SerializeField] private string designId;

        public string DesignId => designId;

        private void Start()
        {
            ApplyAnimationType();
        }

        public void SetDesignId(string id)
        {
            designId = id;
        }

        /// <summary>
        /// JSON 디자인에서 attackAnimType을 읽어 CharacterAnimBridge에 적용한다.
        /// </summary>
        private void ApplyAnimationType()
        {
            if (string.IsNullOrEmpty(designId)) return;

            var design = CharacterDesignLoader.GetDesign(designId);
            if (design == null) return;

            var animBridge = GetComponent<CharacterAnimBridge>();
            if (animBridge != null)
                animBridge.SetAttackType((AttackAnimType)design.attackAnimType);
        }
    }
}
