using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 재화 부족 시 표시되는 팝업.
    /// CurrencyShortageEvent를 구독하여 자동 표시된다.
    /// 부족한 재화 정보와 획득처 바로가기 버튼(최대 3개)을 표시한다.
    /// 정적 구독을 사용하여 GameObject가 비활성 상태에서도 이벤트를 수신한다.
    /// </summary>
    public class CurrencyShortagePopup : BasePopup
    {
        [Header("재화 부족 팝업")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _shortageText;
        [SerializeField] private Transform _sourceButtonContainer;
        [SerializeField] private Button _closeBtn;

        private static readonly Color TextWhite = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextRed = new(0.95f, 0.3f, 0.3f, 1f);
        private static readonly Color TextGold = new(1f, 0.85f, 0.086f, 1f);
        private static readonly Color BtnGreen = new(0.25f, 0.45f, 0.30f, 1f);

        /// <summary>싱글톤 인스턴스 (런타임 자동 등록)</summary>
        public static CurrencyShortagePopup Instance { get; private set; }

        /// <summary>
        /// 앱 시작 시 정적 이벤트 구독.
        /// GameObject 활성 여부와 무관하게 이벤트를 수신한다.
        /// 에디터 도메인 리로드 시 중복 구독 방지를 위해 먼저 해제 후 재구독.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StaticSubscribe()
        {
            EventBus<CurrencyShortageEvent>.Unsubscribe(OnCurrencyShortageStatic);
            EventBus<CurrencyShortageEvent>.Subscribe(OnCurrencyShortageStatic);
        }

        private static void OnCurrencyShortageStatic(CurrencyShortageEvent evt)
        {
            if (Instance == null)
            {
                // 인스턴스가 없으면 비활성 오브젝트 탐색 후 활성화
                var popup = FindFirstObjectByType<CurrencyShortagePopup>(FindObjectsInactive.Include);
                if (popup != null)
                {
                    Instance = popup;
                    popup.Show(evt.Type, evt.Required, evt.Current);
                    return;
                }
                Debug.LogWarning($"[CurrencyShortagePopup] 팝업 인스턴스 없음 — {CurrencySourceMap.GetDisplayName(evt.Type)} 부족!");
                return;
            }
            Instance.Show(evt.Type, evt.Required, evt.Current);
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;

            if (_closeBtn != null)
                _closeBtn.onClick.AddListener(OnBackButton);
        }

        /// <summary>
        /// 재화 부족 팝업을 표시한다.
        /// </summary>
        public void Show(CurrencyType type, BigNumber required, BigNumber current)
        {
            BigNumber shortage = required - current;
            string displayName = CurrencySourceMap.GetDisplayName(type);

            if (_titleText != null)
                _titleText.text = $"{displayName} 부족!";

            if (_shortageText != null)
            {
                _shortageText.text =
                    $"필요: <color=#{ColorUtility.ToHtmlStringRGB(TextGold)}>{NumberFormatter.FormatKorean(required)}</color>\n" +
                    $"보유: <color=#{ColorUtility.ToHtmlStringRGB(TextRed)}>{NumberFormatter.FormatKorean(current)}</color>\n" +
                    $"부족: <color=#{ColorUtility.ToHtmlStringRGB(TextRed)}>{NumberFormatter.FormatKorean(shortage)}</color>";
            }

            BuildSourceButtons(type);
            base.Show();
        }

        private void BuildSourceButtons(CurrencyType type)
        {
            if (_sourceButtonContainer == null) return;

            // 기존 버튼 제거
            for (int i = _sourceButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(_sourceButtonContainer.GetChild(i).gameObject);

            var sources = CurrencySourceMap.GetSources(type, 3);
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                CreateSourceButton(source);
            }
        }

        private void CreateSourceButton(CurrencySource source)
        {
            var btnObj = new GameObject($"Btn_{source.ContentName}");
            btnObj.transform.SetParent(_sourceButtonContainer, false);

            var rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 50);

            var bg = btnObj.AddComponent<Image>();
            bg.color = BtnGreen;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            // 버튼 내부 레이아웃
            var hlg = btnObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(12, 12, 4, 4);

            // 컨텐츠명
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(btnObj.transform, false);
            nameObj.AddComponent<RectTransform>().sizeDelta = new Vector2(80, 0);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = source.ContentName;
            nameTmp.fontSize = 18;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = TextWhite;
            nameTmp.alignment = TextAlignmentOptions.Left;

            // 설명
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(btnObj.transform, false);
            descObj.AddComponent<RectTransform>().sizeDelta = new Vector2(140, 0);
            var descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = source.Description;
            descTmp.fontSize = 13;
            descTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            descTmp.alignment = TextAlignmentOptions.Left;

            // 클릭 동작
            string panelAction = source.PanelAction;
            btn.onClick.AddListener(() =>
            {
                btnObj.transform.DOKill();
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 6, 0.5f)
                    .SetUpdate(true).SetLink(btnObj);
                NavigateToSource(panelAction);
            });

            // 팝인 연출
            rt.localScale = Vector3.zero;
            rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(btnObj);
        }

        private void NavigateToSource(string panelAction)
        {
            Hide();

            // 씬에 존재하는 패널을 직접 찾아서 Show() 호출
            switch (panelAction)
            {
                case "combat":
                    break;
                case "dungeon":
                case "dungeon_gold":
                case "dungeon_star":
                case "dungeon_potential":
                case "dungeon_weapon":
                case "tower":
                    ShowPopupByType<DungeonPanel>();
                    break;
                case "battlepass":
                    HudPanel.ShowToast("배틀패스에서 보상을 획득할 수 있습니다.");
                    break;
                case "costume":
                    HudPanel.ShowToast("코스튬 기능은 준비 중입니다.");
                    break;
                case "shop":
                    var tabBar = Object.FindFirstObjectByType<TabBarUI>();
                    if (tabBar != null) tabBar.SelectTab(3); // 상점 탭 (0:캐릭터/1:장비/2:던전/3:상점)
                    break;
                case "quest":
                case "quest_daily":
                    HudPanel.ShowToast("퀘스트 보상을 통해 획득할 수 있습니다.");
                    break;
                case "achievement":
                    HudPanel.ShowToast("업적 달성으로 획득할 수 있습니다.");
                    break;
                default:
                    Debug.Log($"[CurrencyShortagePopup] 알 수 없는 패널: {panelAction}");
                    break;
            }
        }

        private static void ShowPopupByType<T>() where T : BasePopup
        {
            var panel = Object.FindFirstObjectByType<T>();
            if (panel != null)
                panel.Show();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;

            if (_sourceButtonContainer != null)
            {
                for (int i = _sourceButtonContainer.childCount - 1; i >= 0; i--)
                {
                    var child = _sourceButtonContainer.GetChild(i);
                    child.DOKill();
                }
            }
        }
    }
}
