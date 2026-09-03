using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 장착 장비에 따라 SPUM 캐릭터의 파츠 색상/활성 상태를 변경한다.
    /// Player 오브젝트에 추가하여 사용.
    ///
    /// 매핑:
    ///   Helmet → _hairList[3] (헬멧 슬롯)
    ///   Top → _armorList[0~2] (상체 갑옷)
    ///   Gloves → _clothList[1,2] (좌우 팔)
    ///   Boots → _pantList[0,1] (좌우 다리)
    ///   Weapon → _weaponList[0] (우측 무기)
    ///   FaceAccessory → _hairList[1] (얼굴 장식)
    ///   Ring/Necklace → 비주얼 없음
    /// </summary>
    public class EquipmentVisualMapper : MonoBehaviour
    {
        private static readonly Color UNEQUIPPED_COLOR = Color.white;

        // SPUM_SpriteList 캐싱
        private List<SpriteRenderer> _armorRenderers;
        private List<SpriteRenderer> _clothRenderers;
        private List<SpriteRenderer> _weaponRenderers;
        private List<SpriteRenderer> _hairRenderers;
        private List<SpriteRenderer> _pantRenderers;
        private bool _initialized;

        // 현재 장착 등급 추적 (이벤트 기반)
        private readonly Dictionary<EquipmentSlot, string> _equippedGrades = new();

        private void OnEnable()
        {
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
        }

        private void OnDisable()
        {
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
        }

        private void Start()
        {
            Invoke(nameof(DelayedInit), 0.5f);
        }

        private void DelayedInit()
        {
            CacheSpumRenderers();
            RefreshAllSlots();
        }

        private void CacheSpumRenderers()
        {
            if (_initialized) return;

            var spriteListComp = FindSpumSpriteList();
            if (spriteListComp == null)
            {
                Debug.LogWarning("[EquipmentVisualMapper] SPUM_SpriteList를 찾을 수 없음");
                return;
            }

            _armorRenderers = GetRendererList(spriteListComp, "_armorList");
            _clothRenderers = GetRendererList(spriteListComp, "_clothList");
            _weaponRenderers = GetRendererList(spriteListComp, "_weaponList");
            _hairRenderers = GetRendererList(spriteListComp, "_hairList");
            _pantRenderers = GetRendererList(spriteListComp, "_pantList");

            _initialized = true;
            Debug.Log("[EquipmentVisualMapper] SPUM 렌더러 캐싱 완료");
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (evt.IsEquipped)
                _equippedGrades[evt.Slot] = evt.Grade;
            else
                _equippedGrades.Remove(evt.Slot);

            if (!_initialized) CacheSpumRenderers();
            RefreshAllSlots();
        }

        private void RefreshAllSlots()
        {
            if (!_initialized) return;

            // Helmet → _hairList[3]
            ApplySlotVisual(_hairRenderers, 3, 3, GetGrade(EquipmentSlot.Helmet));

            // Top → _armorList[0,1,2]
            ApplySlotVisual(_armorRenderers, 0, 2, GetGrade(EquipmentSlot.Top));

            // Gloves → _clothList[1,2]
            ApplySlotVisual(_clothRenderers, 1, 2, GetGrade(EquipmentSlot.Gloves));

            // Boots → _pantList[0,1]
            ApplySlotVisual(_pantRenderers, 0, 1, GetGrade(EquipmentSlot.Boots));

            // Weapon → _weaponList[0]
            ApplySlotVisual(_weaponRenderers, 0, 0, GetGrade(EquipmentSlot.Weapon));

            // FaceAccessory → _hairList[1]
            ApplySlotVisual(_hairRenderers, 1, 1, GetGrade(EquipmentSlot.FaceAccessory));
        }

        private string GetGrade(EquipmentSlot slot)
        {
            return _equippedGrades.TryGetValue(slot, out var grade) ? grade : null;
        }

        private void ApplySlotVisual(List<SpriteRenderer> renderers, int startIdx, int endIdx, string grade)
        {
            if (renderers == null) return;

            Color color = grade != null ? GetGradeColor(grade) : UNEQUIPPED_COLOR;

            for (int i = startIdx; i <= endIdx && i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].color = color;
            }
        }

        private static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "Normal" => new Color(0.85f, 0.85f, 0.85f, 1f),
                "Rare" => new Color(0.5f, 0.7f, 1f, 1f),
                "Epic" => new Color(0.7f, 0.4f, 1f, 1f),
                "Unique" => new Color(1f, 0.85f, 0.3f, 1f),
                "Legendary" => new Color(1f, 0.55f, 0.1f, 1f),
                "Mythic" => new Color(1f, 0.3f, 0.4f, 1f),
                _ => Color.white
            };
        }

        private Component FindSpumSpriteList()
        {
            foreach (var comp in GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "SPUM_SpriteList")
                    return comp;
            }
            return null;
        }

        private static List<SpriteRenderer> GetRendererList(Component spriteList, string fieldName)
        {
            var field = spriteList.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return null;
            return field.GetValue(spriteList) as List<SpriteRenderer>;
        }
    }
}
