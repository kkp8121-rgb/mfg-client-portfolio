using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Data;
using MkLike.Equipment;
using MkLike.Utils;
using System.Collections.Generic;

namespace MkLike.UI
{
    /// <summary>
    /// 상점 패널. 동료/장비 가챠 + 루비 잔고 표시.
    /// 닫기 버튼으로 TabController를 통해 패널을 닫을 수 있다.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        [Header("닫기")]
        [SerializeField] private Button _closeButton;

        [Header("서브 탭")]
        [SerializeField] private Button[] subTabButtons;
        [SerializeField] private Image[] subTabBgs;
        [SerializeField] private GameObject[] subTabContents;

        [Header("루비 표시")]
        [SerializeField] private TextMeshProUGUI rubyText;

        [Header("동료 가챠")]
        [SerializeField] private Button companionPull1Btn;
        [SerializeField] private Button companionPull10Btn;
        [SerializeField] private TextMeshProUGUI companionPull1CostText;
        [SerializeField] private TextMeshProUGUI companionPull10CostText;
        [SerializeField] private TextMeshProUGUI companionPityText;
        [SerializeField] private Transform companionResultArea;

        [Header("장비 가챠")]
        [SerializeField] private Button equipmentPull1Btn;
        [SerializeField] private Button equipmentPull10Btn;
        [SerializeField] private TextMeshProUGUI equipmentPull1CostText;
        [SerializeField] private TextMeshProUGUI equipmentPull10CostText;
        [SerializeField] private TextMeshProUGUI equipmentPityText;
        [SerializeField] private Transform equipmentResultArea;

        [Header("무기 가챠")]
        [SerializeField] private Button weaponPull1Btn;
        [SerializeField] private Button weaponPull10Btn;
        [SerializeField] private TextMeshProUGUI weaponPull1CostText;
        [SerializeField] private TextMeshProUGUI weaponPull10CostText;
        [SerializeField] private TextMeshProUGUI weaponPityText;
        [SerializeField] private Transform weaponResultArea;

        // 유물 가챠: 2026-04-20 유물 시스템 완전 제거

        [Header("색상")]
        [SerializeField] private Color subTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        [SerializeField] private Color subTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);

        private int _currentSubTab = -1;
        private GameObject _tenPullPopup;
        private CancellationTokenSource _flashCts;

        /// <summary>뽑기 결과 팝업이 열려있으면 true. AcquisitionShortcutPopup에서 참조한다.</summary>
        public static bool IsPullPopupOpen { get; private set; }

        // 등급별 색상
        private static readonly Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.7f, 0.7f, 0.7f) },
            { "Rare", new Color(0.3f, 0.6f, 1f) },
            { "Epic", new Color(0.7f, 0.3f, 0.9f) },
            { "Unique", new Color(1f, 0.85f, 0.1f) },
            { "Legendary", new Color(1f, 0.5f, 0.1f) },
            { "Mythic", new Color(1f, 0.2f, 0.3f) },
        };

        private bool _initialized;

        private void Start()
        {
            // 에디터 와이어링이 누락된 경우 런타임에서 자식 탐색으로 복구
            EnsureReferences();

            // 배경 테마 적용
            ApplyShopTheme();

            if (subTabButtons != null)
            {
                for (int i = 0; i < subTabButtons.Length; i++)
                {
                    if (subTabButtons[i] == null) continue;
                    int idx = i;
                    subTabButtons[i].onClick.AddListener(() => SwitchSubTab(idx));
                }
            }

            if (equipmentPull1Btn != null)
                equipmentPull1Btn.onClick.AddListener(() => DoPull(GachaPoolType.Equipment, false));
            if (equipmentPull10Btn != null)
                equipmentPull10Btn.onClick.AddListener(() => DoPull(GachaPoolType.Equipment, true));
            if (weaponPull1Btn != null)
                weaponPull1Btn.onClick.AddListener(() => DoPull(GachaPoolType.Weapon, false));
            if (weaponPull10Btn != null)
                weaponPull10Btn.onClick.AddListener(() => DoPull(GachaPoolType.Weapon, true));
            // 유물 가챠: 2026-04-20 유물 시스템 완전 제거

            // 닫기 버튼 와이어링
            EnsureCloseButton();

            _initialized = true;
            SwitchSubTab(0);
        }

        private void EnsureCloseButton()
        {
            if (_closeButton == null)
            {
                // 이름으로 검색
                var buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].name.Contains("Close") || buttons[i].name.Contains("close"))
                    {
                        _closeButton = buttons[i];
                        break;
                    }
                }
            }

            // 없으면 런타임 생성 — 크고 눈에 띄는 빨간 닫기 버튼
            if (_closeButton == null)
            {
                var btnObj = new GameObject("CloseButton", typeof(RectTransform));
                btnObj.transform.SetParent(transform, false);
                var rect = btnObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-12f, -12f);
                rect.sizeDelta = new Vector2(56f, 56f);

                var img = btnObj.AddComponent<Image>();
                img.color = new Color(0.85f, 0.12f, 0.12f, 1f); // 선명한 빨간색

                // UIThemeManager 닫기 스프라이트 적용
                var theme = UIThemeManager.Instance;
                if (theme != null && theme.CloseButton != null)
                {
                    img.sprite = theme.CloseButton;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                }

                // Canvas 내 최상위 렌더링 보장
                var canvas = btnObj.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 999;
                btnObj.AddComponent<GraphicRaycaster>();

                var tmpObj = new GameObject("X", typeof(RectTransform));
                tmpObj.transform.SetParent(btnObj.transform, false);
                var tmpRect = tmpObj.GetComponent<RectTransform>();
                tmpRect.anchorMin = Vector2.zero;
                tmpRect.anchorMax = Vector2.one;
                tmpRect.sizeDelta = Vector2.zero;
                var tmpText = tmpObj.AddComponent<TextMeshProUGUI>();
                tmpText.text = "X";
                tmpText.fontSize = 30f;
                tmpText.fontStyle = FontStyles.Bold;
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.color = Color.white;

                _closeButton = btnObj.AddComponent<Button>();
            }

            // X 버튼 위치를 우상단 코너에 강제 고정 (소환 설명 텍스트와 겹침 방지)
            var closeRt = _closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-10f, -10f);
            closeRt.sizeDelta = new Vector2(50f, 50f);

            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnCloseClicked()
        {
            var tabCtrl = GetComponentInParent<TabController>();
            if (tabCtrl != null)
            {
                tabCtrl.CloseAll();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureReferences()
        {
            if (rubyText == null)
            {
                var allTmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in allTmps)
                {
                    if (tmp.gameObject.name == "RubyText")
                    {
                        rubyText = tmp;
                        break;
                    }
                }
            }

            if (companionResultArea == null)
                companionResultArea = FindContentChild("Content_Companion", "Content");
            if (equipmentResultArea == null)
                equipmentResultArea = FindContentChild("Content_Equipment", "Content");
            if (weaponResultArea == null)
                weaponResultArea = FindContentChild("Content_Weapon", "Content");

            // 천장 텍스트 참조 복구
            EnsurePityTexts();
        }

        private void EnsurePityTexts()
        {
            if (companionPityText == null)
                companionPityText = FindPityTextInContent("Content_Companion");
            if (equipmentPityText == null)
                equipmentPityText = FindPityTextInContent("Content_Equipment");
            if (weaponPityText == null)
                weaponPityText = FindPityTextInContent("Content_Weapon");
        }

        private TextMeshProUGUI FindPityTextInContent(string parentName)
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            bool foundParent = false;
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].gameObject.name == parentName)
                {
                    foundParent = true;
                    var tmps = allTransforms[i].GetComponentsInChildren<TextMeshProUGUI>(true);
                    Debug.Log($"[ShopPanel] FindPityTextInContent('{parentName}') — parent found, TMP count={tmps.Length}");
                    for (int j = 0; j < tmps.Length; j++)
                    {
                        Debug.Log($"[ShopPanel]   TMP[{j}]: name='{tmps[j].gameObject.name}', text='{tmps[j].text}', active={tmps[j].gameObject.activeInHierarchy}");
                        if (tmps[j].gameObject.name.Contains("Pity") || tmps[j].text.Contains("천장"))
                        {
                            Debug.Log($"[ShopPanel]   → MATCH! returning '{tmps[j].gameObject.name}'");
                            return tmps[j];
                        }
                    }
                    break;
                }
            }
            if (!foundParent)
                Debug.LogWarning($"[ShopPanel] FindPityTextInContent('{parentName}') — parent NOT found among {allTransforms.Length} transforms");
            return null;
        }

        private Transform FindContentChild(string parentName, string childName)
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.gameObject.name == parentName)
                {
                    var scrollContent = t.GetComponentInChildren<ScrollRect>(true);
                    if (scrollContent != null && scrollContent.content != null)
                        return scrollContent.content;

                    var child = t.Find(childName);
                    if (child != null) return child;
                }
            }
            return null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            if (_initialized) RefreshAll();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnDestroy()
        {
            transform.DOKill();

            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = null;

            if (_tenPullPopup != null)
                Destroy(_tenPullPopup);

            IsPullPopupOpen = false;
        }

        /// <summary>ShopPanel 배경 + 서브탭 콘텐츠에 테마 스프라이트 적용</summary>
        private void ApplyShopTheme()
        {
            var tm = UIThemeManager.Instance;
            if (tm == null) return;

            // 자신의 배경 Image
            var bg = GetComponent<Image>();
            if (bg != null && bg.sprite == null)
                tm.ApplyPanelBackground(bg);

            // 서브탭 콘텐츠 배경
            if (subTabContents != null)
            {
                for (int i = 0; i < subTabContents.Length; i++)
                {
                    if (subTabContents[i] == null) continue;
                    var contentBg = subTabContents[i].GetComponent<Image>();
                    if (contentBg != null && contentBg.sprite == null && contentBg.color.a < 0.1f)
                    {
                        tm.ApplyFrameBackground(contentBg);
                    }
                }
            }
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            if (evt.Type == CurrencyType.Ruby)
                RefreshRuby();
        }

        // ── 서브 탭 ──

        public void SwitchSubTab(int index)
        {
            if (index == _currentSubTab) return;
            if (subTabContents == null || subTabContents.Length == 0) return;
            if (index < 0 || index >= subTabContents.Length) return;

            for (int i = 0; i < subTabContents.Length; i++)
            {
                if (subTabContents[i] == null) continue;
                bool active = i == index;
                subTabContents[i].SetActive(active);

                if (subTabBgs != null && i < subTabBgs.Length && subTabBgs[i] != null)
                {
                    var bg = subTabBgs[i];
                    ColorTweenHelper.To(bg, active ? subTabSelected : subTabNormal, 0.15f)
                        .SetUpdate(true);
                }
            }

            var activeContent = subTabContents[index];
            if (activeContent != null)
            {
                var cg = activeContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = activeContent.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
            }

            _currentSubTab = index;
            RefreshAll();
        }

        // ── 가챠 실행 ──

        private void DoPull(GachaPoolType poolType, bool isTenPull)
        {
            // 서버/로컬 자동 분기 경로로 위임 (HasAuth==true면 서버 권위, false면 로컬 fallback)
            DoPullAsync(poolType, isTenPull).Forget();
        }

        private async UniTaskVoid DoPullAsync(GachaPoolType poolType, bool isTenPull)
        {
            if (GachaManager.Instance == null) return;

            var resultArea = poolType switch
            {
                GachaPoolType.Weapon => weaponResultArea,
                _ => equipmentResultArea
            };

            _resultIndex = 0;
            if (resultArea != null)
                ClearChildren(resultArea);

            // Pull 전에 플래그 설정 — 이벤트 핸들러보다 먼저 설정해야 이중 팝업 방지
            IsPullPopupOpen = true;

            // 서버 요청 중 버튼 연타 방지
            SetPullButtonsInteractable(poolType, false);
            var ct = this.GetCancellationTokenOnDestroy();

            try
            {
                if (isTenPull)
                {
                    var results = await GachaManager.Instance.PullTenAsync(poolType, ct);
                    if (results == null || results.Count == 0)
                    {
                        IsPullPopupOpen = false;
                        FlashRubyInsufficient();
                        return;
                    }

                    // 10연차: 전체 결과를 오버레이 팝업으로 표시
                    ShowTenPullPopup(results, poolType);
                }
                else
                {
                    var result = await GachaManager.Instance.PullAsync(poolType, ct);
                    if (result == null)
                    {
                        IsPullPopupOpen = false;
                        FlashRubyInsufficient();
                        return;
                    }

                    // 항상 오버레이 팝업으로 표시 (resultArea 유무 관계없이)
                    ShowSinglePullPopup(result, poolType);
                }

                // 뽑기 버튼 펀치 연출
                var btn = poolType switch
                {
                    GachaPoolType.Weapon => isTenPull ? weaponPull10Btn : weaponPull1Btn,
                    _ => isTenPull ? equipmentPull10Btn : equipmentPull1Btn
                };
                if (btn != null)
                {
                    btn.transform.DOKill();
                    btn.transform.localScale = Vector3.one;
                    btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 6, 0.5f).SetUpdate(true).SetLink(btn.gameObject);
                }

                RefreshAll();
            }
            catch (System.OperationCanceledException)
            {
                // 씬 전환/파괴 시
                IsPullPopupOpen = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShopPanel] 가챠 실행 실패: {ex.Message}");
                IsPullPopupOpen = false;
                FlashRubyInsufficient();
            }
            finally
            {
                SetPullButtonsInteractable(poolType, true);
            }
        }

        private void SetPullButtonsInteractable(GachaPoolType poolType, bool interactable)
        {
            switch (poolType)
            {
                case GachaPoolType.Weapon:
                    if (weaponPull1Btn != null) weaponPull1Btn.interactable = interactable;
                    if (weaponPull10Btn != null) weaponPull10Btn.interactable = interactable;
                    break;
                default:
                    if (equipmentPull1Btn != null) equipmentPull1Btn.interactable = interactable;
                    if (equipmentPull10Btn != null) equipmentPull10Btn.interactable = interactable;
                    break;
            }
        }

        private int _resultIndex;

        // ── 단일 뽑기 결과 (기존 리스트 형태) ──

        private void AddResultItem(Transform parent, GachaEntry entry)
        {
            if (parent == null || entry == null) return;

            Color gradeColor = GetGradeColor(entry.grade);
            bool isHighGrade = IsHighGrade(entry.grade);
            float delay = _resultIndex * 0.08f;

            var itemObj = new GameObject(entry.itemId);
            itemObj.transform.SetParent(parent, false);
            var rt = itemObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 60);

            var bg = itemObj.AddComponent<Image>();
            bg.color = isHighGrade
                ? new Color(gradeColor.r * 0.15f, gradeColor.g * 0.15f, gradeColor.b * 0.15f, 0.95f)
                : new Color(0.18f, 0.16f, 0.14f, 0.9f);

            var hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.spacing = 12;
            hlg.padding = new RectOffset(16, 16, 4, 4);

            var markerObj = new GameObject("GradeMarker");
            markerObj.transform.SetParent(itemObj.transform, false);
            var markerImg = markerObj.AddComponent<Image>();
            markerImg.color = gradeColor;
            markerObj.GetComponent<RectTransform>().sizeDelta = new Vector2(8, 48);

            var equipData = GetEquipmentIcon(entry.itemId);
            if (equipData != null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(itemObj.transform, false);
                iconObj.AddComponent<RectTransform>().sizeDelta = new Vector2(48, 48);
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = equipData;
                iconImg.preserveAspect = true;
            }

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(itemObj.transform, false);
            nameObj.AddComponent<RectTransform>().sizeDelta = new Vector2(equipData != null ? 170 : 220, 50);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = FormatItemName(entry.itemId);
            nameTmp.fontSize = 22;
            nameTmp.color = gradeColor;
            nameTmp.alignment = TextAlignmentOptions.Left;

            var gradeObj = new GameObject("Grade");
            gradeObj.transform.SetParent(itemObj.transform, false);
            gradeObj.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 50);
            var gradeTmp = gradeObj.AddComponent<TextMeshProUGUI>();
            gradeTmp.text = DisplayNameUtils.GetGradeDisplayName(entry.grade);
            gradeTmp.fontSize = 20;
            gradeTmp.color = gradeColor;
            gradeTmp.alignment = TextAlignmentOptions.Right;
            gradeTmp.fontStyle = FontStyles.Bold;

            var cg = itemObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            rt.localScale = new Vector3(0.5f, 0f, 1f);

            var seq = DOTween.Sequence()
                .AppendInterval(delay)
                .Append(rt.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.15f))
                .SetUpdate(true)
                .SetLink(itemObj);

            if (isHighGrade)
            {
                seq.AppendCallback(() =>
                {
                    ColorTweenHelper.To(bg,
                        new Color(gradeColor.r * 0.3f, gradeColor.g * 0.3f, gradeColor.b * 0.3f, 0.95f), 0.2f)
                        .SetUpdate(true)
                        .SetLoops(2, LoopType.Yoyo);
                    rt.DOPunchScale(Vector3.one * 0.08f, 0.3f, 4, 0.5f).SetUpdate(true).SetLink(rt.gameObject);
                });
            }

            _resultIndex++;
        }

        // ── 10연차 결과 팝업 ──

        /// <summary>
        /// 10연차 결과를 풀스크린 오버레이 팝업으로 표시한다.
        /// 5열 2행 그리드로 카드 배치, 등급별 배경색 + 이름 + NEW!/중복 표시.
        /// </summary>
        private void ShowTenPullPopup(List<GachaEntry> results, GachaPoolType poolType)
        {
            if (_tenPullPopup != null)
                Destroy(_tenPullPopup);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 오버레이 루트
            var popupObj = new GameObject("TenPullPopup", typeof(RectTransform));
            popupObj.transform.SetParent(canvas.transform, false);
            var popupRt = popupObj.GetComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.sizeDelta = Vector2.zero;
            popupRt.anchoredPosition = Vector2.zero;

            // 반투명 배경 (클릭으로 닫기)
            var dimObj = new GameObject("Dim", typeof(RectTransform));
            dimObj.transform.SetParent(popupObj.transform, false);
            var dimRt = dimObj.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.sizeDelta = Vector2.zero;
            var dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0f);
            var dimBtn = dimObj.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseTenPullPopup);
            ColorTweenHelper.To(dimImg,
                new Color(0f, 0f, 0f, 0.75f), 0.3f).SetUpdate(true);

            // 중앙 패널 — 상하 여백 넉넉하게 확보 (확인 버튼 잘림 방지)
            var panelObj = new GameObject("Panel", typeof(RectTransform));
            panelObj.transform.SetParent(popupObj.transform, false);
            var panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.02f, 0.04f);
            panelRt.anchorMax = new Vector2(0.98f, 0.96f);
            panelRt.sizeDelta = Vector2.zero;
            var panelBg = panelObj.AddComponent<Image>();
            var tm = UIThemeManager.Instance;
            if (tm != null) tm.ApplyPanelBackground(panelBg);
            else panelBg.color = new Color(0.2f, 0.18f, 0.16f, 0.97f);

            // 제목 — 상단 8% 영역
            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(panelObj.transform, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.92f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.sizeDelta = Vector2.zero;
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            string poolName = poolType switch
            {
                GachaPoolType.Weapon => "무기",
                _ => "장비"
            };
            titleTmp.text = $"{poolName} 10연차 결과";
            titleTmp.fontSize = 28;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(1f, 0.9f, 0.6f, 1f);
            titleTmp.alignment = TextAlignmentOptions.Center;

            // 스크롤 영역 (좁은 뷰포트에서 10연차 결과 잘림 방지)
            var scrollObj = new GameObject("ScrollArea", typeof(RectTransform));
            scrollObj.transform.SetParent(panelObj.transform, false);
            var scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, 0.16f);
            scrollRt.anchorMax = new Vector2(0.98f, 0.91f);
            scrollRt.sizeDelta = Vector2.zero;
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollObj.AddComponent<Image>().color = Color.clear; // Mask 필요
            scrollObj.AddComponent<Mask>().showMaskGraphic = false;

            // 그리드 영역 (5열 2행) — 동적 cellSize로 화면 크기 적응
            var gridObj = new GameObject("Grid", typeof(RectTransform));
            gridObj.transform.SetParent(scrollObj.transform, false);
            var gridRt = gridObj.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0f, 1f);
            gridRt.anchorMax = new Vector2(1f, 1f);
            gridRt.pivot = new Vector2(0.5f, 1f);
            gridRt.sizeDelta = new Vector2(0f, 0f); // ContentSizeFitter가 높이 결정
            var gridCsf = gridObj.AddComponent<ContentSizeFitter>();
            gridCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            gridCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = gridRt;
            var gridLayout = gridObj.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            gridLayout.spacing = new Vector2(4, 4);
            gridLayout.padding = new RectOffset(2, 2, 2, 2);
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            // 스크롤 영역 실제 크기 기반으로 cellSize 계산 (화면 크기 적응)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
            Canvas.ForceUpdateCanvases();
            float gridW = scrollRt.rect.width;
            float gridViewH = scrollRt.rect.height;
            Debug.Log($"[ShopPanel] ScrollArea rect — w={gridW}, h={gridViewH}, screen={Screen.width}x{Screen.height}");
            // fallback: 아직 레이아웃 안 됐으면 화면 비율로 추정
            if (gridW <= 0) gridW = Screen.width * 0.94f;
            if (gridViewH <= 0) gridViewH = Screen.height * 0.72f;
            float cellW = (gridW - gridLayout.padding.left - gridLayout.padding.right - gridLayout.spacing.x * 4) / 5f;
            float cellH = (gridViewH - gridLayout.padding.top - gridLayout.padding.bottom - gridLayout.spacing.y) / 2f;
            cellW = Mathf.Clamp(cellW, 60f, 160f);
            cellH = Mathf.Clamp(cellH, 100f, 220f);
            gridLayout.cellSize = new Vector2(cellW, cellH);

            // 그리드 RectTransform 너비를 스크롤 영역에 맞춤 (ContentSizeFitter가 높이만 관리)
            gridRt.sizeDelta = new Vector2(0f, gridRt.sizeDelta.y);
            Debug.Log($"[ShopPanel] CellSize calculated — cellW={cellW}, cellH={cellH}");

            // 결과 카드 생성 — 이전 보유 스냅샷 없이 중복 체크만 수행
            var ownedBefore = new HashSet<string>();
            var newlyAdded = new HashSet<string>();
            for (int i = 0; i < results.Count; i++)
            {
                try
                {
                    var entry = results[i];
                    if (entry == null)
                    {
                        Debug.LogError($"[ShopPanel] 10연차 카드 {i} — entry가 null");
                        continue;
                    }
                    string safeItemId = entry.itemId ?? $"unknown_{i}";
                    bool isNew = !ownedBefore.Contains(safeItemId) && !newlyAdded.Contains(safeItemId);
                    bool isDuplicate = ownedBefore.Contains(safeItemId) || newlyAdded.Contains(safeItemId);
                    if (isNew) newlyAdded.Add(safeItemId);

                    CreateTenPullCard(gridObj.transform, entry, i, isNew, isDuplicate, poolType);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ShopPanel] 10연차 카드 {i} 생성 실패: {ex.Message}\n{ex.StackTrace}");
                }
            }

            // 그리드 레이아웃 강제 리빌드 (WebGL에서 ContentSizeFitter가 즉시 반영 안 되는 문제 방지)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);
            Canvas.ForceUpdateCanvases();

            // fallback: ContentSizeFitter가 높이를 잡지 못했으면 수동 설정
            float totalH = cellH * 2 + gridLayout.spacing.y + gridLayout.padding.top + gridLayout.padding.bottom;
            if (gridRt.rect.height < totalH || gridRt.sizeDelta.y < totalH)
            {
                gridRt.sizeDelta = new Vector2(gridRt.sizeDelta.x, totalH);
                Debug.Log($"[ShopPanel] Grid height forced to {totalH} (was rect={gridRt.rect.height}, sizeDelta={gridRt.sizeDelta.y})");
            }

            // 2차 리빌드 — sizeDelta 변경 후 레이아웃 반영
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRt);
            Canvas.ForceUpdateCanvases();

            // 카드 상태 검증 로그
            for (int ci = 0; ci < gridObj.transform.childCount; ci++)
            {
                var child = gridObj.transform.GetChild(ci);
                var childCg = child.GetComponent<CanvasGroup>();
                Debug.Log($"[ShopPanel] Card_{ci} — alpha={childCg?.alpha ?? -1f}, scale={child.localScale}, active={child.gameObject.activeInHierarchy}, pos={child.position}");
            }

            Debug.Log($"[ShopPanel] 10연차 카드 생성 완료 — count={results.Count}, cellSize={gridLayout.cellSize}, gridH={gridRt.rect.height}, gridSizeDelta={gridRt.sizeDelta}, scrollRect={scrollRt.rect}");

            // 확인 버튼 — 하단 영역 넉넉히 확보 (최소 50px 이상 높이)
            var closeBtnObj = new GameObject("CloseBtn", typeof(RectTransform));
            closeBtnObj.transform.SetParent(panelObj.transform, false);
            var closeBtnRt = closeBtnObj.GetComponent<RectTransform>();
            closeBtnRt.anchorMin = new Vector2(0.2f, 0.02f);
            closeBtnRt.anchorMax = new Vector2(0.8f, 0.14f);
            closeBtnRt.sizeDelta = Vector2.zero;
            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            var closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            var tm2 = UIThemeManager.Instance;
            if (tm2 != null) tm2.ApplyConfirmButton(closeBtnImg);
            else closeBtnImg.color = new Color(0.35f, 0.30f, 0.25f, 1f);
            closeBtn.onClick.AddListener(CloseTenPullPopup);

            var closeTxtObj = new GameObject("Text", typeof(RectTransform));
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.sizeDelta = Vector2.zero;
            var closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
            closeTxt.text = "확인";
            closeTxt.fontSize = 24;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;

            // 팝업 스케일 팝인 연출
            panelObj.transform.localScale = Vector3.one * 0.8f;
            panelObj.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panelObj);

            _tenPullPopup = popupObj;
            IsPullPopupOpen = true;
        }

        /// <summary>
        /// 10연차 카드 1장 생성. 등급 배경 + 이름 + 등급 텍스트 + NEW!/중복 표시.
        /// </summary>
        private void CreateTenPullCard(Transform parent, GachaEntry entry, int index,
            bool isNew, bool isDuplicate, GachaPoolType poolType)
        {
            if (entry == null || parent == null) return;
            string grade = entry.grade ?? "Normal";
            Color gradeColor = GetGradeColor(grade);
            bool isHighGrade = IsHighGrade(grade);
            float delay = index * 0.06f;

            // 카드 루트
            var cardObj = new GameObject($"Card_{index}", typeof(RectTransform));
            cardObj.transform.SetParent(parent, false);

            // 카드 배경 — 밝은 크림색 베이스 + 등급 틴트 (스프라이트 미사용)
            var cardBg = cardObj.AddComponent<Image>();

            Color bgColor = new Color(
                Mathf.Lerp(0.92f, gradeColor.r, 0.18f),
                Mathf.Lerp(0.88f, gradeColor.g, 0.18f),
                Mathf.Lerp(0.85f, gradeColor.b, 0.18f),
                1f);

            cardBg.sprite = null;
            cardBg.color = bgColor;

            // 세로 레이아웃 — childControlHeight=true로 균등 배분
            var vlg = cardObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 1;
            vlg.padding = new RectOffset(3, 3, 3, 3);

            cardBg.raycastTarget = true;

            // 등급 상단 바 — 등급 색상 띠
            var gradeBarObj = new GameObject("GradeBar", typeof(RectTransform));
            gradeBarObj.transform.SetParent(cardObj.transform, false);
            var gradeBarImg = gradeBarObj.AddComponent<Image>();
            gradeBarImg.color = gradeColor;
            var gradeBarLe = gradeBarObj.AddComponent<LayoutElement>();
            gradeBarLe.preferredHeight = isHighGrade ? 6 : 4;
            gradeBarLe.flexibleHeight = 0;

            // 아이콘 영역 — 카드의 주요 영역, 유연하게 확장
            var iconAreaObj = new GameObject("IconArea", typeof(RectTransform));
            iconAreaObj.transform.SetParent(cardObj.transform, false);
            var iconAreaLe = iconAreaObj.AddComponent<LayoutElement>();
            iconAreaLe.preferredHeight = 40;
            iconAreaLe.flexibleHeight = 1; // 남는 공간 흡수

            string safeItemId = entry.itemId ?? $"unknown_{index}";
            Sprite icon = GetEquipmentIcon(safeItemId);

            if (icon != null)
            {
                var iconImg = iconAreaObj.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
            }
            else
            {
                // 아이콘 없으면 한국어 등급 첫 글자로 대체
                var initialTmp = iconAreaObj.AddComponent<TextMeshProUGUI>();
                string gradeKr = DisplayNameUtils.GetGradeDisplayName(grade);
                initialTmp.text = gradeKr.Length > 0 ? gradeKr[0].ToString() : "?";
                initialTmp.fontSize = 28;
                initialTmp.fontStyle = FontStyles.Bold;
                initialTmp.color = gradeColor;
                initialTmp.alignment = TextAlignmentOptions.Center;
            }

            // 이름
            var nameObj = new GameObject("Name", typeof(RectTransform));
            nameObj.transform.SetParent(cardObj.transform, false);
            var nameLe = nameObj.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 26;
            nameLe.flexibleHeight = 0;
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = FormatItemName(safeItemId);
            nameTmp.fontSize = 12;
            nameTmp.color = new Color(0.15f, 0.13f, 0.10f, 1f);
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.enableWordWrapping = true;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;

            // 등급 텍스트 (한국어)
            var gradeObj = new GameObject("Grade", typeof(RectTransform));
            gradeObj.transform.SetParent(cardObj.transform, false);
            var gradeLe = gradeObj.AddComponent<LayoutElement>();
            gradeLe.preferredHeight = 16;
            gradeLe.flexibleHeight = 0;
            var gradeTmp = gradeObj.AddComponent<TextMeshProUGUI>();
            gradeTmp.text = DisplayNameUtils.GetGradeDisplayName(grade);
            gradeTmp.fontSize = 11;
            gradeTmp.fontStyle = FontStyles.Bold;
            gradeTmp.color = gradeColor;
            gradeTmp.alignment = TextAlignmentOptions.Center;

            // NEW! 또는 중복 표시 — 빈 값이면 요소 자체를 숨김
            bool hasTag = isNew || isDuplicate;
            if (hasTag)
            {
                var tagObj = new GameObject("Tag", typeof(RectTransform));
                tagObj.transform.SetParent(cardObj.transform, false);
                var tagLe = tagObj.AddComponent<LayoutElement>();
                tagLe.preferredHeight = 14;
                tagLe.flexibleHeight = 0;
                var tagTmp = tagObj.AddComponent<TextMeshProUGUI>();
                if (isNew)
                {
                    tagTmp.text = "NEW!";
                    tagTmp.color = new Color(0.85f, 0.55f, 0.05f, 1f);
                    tagTmp.fontStyle = FontStyles.Bold;
                }
                else
                {
                    tagTmp.text = "중복";
                    tagTmp.color = new Color(0.45f, 0.45f, 0.45f, 1f);
                    tagTmp.fontStyle = FontStyles.Italic;
                }
                tagTmp.fontSize = 10;
                tagTmp.alignment = TextAlignmentOptions.Center;
            }

            // WebGL에서 지연 트윈이 안정적이지 않으므로, 즉시 표시
            cardObj.transform.localScale = Vector3.one;
            var cg = cardObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // 고등급 추가 연출: 밝은 플래시 + 펀치
            if (isHighGrade)
            {
                ColorTweenHelper.To(cardBg,
                    new Color(1f, 0.97f, 0.9f, 1f), 0.2f)
                    .SetUpdate(true)
                    .SetLoops(2, LoopType.Yoyo).SetLink(cardObj);
                cardObj.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 4, 0.5f)
                    .SetUpdate(true).SetLink(cardObj);
            }
        }

        /// <summary>10연차 팝업 닫기 (페이드아웃 연출)</summary>
        private void CloseTenPullPopup()
        {
            if (_tenPullPopup == null) return;

            IsPullPopupOpen = false;

            var panelTr = _tenPullPopup.transform.Find("Panel");
            if (panelTr != null)
                panelTr.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).SetLink(panelTr.gameObject);

            var dimTr = _tenPullPopup.transform.Find("Dim");
            if (dimTr != null)
            {
                var dimImg = dimTr.GetComponent<Image>();
                if (dimImg != null)
                    ColorTweenHelper.To(dimImg,
                        new Color(0f, 0f, 0f, 0f), 0.2f).SetUpdate(true);
            }

            var popup = _tenPullPopup;
            _tenPullPopup = null;
            DOTween.Sequence()
                .AppendInterval(0.25f)
                .OnComplete(() => { if (popup != null) Destroy(popup); })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        // ── 1회 뽑기 결과 팝업 (resultArea null 시 fallback) ──

        /// <summary>
        /// 1회 뽑기 결과를 오버레이 팝업으로 표시한다.
        /// resultArea가 null일 때의 안전한 대체 표시 방법.
        /// </summary>
        private void ShowSinglePullPopup(GachaEntry result, GachaPoolType poolType)
        {
            if (result == null) return;

            // 기존 10연차 팝업이 열려있으면 닫기
            if (_tenPullPopup != null)
                Destroy(_tenPullPopup);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Color gradeColor = GetGradeColor(result.grade);
            bool isHighGrade = IsHighGrade(result.grade);

            // 오버레이 루트
            var popupObj = new GameObject("SinglePullPopup", typeof(RectTransform));
            popupObj.transform.SetParent(canvas.transform, false);
            var popupRt = popupObj.GetComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.sizeDelta = Vector2.zero;
            popupRt.anchoredPosition = Vector2.zero;

            // 반투명 배경 (클릭으로 닫기)
            var dimObj = new GameObject("Dim", typeof(RectTransform));
            dimObj.transform.SetParent(popupObj.transform, false);
            var dimRt = dimObj.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.sizeDelta = Vector2.zero;
            var dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0f);
            var dimCg = dimObj.AddComponent<CanvasGroup>();
            dimCg.alpha = 0f;

            // 중앙 카드 패널
            var cardObj = new GameObject("Card", typeof(RectTransform));
            cardObj.transform.SetParent(popupObj.transform, false);
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.2f, 0.25f);
            cardRt.anchorMax = new Vector2(0.8f, 0.75f);
            cardRt.sizeDelta = Vector2.zero;
            var cardBg = cardObj.AddComponent<Image>();
            var tmSingle = UIThemeManager.Instance;
            if (tmSingle != null)
            {
                tmSingle.ApplyFrameBackground(cardBg);
                cardBg.color = new Color(
                    Mathf.Lerp(1f, gradeColor.r, 0.3f),
                    Mathf.Lerp(1f, gradeColor.g, 0.3f),
                    Mathf.Lerp(1f, gradeColor.b, 0.3f),
                    1f);
            }
            else
            {
                cardBg.color = new Color(
                    gradeColor.r * 0.4f + 0.3f,
                    gradeColor.g * 0.4f + 0.28f,
                    gradeColor.b * 0.4f + 0.26f,
                    0.98f);
            }

            // 세로 레이아웃
            var vlg = cardObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8;
            vlg.padding = new RectOffset(16, 16, 16, 16);

            // 등급 상단 바
            var gradeBarObj = new GameObject("GradeBar", typeof(RectTransform));
            gradeBarObj.transform.SetParent(cardObj.transform, false);
            var gradeBarImg = gradeBarObj.AddComponent<Image>();
            gradeBarImg.color = gradeColor;
            var gradeBarLe = gradeBarObj.AddComponent<LayoutElement>();
            gradeBarLe.preferredHeight = 8;

            // 아이콘 영역
            var iconAreaObj = new GameObject("IconArea", typeof(RectTransform));
            iconAreaObj.transform.SetParent(cardObj.transform, false);
            var iconAreaLe = iconAreaObj.AddComponent<LayoutElement>();
            iconAreaLe.preferredHeight = 100;

            Sprite icon = GetEquipmentIcon(result.itemId);

            if (icon != null)
            {
                var iconImg = iconAreaObj.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
            }
            else
            {
                var initialTmp = iconAreaObj.AddComponent<TextMeshProUGUI>();
                string gradeKr = DisplayNameUtils.GetGradeDisplayName(result.grade);
                initialTmp.text = gradeKr.Length > 0 ? gradeKr[0].ToString() : "?";
                initialTmp.fontSize = 56;
                initialTmp.fontStyle = FontStyles.Bold;
                initialTmp.color = gradeColor;
                initialTmp.alignment = TextAlignmentOptions.Center;
            }

            // 이름
            var nameObj = new GameObject("Name", typeof(RectTransform));
            nameObj.transform.SetParent(cardObj.transform, false);
            var nameLe = nameObj.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 44;
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = FormatItemName(result.itemId);
            nameTmp.fontSize = 28;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center;

            // 등급
            var gradeObj = new GameObject("Grade", typeof(RectTransform));
            gradeObj.transform.SetParent(cardObj.transform, false);
            var gradeLe = gradeObj.AddComponent<LayoutElement>();
            gradeLe.preferredHeight = 28;
            var gradeTmp = gradeObj.AddComponent<TextMeshProUGUI>();
            gradeTmp.text = DisplayNameUtils.GetGradeDisplayName(result.grade);
            gradeTmp.fontSize = 22;
            gradeTmp.fontStyle = FontStyles.Bold;
            gradeTmp.color = gradeColor;
            gradeTmp.alignment = TextAlignmentOptions.Center;

            // NEW!/중복 태그
            bool isNew = true;

            var tagObj = new GameObject("Tag", typeof(RectTransform));
            tagObj.transform.SetParent(cardObj.transform, false);
            var tagLe = tagObj.AddComponent<LayoutElement>();
            tagLe.preferredHeight = 24;
            var tagTmp = tagObj.AddComponent<TextMeshProUGUI>();
            tagTmp.fontSize = 18;
            tagTmp.alignment = TextAlignmentOptions.Center;
            if (isNew)
            {
                tagTmp.text = "NEW!";
                tagTmp.color = new Color(1f, 0.95f, 0.3f, 1f);
                tagTmp.fontStyle = FontStyles.Bold;
            }
            else
            {
                tagTmp.text = "중복 → 파편";
                tagTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                tagTmp.fontStyle = FontStyles.Italic;
            }

            // 확인 버튼
            var closeBtnObj = new GameObject("ConfirmBtn", typeof(RectTransform));
            closeBtnObj.transform.SetParent(cardObj.transform, false);
            var closeBtnLe = closeBtnObj.AddComponent<LayoutElement>();
            closeBtnLe.preferredHeight = 40;
            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            var closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            var tmCBtn = UIThemeManager.Instance;
            if (tmCBtn != null) tmCBtn.ApplyConfirmButton(closeBtnImg);
            else closeBtnImg.color = new Color(0.35f, 0.30f, 0.25f, 1f);

            var closeTxtObj = new GameObject("Text", typeof(RectTransform));
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.sizeDelta = Vector2.zero;
            var closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
            closeTxt.text = "확인";
            closeTxt.fontSize = 22;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;

            // 닫기 액션 — dim 클릭 + 확인 버튼 모두
            var popupRef = popupObj;
            System.Action closeAction = () =>
            {
                if (popupRef == null) return;
                IsPullPopupOpen = false;
                var panelTr = popupRef.transform.Find("Card");
                if (panelTr != null)
                    panelTr.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).SetLink(panelTr.gameObject);
                DOTween.Sequence()
                    .AppendInterval(0.25f)
                    .OnComplete(() => { if (popupRef != null) Destroy(popupRef); })
                    .SetUpdate(true)
                    .SetLink(gameObject);
                if (_tenPullPopup == popupRef)
                    _tenPullPopup = null;
            };

            closeBtn.onClick.AddListener(() => closeAction());
            var dimBtn = dimObj.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => closeAction());

            // 등장 연출
            dimCg.alpha = 1f;
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            cardObj.transform.localScale = Vector3.one * 0.5f;
            var cardCg = cardObj.AddComponent<CanvasGroup>();
            cardCg.alpha = 0f;

            DOTween.Sequence()
                .Append(cardObj.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => cardCg.alpha, a => cardCg.alpha = a, 1f, 0.2f))
                .SetUpdate(true)
                .SetLink(cardObj);

            // 고등급 추가 연출: 카드 펀치
            if (isHighGrade)
            {
                DOTween.Sequence()
                    .AppendInterval(0.4f)
                    .AppendCallback(() =>
                    {
                        cardObj.transform.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(cardObj);
                    })
                    .SetUpdate(true)
                    .SetLink(cardObj);
            }

            _tenPullPopup = popupObj;
            IsPullPopupOpen = true;
        }

        // ── 등급 판정 ──

        private static bool IsHighGrade(string grade)
        {
            return grade is "Epic" or "Unique" or "Legendary" or "Mythic";
        }

        private static Sprite GetEquipmentIcon(string itemId)
        {
            if (EquipmentManager.Instance == null) return null;
            var data = EquipmentManager.Instance.GetData(itemId);
            return data != null ? data.icon : null;
        }

        // ── 루비 부족 피드백 (풀스크린 오버레이 + 텍스트 플래시) ──

        private bool _isFlashingRuby;

        private void FlashRubyInsufficient()
        {
            Debug.Log($"[ShopPanel] FlashRubyInsufficient 호출됨 — subTab={_currentSubTab}");

            // 기존 텍스트 색상 변경 (WebGL 호환 — 런타임 생성 오버레이 대신 기존 TMP 직접 수정)
            if (!_isFlashingRuby)
                FlashRubyTextAsync().Forget();
        }

        private async UniTaskVoid FlashRubyTextAsync()
        {
            _isFlashingRuby = true;
            var token = this.GetCancellationTokenOnDestroy();

            // rubyText를 "루비 부족!" 빨간색으로 변경 (보조 피드백)
            string originalRubyStr = rubyText != null ? rubyText.text : "";
            Color originalRubyColor = rubyText != null ? rubyText.color : Color.white;
            float originalRubySize = rubyText != null ? rubyText.fontSize : 24f;

            if (rubyText != null)
            {
                rubyText.text = "루비 부족!";
                rubyText.color = new Color(1f, 0.2f, 0.2f, 1f);
                rubyText.fontSize = originalRubySize * 2.0f;
                rubyText.ForceMeshUpdate();
                rubyText.transform.DOPunchScale(Vector3.one * 0.15f, 0.5f, 3, 0.5f).SetUpdate(true).SetLink(rubyText.gameObject);
            }

            // 비용 텍스트도 빨간색 강조
            var costText1 = _currentSubTab switch
            {
                0 => companionPull1CostText,
                1 => equipmentPull1CostText,
                2 => weaponPull1CostText,
                _ => null
            };
            var costText10 = _currentSubTab switch
            {
                0 => companionPull10CostText,
                1 => equipmentPull10CostText,
                2 => weaponPull10CostText,
                _ => null
            };

            Color origCost1Color = costText1 != null ? costText1.color : Color.white;
            Color origCost10Color = costText10 != null ? costText10.color : Color.white;
            float origCost1Size = costText1 != null ? costText1.fontSize : 20f;
            float origCost10Size = costText10 != null ? costText10.fontSize : 20f;

            if (costText1 != null)
            {
                costText1.color = new Color(1f, 0.15f, 0.15f, 1f);
                costText1.fontSize = origCost1Size * 1.2f;
                costText1.ForceMeshUpdate();
            }
            if (costText10 != null)
            {
                costText10.color = new Color(1f, 0.15f, 0.15f, 1f);
                costText10.fontSize = origCost10Size * 1.2f;
                costText10.ForceMeshUpdate();
            }

            // pityText를 "루비가 부족합니다!" 빨간색으로 변경
            var pityText = _currentSubTab switch
            {
                0 => companionPityText,
                1 => equipmentPityText,
                2 => weaponPityText,
                _ => null
            };
            string originalPityStr = pityText != null ? pityText.text : "";
            Color originalPityColor = pityText != null ? pityText.color : Color.white;
            float originalPitySize = pityText != null ? pityText.fontSize : 16f;

            if (pityText != null)
            {
                BigNumber rubyHeld = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby) : BigNumber.Zero;
                Debug.Log($"[ShopPanel] pityText object: name={pityText.gameObject.name}, parent={pityText.transform.parent?.name}, active={pityText.gameObject.activeInHierarchy}, text BEFORE='{pityText.text}'");
                pityText.text = $"루비가 부족합니다! (보유: {NumberFormatter.FormatKorean(rubyHeld)})";
                pityText.color = new Color(1f, 0.2f, 0.2f, 1f);
                pityText.fontSize = originalPitySize * 1.8f;
                pityText.gameObject.SetActive(true);
                pityText.enabled = true;
                pityText.ForceMeshUpdate();
                Debug.Log($"[ShopPanel] pityText AFTER: text='{pityText.text}', color={pityText.color}, active={pityText.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError($"[ShopPanel] pityText is NULL for subTab {_currentSubTab} — EnsurePityTexts 재시도");
                EnsurePityTexts();
                var retryPityText = _currentSubTab switch
                {
                    0 => companionPityText,
                    1 => equipmentPityText,
                    2 => weaponPityText,
                    _ => null
                };
                if (retryPityText != null)
                {
                    BigNumber rubyHeld = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby) : BigNumber.Zero;
                    retryPityText.text = $"루비가 부족합니다! (보유: {NumberFormatter.FormatKorean(rubyHeld)})";
                    retryPityText.color = new Color(1f, 0.2f, 0.2f, 1f);
                    retryPityText.fontSize = 16f * 1.8f;
                    retryPityText.gameObject.SetActive(true);
                    retryPityText.enabled = true;
                    retryPityText.ForceMeshUpdate();
                    Debug.Log($"[ShopPanel] pityText 재시도 성공: name={retryPityText.gameObject.name}");
                }
                else
                {
                    Debug.LogError("[ShopPanel] pityText 재시도 후에도 NULL — FindPityTextInContent 실패");
                }
            }

            // 2.5초 대기 후 원래 상태 복원
            try
            {
                await UniTask.Delay(2500, cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                _isFlashingRuby = false;
                return;
            }

            if (rubyText != null)
            {
                rubyText.color = originalRubyColor;
                rubyText.fontSize = originalRubySize;
                RefreshRuby();
            }
            if (costText1 != null)
            {
                costText1.color = origCost1Color;
                costText1.fontSize = origCost1Size;
            }
            if (costText10 != null)
            {
                costText10.color = origCost10Color;
                costText10.fontSize = origCost10Size;
            }
            if (pityText != null)
            {
                pityText.color = originalPityColor;
                pityText.fontSize = originalPitySize;
                pityText.text = originalPityStr;
            }

            _isFlashingRuby = false;
        }

        // ── UI 갱신 ──

        private void RefreshAll()
        {
            RefreshRuby();
            RefreshCosts();
            RefreshPity();
            RefreshButtonStates();
        }

        private void RefreshRuby()
        {
            if (rubyText == null) return;
            if (_isFlashingRuby) return; // Flash 중에는 덮어쓰지 않음
            BigNumber ruby = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby) : BigNumber.Zero;
            rubyText.text = HudPanel.FormatGold(ruby);
        }

        private void RefreshCosts()
        {
            if (_isFlashingRuby) return; // Flash 중에는 비용 텍스트를 덮어쓰지 않음
            if (GachaManager.Instance == null) return;

            int cost1 = GachaManager.Instance.SinglePullCost;
            int cost10 = GachaManager.Instance.TenPullCost;

            if (companionPull1CostText != null) companionPull1CostText.text = $"{cost1}";
            if (companionPull10CostText != null) companionPull10CostText.text = $"{cost10}";
            if (equipmentPull1CostText != null) equipmentPull1CostText.text = $"{cost1}";
            if (equipmentPull10CostText != null) equipmentPull10CostText.text = $"{cost10}";
            if (weaponPull1CostText != null) weaponPull1CostText.text = $"{cost1}";
            if (weaponPull10CostText != null) weaponPull10CostText.text = $"{cost10}";
            // 유물 가챠: 2026-04-20 유물 시스템 완전 제거
        }

        private void RefreshPity()
        {
            if (_isFlashingRuby) return; // Flash 중에는 pityText를 덮어쓰지 않음
            if (GachaManager.Instance == null) return;

            // 참조가 누락된 경우 재탐색
            if (companionPityText == null || equipmentPityText == null || weaponPityText == null)
                EnsurePityTexts();

            int equipLevel = GachaManager.Instance.GetSummonLevel(GachaPoolType.Equipment);
            int equipMax = GachaManager.Instance.GetMaxSummonLevel(GachaPoolType.Equipment);
            int equipRemain = GachaManager.Instance.GetPullsToNextLevel(GachaPoolType.Equipment);
            int weaponLevel = GachaManager.Instance.GetSummonLevel(GachaPoolType.Weapon);
            int weaponMax = GachaManager.Instance.GetMaxSummonLevel(GachaPoolType.Weapon);
            int weaponRemain = GachaManager.Instance.GetPullsToNextLevel(GachaPoolType.Weapon);

            if (equipmentPityText != null)
            {
                string levelInfo = equipRemain >= 0
                    ? $"소환 Lv.{equipLevel}/{equipMax} (다음 레벨까지 {equipRemain}회)"
                    : $"소환 Lv.{equipLevel}/{equipMax} (MAX)";
                string rateInfo = FormatGradeRates(GachaPoolType.Equipment, equipLevel);
                equipmentPityText.text = $"{levelInfo}\n{rateInfo}";
            }
            if (weaponPityText != null)
            {
                string levelInfo = weaponRemain >= 0
                    ? $"소환 Lv.{weaponLevel}/{weaponMax} (다음 레벨까지 {weaponRemain}회)"
                    : $"소환 Lv.{weaponLevel}/{weaponMax} (MAX)";
                string rateInfo = FormatGradeRates(GachaPoolType.Weapon, weaponLevel);
                weaponPityText.text = $"{levelInfo}\n{rateInfo}";
            }

            // 유물 가챠: 2026-04-20 유물 시스템 완전 제거
        }

        /// <summary>소환 레벨별 등급 확률을 한 줄 문자열로 포맷</summary>
        private static string FormatGradeRates(GachaPoolType poolType, int summonLevel)
        {
            if (GachaManager.Instance == null) return "";
            var pool = GachaManager.Instance.GetPool(poolType);
            if (pool == null) return "";

            float[] weights = pool.GetGradeWeightsForLevel(summonLevel);
            if (weights == null || weights.Length == 0) return "";

            var sb = new System.Text.StringBuilder();
            string[] gradeShort = { "N", "R", "E", "U", "L", "M", "A" };
            for (int i = 0; i < weights.Length && i < gradeShort.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append($"{gradeShort[i]}:{weights[i]:F1}%");
            }
            return sb.ToString();
        }

        private static readonly Color _btnNormalColor = Color.white;
        private static readonly Color _btnDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        private static readonly Color _costNormalColor = Color.white;
        private static readonly Color _costInsufficientColor = new Color(1f, 0.25f, 0.25f, 1f);

        private void RefreshButtonStates()
        {
            if (_isFlashingRuby) return; // Flash 중에는 버튼 상태를 덮어쓰지 않음
            BigNumber ruby = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Ruby) : BigNumber.Zero;
            int cost1 = GachaManager.Instance != null ? GachaManager.Instance.SinglePullCost : 0;
            int cost10 = GachaManager.Instance != null ? GachaManager.Instance.TenPullCost : 0;

            bool canPull1 = ruby >= cost1;
            bool canPull10 = ruby >= cost10;

            ApplyButtonState(companionPull1Btn, companionPull1CostText, canPull1);
            ApplyButtonState(companionPull10Btn, companionPull10CostText, canPull10);
            ApplyButtonState(equipmentPull1Btn, equipmentPull1CostText, canPull1);
            ApplyButtonState(equipmentPull10Btn, equipmentPull10CostText, canPull10);
            ApplyButtonState(weaponPull1Btn, weaponPull1CostText, canPull1);
            ApplyButtonState(weaponPull10Btn, weaponPull10CostText, canPull10);
            // 유물 가챠 버튼: 2026-04-20 유물 시스템 완전 제거
        }

        private void ApplyButtonState(Button btn, TextMeshProUGUI costText, bool canAfford)
        {
            if (btn != null)
            {
                var btnImg = btn.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = canAfford ? _btnNormalColor : _btnDisabledColor;
            }

            if (costText != null)
                costText.color = canAfford ? _costNormalColor : _costInsufficientColor;
        }

        // ── 유틸 ──

        private static Color GetGradeColor(string grade)
        {
            return GradeColors.TryGetValue(grade, out var color) ? color : Color.white;
        }

        private static string FormatItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;

            // 무기는 WeaponManager 우선 조회 (itemId "weapon_..." prefix)
            if (itemId.StartsWith("weapon_") && WeaponManager.Instance != null)
            {
                var wData = WeaponManager.Instance.GetData(itemId);
                if (wData != null && !string.IsNullOrEmpty(wData.displayName))
                    return wData.displayName;
            }

            // 장비 SO에서 한글 이름 조회
            if (EquipmentManager.Instance != null)
            {
                var eqData = EquipmentManager.Instance.GetData(itemId);
                if (eqData != null && !string.IsNullOrEmpty(eqData.displayName))
                    return eqData.displayName;
            }

            // SO를 찾지 못한 경우 ID에서 변환 (fallback)
            string[] parts = itemId.Split('_');
            int start = parts.Length > 1 ? 1 : 0;
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < parts.Length; i++)
            {
                if (i > start) sb.Append(' ');
                if (parts[i].Length > 0)
                    sb.Append(char.ToUpper(parts[i][0])).Append(parts[i], 1, parts[i].Length - 1);
            }
            return sb.ToString();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
