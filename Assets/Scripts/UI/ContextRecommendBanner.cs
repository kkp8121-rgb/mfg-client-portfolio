using System.Collections.Generic;
using DG.Tweening;
using MkLike.Dungeon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 전투 화면 상황별 추천 배너.
    /// 5초 간격으로 레드닷 상태/던전 등을 분석하여 추천 메시지를 로테이션한다.
    /// 배너 클릭 시 해당 패널을 연다.
    /// </summary>
    public class ContextRecommendBanner : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private RectTransform _bannerRoot;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Button _bannerButton;

        [Header("설정")]
        [SerializeField] private float _rotationInterval = 5f;
        [SerializeField] private float _slideDistance = 60f;
        [SerializeField] private float _slideDuration = 0.3f;

        private readonly List<BannerEntry> _entries = new();
        private int _currentIndex;
        private float _timer;
        private bool _isTransitioning;
        private Tween _outTween;
        private Tween _inTween;

        private struct BannerEntry
        {
            public string Message;
            public string PanelName;
        }

        private void Awake()
        {
            // 추천 배너는 아직 콘텐츠가 부족하여 기본 비활성화
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_entries.Count == 0)
            {
                _timer += Time.deltaTime;
                if (_timer >= _rotationInterval)
                {
                    _timer = 0f;
                    RefreshEntries();
                    UpdateVisibility();
                }
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _rotationInterval && !_isTransitioning)
            {
                _timer = 0f;
                RefreshEntries();

                if (_entries.Count == 0)
                {
                    UpdateVisibility();
                    return;
                }

                int nextIndex = (_currentIndex + 1) % _entries.Count;
                TransitionTo(nextIndex);
            }
        }

        private void RefreshEntries()
        {
            _entries.Clear();

            var redDot = RedDotManager.Instance;

            // 장비 강화 가능
            if (redDot != null && redDot.HasRedDot(RedDotCategory.Equipment))
            {
                int cpGain = EstimateEquipmentCpGain();
                string msg = cpGain > 0
                    ? $"장비 강화 가능! 공격력 +{cpGain} 예상"
                    : "더 좋은 장비를 장착할 수 있습니다!";
                _entries.Add(new BannerEntry { Message = msg, PanelName = "Equipment" });
            }

            // 던전 열쇠 보유
            if (redDot != null && redDot.HasRedDot(RedDotCategory.Dungeon))
            {
                int keys = DungeonManager.Instance != null ? DungeonManager.Instance.CurrentKeys : 0;
                if (keys > 0)
                {
                    string dungeonRec = GetDungeonRecommendation();
                    _entries.Add(new BannerEntry
                    {
                        Message = $"던전 열쇠 {keys}개! {dungeonRec}",
                        PanelName = "Dungeon"
                    });
                }
            }

            // 스킬포인트 미분배
            if (redDot != null && redDot.HasRedDot(RedDotCategory.Skill))
            {
                _entries.Add(new BannerEntry
                {
                    Message = "스킬포인트 미분배!",
                    PanelName = "Character"
                });
            }

            // 퀘스트 보상 미수령
            if (redDot != null && redDot.HasRedDot(RedDotCategory.Quest))
            {
                _entries.Add(new BannerEntry
                {
                    Message = "퀘스트 보상 미수령!",
                    PanelName = "Quest"
                });
            }

            // 인덱스 범위 보정
            if (_entries.Count > 0 && _currentIndex >= _entries.Count)
                _currentIndex = 0;
        }

        private void UpdateVisibility()
        {
            bool hasEntries = _entries.Count > 0;

            // 배경과 콘텐츠만 숨김 (자기 자신은 SetActive(false) 하면 Update 안 돌아서 복구 불가)
            if (_backgroundImage != null)
                _backgroundImage.enabled = hasEntries;
            if (_messageText != null)
                _messageText.enabled = hasEntries;
            if (_bannerButton != null)
                _bannerButton.interactable = hasEntries;

            if (hasEntries && _messageText != null)
            {
                _currentIndex = 0;
                _messageText.text = _entries[0].Message;
            }
        }

        private void TransitionTo(int nextIndex)
        {
            if (_messageText == null || _entries.Count == 0) return;

            _isTransitioning = true;
            KillTweens();

            // 현재 메시지 왼쪽으로 슬라이드 아웃
            var startPos = _messageText.rectTransform.anchoredPosition;
            float targetOutX = startPos.x - _slideDistance;

            _outTween = DOTween.To(
                () => _messageText.rectTransform.anchoredPosition.x,
                x =>
                {
                    var p = _messageText.rectTransform.anchoredPosition;
                    p.x = x;
                    _messageText.rectTransform.anchoredPosition = p;
                },
                targetOutX, _slideDuration * 0.5f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    // 새 메시지로 교체 후 오른쪽에서 슬라이드 인
                    _currentIndex = nextIndex;
                    _messageText.text = _entries[_currentIndex].Message;

                    var p = _messageText.rectTransform.anchoredPosition;
                    p.x = startPos.x + _slideDistance;
                    _messageText.rectTransform.anchoredPosition = p;

                    _inTween = DOTween.To(
                        () => _messageText.rectTransform.anchoredPosition.x,
                        x2 =>
                        {
                            var p2 = _messageText.rectTransform.anchoredPosition;
                            p2.x = x2;
                            _messageText.rectTransform.anchoredPosition = p2;
                        },
                        startPos.x, _slideDuration * 0.5f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true)
                        .SetLink(gameObject)
                        .OnComplete(() => _isTransitioning = false);
                });
        }

        private void OnBannerClicked()
        {
            if (_entries.Count == 0 || _currentIndex >= _entries.Count) return;

            string panelName = _entries[_currentIndex].PanelName;
            if (string.IsNullOrEmpty(panelName)) return;

            // BasePopup 기반 패널은 UIManager.OpenPopup으로 오픈
            if (UIManager.Instance != null)
            {
                switch (panelName)
                {
                    case "Dungeon":
                        UIManager.Instance.OpenPopup<DungeonPanel>("DungeonPanel");
                        break;
                    case "Equipment":
                        // EquipmentPanel은 CharacterPanel의 서브탭(인덱스 1)이므로
                        // OpenPopup 대신 CharacterPanel을 열고 서브탭 전환
                        UIManager.Instance.OpenCharacterPanelTab(1);
                        break;
                    case "Character":
                        UIManager.Instance.OpenCharacterPanelTab(0); // 스탯 탭
                        break;
                    case "Quest":
                        UIManager.Instance.OpenPopup<BasePopup>("QuestPanel");
                        break;
                    default:
                        Debug.LogWarning($"[ContextRecommendBanner] 알 수 없는 패널: {panelName}");
                        break;
                }
            }
        }

        private int EstimateEquipmentCpGain()
        {
            if (RecommendationManager.Instance == null) return 0;

            var recs = RecommendationManager.Instance.GetRecommendedEquipment();
            if (recs == null || recs.Count == 0) return 0;

            return recs[0].CpGain;
        }

        private string GetDungeonRecommendation()
        {
            if (RecommendationManager.Instance == null) return "던전 추천";

            var rec = RecommendationManager.Instance.GetRecommendedDungeon();
            return string.IsNullOrEmpty(rec.Reason) ? "던전 추천" : rec.Reason;
        }

        private void KillTweens()
        {
            _outTween?.Kill();
            _outTween = null;
            _inTween?.Kill();
            _inTween = null;
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

            // _bannerRoot: 하단 중앙 작은 배너 (화면 가리지 않도록)
            if (_bannerRoot == null)
            {
                _bannerRoot = GetComponent<RectTransform>();
                if (_bannerRoot == null)
                    _bannerRoot = gameObject.AddComponent<RectTransform>();
            }
            // 스킬바 위, 하단 중앙에 작은 띠 형태로 배치
            _bannerRoot.anchorMin = new Vector2(0.15f, 0f);
            _bannerRoot.anchorMax = new Vector2(0.85f, 0f);
            _bannerRoot.pivot = new Vector2(0.5f, 0f);
            _bannerRoot.anchoredPosition = new Vector2(0f, 290f);
            _bannerRoot.sizeDelta = new Vector2(0f, 32f);

            // 루트에 배경 Image (은은한 반투명)
            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
                if (_backgroundImage == null)
                    _backgroundImage = gameObject.AddComponent<Image>();

                var tm = UIThemeManager.Instance;
                if (tm != null)
                    tm.ApplyFrameBackground(_backgroundImage);
                else
                    _backgroundImage.color = new Color(0.1f, 0.1f, 0.18f, 0.75f);

                _backgroundImage.raycastTarget = true;
            }

            // _bannerButton: 위젯 전체를 덮는 투명 버튼 (어디를 클릭해도 반응)
            if (_bannerButton == null)
            {
                var btnGo = CreateUIChild("BannerButton");
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = Vector2.zero;
                btnRt.anchorMax = Vector2.one;
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.anchoredPosition = Vector2.zero;
                btnRt.sizeDelta = Vector2.zero;

                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0f, 0f, 0f, 0.01f); // 거의 투명한 클릭 영역
                _bannerButton = btnGo.AddComponent<Button>();
                _bannerButton.targetGraphic = btnImg;

                var btnColors = _bannerButton.colors;
                btnColors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
                btnColors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
                _bannerButton.colors = btnColors;
            }

            // _messageText: 가운데 정렬, 적절한 크기의 텍스트
            if (_messageText == null)
            {
                var txtGo = CreateUIChild("MessageText");
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.pivot = new Vector2(0.5f, 0.5f);
                txtRt.anchoredPosition = Vector2.zero;
                txtRt.sizeDelta = new Vector2(-20f, 0f); // 좌우 10px 패딩

                _messageText = txtGo.AddComponent<TextMeshProUGUI>();
                _messageText.fontSize = 14f;
                _messageText.fontStyle = FontStyles.Bold;
                _messageText.alignment = TextAlignmentOptions.Center;
                _messageText.color = Color.white;
                _messageText.raycastTarget = false;
            }
        }

        private GameObject CreateUIChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void OnDestroy()
        {
            KillTweens();
            if (_messageText != null)
                DOTween.Kill(_messageText.rectTransform);
        }
    }
}
