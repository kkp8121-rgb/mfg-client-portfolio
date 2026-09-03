using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// UITK 패널의 close-btn을 자동 바인딩하는 유틸.
    /// 각 컨트롤러의 OnEnable에서 한 줄로 호출하면 닫기 버튼이 연결된다.
    /// ClickEvent + clicked 이중 등록으로 테스트(SimulateClick)와 실제 클릭 모두 동작.
    /// </summary>
    public static class PanelCloseHelper
    {
        /// <summary>
        /// 닫기 버튼 바인딩. 탭 패널이면 CloseAll(), 독립 패널이면 직접 비활성화.
        /// 어느 쪽이든 자신의 GO를 반드시 닫는다.
        /// </summary>
        public static void BindCloseButton(VisualElement root, GameObject panelGO)
        {
            if (root == null || panelGO == null) return;

            var closeBtn = root.Q<Button>("close-btn");
            if (closeBtn == null) return;

            void CloseAction()
            {
                var tabBar = Object.FindFirstObjectByType<TabBarUI>();
                if (tabBar != null)
                    tabBar.CloseAll();

                // 독립 패널(던전/상점 등)은 CloseAll 대상이 아니므로 직접 닫기
                if (panelGO.activeSelf)
                {
                    panelGO.SetActive(false);
                    UIState.NotifyPanelClosed();
                }
            }

            // clicked: 실제 유저 클릭 (PointerDown→Up→Clickable)
            closeBtn.clicked += CloseAction;
            // ClickEvent: SimulateClick(테스트) 대응
            closeBtn.RegisterCallback<ClickEvent>(_ => CloseAction());
        }

        /// <summary>
        /// 서브탭 패널용 — 자신을 닫고 부모 탭(캐릭터 스탯)으로 돌아간다.
        /// </summary>
        public static void BindCloseButtonAsSubtab(VisualElement root, GameObject panelGO)
        {
            if (root == null || panelGO == null) return;

            var closeBtn = root.Q<Button>("close-btn");
            if (closeBtn == null) return;

            void CloseAction()
            {
                panelGO.SetActive(false);

                // 캐릭터 스탯 탭으로 복귀
                var statTab = Object.FindFirstObjectByType<CharacterStatTabUI>(FindObjectsInactive.Include);
                if (statTab != null)
                    statTab.ReturnToStatTab();
            }

            closeBtn.clicked += CloseAction;
            closeBtn.RegisterCallback<ClickEvent>(_ => CloseAction());
        }
    }
}
