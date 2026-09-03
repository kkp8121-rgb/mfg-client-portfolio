using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// UI 전역 관리자. 팝업 스택 관리 및 팝업 생성/소멸을 담당한다.
    /// 씬에 종속되므로 DontDestroyOnLoad를 사용하지 않는다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private Transform popupParent; // 팝업이 생성될 부모 Transform

        // 팝업 스택 — 가장 최근에 열린 팝업이 top
        private readonly Stack<BasePopup> _popupStack = new Stack<BasePopup>();

        // 팝업 프리팹 캐시 (Resources/UI/Popups/ 에서 로드)
        private readonly Dictionary<string, BasePopup> _popupPrefabCache = new Dictionary<string, BasePopup>();

        /// <summary>열린 팝업이 하나라도 있으면 true</summary>
        public bool HasOpenPopup => _popupStack.Count > 0;

        private void Awake()
        {
            // 싱글톤 설정 (DontDestroyOnLoad 하지 않음)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // 씬에 존재하는 패널 인스턴스 캐시 (Phase2SetupEditor가 생성한 오브젝트)
        private readonly Dictionary<string, BasePopup> _scenePopupCache = new Dictionary<string, BasePopup>();

        /// <summary>
        /// 팝업을 열고 스택에 push한다.
        /// 1순위: 씬에 이미 존재하는 컴포넌트를 찾아서 Show
        /// 2순위: Resources/UI/Popups/{popupName}에서 프리팹 로드
        /// 3순위: 동적 생성 (AddComponent)
        /// </summary>
        public T OpenPopup<T>(string popupName) where T : BasePopup
        {
            // 1순위: 씬에 이미 존재하는 인스턴스 찾기
            T existing = FindScenePopup<T>(popupName);
            if (existing != null)
            {
                if (_popupStack.Contains(existing))
                    return existing; // 이미 열린 팝업은 재오픈하지 않음

                _popupStack.Push(existing);
                UIState.NotifyPanelOpened();
                existing.gameObject.SetActive(true);
                existing.Show();
                return existing;
            }

            // 2순위: Resources에서 프리팹 로드
            if (!_popupPrefabCache.TryGetValue(popupName, out BasePopup prefab))
            {
                string path = $"UI/Popups/{popupName}";
                GameObject loaded = Resources.Load<GameObject>(path);
                if (loaded != null)
                {
                    prefab = loaded.GetComponent<BasePopup>();
                    if (prefab != null)
                        _popupPrefabCache[popupName] = prefab;
                }
            }

            if (prefab != null)
            {
                BasePopup instance = Instantiate(prefab, popupParent);
                T popup = instance as T;
                if (popup != null)
                {
                    _popupStack.Push(popup);
                    UIState.NotifyPanelOpened();
                    popup.Show();
                    return popup;
                }
                Debug.LogError($"[UIManager] 팝업 캐스트 실패: {popupName} → {typeof(T).Name}");
                Destroy(instance.gameObject);
            }

            // 3순위: 동적 생성 fallback
            T dynamicPopup = CreateDynamicPopup<T>(popupName);
            if (dynamicPopup != null)
            {
                _popupStack.Push(dynamicPopup);
                UIState.NotifyPanelOpened();
                dynamicPopup.Show();
                return dynamicPopup;
            }

            Debug.LogWarning($"[UIManager] 팝업을 열 수 없습니다: {popupName} (씬/Resources/동적 생성 모두 실패)");
            return null;
        }

        private T FindScenePopup<T>(string popupName) where T : BasePopup
        {
            // 캐시 확인
            if (_scenePopupCache.TryGetValue(popupName, out BasePopup cached) && cached != null)
                return cached as T;

            // 씬에서 타입으로 검색
            T found = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found != null)
            {
                _scenePopupCache[popupName] = found;
                return found;
            }

            // 이름으로 검색 (Phase2SetupEditor가 생성한 이름)
            var go = GameObject.Find(popupName);
            if (go != null)
            {
                var comp = go.GetComponent<T>();
                if (comp != null)
                {
                    _scenePopupCache[popupName] = comp;
                    return comp;
                }
            }

            return null;
        }

        private T CreateDynamicPopup<T>(string popupName) where T : BasePopup
        {
            Transform parent = popupParent != null ? popupParent : transform;
            var go = new GameObject(popupName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // Canvas 확인
            if (go.GetComponentInParent<Canvas>() == null)
            {
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                go.AddComponent<UnityEngine.UI.CanvasScaler>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            var popup = go.AddComponent<T>();
            _scenePopupCache[popupName] = popup;
            return popup;
        }

        /// <summary>
        /// 최상위 팝업을 닫고 스택에서 제거한다.
        /// </summary>
        public void ClosePopup()
        {
            if (_popupStack.Count == 0)
            {
                Debug.LogWarning("[UIManager] 닫을 팝업이 없습니다.");
                return;
            }

            BasePopup popup = _popupStack.Pop();
            UIState.NotifyPanelClosed();
            popup.Hide();

            // 씬 캐시에 있는 패널은 비활성화만, 동적 생성된 것은 Destroy
            if (_scenePopupCache.ContainsValue(popup))
                popup.gameObject.SetActive(false);
            else
                Destroy(popup.gameObject);
        }

        /// <summary>
        /// 모든 팝업을 닫고 스택을 비운다.
        /// </summary>
        public void CloseAllPopups()
        {
            while (_popupStack.Count > 0)
            {
                BasePopup popup = _popupStack.Pop();
                popup.Hide();

                if (_scenePopupCache.ContainsValue(popup))
                    popup.gameObject.SetActive(false);
                else
                    Destroy(popup.gameObject);
            }
            UIState.Reset();
        }

        /// <summary>
        /// 현재 최상위 팝업을 반환한다. 없으면 null.
        /// </summary>
        public BasePopup GetCurrentPopup()
        {
            return _popupStack.Count > 0 ? _popupStack.Peek() : null;
        }

        /// <summary>
        /// CharacterPanel을 TabBarUI를 통해 열고, 지정된 서브탭 UIDocument를 활성화한다.
        /// </summary>
        /// <param name="subTabIndex">서브탭 인덱스 (0=스탯, 1=장비, 2=스킬, 3=전직, 4=유물, 5=등반, 6=어빌리티)</param>
        public void OpenCharacterPanelTab(int subTabIndex)
        {
            // UI Toolkit TabBarUI를 통해 캐릭터 탭(인덱스 0)을 연다
            var tabBarUI = FindFirstObjectByType<TabBarUI>(FindObjectsInactive.Include);
            if (tabBarUI == null)
            {
                Debug.LogWarning("[UIManager] TabBarUI를 찾을 수 없습니다.");
                return;
            }

            // 캐릭터 탭이 아직 안 열려있으면 열기
            if (tabBarUI.GetCurrentTabIndex() != 0)
            {
                // TabBarUI의 탭 패널을 직접 활성화
                var charStatGo = GameObject.Find("[UITK] Char_Stat");
                if (charStatGo == null)
                {
                    var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var doc in docs)
                    {
                        if (doc.gameObject.name == "[UITK] Char_Stat")
                        {
                            charStatGo = doc.gameObject;
                            break;
                        }
                    }
                }
                if (charStatGo != null) charStatGo.SetActive(true);
            }

            // 서브탭 전환: 해당 서브탭 활성화, 나머지 비활성화
            // 2026-04-20 유물 시스템 완전 제거 — Char_Relic 제외
            string[] subTabNames =
            {
                "[UITK] Char_Stat", "[UITK] Char_Equip", "[UITK] Char_Skill",
                "[UITK] Char_Job", "[UITK] Char_Climber", "[UITK] Char_Ability"
            };

            if (subTabIndex < 0 || subTabIndex >= subTabNames.Length) return;

            var allDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var doc in allDocs)
            {
                for (int i = 0; i < subTabNames.Length; i++)
                {
                    if (doc.gameObject.name == subTabNames[i])
                    {
                        doc.gameObject.SetActive(i == subTabIndex);
                        break;
                    }
                }
            }
        }
    }
}
