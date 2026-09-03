using Cysharp.Threading.Tasks;
using MkLike.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// 로그인 팝업. Google/Apple/게스트 3버튼 세로 배치.
    /// 게스트만 실제 Mock 로그인을 수행하고, Google/Apple은 placeholder(준비 중 상태 표시).
    /// </summary>
    public class LoginPopup : BasePopup
    {
        [SerializeField] private Button _googleButton;
        [SerializeField] private Button _appleButton;
        [SerializeField] private Button _guestButton;
        [SerializeField] private TMP_Text _statusText;

        private static readonly Color PanelColor = new(0.98f, 0.98f, 0.96f, 0.82f);
        private static readonly Color ButtonColor = Color.white;
        private static readonly Color BorderColor = new(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color TextColor = new(0.18f, 0.18f, 0.18f);

        protected override void Awake()
        {
            base.Awake();
            StripBackdrop();
            EnsureComponents();

            HookButton(_googleButton, () => ShowComingSoon("Google"));
            HookButton(_appleButton, () => ShowComingSoon("Apple"));
            HookButton(_guestButton, OnGuestClicked);
        }

        /// <summary>
        /// BasePopup.ApplyPopupTheme가 입힌 전체 화면 딤드(self Image)를 제거한다.
        /// Image는 유지하여 raycast blocker 역할만 수행(투명).
        /// </summary>
        private void StripBackdrop()
        {
            var selfImg = GetComponent<Image>();
            if (selfImg != null)
            {
                selfImg.color = new Color(0f, 0f, 0f, 0f);
                selfImg.sprite = null;
                selfImg.raycastTarget = false;
            }
        }

        private static void HookButton(Button btn, System.Action onClick)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void EnsureComponents()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            // 구버전에서 만들어진 Facebook/Nexon 버튼이 있으면 정리
            CleanupLegacyChildren();

            RectTransform panel = transform.Find("Panel") as RectTransform;
            if (panel == null)
            {
                var panelGo = new GameObject("Panel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                panel = panelGo.GetComponent<RectTransform>();
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(780, 680);
                panel.anchoredPosition = Vector2.zero;

                var bg = panelGo.AddComponent<Image>();
                bg.color = PanelColor;
            }
            else
            {
                // 기존 패널 크기/위치 재조정 (중앙 대칭)
                panel.sizeDelta = new Vector2(780, 680);
                panel.anchoredPosition = Vector2.zero;
                var bg = panel.GetComponent<Image>();
                if (bg != null) bg.color = PanelColor;
            }

            EnsureTitle(panel, "로그인 방법을 선택하세요");

            // 세로 3개 버튼: Google / Apple / 게스트
            float rowTop = 130f;
            float rowMid = 0f;
            float rowBot = -130f;
            float statusY = -260f;

            if (_googleButton == null)
                _googleButton = CreateSocialButton(panel, "GoogleButton", "Google로 로그인",
                    "G", new Color(0.92f, 0.26f, 0.21f), new Vector2(0, rowTop));

            if (_appleButton == null)
                _appleButton = CreateSocialButton(panel, "AppleButton", "Apple로 로그인",
                    "\uF179", new Color(0.1f, 0.1f, 0.12f), new Vector2(0, rowMid));

            if (_guestButton == null)
                _guestButton = CreateSocialButton(panel, "GuestButton", "게스트로 로그인",
                    "\u2606", new Color(0.45f, 0.45f, 0.5f), new Vector2(0, rowBot));

            if (_statusText == null)
                _statusText = CreateLabel(panel, "StatusText", "", new Vector2(0, statusY), 30,
                    new Color(0.55f, 0.3f, 0.1f));
        }

        private void CleanupLegacyChildren()
        {
            var panel = transform.Find("Panel");
            if (panel == null) return;
            var legacyNames = new[] { "FacebookButton", "NexonButton" };
            foreach (var name in legacyNames)
            {
                var t = panel.Find(name);
                if (t != null)
                {
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                }
            }
        }

        private void EnsureTitle(RectTransform panel, string title)
        {
            var existing = panel.Find("TitleLabel") as RectTransform;
            if (existing != null)
            {
                existing.anchoredPosition = new Vector2(0, 260);
                return;
            }
            CreateLabel(panel, "TitleLabel", title, new Vector2(0, 260), 38, TextColor);
        }

        private static Button CreateSocialButton(RectTransform parent, string name, string label,
            string iconGlyph, Color iconColor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(680, 120);
            rt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = ButtonColor;
            go.AddComponent<Outline>().effectColor = BorderColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.94f, 0.96f, 1f);
            colors.pressedColor = new Color(0.86f, 0.9f, 0.98f);
            colors.disabledColor = new Color(0.85f, 0.85f, 0.85f);
            btn.colors = colors;

            // 아이콘 원형 배경
            var iconBg = new GameObject("IconBg", typeof(RectTransform));
            iconBg.transform.SetParent(go.transform, false);
            var iconRt = iconBg.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(70, 70);
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(70, 0);
            var iconImg = iconBg.AddComponent<Image>();
            iconImg.color = iconColor;

            // 아이콘 글리프
            var iconLabelGo = new GameObject("IconText", typeof(RectTransform));
            iconLabelGo.transform.SetParent(iconBg.transform, false);
            var iconLabelRt = iconLabelGo.GetComponent<RectTransform>();
            iconLabelRt.anchorMin = Vector2.zero; iconLabelRt.anchorMax = Vector2.one;
            iconLabelRt.offsetMin = Vector2.zero; iconLabelRt.offsetMax = Vector2.zero;
            var iconTmp = iconLabelGo.AddComponent<TextMeshProUGUI>();
            iconTmp.text = iconGlyph;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.fontSize = 48;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = Color.white;

            // 메인 라벨
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(130, 0); lrt.offsetMax = new Vector2(-30, 0);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 40;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = TextColor;
            tmp.fontStyle = FontStyles.Bold;

            return btn;
        }

        private static TMP_Text CreateLabel(RectTransform parent, string name, string text, Vector2 pos,
            float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700, 60);
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
        }

        private void ShowComingSoon(string provider)
        {
            SetStatus($"{provider} 로그인은 준비 중입니다");
        }

        private void OnGuestClicked()
        {
            if (_guestButton != null) _guestButton.interactable = false;
            SetStatus("로그인 중...");
            TryGuestLogin().Forget();
        }

        /// <summary>봇 자동화 진입점 — 게스트 로그인 트리거.</summary>
        public void BotGuestLogin() => OnGuestClicked();

        private async UniTaskVoid TryGuestLogin()
        {
            if (LoginManager.Instance == null)
            {
                SetStatus("LoginManager 없음");
                if (_guestButton != null) _guestButton.interactable = true;
                return;
            }

            // 2026-04-23 P2 수정: 팝업이 await 중 Destroy되면 이후 필드 접근이
            // MissingReferenceException 유발. CancellationTokenOnDestroy 바인딩으로 방지.
            var ct = this.GetCancellationTokenOnDestroy();
            bool ok;
            try
            {
                ok = await LoginManager.Instance.MockLoginAsync().AttachExternalCancellation(ct);
            }
            catch (System.OperationCanceledException)
            {
                return; // 팝업 이미 파괴됨 — 필드 접근 금지
            }

            // 추가 안전망: 파괴 여부 재확인 (ct 토큰이 설정 안 됐을 edge case)
            if (this == null) return;

            if (!ok)
            {
                SetStatus("로그인 실패 — 서버 확인");
                if (_guestButton != null) _guestButton.interactable = true;
                return;
            }

            SetStatus("로그인 성공");
            Hide();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
            Debug.Log($"[LoginPopup] {message}");
        }
    }
}
