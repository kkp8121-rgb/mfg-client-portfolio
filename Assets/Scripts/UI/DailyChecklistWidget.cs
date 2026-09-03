using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Core;
using MkLike.Data;
using MkLike.Quest;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 메인 전투 화면 접이식 일일 체크리스트 위젯.
    /// QuestManager의 일일 퀘스트를 표시하며, 완료 시 체크마크 + 줄긋기 연출.
    /// 전부 완료 시 황금 보상 상자 연출.
    /// </summary>
    public class DailyChecklistWidget : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private RectTransform _widgetRoot;
        [SerializeField] private CanvasGroup _contentGroup;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Image _toggleArrow;

        [Header("전체 완료 연출")]
        [SerializeField] private GameObject _allClearEffect;
        [SerializeField] private TMP_Text _allClearText;
        [SerializeField] private Image _allClearGlow;

        [Header("설정")]
        [SerializeField] private float _expandedX;
        [SerializeField] private float _collapsedX = 220f;
        [SerializeField] private float _slideSpeed = 0.25f;
        [SerializeField] private Color _completedTextColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color _activeTextColor = Color.white;
        [SerializeField] private Color _checkColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color _doubleRewardColor = new Color(1f, 0.85f, 0.1f);

        [Tooltip("메인 화면에 자동으로 띄울지 여부. false면 위젯 자체가 숨겨지고 별도 탭/메뉴를 통해서만 노출.")]
        [SerializeField] private bool _showOnMain;

        private bool _isExpanded;
        private bool _allCleared;
        private Tween _slideTween;
        private readonly List<ChecklistRowData> _rows = new();

        private void Awake()
        {
            // 메인 화면 자동 노출이 꺼져 있으면 위젯 GameObject 자체를 비활성화 (개발용 UI 노출 방지).
            // 별도 탭/메뉴에서 SetActive(true) + RebuildList()로 표시할 수 있다.
            if (!_showOnMain)
            {
                gameObject.SetActive(false);
                return;
            }
            EnsureComponents();
        }

        private void Start()
        {
            if (!_showOnMain) return;

            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(ToggleExpand);

            if (_allClearEffect != null)
                _allClearEffect.SetActive(false);

            RebuildList();
        }

        private void OnEnable()
        {
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<QuestRewardClaimedEvent>.Subscribe(OnRewardClaimed);
            EventBus<DailyChecklistAllClearEvent>.Subscribe(OnAllClearBonus);
        }

        private void OnDisable()
        {
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<QuestRewardClaimedEvent>.Unsubscribe(OnRewardClaimed);
            EventBus<DailyChecklistAllClearEvent>.Unsubscribe(OnAllClearBonus);
        }

        private void ToggleExpand()
        {
            _isExpanded = !_isExpanded;

            _slideTween?.Kill();
            float targetX = _isExpanded ? _expandedX : _collapsedX;

            _slideTween = DOTween.To(
                () => _widgetRoot.anchoredPosition.x,
                x => { var p = _widgetRoot.anchoredPosition; p.x = x; _widgetRoot.anchoredPosition = p; },
                targetX, _slideSpeed)
                .SetEase(_isExpanded ? Ease.OutBack : Ease.InBack)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (_toggleArrow != null)
            {
                _toggleArrow.rectTransform.DORotate(
                    new Vector3(0f, 0f, _isExpanded ? 0f : 180f), _slideSpeed)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_contentGroup != null)
            {
                DOTween.To(() => _contentGroup.alpha, a => _contentGroup.alpha = a,
                    _isExpanded ? 1f : 0f, _slideSpeed * 0.7f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// QuestManager에서 일일 퀘스트를 읽어 행을 재구성한다.
        /// </summary>
        public void RebuildList()
        {
            ClearRows();

            var qm = QuestManager.Instance;
            if (qm == null) return;

            int totalDaily = 0;
            int completedDaily = 0;

            var activeQuests = qm.ActiveQuests;
            for (int i = 0; i < activeQuests.Count; i++)
            {
                QuestProgress progress = activeQuests[i];

                // QuestDataSO 조회
                QuestDataSO data = qm.GetQuestData(progress.questId);
                if (data == null || data.questType != QuestType.Daily) continue;

                totalDaily++;
                if (progress.isCompleted) completedDaily++;

                CreateRow(data, progress);
            }

            UpdateHeader(completedDaily, totalDaily);

            _allCleared = totalDaily > 0 && completedDaily >= totalDaily;

            // 일일 퀘스트가 없으면 위젯 전체를 숨김
            if (totalDaily == 0)
            {
                var bg = GetComponent<Image>();
                if (bg != null) bg.enabled = false;
                if (_headerText != null) _headerText.enabled = false;
                if (_toggleButton != null) _toggleButton.gameObject.SetActive(false);
            }
        }

        private void CreateRow(QuestDataSO data, QuestProgress progress)
        {
            var rowObj = new GameObject($"Row_{data.id}");
            rowObj.transform.SetParent(_rowContainer, false);

            var rt = rowObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 32);

            var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(4, 4, 0, 0);

            // 체크마크
            var checkObj = new GameObject("Check");
            checkObj.transform.SetParent(rowObj.transform, false);
            var checkRt = checkObj.AddComponent<RectTransform>();
            checkRt.sizeDelta = new Vector2(24, 24);
            var checkText = checkObj.AddComponent<TextMeshProUGUI>();
            checkText.fontSize = 20;
            checkText.alignment = TextAlignmentOptions.Center;

            if (progress.isCompleted)
            {
                checkText.text = "\u2713"; // checkmark
                checkText.color = _checkColor;
            }
            else
            {
                checkText.text = "\u25CB"; // empty circle
                checkText.color = new Color(0.4f, 0.4f, 0.4f);
            }

            // 2x 보상 뱃지 (랜덤 1개)
            bool isDoubleReward = DailyChecklistManager.Instance != null
                && DailyChecklistManager.Instance.IsDoubleRewardQuest(data.id);

            // 퀘스트 이름 + 진행도
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(rowObj.transform, false);
            var nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.sizeDelta = new Vector2(180, 28);
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 16;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            string doubleTag = isDoubleReward ? $"<color=#{ColorUtility.ToHtmlStringRGB(_doubleRewardColor)}>x2</color> " : "";

            if (progress.isCompleted)
            {
                nameText.text = $"<s>{doubleTag}{data.displayName}</s>";
                nameText.color = _completedTextColor;
            }
            else
            {
                string progressStr = $"({progress.currentAmount}/{data.requiredAmount})";
                nameText.text = $"{doubleTag}{data.displayName} {progressStr}";
                nameText.color = isDoubleReward ? _doubleRewardColor : _activeTextColor;
            }

            _rows.Add(new ChecklistRowData
            {
                Root = rowObj,
                CheckText = checkText,
                NameText = nameText,
                QuestId = data.id
            });
        }

        private void UpdateHeader(int completed, int total)
        {
            if (_headerText != null)
                _headerText.text = $"일일 미션 ({completed}/{total})";
        }

        // ── 이벤트 핸들러 ──

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            if (evt.QuestType != QuestType.Daily) return;

            // 해당 행 찾아서 체크 연출
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].QuestId != evt.QuestId) continue;

                var row = _rows[i];
                PlayCheckAnimation(row);
                break;
            }

            // 전체 완료 체크
            CheckAllCleared();
        }

        private void OnRewardClaimed(QuestRewardClaimedEvent evt)
        {
            RebuildList();
        }

        private void OnAllClearBonus(DailyChecklistAllClearEvent evt)
        {
            // 전체 완료 보너스 수령 시 연출 트리거
            PlayAllClearEffect().Forget();
        }

        private void PlayCheckAnimation(ChecklistRowData row)
        {
            if (row.CheckText != null)
            {
                row.CheckText.text = "\u2713";
                row.CheckText.color = _checkColor;
                row.CheckText.transform.localScale = Vector3.one * 2f;
                row.CheckText.transform.DOScale(1f, 0.3f)
                    .SetEase(Ease.OutBounce)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (row.NameText != null)
            {
                // 줄긋기 + 색상 변경
                string rawText = row.NameText.text;
                if (!rawText.StartsWith("<s>"))
                    row.NameText.text = $"<s>{rawText}</s>";

                ColorTweenHelper.To(row.NameText, _completedTextColor, 0.3f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_check");
        }

        private void CheckAllCleared()
        {
            if (_allCleared) return;

            var qm = QuestManager.Instance;
            if (qm == null) return;

            int total = 0;
            int completed = 0;

            var activeQuests = qm.ActiveQuests;
            for (int i = 0; i < activeQuests.Count; i++)
            {
                QuestProgress progress = activeQuests[i];
                QuestDataSO data = qm.GetQuestData(progress.questId);
                if (data == null || data.questType != QuestType.Daily) continue;

                total++;
                if (progress.isCompleted) completed++;
            }

            UpdateHeader(completed, total);

            if (total > 0 && completed >= total)
            {
                _allCleared = true;
                PlayAllClearEffect().Forget();
            }
        }

        private async UniTaskVoid PlayAllClearEffect()
        {
            var token = this.GetCancellationTokenOnDestroy();

            AudioManager.Instance?.PlaySfx(SfxType.UiReward);

            if (_allClearEffect != null)
            {
                _allClearEffect.SetActive(true);
                _allClearEffect.transform.localScale = Vector3.zero;
                _allClearEffect.transform.DOScale(1f, 0.4f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_allClearText != null)
            {
                _allClearText.text = "일일 미션 완료!";
                _allClearText.transform.localScale = Vector3.zero;
                _allClearText.transform.DOScale(1.2f, 0.3f)
                    .SetEase(Ease.OutElastic)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_allClearGlow != null)
            {
                _allClearGlow.color = new Color(1f, 0.85f, 0.1f, 0f);
                DOTween.To(() => _allClearGlow.color.a,
                    x => { var c = _allClearGlow.color; c.a = x; _allClearGlow.color = c; },
                    0.8f, 0.3f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 헤더 펀치
            if (_headerText != null)
            {
                _headerText.color = new Color(1f, 0.85f, 0.1f);
                _headerText.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            await UniTask.Delay(3000, cancellationToken: token, ignoreTimeScale: true);

            // 페이드아웃
            if (_allClearEffect != null)
            {
                var cg = _allClearEffect.GetComponent<CanvasGroup>();
                if (cg == null) cg = _allClearEffect.AddComponent<CanvasGroup>();
                DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.4f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (_allClearEffect != null)
                            _allClearEffect.SetActive(false);
                    });
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Root != null)
                    Destroy(_rows[i].Root);
            }
            _rows.Clear();
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

            // _widgetRoot = 이 오브젝트 자체의 RectTransform
            if (_widgetRoot == null)
                _widgetRoot = transform as RectTransform ?? gameObject.AddComponent<RectTransform>();

            // 배경: 둥근 모서리 + 어두운 슬레이트 + 옅은 외곽선
            var bg = GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.sprite = null; // ApplyWidgetTheme의 frame_brown 무시
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.10f, 0.11f, 0.16f, 0.92f);

            // 외곽선 효과 (내부에 border GameObject)
            if (transform.Find("BorderHighlight") == null)
            {
                var borderGo = CreateUIChild("BorderHighlight");
                var brt = borderGo.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(-1.5f, -1.5f);
                brt.offsetMax = new Vector2(1.5f, 1.5f);
                var bImg = borderGo.AddComponent<Image>();
                bImg.color = new Color(0.95f, 0.78f, 0.25f, 0.55f); // 골드 외곽
                bImg.raycastTarget = false;
                borderGo.transform.SetAsFirstSibling();
            }

            // 헤더 위 강조 바 (그라데이션 느낌)
            if (transform.Find("HeaderBar") == null)
            {
                var hbGo = CreateUIChild("HeaderBar");
                var hbRt = hbGo.GetComponent<RectTransform>();
                hbRt.anchorMin = new Vector2(0f, 1f);
                hbRt.anchorMax = new Vector2(1f, 1f);
                hbRt.pivot = new Vector2(0.5f, 1f);
                hbRt.anchoredPosition = Vector2.zero;
                hbRt.sizeDelta = new Vector2(0f, 36f);
                var hbImg = hbGo.AddComponent<Image>();
                hbImg.color = new Color(0.20f, 0.16f, 0.10f, 0.95f);
                hbImg.raycastTarget = false;
            }

            // CanvasGroup (접기/펼치기 페이드용)
            if (_contentGroup == null)
                _contentGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // ── 토글 버튼: 위젯 우측 가장자리에 탭 형태 ──
            if (_toggleButton == null)
            {
                var go = CreateUIChild("ToggleButton");
                var rt = go.GetComponent<RectTransform>();
                // 우측 중앙에 앵커, 위젯 밖으로 살짝 튀어나오는 탭
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f); // 좌측 피봇 → 우측으로 뻗음
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(28f, 60f);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
                _toggleButton = go.AddComponent<Button>();
            }

            // ── 토글 화살표: 토글 버튼 안에 중앙 배치 ──
            if (_toggleArrow == null)
            {
                var go = new GameObject("ToggleArrow", typeof(RectTransform));
                go.transform.SetParent(_toggleButton.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(20f, 20f);

                _toggleArrow = go.AddComponent<Image>();
                _toggleArrow.color = new Color(0f, 0f, 0f, 0f); // 이미지는 투명

                // 텍스트로 화살표 표시
                var arrowTextGo = new GameObject("ArrowText", typeof(RectTransform));
                arrowTextGo.transform.SetParent(go.transform, false);
                var arrowTmp = arrowTextGo.AddComponent<TextMeshProUGUI>();
                arrowTmp.text = "\u25C0"; // ◀
                arrowTmp.fontSize = 14f;
                arrowTmp.alignment = TextAlignmentOptions.Center;
                arrowTmp.color = new Color(0.7f, 0.7f, 0.8f);
                arrowTmp.raycastTarget = false;
                var arrowTmpRt = arrowTextGo.GetComponent<RectTransform>();
                arrowTmpRt.anchorMin = Vector2.zero;
                arrowTmpRt.anchorMax = Vector2.one;
                arrowTmpRt.offsetMin = Vector2.zero;
                arrowTmpRt.offsetMax = Vector2.zero;
            }

            // ── 헤더 텍스트: ★ 아이콘 + 미션 카운트, 큰 폰트 ──
            if (_headerText == null)
            {
                var go = CreateUIChild("HeaderText");
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -6f);
                rt.sizeDelta = new Vector2(-20f, 30f);

                _headerText = go.AddComponent<TextMeshProUGUI>();
                _headerText.fontSize = 20f;
                _headerText.fontStyle = FontStyles.Bold;
                _headerText.alignment = TextAlignmentOptions.Left;
                _headerText.color = new Color(1f, 0.92f, 0.55f);
                _headerText.text = "★ 일일 미션";
                _headerText.outlineWidth = 0.18f;
                _headerText.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            }

            // ── 행 컨테이너: 헤더 바(36px) 아래부터 하단까지 스트레치 ──
            if (_rowContainer == null)
            {
                var go = CreateUIChild("RowContainer");
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(6f, 6f);
                rt.offsetMax = new Vector2(-6f, -40f); // 헤더 바(36) + 4px 갭

                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 4; // 행 간격
                vlg.padding = new RectOffset(2, 2, 2, 2);

                var csf = go.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                _rowContainer = go.transform;
            }

            // ── 전체 완료 글로우: 위젯 전체 뒤에 깔리는 발광 이미지 ──
            if (_allClearGlow == null)
            {
                var go = CreateUIChild("AllClearGlow");
                var rt = go.GetComponent<RectTransform>();
                // 전체 스트레치 + 여백으로 약간 밖으로 확장
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(-8f, -8f);
                rt.offsetMax = new Vector2(8f, 8f);

                _allClearGlow = go.AddComponent<Image>();
                _allClearGlow.color = new Color(1f, 0.85f, 0.1f, 0f);
                _allClearGlow.raycastTarget = false;

                // 글로우는 뒤에 깔려야 하므로 첫 번째 자식으로 이동
                go.transform.SetAsFirstSibling();
            }

            // ── 전체 완료 이펙트: 위젯 전체 오버레이 ──
            if (_allClearEffect == null)
            {
                _allClearEffect = new GameObject("AllClearEffect", typeof(RectTransform));
                _allClearEffect.transform.SetParent(transform, false);
                var rt = _allClearEffect.GetComponent<RectTransform>();
                // 위젯 전체를 덮는 오버레이
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // 반투명 오버레이 배경
                var overlayImg = _allClearEffect.AddComponent<Image>();
                overlayImg.color = new Color(0f, 0f, 0f, 0.5f);
                overlayImg.raycastTarget = false;

                _allClearEffect.SetActive(false);
            }

            // ── 전체 완료 텍스트: allClearEffect 내부 중앙 ──
            if (_allClearText == null)
            {
                var go = new GameObject("AllClearText", typeof(RectTransform));
                go.transform.SetParent(_allClearEffect.transform, false);
                var rt = go.GetComponent<RectTransform>();
                // 부모(allClearEffect) 중앙에 배치
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(200f, 40f);

                _allClearText = go.AddComponent<TextMeshProUGUI>();
                _allClearText.fontSize = 22f;
                _allClearText.alignment = TextAlignmentOptions.Center;
                _allClearText.color = new Color(1f, 0.85f, 0.1f);
                _allClearText.raycastTarget = false;
            }

            ApplyWidgetTheme();
        }

        private void ApplyWidgetTheme()
        {
            var tm = UIThemeManager.Instance;
            if (tm == null) return;

            var rootBg = GetComponent<Image>();
            if (rootBg != null)
                tm.ApplyFrameBackground(rootBg);

            // 토글 버튼
            if (_toggleButton != null)
            {
                var btnImg = _toggleButton.GetComponent<Image>();
                if (btnImg != null)
                    tm.ApplyButtonFull(_toggleButton);
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
            _slideTween?.Kill();
            if (_widgetRoot != null) _widgetRoot.DOKill();
            if (_toggleArrow != null) _toggleArrow.rectTransform.DOKill();
            if (_allClearEffect != null) _allClearEffect.transform.DOKill();
            if (_allClearText != null) _allClearText.transform.DOKill();
            if (_headerText != null) _headerText.transform.DOKill();
        }

        private struct ChecklistRowData
        {
            public GameObject Root;
            public TMP_Text CheckText;
            public TMP_Text NameText;
            public string QuestId;
        }
    }
}
