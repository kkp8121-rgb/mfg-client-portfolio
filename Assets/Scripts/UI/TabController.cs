using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// 탭 전환 관리. 팝업 토글 방식 + DOTween 슬라이드 연출.
    /// BasePopup 패널은 Show()/Hide()를 호출하여 정상 동작을 보장한다.
    /// 탭 바 Canvas를 sortingOrder 150으로 설정하여 패널(100) 위에 항상 렌더링한다.
    /// </summary>
    public class TabController : MonoBehaviour
    {
        [SerializeField] private TabButton[] tabButtons;
        [SerializeField] private GameObject[] tabPanels;

        private int _currentTabIndex = -1;
        private Sequence _panelSequence;
        private HudPanel _hudPanel;

        public int GetCurrentTabIndex() => _currentTabIndex;

        /// <summary>탭 패널이 하나라도 열려있으면 true</summary>
        public bool IsAnyPanelOpen => _currentTabIndex >= 0;

        private void Start()
        {
            EnsureTabBarOnTop();
            _hudPanel = FindFirstObjectByType<HudPanel>(FindObjectsInactive.Include);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                    tabButtons[i].Initialize(this);
            }
            CloseAll();
        }

        /// <summary>
        /// 탭 바가 패널 Canvas(sortingOrder=100) 위에 항상 렌더링되도록
        /// 자체 Canvas + GraphicRaycaster를 추가한다.
        /// </summary>
        private void EnsureTabBarOnTop()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 150;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        public void SwitchTab(int index)
        {
            if (index < 0 || index >= tabPanels.Length)
                return;

            _panelSequence?.Kill(true);

            if (_currentTabIndex == index)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    if (tabButtons[i] != null)
                        tabButtons[i].SetSelected(false);
                }
                ClosePanel(_currentTabIndex);
                _currentTabIndex = -1;

                // 전역 UI 상태 갱신
                UIState.NotifyPanelClosed();

                // 모든 패널 닫힘 → 사망 오버레이 복원
                if (_hudPanel != null)
                    _hudPanel.SetDeathOverlayVisible(true);
                return;
            }

            if (_currentTabIndex >= 0 && _currentTabIndex < tabPanels.Length)
            {
                ClosePanel(_currentTabIndex);
                // 이전 패널 닫힘 → 카운트 감소 (바로 새 패널이 열리므로)
                UIState.NotifyPanelClosed();
            }

            OpenPanel(index);

            // 전역 UI 상태 갱신
            UIState.NotifyPanelOpened();

            // 패널 열림 → 사망 오버레이 임시 숨김
            if (_hudPanel != null)
                _hudPanel.SetDeathOverlayVisible(false);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                    tabButtons[i].SetSelected(i == index);
            }

            _currentTabIndex = index;
        }

        public void CloseAll()
        {
            _panelSequence?.Kill(true);

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    var popup = tabPanels[i].GetComponent<BasePopup>();
                    if (popup != null)
                    {
                        popup.Hide();
                    }
                    else
                    {
                        var cg = GetOrAddCanvasGroup(tabPanels[i]);
                        cg.alpha = 0;
                        tabPanels[i].SetActive(false);
                    }
                }
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                    tabButtons[i].SetSelected(false);
            }

            _currentTabIndex = -1;

            // 전역 UI 상태 초기화
            UIState.Reset();

            // 모든 패널 닫힘 → 사망 오버레이 복원
            if (_hudPanel != null)
                _hudPanel.SetDeathOverlayVisible(true);
        }

        private void ClosePanel(int index)
        {
            if (index < 0 || index >= tabPanels.Length || tabPanels[index] == null)
                return;

            var popup = tabPanels[index].GetComponent<BasePopup>();
            if (popup != null)
            {
                popup.Hide();
            }
            else
            {
                AnimateClose(index);
            }
        }

        private void OpenPanel(int index)
        {
            if (index < 0 || index >= tabPanels.Length || tabPanels[index] == null)
                return;

            // 패널 배경이 반투명이면 불투명으로 보정 (전투 화면 비침 방지)
            EnsureOpaqueBackground(tabPanels[index]);

            var popup = tabPanels[index].GetComponent<BasePopup>();
            if (popup != null)
            {
                tabPanels[index].SetActive(true);
                popup.Show();
            }
            else
            {
                AnimateOpen(index);
            }
        }

        /// <summary>
        /// 패널의 Image 배경 alpha가 1 미만이면 1로 보정한다.
        /// BasePopup이 아닌 패널(CharacterPanel, ShopPanel 등)에서 전투 화면이 비치는 문제 방지.
        /// </summary>
        private static void EnsureOpaqueBackground(GameObject panel)
        {
            var img = panel.GetComponent<Image>();
            if (img == null)
            {
                // Image가 없으면 불투명 배경 추가
                img = panel.AddComponent<Image>();
                var theme = UIThemeManager.Instance;
                if (theme != null)
                    theme.ApplyPanelBackground(img);
                else
                    img.color = new Color(0.10f, 0.10f, 0.18f, 1f);
                return;
            }

            // Stone Kit 스프라이트가 있으면 color=white 보장
            if (img.sprite != null && img.sprite.name != "UISprite" && img.sprite.name != "Background")
            {
                img.color = Color.white;
                return;
            }

            // alpha가 1 미만이면 불투명으로 보정
            if (img.color.a < 1f)
            {
                var c = img.color;
                c.a = 1f;
                img.color = c;
            }
        }

        private void AnimateOpen(int index)
        {
            var panel = tabPanels[index];
            if (panel == null) return;

            panel.SetActive(true);
            var rt = panel.GetComponent<RectTransform>();
            var cg = GetOrAddCanvasGroup(panel);

            float slideOffset = 80f;
            var startPos = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(startPos.x, -slideOffset);
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            _panelSequence = DOTween.Sequence()
                .Append(DOTween.To(
                    () => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    new Vector2(startPos.x, 0), 0.3f).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, 0.2f))
                .OnComplete(() =>
                {
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                })
                .SetUpdate(true).SetLink(gameObject);
        }

        private void AnimateClose(int index)
        {
            var panel = tabPanels[index];
            if (panel == null) return;

            var rt = panel.GetComponent<RectTransform>();
            var cg = GetOrAddCanvasGroup(panel);
            cg.interactable = false;
            cg.blocksRaycasts = false;

            float slideOffset = 60f;

            _panelSequence = DOTween.Sequence()
                .Append(DOTween.To(
                    () => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    new Vector2(rt.anchoredPosition.x, -slideOffset), 0.2f).SetEase(Ease.InCubic))
                .Join(DOTween.To(() => cg.alpha, x => cg.alpha = x, 0f, 0.15f))
                .OnComplete(() =>
                {
                    panel.SetActive(false);
                    rt.anchoredPosition = Vector2.zero;
                })
                .SetUpdate(true).SetLink(gameObject);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject obj)
        {
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            return cg;
        }

        private void OnDestroy()
        {
            _panelSequence?.Kill();
        }
    }
}
