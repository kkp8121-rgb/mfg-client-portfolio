using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// 닉네임 입력 팝업. 2~16자 제한, 공백/특수문자 금지.
    /// 뒤로가기 금지 (온보딩 플로우 강제).
    /// </summary>
    public class NicknamePopup : BasePopup
    {
        private const int MIN_LENGTH = 2;
        private const int MAX_LENGTH = 16;
        private static readonly Regex _validChars = new Regex(@"^[\w가-힣]+$", RegexOptions.Compiled);

        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TMP_Text _errorText;
        [SerializeField] private Button _confirmButton;

        public event Action<string> Confirmed;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();

            if (_inputField != null)
            {
                _inputField.characterLimit = MAX_LENGTH;
                _inputField.onValueChanged.AddListener(OnValueChanged);
            }

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            ClearError();
        }

        private void EnsureComponents()
        {
            RectTransform panel;
            var panelTr = transform.Find("Panel");
            if (panelTr == null)
            {
                var panelGo = new GameObject("Panel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                panel = panelGo.GetComponent<RectTransform>();
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(800, 700);
                var img = panelGo.AddComponent<Image>();
                img.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);
            }
            else panel = panelTr as RectTransform;

            // 제목
            if (panel.Find("Title") == null)
            {
                var titleGo = new GameObject("Title", typeof(RectTransform));
                titleGo.transform.SetParent(panel, false);
                var trt = titleGo.GetComponent<RectTransform>();
                trt.sizeDelta = new Vector2(700, 80);
                trt.anchoredPosition = new Vector2(0, 220);
                var tmp = titleGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "닉네임 설정";
                tmp.fontSize = 56;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            if (_inputField == null)
            {
                var ifGo = new GameObject("InputField", typeof(RectTransform));
                ifGo.transform.SetParent(panel, false);
                var rt = ifGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(600, 120);
                rt.anchoredPosition = new Vector2(0, 60);
                var img = ifGo.AddComponent<Image>();
                img.color = Color.white;

                var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
                textArea.transform.SetParent(ifGo.transform, false);
                var trt = textArea.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(20, 10); trt.offsetMax = new Vector2(-20, -10);

                var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
                placeholderGo.transform.SetParent(textArea.transform, false);
                var prt = placeholderGo.GetComponent<RectTransform>();
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var ph = placeholderGo.AddComponent<TextMeshProUGUI>();
                ph.text = "닉네임을 입력하세요 (2~16자)";
                ph.fontSize = 42;
                ph.color = new Color(0.5f, 0.5f, 0.5f);
                ph.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Center;

                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(textArea.transform, false);
                var txRt = textGo.GetComponent<RectTransform>();
                txRt.anchorMin = Vector2.zero; txRt.anchorMax = Vector2.one;
                txRt.offsetMin = Vector2.zero; txRt.offsetMax = Vector2.zero;
                var tx = textGo.AddComponent<TextMeshProUGUI>();
                tx.fontSize = 42;
                tx.color = Color.black;
                tx.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Center;

                _inputField = ifGo.AddComponent<TMP_InputField>();
                _inputField.textViewport = textArea.GetComponent<RectTransform>();
                _inputField.textComponent = tx;
                _inputField.placeholder = ph;
                _inputField.characterLimit = MAX_LENGTH;
            }

            if (_errorText == null)
            {
                var errGo = new GameObject("ErrorText", typeof(RectTransform));
                errGo.transform.SetParent(panel, false);
                var rt = errGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(700, 60);
                rt.anchoredPosition = new Vector2(0, -40);
                _errorText = errGo.AddComponent<TextMeshProUGUI>();
                _errorText.text = "";
                _errorText.fontSize = 36;
                _errorText.alignment = TextAlignmentOptions.Center;
                _errorText.color = new Color(1f, 0.4f, 0.4f);
            }

            if (_confirmButton == null)
            {
                var btnGo = new GameObject("ConfirmButton", typeof(RectTransform));
                btnGo.transform.SetParent(panel, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 120);
                rt.anchoredPosition = new Vector2(0, -200);
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.25f, 0.6f, 0.35f);
                _confirmButton = btnGo.AddComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "확인";
                tmp.fontSize = 48;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }

        private void OnValueChanged(string value) => ClearError();

        /// <summary>봇 자동화 진입점 — 닉네임 세팅 + 확정 트리거.</summary>
        public void BotSubmit(string nickname)
        {
            if (_inputField != null) _inputField.text = nickname;
            OnConfirmClicked();
        }

        private void OnConfirmClicked()
        {
            var value = _inputField != null ? _inputField.text.Trim() : string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                ShowError("닉네임을 입력해주세요.");
                return;
            }

            if (value.Length < MIN_LENGTH)
            {
                ShowError($"{MIN_LENGTH}자 이상 입력해주세요.");
                return;
            }

            if (value.Length > MAX_LENGTH)
            {
                ShowError($"{MAX_LENGTH}자 이하로 입력해주세요.");
                return;
            }

            if (!_validChars.IsMatch(value))
            {
                ShowError("한글, 영문, 숫자만 사용 가능합니다.");
                return;
            }

            Confirmed?.Invoke(value);
            Hide();
        }

        private void ShowError(string message)
        {
            if (_errorText != null)
            {
                _errorText.text = message;
                _errorText.gameObject.SetActive(true);
            }
        }

        private void ClearError()
        {
            if (_errorText != null)
                _errorText.gameObject.SetActive(false);
        }

        public override void OnBackButton() { }
    }
}
