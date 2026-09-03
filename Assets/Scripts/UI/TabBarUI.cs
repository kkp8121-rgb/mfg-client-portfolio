using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Quest;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 하단 탭 바.
    /// 캐릭터 / 던전 / 상점 패널을 토글 방식으로 활성화/비활성화한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TabBarUI : MonoBehaviour
    {
        [Header("탭 패널 (소환 / 캐릭터 / 장비 / 스킬 / 무기)")]
        [SerializeField] private GameObject[] _tabPanels = new GameObject[5];

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement[] _tabButtons;
        private HudPanel _hudPanel;
        private int _currentTabIndex = -1;

        // 2026-04-23 이슈 5+8: tab-weapon → tab-dungeon 교체 (무기 탭 제거, 던전 탭 복구)
        private static readonly string[] TAB_NAMES =
        {
            "tab-summon", "tab-character", "tab-equipment", "tab-skill", "tab-dungeon"
        };
        private const string SELECTED_CLASS = "tab-btn--selected";

        /// <summary>탭 패널이 하나라도 열려있으면 true</summary>
        public bool IsAnyPanelOpen => _currentTabIndex >= 0;

        public int GetCurrentTabIndex() => _currentTabIndex;

        // 탭별 패널 이름 매핑 (씬에서 자동 탐색)
        // 2026-04-23 이슈 5+8: 무기 탭 → 던전 탭 교체 (idle 핵심 시스템 복구, 무기는 장비와 중복)
        private static readonly string[][] TAB_PANEL_NAMES =
        {
            new[] { "[UITK] Summon" },       // 0: 소환 탭
            new[] { "[UITK] Char_Stat" },    // 1: 캐릭터 탭 → 스탯
            new[] { "[UITK] Char_Equip" },   // 2: 장비 탭
            new[] { "[UITK] Char_Skill" },   // 3: 스킬 탭
            new[] { "[UITK] Dungeon" },      // 4: 던전 탭 (2026-04-23 복구)
        };

        // 캐릭터 서브탭 (탭 닫을 때 같이 닫기 — 스킬은 독립 탭이므로 제외)
        private static readonly string[] CHAR_SUBTAB_NAMES =
        {
            "[UITK] Char_Stat",
            "[UITK] Char_Job"
        };

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();

            // TabBar는 패널(50) 위에 렌더링되어야 항상 클릭 가능
            _uiDocument.sortingOrder = 60;

            _root = _uiDocument.rootVisualElement;
            if (_root == null)
            {
                // rootVisualElement가 아직 null — UIDocument가 VisualTreeAsset을 빌드하지 못한 상태
                // (intermittent UI 미로딩 증상 방어). 다음 프레임에 재시도.
                Debug.LogWarning("[TabBarUI] rootVisualElement == null, OnEnable 재시도 필요 (UIDocument 미초기화)");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;
            _hudPanel = FindFirstObjectByType<HudPanel>(FindObjectsInactive.Include);

            // 탭 패널 자동 탐색
            int tabCount = TAB_PANEL_NAMES.Length;
            if (_tabPanels == null || _tabPanels.Length < tabCount || _tabPanels[0] == null)
            {
                _tabPanels = new GameObject[tabCount];
                for (int i = 0; i < tabCount; i++)
                {
                    var go = GameObject.Find(TAB_PANEL_NAMES[i][0]);
                    if (go == null)
                    {
                        // 비활성 오브젝트는 Find로 못 찾으므로 전체 탐색
                        var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var doc in docs)
                        {
                            if (doc.gameObject.name == TAB_PANEL_NAMES[i][0])
                            {
                                go = doc.gameObject;
                                break;
                            }
                        }
                    }
                    _tabPanels[i] = go;
                    if (go == null)
                        Debug.LogWarning($"[TabBarUI] 패널 못 찾음: {TAB_PANEL_NAMES[i][0]}");
                }
            }

            _tabButtons = new VisualElement[TAB_NAMES.Length];
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                _tabButtons[i] = _root.Q<VisualElement>(TAB_NAMES[i]);
                if (_tabButtons[i] == null)
                {
                    Debug.LogWarning($"[TabBarUI] '{TAB_NAMES[i]}' 요소를 찾을 수 없음");
                    continue;
                }

                int index = i;
                _tabButtons[i].RegisterCallback<ClickEvent>(_ => OnTabClicked(index));
            }

            // 탭 해금 갱신 이벤트 (가이드 퀘스트 완료 시 + 세이브 로드 시)
            EventBus<GuideQuestCompletedEvent>.Subscribe(OnGuideQuestCompletedForTabs);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);

            CloseAll();
        }

        private void Start()
        {
            // LoadCompletedEvent 늦은 구독자 방어:
            // SaveManager.Awake(-10000)에서 먼저 발행되므로 TabBarUI.OnEnable이 놓칠 수 있음.
            // Start 시점에 CurrentData가 존재하면 탭 잠금 상태를 한 번 강제 갱신.
            if (Core.Save.SaveManager.Instance?.CurrentData != null)
                UpdateTabVisuals(_currentTabIndex);
        }

        private void OnDisable()
        {
            EventBus<GuideQuestCompletedEvent>.Unsubscribe(OnGuideQuestCompletedForTabs);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            // QuestManager 초기화 후 탭 잠금 상태 재갱신
            UpdateTabVisuals(_currentTabIndex);
        }

        private void OnGuideQuestCompletedForTabs(GuideQuestCompletedEvent evt)
        {
            UpdateTabVisuals(_currentTabIndex);
        }

        // 탭 해금 조건 (TutorialStep 기준, 가이드 퀘스트 완료 시 자동 진행)
        private static readonly TutorialStep[] TAB_UNLOCK_STEPS =
        {
            TutorialStep.None,             // 0: 소환 — 가이드 퀘스트 기반 해금
            TutorialStep.None,             // 1: 캐릭터 — 항상 열림
            TutorialStep.None,             // 2: 장비 — 가이드 퀘스트 기반 해금
            TutorialStep.None,             // 3: 스킬 — 가이드 퀘스트 기반 해금
            TutorialStep.None,             // 4: 무기 — 가이드 퀘스트 기반 해금
        };

        // 탭별 해금에 필요한 가이드 퀘스트 chainIndex (-1 = 항상 열림)
        // 14종 순환 350개 체인 기준 (guide-quest-design.md 참조)
        private static readonly int[] TAB_UNLOCK_GUIDE_INDEX =
        {
            3,   // 0: 소환 — Q3(사냥연습) 완료 후 → Q4(첫 소환) 가능
            -1,  // 1: 캐릭터 — 항상 열림
            4,   // 2: 장비 — Q4(첫 소환) 완료 후 → Q5(장비 장착) 가능
            7,   // 3: 스킬 — Q7(몬스터 30마리) 완료 후 → Q8(스킬 강화) 가능
            12,  // 4: 무기 — Q12(챕터1 클리어) 완료 후 → Q13(무기 소환) 가능
        };

        private bool IsTabUnlocked(int index)
        {
            if (index < 0 || index >= TAB_UNLOCK_GUIDE_INDEX.Length) return false;
            int requiredGuideIndex = TAB_UNLOCK_GUIDE_INDEX[index];
            if (requiredGuideIndex < 0) return true;

            var qm = Quest.QuestManager.Instance;
            if (qm != null) return qm.CompletedGuideIndex >= requiredGuideIndex;

            // QuestManager 미초기화 시 SaveData 직접 조회 (OnEnable→LoadCompleted 사이 프레임 딤드 방지)
            var save = Core.Save.SaveManager.Instance?.CurrentData?.quest;
            if (save == null) return false;
            return save.completedGuideIndex >= requiredGuideIndex;
        }

        private void OnTabClicked(int index)
        {
            if (index < 0 || index >= _tabPanels.Length)
                return;

            // 잠금 확인
            if (!IsTabUnlocked(index))
            {
                AudioManager.Instance?.PlayUiSfx("sfx_ui_error");
                ToastUI.Show("가이드 퀘스트를 진행하세요", "🔒");
                return;
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_tab_switch");

            // 이미 열린 탭 다시 클릭 → 닫기
            if (_currentTabIndex == index)
            {
                ClosePanel(_currentTabIndex);
                UpdateTabVisuals(-1);
                _currentTabIndex = -1;

                UIState.NotifyPanelClosed();

                if (_hudPanel != null)
                    _hudPanel.SetDeathOverlayVisible(true);
                return;
            }

            // 이전 탭이 열려있으면 닫기
            if (_currentTabIndex >= 0 && _currentTabIndex < _tabPanels.Length)
            {
                ClosePanel(_currentTabIndex);
                UIState.NotifyPanelClosed();
            }

            // 퀵메뉴 패널 닫기 (도감/배틀패스/코스юm)
            CloseQuickMenuPanels();

            // 새 탭 열기
            // 2026-04-23 침묵 버그 #5 (Char_Equip/Skill NaN worldBound) 수정 시도한 1프레임 지연은
            // Play 모드 세션 검증에서 탭 자체가 안 열리는 부작용 발생으로 롤백.
            // 근본 원인은 UniTask cancellation/TabBar 라이프사이클 상호작용. 별도 세션에서 재조사.
            OpenPanel(index);
            UIState.NotifyPanelOpened();

            if (_hudPanel != null)
                _hudPanel.SetDeathOverlayVisible(false);

            UpdateTabVisuals(index);
            _currentTabIndex = index;
        }

        /// <summary>모든 패널을 닫고 탭 선택 초기화</summary>
        public void CloseAll()
        {
            if (_tabPanels == null) return;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null)
                    _tabPanels[i].SetActive(false);
            }

            UpdateTabVisuals(-1);
            _currentTabIndex = -1;
            UIState.Reset();

            if (_hudPanel != null)
                _hudPanel.SetDeathOverlayVisible(true);
        }

        /// <summary>퀵메뉴 패널(도감/배틀패스) 닫기</summary>
        private void CloseQuickMenuPanels()
        {
            string[] quickPanelNames = { "[UITK] CollectionBook", "[UITK] BattlePass" };
            var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                foreach (var name in quickPanelNames)
                {
                    if (doc.gameObject.name == name && doc.gameObject.activeSelf)
                        doc.gameObject.SetActive(false);
                }
            }
        }

        private void OpenPanel(int index)
        {
            if (index < 0 || index >= _tabPanels.Length || _tabPanels[index] == null)
                return;

            _tabPanels[index].SetActive(true);
        }

        private void ClosePanel(int index)
        {
            if (index < 0 || index >= _tabPanels.Length || _tabPanels[index] == null)
                return;

            _tabPanels[index].SetActive(false);

            // 캐릭터 탭(1) 닫을 때 모든 서브탭도 닫기
            if (index == 1)
            {
                foreach (var subName in CHAR_SUBTAB_NAMES)
                {
                    var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var doc in docs)
                    {
                        if (doc.gameObject.name == subName)
                            doc.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>외부에서 특정 탭을 프로그래밍 방식으로 선택한다.</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabPanels.Length) return;
            OnTabClicked(index);
        }

        private const string LOCKED_CLASS = "tab-btn--locked";

        /// <summary>탭 버튼 시각 상태 갱신</summary>
        private void UpdateTabVisuals(int selectedIndex)
        {
            if (_tabButtons == null) return;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;

                bool locked = !IsTabUnlocked(i);

                if (locked)
                {
                    _tabButtons[i].AddToClassList(LOCKED_CLASS);
                    _tabButtons[i].RemoveFromClassList(SELECTED_CLASS);
                }
                else
                {
                    _tabButtons[i].RemoveFromClassList(LOCKED_CLASS);
                    if (i == selectedIndex)
                        _tabButtons[i].AddToClassList(SELECTED_CLASS);
                    else
                        _tabButtons[i].RemoveFromClassList(SELECTED_CLASS);
                }
            }
        }
    }
}
