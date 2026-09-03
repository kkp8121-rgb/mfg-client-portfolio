using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit의 모든 Button 클릭에 사운드 피드백을 연결하는 브릿지.
    /// UIDocument가 있는 GameObject에 추가하면 자동으로 모든 Button에 클릭 사운드를 등록한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIToolkitSoundBridge : MonoBehaviour
    {
        [SerializeField] private string _clickSfxKey = "sfx_ui_tap";

        private UIDocument _doc;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc?.rootVisualElement == null) return;

            RegisterButtonSounds(_doc.rootVisualElement);
        }

        private void RegisterButtonSounds(VisualElement root)
        {
            // 모든 Button 자손에 클릭 사운드 등록
            root.Query<Button>().ForEach(btn =>
            {
                btn.RegisterCallback<ClickEvent>(OnButtonClicked);
            });
        }

        private void OnButtonClicked(ClickEvent evt)
        {
            AudioManager.Instance?.PlayUiSfx(_clickSfxKey);
        }

        private void OnDisable()
        {
            if (_doc?.rootVisualElement == null) return;

            _doc.rootVisualElement.Query<Button>().ForEach(btn =>
            {
                btn.UnregisterCallback<ClickEvent>(OnButtonClicked);
            });
        }
    }
}
