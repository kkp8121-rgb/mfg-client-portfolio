using System;
using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Core
{
    /// <summary>
    /// 아이콘 ID-스프라이트 매핑 엔트리.
    /// </summary>
    [Serializable]
    public struct IconEntry
    {
        public string id;
        public Sprite sprite;
    }

    /// <summary>
    /// 장비 부위 열거. 아이콘 매핑 키로 사용.
    /// </summary>
    public enum EquipSlot
    {
        Weapon,
        Helmet,
        Armor,
        Gloves,
        Boots,
        Shield,
        Ring,
        Necklace,
        Earring
    }

    /// <summary>
    /// 중앙 아이콘 레지스트리. 스킬/아이템/장비/재화 아이콘을 ID로 조회한다.
    /// Inspector에서 직접 할당하거나, SO의 icon 필드를 사용한다.
    /// Resources 폴더 기반 자동 로딩으로 GUI Kit Dark Geo/스킬 아이콘 팩을 매핑한다.
    /// 아이콘이 없으면 fallback 스프라이트를 반환한다.
    /// </summary>
    public class IconRegistry : MonoBehaviour
    {
        public static IconRegistry Instance { get; private set; }

        [Header("스킬 아이콘")]
        [SerializeField] private IconEntry[] _skillIcons;

        [Header("아이템/장비 아이콘")]
        [SerializeField] private IconEntry[] _itemIcons;

        [Header("폴백")]
        [Tooltip("아이콘이 없을 때 반환할 기본 스프라이트")]
        [SerializeField] private Sprite _fallbackSprite;

        private readonly Dictionary<string, Sprite> _skillIconMap = new();
        private readonly Dictionary<string, Sprite> _itemIconMap = new();
        private readonly Dictionary<CurrencyType, Sprite> _currencyIconMap = new();
        private readonly Dictionary<EquipSlot, Sprite> _equipSlotIconMap = new();
        private readonly Dictionary<string, Sprite> _uiIconMap = new();

        // 직업별 스킬 아이콘 색상 매핑 (skill icon set free 폴더의 색상별 스프라이트시트)
        private static readonly Dictionary<string, string> JobColorMap = new()
        {
            { "warrior", "red" },
            { "knight", "blue" },
            { "warlord", "red" },
            { "titan", "red" },
            { "archer", "green" },
            { "scout", "green" },
            { "hawkeye", "green" },
            { "windwalker", "cyan" },
            { "mage", "purple" },
            { "sorcerer", "purple" },
            { "sage", "blue" },
            { "runemaster", "purple" },
            { "archmage", "purple" },
            { "dragonslayer", "orange" },
            { "stormbringer", "cyan" },
            { "stormcaller", "cyan" },
            { "dragonknight", "orange" },
            { "paladin", "yellow" },
            { "ancient", "white" },
        };

        // 재화 → GUI Kit The Stone 아이콘 매핑
        private static readonly Dictionary<CurrencyType, string> CurrencyIconPaths = new()
        {
            { CurrencyType.Gold, "Icons/Stone/icon_gold" },
            { CurrencyType.Ruby, "Icons/Stone/icon_ruby" },
            { CurrencyType.BlueDiamond, "Icons/Stone/icon_purplegem" },
            { CurrencyType.WeaponTicket, "Icons/Stone/icon_sword" },
            { CurrencyType.RuneFragment, "Icons/Stone/icon_energy" },
            { CurrencyType.StarCrystal, "Icons/Stone/icon_star" },
            { CurrencyType.PotentialStone, "Icons/Stone/icon_soulgem" },
            { CurrencyType.SuperPotentialStone, "Icons/Stone/icon_potion_purple" },
            { CurrencyType.ClimbToken, "Icons/Stone/icon_key" },
            { CurrencyType.HuntPoint, "Icons/Stone/icon_skull" },
            { CurrencyType.WeaponStone, "Icons/Stone/icon_hammer" },
        };

        // 장비 부위 → GUI Kit The Stone 아이콘 매핑
        private static readonly Dictionary<EquipSlot, string> EquipSlotIconPaths = new()
        {
            { EquipSlot.Weapon, "Icons/Stone/icon_sword" },
            { EquipSlot.Helmet, "Icons/Stone/icon_crown" },
            { EquipSlot.Armor, "Icons/Stone/icon_shield" },
            { EquipSlot.Gloves, "Icons/Stone/icon_hammer" },
            { EquipSlot.Boots, "Icons/Stone/icon_horseshoes" },
            { EquipSlot.Shield, "Icons/Stone/icon_shield" },
            { EquipSlot.Ring, "Icons/Stone/icon_treasure" },
            { EquipSlot.Necklace, "Icons/Stone/icon_gem" },
            { EquipSlot.Earring, "Icons/Stone/icon_star" },
        };

        // 공용 UI 아이콘 (GUI Kit The Stone)
        private static readonly Dictionary<string, string> UIIconPaths = new()
        {
            { "close", "Icons/Stone/btn_back" },
            { "setting", "Icons/Stone/icon_setting" },
            { "search", "Icons/Stone/btn_search" },
            { "store", "Icons/Stone/icon_shop" },
            { "gift", "Icons/Stone/icon_gift" },
            { "trophy", "Icons/Stone/icon_trophy" },
            { "home", "Icons/Stone/btn_home" },
            { "timer", "Icons/Stone/icon_timer" },
            { "message", "Icons/Stone/icon_letter" },
            { "lock", "Icons/Stone/icon_lock" },
            { "key", "Icons/Stone/icon_key" },
            { "play", "Icons/Stone/icon_energy" },
            { "pause", "Icons/Stone/icon_timer" },
            { "info", "Icons/Stone/icon_scroll" },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildMaps();
            LoadCurrencyIcons();
            LoadEquipSlotIcons();
            LoadUIIcons();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void BuildMaps()
        {
            if (_skillIcons != null)
            {
                for (int i = 0; i < _skillIcons.Length; i++)
                {
                    var entry = _skillIcons[i];
                    if (!string.IsNullOrEmpty(entry.id) && entry.sprite != null)
                        _skillIconMap[entry.id] = entry.sprite;
                }
            }

            if (_itemIcons != null)
            {
                for (int i = 0; i < _itemIcons.Length; i++)
                {
                    var entry = _itemIcons[i];
                    if (!string.IsNullOrEmpty(entry.id) && entry.sprite != null)
                        _itemIconMap[entry.id] = entry.sprite;
                }
            }
        }

        private void LoadCurrencyIcons()
        {
            foreach (var kvp in CurrencyIconPaths)
            {
                var sprite = Resources.Load<Sprite>(kvp.Value);
                if (sprite != null)
                    _currencyIconMap[kvp.Key] = sprite;
                else
                    _currencyIconMap[kvp.Key] = GenerateColorSprite(GetCurrencyColor(kvp.Key));
            }
        }

        private void LoadEquipSlotIcons()
        {
            foreach (var kvp in EquipSlotIconPaths)
            {
                var sprite = Resources.Load<Sprite>(kvp.Value);
                if (sprite != null)
                    _equipSlotIconMap[kvp.Key] = sprite;
                else
                    _equipSlotIconMap[kvp.Key] = GenerateColorSprite(GetEquipSlotColor(kvp.Key));
            }
        }

        private void LoadUIIcons()
        {
            foreach (var kvp in UIIconPaths)
            {
                var sprite = Resources.Load<Sprite>(kvp.Value);
                if (sprite != null)
                    _uiIconMap[kvp.Key] = sprite;
            }
        }

        private static Color GetCurrencyColor(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => new Color(1f, 0.84f, 0f),
                CurrencyType.Ruby => new Color(0.9f, 0.2f, 0.4f),
                CurrencyType.BlueDiamond => new Color(0.4f, 0.7f, 1f),
                CurrencyType.WeaponTicket => new Color(0.7f, 0.7f, 0.8f),
                CurrencyType.RuneFragment => new Color(0.6f, 0.3f, 0.9f),
                CurrencyType.StarCrystal => new Color(1f, 1f, 0.5f),
                CurrencyType.PotentialStone => new Color(0.3f, 0.6f, 1f),
                CurrencyType.SuperPotentialStone => new Color(0.2f, 0.9f, 0.5f),
                CurrencyType.ClimbToken => new Color(0.8f, 0.6f, 0.3f),
                CurrencyType.HuntPoint => new Color(0.8f, 0.2f, 0.2f),
                CurrencyType.WeaponStone => new Color(0.9f, 0.4f, 0.3f),
                _ => Color.white,
            };
        }

        private static Color GetEquipSlotColor(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Weapon => new Color(0.8f, 0.3f, 0.3f),
                EquipSlot.Helmet => new Color(0.6f, 0.6f, 0.8f),
                EquipSlot.Armor => new Color(0.5f, 0.5f, 0.7f),
                EquipSlot.Gloves => new Color(0.7f, 0.6f, 0.5f),
                EquipSlot.Boots => new Color(0.6f, 0.5f, 0.4f),
                EquipSlot.Shield => new Color(0.4f, 0.6f, 0.8f),
                EquipSlot.Ring => new Color(1f, 0.84f, 0f),
                EquipSlot.Necklace => new Color(0.9f, 0.3f, 0.5f),
                EquipSlot.Earring => new Color(0.8f, 0.8f, 1f),
                _ => Color.gray,
            };
        }

        /// <summary>
        /// 에셋 없이 단색 정사각 스프라이트를 생성한다 (fallback용).
        /// </summary>
        private static Sprite GenerateColorSprite(Color color, int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 스킬 ID로 아이콘을 조회한다. 없으면 직업 색상 기반 자동 매핑 시도 후 fallback 반환.
        /// </summary>
        public Sprite GetSkillIcon(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return _fallbackSprite;

            if (_skillIconMap.TryGetValue(skillId, out var sprite))
                return sprite;

            // 자동 매핑: Skill_{job}_{name} → 직업별 색상 스프라이트시트에서 로드
            var autoSprite = TryAutoLoadSkillIcon(skillId);
            if (autoSprite != null)
            {
                _skillIconMap[skillId] = autoSprite;
                return autoSprite;
            }

            return _fallbackSprite;
        }

        /// <summary>
        /// 아이템/장비 ID로 아이콘을 조회한다. 없으면 fallback 반환.
        /// </summary>
        public Sprite GetItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return _fallbackSprite;

            if (_itemIconMap.TryGetValue(itemId, out var sprite))
                return sprite;

            return _fallbackSprite;
        }

        /// <summary>
        /// 재화 타입으로 아이콘을 조회한다.
        /// </summary>
        public Sprite GetCurrencyIcon(CurrencyType type)
        {
            if (_currencyIconMap.TryGetValue(type, out var sprite))
                return sprite;
            return _fallbackSprite;
        }

        /// <summary>
        /// 장비 슬롯으로 기본 아이콘을 조회한다.
        /// </summary>
        public Sprite GetEquipSlotIcon(EquipSlot slot)
        {
            if (_equipSlotIconMap.TryGetValue(slot, out var sprite))
                return sprite;
            return _fallbackSprite;
        }

        /// <summary>
        /// UI 아이콘을 키 이름으로 조회한다 (close, setting, store 등).
        /// </summary>
        public Sprite GetUIIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
                return _fallbackSprite;
            if (_uiIconMap.TryGetValue(key, out var sprite))
                return sprite;
            return _fallbackSprite;
        }

        /// <summary>
        /// 폴백 스프라이트를 반환한다.
        /// </summary>
        public Sprite FallbackSprite => _fallbackSprite;

        /// <summary>
        /// 런타임에 스킬 아이콘을 등록한다 (SO 로딩 시 자동 등록용).
        /// </summary>
        public void RegisterSkillIcon(string skillId, Sprite sprite)
        {
            if (string.IsNullOrEmpty(skillId) || sprite == null) return;
            _skillIconMap[skillId] = sprite;
        }

        /// <summary>
        /// 런타임에 아이템 아이콘을 등록한다.
        /// </summary>
        public void RegisterItemIcon(string itemId, Sprite sprite)
        {
            if (string.IsNullOrEmpty(itemId) || sprite == null) return;
            _itemIconMap[itemId] = sprite;
        }

        /// <summary>
        /// 런타임에 재화 아이콘을 등록한다.
        /// </summary>
        public void RegisterCurrencyIcon(CurrencyType type, Sprite sprite)
        {
            if (sprite == null) return;
            _currencyIconMap[type] = sprite;
        }

        /// <summary>
        /// 스킬ID에서 직업명을 추출하여 색상별 스프라이트시트에서 로드를 시도한다.
        /// 패턴: "Skill_{job}_{name}" → job으로 색상 결정 → Resources/Icons/Skills/{color} 폴더에서 로드.
        /// </summary>
        private Sprite TryAutoLoadSkillIcon(string skillId)
        {
            // Skill_{job}_{skillname} 형태 파싱
            if (!skillId.StartsWith("Skill_", StringComparison.OrdinalIgnoreCase))
                return null;

            string remainder = skillId.Substring(6); // "Skill_" 이후
            int underscoreIdx = remainder.IndexOf('_');
            if (underscoreIdx <= 0)
                return null;

            string job = remainder.Substring(0, underscoreIdx).ToLowerInvariant();

            // 직업별 색상 결정
            if (!JobColorMap.TryGetValue(job, out string color))
                color = "white";

            // Resources/Icons/Skills/{skillId} 시도
            var sprite = Resources.Load<Sprite>($"Icons/Skills/{skillId}");
            if (sprite != null) return sprite;

            // Resources/Icons/Skills/{color}/{skillId} 시도
            sprite = Resources.Load<Sprite>($"Icons/Skills/{color}/{skillId}");
            if (sprite != null) return sprite;

            return null;
        }
    }
}
