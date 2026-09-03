using UnityEngine;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 장비/무기 장착 시 캐릭터 주변 글로우 링 + 펀치스케일 연출.
    /// EquipmentChangedEvent, WeaponChangedEvent 구독.
    /// 플레이어 캐릭터에 부착한다.
    /// </summary>
    public class EquipFlashFeedback : MonoBehaviour
    {
        [SerializeField] private float _ringRadius = 1.2f;
        [SerializeField] private float _punchScale = 0.15f;
        [SerializeField] private float _punchDuration = 0.2f;

        private static readonly Color EquipGlowColor = new Color(0.4f, 0.8f, 1f);
        private static readonly Color WeaponGlowColor = new Color(1f, 0.7f, 0.2f);

        private SpriteRenderer[] _spriteRenderers;
        private Tween _flashTween;

        private void Awake()
        {
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnEnable()
        {
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Subscribe(OnWeaponChanged);
        }

        private void OnDisable()
        {
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Unsubscribe(OnWeaponChanged);
        }

        private void OnEquipmentChanged(EquipmentChangedEvent e)
        {
            if (!e.IsEquipped) return;
            PlayFlash(EquipGlowColor);
        }

        private void OnWeaponChanged(WeaponChangedEvent e)
        {
            if (!e.IsEquipped) return;
            PlayFlash(WeaponGlowColor);
        }

        private void PlayFlash(Color glowColor)
        {
            // SFX
            AudioManager.Instance?.PlaySfx(SfxType.Equip);

            // 1. 글로우 링 VFX
            SkillVfx.SpawnRing(transform.position, _ringRadius, glowColor, 0.4f);

            // 2. 캐릭터 펀치스케일
            transform.DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0f)
                .SetLink(gameObject);

            // 3. 스프라이트 색상 플래시 (백색 → 원래 색)
            FlashSprites(glowColor);
        }

        private void FlashSprites(Color flashColor)
        {
            _flashTween?.Kill();

            if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;

            // 밝은 색으로 즉시 변경
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] == null) continue;
                _spriteRenderers[i].color = flashColor;
            }

            // 0.3초에 걸쳐 원래 색(흰색)으로 복원
            float progress = 0f;
            _flashTween = DOTween.To(() => progress, x =>
            {
                progress = x;
                Color current = Color.Lerp(flashColor, Color.white, x);
                for (int i = 0; i < _spriteRenderers.Length; i++)
                {
                    if (_spriteRenderers[i] == null) continue;
                    _spriteRenderers[i].color = current;
                }
            }, 1f, 0.3f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _flashTween?.Kill();
            transform.DOKill();
        }
    }
}
