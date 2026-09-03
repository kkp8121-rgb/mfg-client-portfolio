using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.UI
{
    /// <summary>
    /// 개별 스킬 슬롯 UI. 아이콘, 쿨타임 오버레이, 버프 지속시간 바를 표시한다.
    /// SkillSystem에서 매 프레임 갱신한다.
    /// </summary>
    public class SkillSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TextMeshProUGUI _cooldownText;
        [SerializeField] private Image _durationBar;
        [SerializeField] private Image _activeBorder;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _lockIcon;

        private SkillDataSO _skill;
        private bool _isAssigned;
        private bool _isBuffActive;
        private Tween _castTween;

        private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        private static readonly Color CooldownOverlayColor = new Color(0f, 0f, 0f, 0.6f);

        public bool IsAssigned => _isAssigned;
        public bool IsUnlocked => _isAssigned && _isUnlocked;
        public string SkillId => _skill != null ? _skill.id : null;

        private bool _isUnlocked;

        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>
        /// 잠금 상태로 표시. 슬롯은 보이지만 스킬 미할당.
        /// </summary>
        public void ShowLocked()
        {
            _isAssigned = false;
            _isUnlocked = false;
            _skill = null;
            gameObject.SetActive(true);

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.color = LockedColor;
            }
            if (_cooldownOverlay != null)
                _cooldownOverlay.gameObject.SetActive(false);
            if (_cooldownText != null)
                _cooldownText.gameObject.SetActive(false);
            if (_activeBorder != null)
                _activeBorder.gameObject.SetActive(false);
            if (_durationBar != null)
                _durationBar.gameObject.SetActive(false);
            if (_lockIcon != null)
                _lockIcon.gameObject.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0.5f;
        }

        /// <summary>
        /// 미해금 스킬 미리보기. 아이콘은 보이지만 어둡게 표시.
        /// </summary>
        public void ShowLockedPreview(SkillDataSO skill)
        {
            _skill = skill;
            _isAssigned = true;
            _isUnlocked = false;
            gameObject.SetActive(true);

            if (_iconImage != null)
            {
                var icon = skill.icon != null ? skill.icon : ResolveSkillIcon(skill.id);
                _iconImage.sprite = icon;
                _iconImage.color = icon != null
                    ? new Color(0.3f, 0.3f, 0.3f, 0.6f)
                    : LockedColor;
            }
            if (_cooldownOverlay != null)
                _cooldownOverlay.gameObject.SetActive(false);
            if (_cooldownText != null)
                _cooldownText.gameObject.SetActive(false);
            if (_activeBorder != null)
                _activeBorder.gameObject.SetActive(false);
            if (_durationBar != null)
                _durationBar.gameObject.SetActive(false);
            if (_lockIcon != null)
                _lockIcon.gameObject.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0.6f;
        }

        /// <summary>
        /// 잠금 미리보기 → 해금으로 전환. 해금 연출 포함.
        /// </summary>
        public void Unlock()
        {
            if (_skill == null) return;
            _isUnlocked = true;

            if (_iconImage != null)
            {
                var icon = _skill.icon != null ? _skill.icon : ResolveSkillIcon(_skill.id);
                _iconImage.color = icon != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            }

            if (_lockIcon != null)
                _lockIcon.gameObject.SetActive(false);

            SetCooldown(0f, _skill.cooldown);
            SetBuffActive(false, 0f, 0f);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.4f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            PlayCastEffect();
        }

        /// <summary>
        /// 스킬 슬롯에 스킬을 할당한다. 해금 시 호출.
        /// </summary>
        public void AssignSkill(SkillDataSO skill)
        {
            _skill = skill;
            _isAssigned = true;
            _isUnlocked = true;

            if (_iconImage != null)
            {
                var icon = skill.icon != null ? skill.icon : ResolveSkillIcon(skill.id);
                _iconImage.sprite = icon;
                _iconImage.color = icon != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            }

            // 직업별 색상 액티브 보더 적용
            ApplyJobColor(skill.id);

            if (_lockIcon != null)
                _lockIcon.gameObject.SetActive(false);

            // 초기 상태: 쿨타임 없음
            SetCooldown(0f, skill.cooldown);
            SetBuffActive(false, 0f, 0f);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.3f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 쿨타임 상태를 갱신한다. 매 프레임 호출.
        /// </summary>
        public void SetCooldown(float remaining, float total)
        {
            if (!_isAssigned) return;

            bool isOnCooldown = remaining > 0f && total > 0f;

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = isOnCooldown ? remaining / total : 0f;
                _cooldownOverlay.gameObject.SetActive(isOnCooldown);
            }

            if (_cooldownText != null)
            {
                if (isOnCooldown)
                {
                    _cooldownText.text = remaining >= 10f
                        ? $"{Mathf.CeilToInt(remaining)}"
                        : $"{remaining:0.0}";
                    _cooldownText.gameObject.SetActive(true);
                }
                else
                {
                    _cooldownText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 버프 활성 상태를 갱신한다.
        /// </summary>
        public void SetBuffActive(bool isActive, float remaining, float total)
        {
            bool changed = _isBuffActive != isActive;
            _isBuffActive = isActive;

            if (_activeBorder != null)
                _activeBorder.gameObject.SetActive(isActive);

            if (_durationBar != null)
            {
                _durationBar.gameObject.SetActive(isActive);
                if (isActive && total > 0f)
                    _durationBar.fillAmount = remaining / total;
            }

            // 버프 시전 시 펀치 연출
            if (changed && isActive)
                PlayCastEffect();
        }

        /// <summary>
        /// 슬롯을 비운다.
        /// </summary>
        public void Clear()
        {
            _skill = null;
            _isAssigned = false;
            _isUnlocked = false;
            gameObject.SetActive(false);
        }

        private Sprite ResolveSkillIcon(string skillId)
        {
            if (IconRegistry.Instance == null) return null;
            return IconRegistry.Instance.GetSkillIcon(skillId);
        }

        private void EnsureComponents()
        {
            // --- 루트: 어두운 프레임 배경 (슬롯 테두리 역할) ---
            var rootImage = GetComponent<Image>();
            if (rootImage == null)
            {
                rootImage = gameObject.AddComponent<Image>();
                rootImage.color = new Color(0.12f, 0.12f, 0.2f, 0.9f);
                rootImage.raycastTarget = true;
            }

            // --- RectMask2D: 아이콘이 프레임 밖으로 삐져나가지 않도록 클리핑 ---
            var rectMask = GetComponent<UnityEngine.UI.RectMask2D>();
            if (rectMask == null)
                rectMask = gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            rectMask.padding = Vector4.zero;

            // --- ActiveBorder: 가장 뒤에 깔리는 글로우 보더 (아이콘보다 약간 큼) ---
            if (_activeBorder == null)
            {
                var go = CreateUIChild("ActiveBorder");
                _activeBorder = go.AddComponent<Image>();
                _activeBorder.color = new Color(1f, 0.85f, 0.1f, 0.7f);
                _activeBorder.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-2f, -2f);
                rt.offsetMax = new Vector2(2f, 2f);
                go.SetActive(false);
            }

            // --- Icon: 4px 패딩으로 안쪽에 배치 ---
            if (_iconImage == null)
            {
                var go = CreateUIChild("Icon");
                _iconImage = go.AddComponent<Image>();
                _iconImage.color = LockedColor;
                _iconImage.preserveAspect = true;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(4f, 4f);
                rt.offsetMax = new Vector2(-4f, -4f);
            }

            // --- CooldownOverlay: 슬롯 전체 덮기, 반투명 검정 ---
            if (_cooldownOverlay == null)
            {
                var go = CreateUIChild("CooldownOverlay");
                _cooldownOverlay = go.AddComponent<Image>();
                _cooldownOverlay.sprite = CreateWhiteSprite();
                _cooldownOverlay.color = CooldownOverlayColor;
                _cooldownOverlay.type = Image.Type.Filled;
                _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
                _cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
                _cooldownOverlay.fillClockwise = false;
                _cooldownOverlay.fillAmount = 0f;
                _cooldownOverlay.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.SetActive(false);
            }
            else
            {
                // 기존 오버레이도 Radial360으로 설정 보장
                _cooldownOverlay.type = Image.Type.Filled;
                _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
                _cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
                _cooldownOverlay.fillClockwise = false;
            }

            // --- CooldownText: 오버레이 위 중앙, 흰색 볼드 ---
            if (_cooldownText == null)
            {
                var go = CreateUIChild("CooldownText");
                _cooldownText = go.AddComponent<TextMeshProUGUI>();
                _cooldownText.fontSize = 14f;
                _cooldownText.alignment = TextAlignmentOptions.Center;
                _cooldownText.color = Color.white;
                _cooldownText.fontStyle = FontStyles.Bold;
                _cooldownText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.SetActive(false);
            }

            // --- DurationBar: 하단 얇은 가로 바 (높이 4px) ---
            if (_durationBar == null)
            {
                var go = CreateUIChild("DurationBar");
                _durationBar = go.AddComponent<Image>();
                _durationBar.sprite = CreateWhiteSprite();
                _durationBar.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                _durationBar.type = Image.Type.Filled;
                _durationBar.fillMethod = Image.FillMethod.Horizontal;
                _durationBar.fillAmount = 0f;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, 6f);
                go.SetActive(false);
            }

            // --- LockIcon: 중앙, 잠금 텍스트로 표시 (스프라이트 없어도 깨지지 않도록) ---
            if (_lockIcon == null)
            {
                var go = CreateUIChild("LockIcon");
                // Image는 투명하게 두고, 텍스트로 자물쇠 표시
                _lockIcon = go.AddComponent<Image>();
                _lockIcon.color = new Color(0f, 0f, 0f, 0f); // 투명
                _lockIcon.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // 자물쇠 텍스트
                var lockTextGo = new GameObject("LockText", typeof(RectTransform));
                lockTextGo.transform.SetParent(go.transform, false);
                var lockTmp = lockTextGo.AddComponent<TextMeshProUGUI>();
                lockTmp.text = "?";
                lockTmp.fontSize = 22f;
                lockTmp.fontStyle = FontStyles.Bold;
                lockTmp.alignment = TextAlignmentOptions.Center;
                lockTmp.color = new Color(0.5f, 0.5f, 0.55f, 0.9f);
                lockTmp.raycastTarget = false;
                var lockTmpRt = lockTextGo.GetComponent<RectTransform>();
                lockTmpRt.anchorMin = Vector2.zero;
                lockTmpRt.anchorMax = Vector2.one;
                lockTmpRt.offsetMin = Vector2.zero;
                lockTmpRt.offsetMax = Vector2.zero;
                go.SetActive(false);
            }

            // --- CanvasGroup: 페이드 연출용 ---
            if (_canvasGroup == null)
                _canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            ApplySlotTheme();
        }

        private void ApplySlotTheme()
        {
            var theme = UIThemeManager.Instance;
            if (theme == null) return;

            var (normal, _) = theme.GetItemFrameSprites();
            if (normal == null) return;

            var rootImg = GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = normal;
                rootImg.type = Image.Type.Sliced;
            }
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        private GameObject CreateUIChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        /// <summary>
        /// 스킬 ID에서 직업명을 추출하여 액티브 보더에 직업 색상을 적용한다.
        /// </summary>
        private void ApplyJobColor(string skillId)
        {
            if (_activeBorder == null || string.IsNullOrEmpty(skillId)) return;

            // Skill_{job}_{name} 형태에서 직업명 추출
            if (!skillId.StartsWith("Skill_", System.StringComparison.OrdinalIgnoreCase)) return;
            string remainder = skillId.Substring(6);
            int idx = remainder.IndexOf('_');
            if (idx <= 0) return;

            string job = remainder.Substring(0, idx).ToLowerInvariant();
            Color jobColor = job switch
            {
                "warrior" or "warlord" or "titan" => new Color(0.9f, 0.3f, 0.2f, 0.7f),
                "knight" or "paladin" => new Color(0.3f, 0.5f, 0.9f, 0.7f),
                "archer" or "scout" or "hawkeye" => new Color(0.2f, 0.8f, 0.3f, 0.7f),
                "windwalker" or "stormcaller" or "stormbringer" => new Color(0.3f, 0.85f, 0.9f, 0.7f),
                "mage" or "sorcerer" or "runemaster" or "archmage" => new Color(0.6f, 0.3f, 0.9f, 0.7f),
                "sage" => new Color(0.3f, 0.4f, 0.9f, 0.7f),
                "dragonslayer" or "dragonknight" => new Color(1f, 0.6f, 0.2f, 0.7f),
                "ancient" => new Color(0.95f, 0.95f, 0.95f, 0.7f),
                _ => new Color(1f, 0.85f, 0.1f, 0.7f),
            };

            _activeBorder.color = jobColor;
        }

        private void PlayCastEffect()
        {
            _castTween?.Kill();
            transform.localScale = Vector3.one;
            _castTween = transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f)
                .SetUpdate(true).SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _castTween?.Kill();
        }
    }
}
