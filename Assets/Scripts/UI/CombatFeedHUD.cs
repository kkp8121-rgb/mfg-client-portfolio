using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 전투 로그 HUD. 경험치바 위에 정적 로그를 표시.
    /// 최신 로그가 맨 아래, 오래된 로그가 위로 밀려나는 구조.
    /// 일정 시간 후 자동으로 페이드 아웃.
    /// </summary>
    public class CombatFeedHUD : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int _maxEntries = 5;
        [SerializeField] private float _displayDuration = 4f;
        [SerializeField] private float _entryHeight = 22f;
        [SerializeField] private int _fontSize = 13;
        [SerializeField] private float _bgAlpha = 0.25f;

        private RectTransform _container;
        private TMP_Text[] _texts;
        private CanvasGroup[] _groups;
        private RectTransform[] _rects;
        private float[] _timers;
        private string[] _messages;
        private int _writeIndex;
        private Image _backgroundImage;

        private static readonly Color ColorExp = new Color(0.3f, 0.9f, 0.95f, 1f);
        private static readonly Color ColorGold = new Color(1f, 0.84f, 0f, 1f);
        private static readonly Color ColorKill = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color ColorLevelUp = new Color(1f, 1f, 0.2f, 1f);
        private static readonly Color ColorRuby = new Color(0.9f, 0.2f, 0.4f, 1f);
        private static readonly Color ColorBlueDiamond = new Color(0.6f, 0.8f, 1f, 1f);
        private static readonly Color ColorDefault = new Color(0.85f, 0.85f, 0.85f, 1f);

        private void Awake()
        {
            EnsureComponents();
            _container = GetComponent<RectTransform>();
            InitSlots();
        }

        private void OnEnable()
        {
            // ExpGainedEvent, GoldGainedEvent는 HudUI에서 처리 (중복 방지)
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<LootDroppedEvent>.Subscribe(OnLootDropped);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<LootDroppedEvent>.Unsubscribe(OnLootDropped);
        }

        private void InitSlots()
        {
            _texts = new TMP_Text[_maxEntries];
            _groups = new CanvasGroup[_maxEntries];
            _rects = new RectTransform[_maxEntries];
            _timers = new float[_maxEntries];
            _messages = new string[_maxEntries];

            for (int i = 0; i < _maxEntries; i++)
            {
                var entryObj = new GameObject($"Log_{i}");
                entryObj.transform.SetParent(_container, false);

                var rect = entryObj.AddComponent<RectTransform>();
                // 아래부터 위로 쌓임 (slot 0 = 맨 아래)
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(0f, _entryHeight);
                rect.anchoredPosition = new Vector2(0f, i * _entryHeight);

                var bg = entryObj.AddComponent<Image>();
                var tmEntry = UIThemeManager.Instance;
                if (tmEntry != null && tmEntry.FrameBackground != null)
                {
                    bg.sprite = tmEntry.FrameBackground;
                    bg.type = Image.Type.Sliced;
                    bg.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);
                }
                else bg.color = new Color(0f, 0f, 0f, _bgAlpha);
                bg.raycastTarget = false;

                var group = entryObj.AddComponent<CanvasGroup>();
                group.alpha = 0f;

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(entryObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.offsetMin = new Vector2(6f, 0f);
                textRect.offsetMax = new Vector2(-4f, 0f);

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = _fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;

                _texts[i] = tmp;
                _groups[i] = group;
                _rects[i] = rect;
            }
        }

        private void OnExpGained(ExpGainedEvent evt)
        {
            if (evt.Amount <= 0) return;
            AddLog($"+{FormatNumber(evt.Amount)} 경험치", ColorExp);
        }

        private void OnGoldGained(GoldGainedEvent evt)
        {
            if (evt.Amount <= 0) return;
            AddLog($"+{FormatNumber(evt.Amount)} 골드", ColorGold);
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            AddLog("몬스터 처치!", ColorKill);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            AddLog($"★ 레벨 업! Lv.{evt.CurrentLevel}", ColorLevelUp);
        }

        private void OnLootDropped(LootDroppedEvent evt)
        {
            string currencyName = DisplayNameUtils.GetCurrencyDisplayName(evt.Type);
            AddLog($"+{FormatNumber(evt.Amount)} {currencyName}", GetCurrencyColor(evt.Type));
        }

        private void AddLog(string message, Color color)
        {
            // 모든 슬롯을 한 칸 위로 밀기 (slot[i] = slot[i-1]의 내용)
            for (int i = _maxEntries - 1; i > 0; i--)
            {
                _texts[i].text = _texts[i - 1].text;
                _texts[i].color = _texts[i - 1].color;
                _groups[i].alpha = _groups[i - 1].alpha;
                _timers[i] = _timers[i - 1];
            }

            // 맨 아래 슬롯에 새 로그
            _texts[0].text = message;
            _texts[0].color = color;
            _groups[0].alpha = 1f;
            _timers[0] = _displayDuration;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool hasVisibleEntry = false;

            // 패널이 열려있으면 전투 로그 숨기기 (패널 위에 렌더링되는 문제 방지)
            bool isPanelOpen = UIState.IsAnyPanelOpen;

            for (int i = 0; i < _maxEntries; i++)
            {
                if (isPanelOpen)
                {
                    _groups[i].alpha = 0f;
                    continue;
                }

                if (_timers[i] <= 0f)
                {
                    if (_groups[i].alpha > 0f)
                        _groups[i].alpha = Mathf.Max(0f, _groups[i].alpha - dt * 2f);
                }
                else
                {
                    _timers[i] -= dt;

                    // 마지막 1초는 서서히 페이드
                    if (_timers[i] < 1f)
                        _groups[i].alpha = Mathf.Max(0f, _timers[i]);
                }

                if (_groups[i].alpha > 0.01f)
                    hasVisibleEntry = true;
            }

            // 로그가 없거나 패널 열려있으면 배경 숨기기
            if (_backgroundImage != null)
                _backgroundImage.enabled = hasVisibleEntry && !isPanelOpen;
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            var rt = transform as RectTransform;
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>() as RectTransform;

            // 좌측 벽, 화면 1/3 폭
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.33f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(8f, 270f);
            rt.sizeDelta = new Vector2(0f, _maxEntries * _entryHeight + 4f);

            // 은은한 반투명 배경 (좌측 1/3만) — 로그가 없을 때 숨김
            var bg = GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.3f);
            bg.raycastTarget = false;
            _backgroundImage = bg;
            _backgroundImage.enabled = false; // 초기 상태: 숨김
        }

        private Color GetCurrencyColor(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Gold: return ColorGold;
                case CurrencyType.Ruby: return ColorRuby;
                case CurrencyType.BlueDiamond: return ColorBlueDiamond;
                default: return ColorDefault;
            }
        }

        private string FormatNumber(long value)
        {
            return NumberFormatter.FormatKorean(value);
        }
    }
}
