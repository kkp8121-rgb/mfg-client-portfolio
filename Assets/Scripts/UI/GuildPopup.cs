using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Guild;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 길드 팝업.
    /// 길드 정보 + 기부 + 보스전 (CP 비교 즉시 결과).
    /// 퀵메뉴 길드 버튼에서 호출.
    /// </summary>
    public class GuildPopup : BasePopup
    {
        [Header("UI 참조")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _guildInfoText;
        [SerializeField] private TMP_Text _donateInfoText;
        [SerializeField] private TMP_Text _bossInfoText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _createBtn;
        [SerializeField] private Button _donateGoldBtn;
        [SerializeField] private Button _donateRubyBtn;
        [SerializeField] private Button _donateTicketBtn;
        [SerializeField] private Button _bossBtn;
        [SerializeField] private Button _raidBtn;
        [SerializeField] private Button _closeBtn;

        private bool _hasResult;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
        }

        private void Start()
        {
            if (_createBtn != null) _createBtn.onClick.AddListener(OnCreateClicked);
            if (_donateGoldBtn != null) _donateGoldBtn.onClick.AddListener(() => OnDonate("gold"));
            if (_donateRubyBtn != null) _donateRubyBtn.onClick.AddListener(() => OnDonate("ruby"));
            if (_donateTicketBtn != null) _donateTicketBtn.onClick.AddListener(() => OnDonate("ticket"));
            if (_bossBtn != null) _bossBtn.onClick.AddListener(OnBossClicked);
            if (_raidBtn != null) _raidBtn.onClick.AddListener(OnRaidClicked);
            if (_closeBtn != null) _closeBtn.onClick.AddListener(() => Hide());
        }

        private void OnEnable()
        {
            EventBus<GuildDonatedEvent>.Subscribe(OnDonated);
            EventBus<GuildBossCompletedEvent>.Subscribe(OnBossCompleted);
        }

        private void OnDisable()
        {
            EventBus<GuildDonatedEvent>.Unsubscribe(OnDonated);
            EventBus<GuildBossCompletedEvent>.Unsubscribe(OnBossCompleted);
        }

        protected override void OnShow()
        {
            _hasResult = false;
            RefreshUI();
        }

        // ═══ UI 갱신 ═══

        private void RefreshUI()
        {
            var mgr = GuildManager.Instance;
            if (mgr == null)
            {
                if (_titleText != null) _titleText.text = "길드 (미연결)";
                return;
            }

            bool isUnlocked = mgr.IsUnlocked;
            bool isJoined = mgr.IsJoined;

            if (_titleText != null)
                _titleText.text = isJoined ? $"길드: {mgr.GuildName}" : (isUnlocked ? "길드" : "길드 (미해금)");

            // 길드 정보
            if (_guildInfoText != null)
            {
                if (isJoined)
                {
                    _guildInfoText.text = $"Lv.{mgr.GuildLevel}  |  EXP: {mgr.GuildExp:N0}/{mgr.NextLevelExp:N0}";
                }
                else
                {
                    _guildInfoText.text = isUnlocked ? "길드에 가입하거나 새로 만드세요" : $"스테이지 150 클리어 필요";
                }
            }

            // 기부 정보
            if (_donateInfoText != null && isJoined)
            {
                int goldRemain = mgr.GetRemainingDonations("gold");
                int rubyRemain = mgr.GetRemainingDonations("ruby");
                int ticketRemain = mgr.GetRemainingDonations("ticket");
                _donateInfoText.text = $"기부 잔여 — 골드: {goldRemain}회 | 루비: {rubyRemain}회 | 소탕권: {ticketRemain}회";
            }
            else if (_donateInfoText != null)
            {
                _donateInfoText.text = "";
            }

            // 보스 정보
            if (_bossInfoText != null && isJoined)
            {
                long maxHp = mgr.BossMaxHp;
                _bossInfoText.text = mgr.IsBossDefeated
                    ? "이번 주 보스 처치 완료!"
                    : $"보스 HP: {mgr.BossTotalDamage:N0}/{maxHp:N0}  |  남은 도전: {mgr.RemainingBossAttempts}회";
            }
            else if (_bossInfoText != null)
            {
                _bossInfoText.text = "";
            }

            // 결과
            if (_resultText != null && !_hasResult)
                _resultText.text = "";

            // 버튼 상태
            if (_createBtn != null) _createBtn.gameObject.SetActive(!isJoined && isUnlocked);
            if (_donateGoldBtn != null) _donateGoldBtn.gameObject.SetActive(isJoined);
            if (_donateRubyBtn != null) _donateRubyBtn.gameObject.SetActive(isJoined);
            if (_donateTicketBtn != null) _donateTicketBtn.gameObject.SetActive(isJoined);
            if (_bossBtn != null)
            {
                _bossBtn.gameObject.SetActive(isJoined);
                _bossBtn.interactable = isJoined && mgr.CanChallengeBoss();
            }

            if (_raidBtn != null)
                _raidBtn.interactable = isJoined && mgr.CanRaid;

            if (_donateGoldBtn != null) _donateGoldBtn.interactable = mgr.CanDonate("gold");
            if (_donateRubyBtn != null) _donateRubyBtn.interactable = mgr.CanDonate("ruby");
            if (_donateTicketBtn != null) _donateTicketBtn.interactable = mgr.CanDonate("ticket");
        }

        // ═══ 이벤트 핸들러 ═══

        private void OnCreateClicked()
        {
            var mgr = GuildManager.Instance;
            if (mgr == null) return;

            // 자동 이름 생성 (로컬 시뮬레이션)
            string autoName = $"길드{UnityEngine.Random.Range(1000, 9999)}";
            if (mgr.CreateGuild(autoName))
            {
                _hasResult = true;
                if (_resultText != null)
                    _resultText.text = $"'{autoName}' 길드를 창설했습니다!";
                RefreshUI();
            }
        }

        private void OnDonate(string type)
        {
            GuildManager.Instance?.Donate(type);
        }

        private void OnDonated(GuildDonatedEvent evt)
        {
            _hasResult = true;
            if (_resultText != null)
                _resultText.text = $"기부 완료! 길드 EXP +{evt.GuildExpGained}";
            RefreshUI();
        }

        private void OnRaidClicked()
        {
            var mgr = GuildManager.Instance;
            if (mgr == null || !mgr.CanRaid) return;

            var result = mgr.SimulateRaid();

            _hasResult = true;
            if (_resultText != null)
            {
                string clearText = result.WavesCleared >= 5
                    ? "<color=#FFD700>완전 클리어!</color>"
                    : $"{result.WavesCleared}/5 웨이브 클리어";
                _resultText.text = $"토벌전 결과\n" +
                    $"처치: {result.TotalKills}마리 | {clearText}\n" +
                    $"시간: {result.Duration:F1}초 | DMG: {result.DamageDealt:N0}";
            }
            RefreshUI();
        }

        private void OnBossClicked()
        {
            var mgr = GuildManager.Instance;
            if (mgr == null || !mgr.CanChallengeBoss()) return;

            // CP 비교 즉시 결과
            var result = mgr.SimulateBossDetailed();
            mgr.ProcessBossResult(result.TotalDamage);
        }

        private void OnBossCompleted(GuildBossCompletedEvent evt)
        {
            _hasResult = true;
            if (_resultText != null)
            {
                var mgr = GuildManager.Instance;
                string dmgText = $"데미지: {evt.DamageDealt:N0}";
                string defeatText = evt.IsBossDefeated ? "\n<color=#FFD700>보스 처치 성공! 보상 지급!</color>" : "";

                // 기여도 정보
                string contribText = "";
                if (mgr != null && mgr.LastRewardMultiplier > 0)
                {
                    string rankColor = mgr.LastContributionRank == 1 ? "#FFD700" : "#FFFFFF";
                    string mvpTag = mgr.LastContributionRank == 1 ? " MVP!" : "";
                    contribText = $"\n기여도: {mgr.LastContributionPercent * 100:F1}% ({mgr.LastContributionRank}위{mvpTag})" +
                                  $"\n<color={rankColor}>보상 배율: x{mgr.LastRewardMultiplier:F1}</color>";
                }

                _resultText.text = dmgText + defeatText + contribText;
            }
            RefreshUI();
        }

        // ═══ 코드 생성 UI ═══

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.03f, 0.08f, 0.93f);
            }

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 메인 패널
            var panel = transform.Find("Panel");
            if (panel == null)
            {
                var panelGo = new GameObject("Panel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                var panelRt = panelGo.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.05f, 0.12f);
                panelRt.anchorMax = new Vector2(0.95f, 0.88f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
                var panelBg = panelGo.AddComponent<Image>();
                panelBg.color = new Color(0.06f, 0.08f, 0.06f, 0.98f);

                var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 8;
                vlg.padding = new RectOffset(16, 16, 16, 16);

                panel = panelGo.transform;
            }

            if (_titleText == null)
            {
                var go = CreateText(panel, "Title", "길드", 24, FontStyles.Bold, Color.white);
                _titleText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 34);
            }

            if (_guildInfoText == null)
            {
                var go = CreateText(panel, "GuildInfo", "", 16, FontStyles.Normal, new Color(0.7f, 0.9f, 0.7f));
                _guildInfoText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 24);
            }

            if (_donateInfoText == null)
            {
                var go = CreateText(panel, "DonateInfo", "", 14, FontStyles.Normal, new Color(0.8f, 0.8f, 0.6f));
                _donateInfoText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 20);
            }

            // 기부 버튼 영역
            var donateBtnArea = panel.Find("DonateBtnArea");
            if (donateBtnArea == null)
            {
                var btnGo = new GameObject("DonateBtnArea", typeof(RectTransform));
                btnGo.transform.SetParent(panel, false);
                btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
                var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.spacing = 8;
                donateBtnArea = btnGo.transform;
            }

            if (_donateGoldBtn == null)
                _donateGoldBtn = CreateButton(donateBtnArea, "DonateGoldBtn", "골드 기부", new Color(0.7f, 0.55f, 0.2f));
            if (_donateRubyBtn == null)
                _donateRubyBtn = CreateButton(donateBtnArea, "DonateRubyBtn", "루비 기부", new Color(0.7f, 0.2f, 0.3f));
            if (_donateTicketBtn == null)
                _donateTicketBtn = CreateButton(donateBtnArea, "DonateTicketBtn", "소탕권 기부", new Color(0.3f, 0.5f, 0.7f));

            if (_bossInfoText == null)
            {
                var go = CreateText(panel, "BossInfo", "", 14, FontStyles.Normal, new Color(1f, 0.6f, 0.4f));
                _bossInfoText = go.GetComponent<TMP_Text>();
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 20);
            }

            if (_bossBtn == null)
            {
                _bossBtn = CreateButton(panel, "BossBtn", "길드 보스 도전", new Color(0.6f, 0.2f, 0.2f));
                _bossBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 46);
            }

            if (_raidBtn == null)
            {
                _raidBtn = CreateButton(panel, "RaidBtn", "토벌전 (주 1회)", new Color(0.5f, 0.3f, 0.15f));
                _raidBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 46);
            }

            if (_resultText == null)
            {
                var go = CreateText(panel, "Result", "", 15, FontStyles.Normal, new Color(0.9f, 0.95f, 0.9f));
                _resultText = go.GetComponent<TMP_Text>();
                _resultText.richText = true;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
            }

            if (_createBtn == null)
            {
                _createBtn = CreateButton(panel, "CreateBtn", "길드 창설 (500 루비)", new Color(0.2f, 0.5f, 0.3f));
                _createBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 46);
            }

            if (_closeBtn == null)
            {
                _closeBtn = CreateButton(panel, "CloseBtn", "닫기", new Color(0.4f, 0.3f, 0.3f));
                _closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
            }
        }

        private static GameObject CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = color;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return btn;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
