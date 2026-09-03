using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI 테마 변경 이벤트. 테마 스프라이트가 교체될 때 발행된다.
    /// </summary>
    public struct UIThemeChangedEvent : IEvent { }

    /// <summary>
    /// 색상 팔레트 정의. Dark Geo 기반 어두운 배경 + 네온 악센트.
    /// </summary>
    [System.Serializable]
    public class UITheme
    {
        public Color panelBackground = new Color(0.10f, 0.10f, 0.18f, 1f);   // #1a1a2e
        public Color panelBorder = new Color(0.06f, 0.20f, 0.38f, 1f);       // #0f3460
        public Color buttonNormal = new Color(0.33f, 0.20f, 0.51f, 1f);      // #533483
        public Color buttonHighlight = new Color(0.43f, 0.30f, 0.61f, 1f);
        public Color buttonPressed = new Color(0.23f, 0.14f, 0.38f, 1f);
        public Color textPrimary = new Color(0.88f, 0.88f, 0.88f, 1f);       // #e0e0e0
        public Color textSecondary = new Color(0.50f, 0.50f, 0.50f, 1f);     // #808080
        public Color accentGold = new Color(1f, 0.84f, 0f, 1f);              // #ffd700
        public Color accentRed = new Color(0.91f, 0.27f, 0.38f, 1f);         // #e94560
        public Color accentGreen = new Color(0.30f, 0.78f, 0.47f, 1f);
        public Color accentBlue = new Color(0.29f, 0.56f, 0.85f, 1f);        // #4a90d9

        public Color gradeNormal = new Color(0.50f, 0.50f, 0.50f, 1f);       // #808080
        public Color gradeRare = new Color(0.29f, 0.56f, 0.85f, 1f);         // #4a90d9
        public Color gradeEpic = new Color(0.66f, 0.33f, 0.97f, 1f);         // #a855f7
        public Color gradeUnique = new Color(1f, 0.40f, 0.40f, 1f);
        public Color gradeLegendary = new Color(1f, 0.84f, 0f, 1f);          // #ffd700
        public Color gradeMythic = new Color(1f, 0.27f, 0.27f, 1f);          // #ff4444
    }

    /// <summary>
    /// UI 테마 스프라이트 레지스트리 + 색상 팔레트 중앙 관리.
    /// </summary>
    public class UIThemeManager : MonoBehaviour
    {
        public static UIThemeManager Instance { get; private set; }

        [Header("색상 테마")]
        [SerializeField] private UITheme _theme = new UITheme();

        [Header("버튼")]
        [SerializeField] private Sprite _buttonNormalSprite;
        [SerializeField] private Sprite _buttonHover;
        [SerializeField] private Sprite _buttonPressed;
        [SerializeField] private Sprite _buttonDisabled;

        [Header("패널/프레임")]
        [SerializeField] private Sprite _panelBackground;
        [SerializeField] private Sprite _frameBackground;
        [SerializeField] private Sprite _headerBackground;

        [Header("슬라이더")]
        [SerializeField] private Sprite _sliderFill;
        [SerializeField] private Sprite _sliderHandle;
        [SerializeField] private Sprite _sliderBackground;

        [Header("탭")]
        [SerializeField] private Sprite _tabNormal;
        [SerializeField] private Sprite _tabSelected;

        [Header("공용 버튼")]
        [SerializeField] private Sprite _closeButton;
        [SerializeField] private Sprite _backButton;
        [SerializeField] private Sprite _confirmButton;

        public UITheme Theme => _theme;

        public Sprite ButtonNormal => _buttonNormalSprite;
        public Sprite ButtonHover => _buttonHover;
        public Sprite ButtonPressed => _buttonPressed;
        public Sprite ButtonDisabled => _buttonDisabled;
        public Sprite PanelBackground => _panelBackground;
        public Sprite FrameBackground => _frameBackground;
        public Sprite HeaderBackground => _headerBackground;
        public Sprite SliderFill => _sliderFill;
        public Sprite SliderHandle => _sliderHandle;
        public Sprite SliderBackground => _sliderBackground;
        public Sprite TabNormal => _tabNormal;
        public Sprite TabSelected => _tabSelected;
        public Sprite CloseButton => _closeButton;
        public Sprite BackButton => _backButton;
        public Sprite ConfirmButton => _confirmButton;

        private static readonly Dictionary<string, System.Func<UITheme, Color>> GradeColorMap = new()
        {
            { "Normal", t => t.gradeNormal },
            { "Rare", t => t.gradeRare },
            { "Epic", t => t.gradeEpic },
            { "Unique", t => t.gradeUnique },
            { "Legendary", t => t.gradeLegendary },
            { "Mythic", t => t.gradeMythic },
        };

        // GUI Kit The Stone — Resources 경로 매핑
        private static readonly string GuiKitBase = "UI/Stone";

        private static readonly Dictionary<string, string> SpritePathMap = new()
        {
            { "button_normal", "Button/btn_normal" },
            { "button_hover", "Button/btn_highlight" },
            { "button_pressed", "Button/btn_highlight" },
            { "button_disabled", "Button/btn_normal" },
            { "panel_bg", "Frame/panel_bg" },
            { "frame_bg", "Frame/frame_brown" },
            { "header_bg", "Frame/label_brown" },
            { "slider_fill", "Gage/gage_orange" },
            { "slider_bg", "Gage/gage_bg" },
            { "tab_normal", "Tab/tab_normal" },
            { "tab_selected", "Tab/tab_selected" },
            { "close_btn", "Icon/btn_close" },
            { "back_btn", "Icon/btn_close" },
            { "confirm_btn", "Button/btn_confirm" },
            { "item_frame_n", "Skill/frame_silver" },
            { "item_frame_s", "Skill/frame_yellow" },
            { "popup_bg", "Popup/title_bar_00" },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AutoLoadSprites();
        }

        /// <summary>
        /// 등급 문자열로 테마 등급 색상을 반환한다.
        /// </summary>
        public Color GetGradeColor(string grade)
        {
            if (grade != null && GradeColorMap.TryGetValue(grade, out var getter))
                return getter(_theme);
            return _theme.gradeNormal;
        }

        /// <summary>
        /// Image에 Stone Kit 스프라이트를 적용한다.
        /// 스프라이트가 존재하면 color를 White로 설정하여 원본 스프라이트가 보이게 한다.
        /// 스프라이트가 null이면 fallback 색상을 적용한다.
        /// </summary>
        public static void ApplySpriteOrColor(Image img, Sprite sprite, Color fallbackColor, Image.Type imageType = Image.Type.Sliced)
        {
            if (img == null) return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = imageType;
                img.color = Color.white;
            }
            else
            {
                img.color = fallbackColor;
            }
        }

        /// <summary>
        /// Image에 패널 배경 스프라이트를 적용한다 (편의 메서드).
        /// </summary>
        public void ApplyPanelBackground(Image img)
        {
            ApplySpriteOrColor(img, _panelBackground, _theme.panelBackground);
        }

        /// <summary>
        /// Image에 프레임 배경 스프라이트를 적용한다 (편의 메서드).
        /// </summary>
        public void ApplyFrameBackground(Image img)
        {
            ApplySpriteOrColor(img, _frameBackground, _theme.panelBorder);
        }

        /// <summary>
        /// Image에 버튼 스프라이트를 적용한다 (편의 메서드).
        /// </summary>
        public void ApplyButtonSprite(Image img)
        {
            ApplySpriteOrColor(img, _buttonNormalSprite, _theme.buttonNormal);
        }

        /// <summary>
        /// Button에 스프라이트 + SpriteState를 적용한다.
        /// </summary>
        public void ApplyButtonFull(Button btn)
        {
            if (btn == null) return;
            var btnImg = btn.GetComponent<Image>();
            ApplyButtonSprite(btnImg);

            var ss = new SpriteState();
            if (_buttonHover != null) ss.highlightedSprite = _buttonHover;
            if (_buttonPressed != null) ss.pressedSprite = _buttonPressed;
            if (_buttonDisabled != null) ss.disabledSprite = _buttonDisabled;
            btn.spriteState = ss;
            btn.transition = Selectable.Transition.SpriteSwap;
        }

        /// <summary>
        /// Image에 닫기 버튼 스프라이트를 적용한다.
        /// </summary>
        public void ApplyCloseButton(Image img)
        {
            ApplySpriteOrColor(img, _closeButton, _theme.accentRed, Image.Type.Simple);
            if (img != null) img.preserveAspect = true;
        }

        /// <summary>
        /// Image에 확인 버튼 스프라이트를 적용한다.
        /// </summary>
        public void ApplyConfirmButton(Image img)
        {
            ApplySpriteOrColor(img, _confirmButton, _theme.accentGreen);
        }

        /// <summary>
        /// Slider에 Stone Kit 게이지 스프라이트를 적용한다.
        /// </summary>
        public void ApplySlider(Slider slider, Color? fillTint = null)
        {
            if (slider == null) return;
            var bgImg = slider.GetComponentInChildren<Image>();
            if (bgImg != null)
                ApplySpriteOrColor(bgImg, _sliderBackground, new Color(0.15f, 0.15f, 0.2f));

            var fillRect = slider.fillRect;
            if (fillRect != null)
            {
                var fillImg = fillRect.GetComponent<Image>();
                if (fillImg != null)
                {
                    if (_sliderFill != null)
                    {
                        fillImg.sprite = _sliderFill;
                        fillImg.type = Image.Type.Sliced;
                        fillImg.color = fillTint ?? Color.white;
                    }
                }
            }
        }

        /// <summary>
        /// root 하위의 Image/TMP_Text에 테마 색상을 적용한다.
        /// 스프라이트가 있는 Image는 Color.white를 유지한다.
        /// </summary>
        public void ApplyTheme(GameObject root)
        {
            if (root == null) return;

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                // 이미 Stone Kit 스프라이트가 적용된 Image는 건드리지 않음
                if (img.sprite != null && !IsDefaultSprite(img.sprite))
                    continue;

                if (img.CompareTag("ThemePanel"))
                    img.color = _theme.panelBackground;
                else if (img.CompareTag("ThemeBorder"))
                    img.color = _theme.panelBorder;
                else if (img.CompareTag("ThemeButton"))
                    img.color = _theme.buttonNormal;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var txt = texts[i];
                if (txt.CompareTag("ThemeTextPrimary"))
                    txt.color = _theme.textPrimary;
                else if (txt.CompareTag("ThemeTextSecondary"))
                    txt.color = _theme.textSecondary;
            }
        }

        /// <summary>
        /// root 하위의 모든 Image에 패널 배경색, 모든 텍스트에 기본 텍스트색을 일괄 적용한다.
        /// 스프라이트가 있는 Image는 Color.white를 유지한다.
        /// </summary>
        public void ApplyThemeForced(GameObject root)
        {
            if (root == null) return;

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                // Stone Kit 스프라이트가 적용된 Image는 건드리지 않음
                if (img.sprite != null && !IsDefaultSprite(img.sprite))
                    continue;

                if (img.GetComponent<Button>() != null)
                    img.color = _theme.buttonNormal;
                else
                    img.color = _theme.panelBackground;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].color = _theme.textPrimary;
        }

        /// <summary>
        /// Unity 기본 스프라이트(White)인지 판별한다.
        /// </summary>
        private static bool IsDefaultSprite(Sprite sprite)
        {
            return sprite != null && (sprite.name == "UISprite" || sprite.name == "Background" || sprite.name == "UIMask" || sprite.name == "Knob");
        }

        public (Sprite normal, Sprite hover, Sprite pressed, Sprite disabled) GetButtonSprites()
        {
            return (_buttonNormalSprite, _buttonHover, _buttonPressed, _buttonDisabled);
        }

        public Sprite GetPanelSprite()
        {
            return _panelBackground;
        }

        public (Sprite normal, Sprite selected) GetTabSprites()
        {
            return (_tabNormal, _tabSelected);
        }

        public (Sprite fill, Sprite handle, Sprite background) GetSliderSprites()
        {
            return (_sliderFill, _sliderHandle, _sliderBackground);
        }

        /// <summary>
        /// GUI Kit Dark Geo 에셋에서 자동으로 스프라이트를 로드한다.
        /// SerializeField가 null인 슬롯만 Resources에서 채운다.
        /// </summary>
        private void AutoLoadSprites()
        {
            if (_buttonNormalSprite == null)
                _buttonNormalSprite = LoadGuiKitSprite("button_normal");
            if (_buttonHover == null)
                _buttonHover = LoadGuiKitSprite("button_hover");
            if (_buttonPressed == null)
                _buttonPressed = LoadGuiKitSprite("button_pressed");
            if (_buttonDisabled == null)
                _buttonDisabled = LoadGuiKitSprite("button_disabled");
            if (_panelBackground == null)
                _panelBackground = LoadGuiKitSprite("panel_bg");
            if (_frameBackground == null)
                _frameBackground = LoadGuiKitSprite("frame_bg");
            if (_headerBackground == null)
                _headerBackground = LoadGuiKitSprite("header_bg");
            if (_sliderFill == null)
                _sliderFill = LoadGuiKitSprite("slider_fill");
            if (_sliderBackground == null)
                _sliderBackground = LoadGuiKitSprite("slider_bg");
            if (_tabNormal == null)
                _tabNormal = LoadGuiKitSprite("tab_normal");
            if (_tabSelected == null)
                _tabSelected = LoadGuiKitSprite("tab_selected");
            if (_closeButton == null)
                _closeButton = LoadGuiKitSprite("close_btn");
            if (_backButton == null)
                _backButton = LoadGuiKitSprite("back_btn");
            if (_confirmButton == null)
                _confirmButton = LoadGuiKitSprite("confirm_btn");
        }

        private Sprite LoadGuiKitSprite(string key)
        {
            if (!SpritePathMap.TryGetValue(key, out string subPath))
                return null;

            string fullPath = $"{GuiKitBase}/{subPath}";
            var sprite = Resources.Load<Sprite>(fullPath);
            if (sprite != null) return sprite;

            // Resources 로드 실패 시 테마 색상 기반 프로시져럴 스프라이트 생성
            Color color = key switch
            {
                "button_normal" => _theme.buttonNormal,
                "button_hover" => _theme.buttonHighlight,
                "button_pressed" => _theme.buttonPressed,
                "button_disabled" => new Color(0.2f, 0.2f, 0.2f, 0.5f),
                "panel_bg" => _theme.panelBackground,
                "frame_bg" => _theme.panelBorder,
                "header_bg" => new Color(_theme.panelBackground.r * 0.8f, _theme.panelBackground.g * 0.8f, _theme.panelBackground.b * 0.8f),
                "slider_fill" => _theme.accentGold,
                "slider_bg" => new Color(0.15f, 0.15f, 0.2f),
                "tab_normal" => _theme.panelBorder,
                "tab_selected" => _theme.buttonNormal,
                "close_btn" => _theme.accentRed,
                "confirm_btn" => _theme.accentGreen,
                _ => _theme.panelBackground,
            };

            return GenerateRoundedSprite(color);
        }

        /// <summary>
        /// 테마 색상으로 단순 정사각 스프라이트를 생성한다 (프로시져럴 fallback).
        /// </summary>
        private static Sprite GenerateRoundedSprite(Color color, int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float halfSize = size * 0.5f;
            float radius = size * 0.15f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 라운딩된 사각형 마스크
                    float dx = Mathf.Max(0, Mathf.Abs(x - halfSize + 0.5f) - (halfSize - radius));
                    float dy = Mathf.Max(0, Mathf.Abs(y - halfSize + 0.5f) - (halfSize - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                        tex.SetPixel(x, y, color);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 아이템 프레임 스프라이트를 반환한다 (선택/비선택).
        /// </summary>
        public (Sprite normal, Sprite selected) GetItemFrameSprites()
        {
            var normal = Resources.Load<Sprite>($"{GuiKitBase}/Skill/frame_silver");
            var selected = Resources.Load<Sprite>($"{GuiKitBase}/Skill/frame_yellow");
            return (normal, selected);
        }

        /// <summary>
        /// 팝업 배경 스프라이트를 반환한다.
        /// </summary>
        public Sprite GetPopupBackground()
        {
            return Resources.Load<Sprite>($"{GuiKitBase}/Popup/Popup01_Title");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
