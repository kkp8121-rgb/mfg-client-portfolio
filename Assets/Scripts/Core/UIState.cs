using System;

namespace MkLike.Core
{
    /// <summary>
    /// UI 상태를 전역적으로 추적하는 정적 클래스.
    /// UI 레이어에서 설정하고, Combat 등 다른 레이어에서 읽는다.
    /// asmdef 순환 참조 없이 UI 상태를 공유하기 위한 브릿지.
    /// </summary>
    public static class UIState
    {
        /// <summary>
        /// 팝업 또는 탭 패널이 열려있으면 true.
        /// 월드 스페이스 이펙트(레벨업 텍스트 등)가 팝업 위에 렌더링되는 문제를 방지할 때 사용한다.
        /// </summary>
        public static bool IsAnyPanelOpen { get; set; }

        /// <summary>패널 열림/닫힘 시 발행. bool = IsAnyPanelOpen</summary>
        public static event Action<bool> OnPanelStateChanged;

        /// <summary>열려있는 팝업/패널 수 (중첩 대응)</summary>
        private static int _openCount;

        /// <summary>패널/팝업이 열릴 때 호출</summary>
        public static void NotifyPanelOpened()
        {
            _openCount++;
            IsAnyPanelOpen = _openCount > 0;
            OnPanelStateChanged?.Invoke(IsAnyPanelOpen);
        }

        /// <summary>패널/팝업이 닫힐 때 호출</summary>
        public static void NotifyPanelClosed()
        {
            _openCount = _openCount > 0 ? _openCount - 1 : 0;
            IsAnyPanelOpen = _openCount > 0;
            OnPanelStateChanged?.Invoke(IsAnyPanelOpen);
        }

        /// <summary>전체 초기화 (씬 전환 시)</summary>
        public static void Reset()
        {
            _openCount = 0;
            IsAnyPanelOpen = false;
            OnPanelStateChanged?.Invoke(false);
        }
    }
}
