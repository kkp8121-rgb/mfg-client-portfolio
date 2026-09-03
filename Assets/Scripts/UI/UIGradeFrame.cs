using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 아이템 슬롯에 부착하여 등급에 따라 테두리 색상을 자동 변경한다.
    /// Legendary/Mythic은 글로우 펄스 애니메이션이 재생된다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIGradeFrame : MonoBehaviour
    {
        [SerializeField] private float _pulseMinAlpha = 0.6f;
        [SerializeField] private float _pulseDuration = 0.8f;

        private Image _frameImage;
        private string _currentGrade;
        private Tween _pulseTween;

        private void Awake()
        {
            _frameImage = GetComponent<Image>();
        }

        // Stone Kit 등급별 프레임 스프라이트 경로
        private static readonly System.Collections.Generic.Dictionary<string, string> GradeFramePath = new()
        {
            { "Normal", "UI/Stone/Skill/frame_silver" },
            { "Rare", "UI/Stone/Skill/frame_blue" },
            { "Epic", "UI/Stone/Skill/frame_purple" },
            { "Unique", "UI/Stone/Skill/frame_red" },
            { "Legendary", "UI/Stone/Skill/frame_yellow" },
            { "Mythic", "UI/Stone/Skill/frame_red" },
        };

        /// <summary>
        /// 등급을 설정하여 테두리 스프라이트/색상과 이펙트를 갱신한다.
        /// </summary>
        public void SetGrade(string grade)
        {
            if (_frameImage == null)
                _frameImage = GetComponent<Image>();

            _currentGrade = grade;

            KillPulse();

            // Stone Kit 등급 프레임 스프라이트 적용
            Sprite gradeSprite = null;
            if (GradeFramePath.TryGetValue(grade ?? "Normal", out string path))
                gradeSprite = Resources.Load<Sprite>(path);

            if (gradeSprite != null)
            {
                _frameImage.sprite = gradeSprite;
                _frameImage.type = Image.Type.Sliced;
                _frameImage.color = Color.white;
            }
            else
            {
                Color gradeColor;
                if (UIThemeManager.Instance != null)
                    gradeColor = UIThemeManager.Instance.GetGradeColor(grade);
                else
                    gradeColor = GetFallbackColor(grade);
                _frameImage.color = gradeColor;
            }

            if (grade == "Legendary" || grade == "Mythic")
            {
                Color pulseColor = UIThemeManager.Instance != null
                    ? UIThemeManager.Instance.GetGradeColor(grade)
                    : GetFallbackColor(grade);
                StartPulse(pulseColor);
            }
        }

        private void StartPulse(Color baseColor)
        {
            // 스프라이트가 있으면 스케일 펄스, 없으면 컬러 펄스
            if (_frameImage.sprite != null && _frameImage.sprite.name != "UISprite")
            {
                _pulseTween = _frameImage.transform.DOScale(
                    Vector3.one * 1.05f,
                    _pulseDuration
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
            }
            else
            {
                Color dimColor = baseColor;
                dimColor.a = _pulseMinAlpha;

                _pulseTween = DOTween.To(
                    () => _frameImage.color,
                    c => _frameImage.color = c,
                    dimColor,
                    _pulseDuration
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
            }
        }

        private void KillPulse()
        {
            if (_pulseTween != null && _pulseTween.IsActive())
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }
        }

        private static Color GetFallbackColor(string grade)
        {
            return grade switch
            {
                "Normal" => new Color(0.50f, 0.50f, 0.50f),
                "Rare" => new Color(0.29f, 0.56f, 0.85f),
                "Epic" => new Color(0.66f, 0.33f, 0.97f),
                "Unique" => new Color(1f, 0.40f, 0.40f),
                "Legendary" => new Color(1f, 0.84f, 0f),
                "Mythic" => new Color(1f, 0.27f, 0.27f),
                _ => Color.white
            };
        }

        private void OnDestroy()
        {
            KillPulse();
        }
    }
}
