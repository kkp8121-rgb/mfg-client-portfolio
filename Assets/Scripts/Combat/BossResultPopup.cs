using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace MkLike.Combat
{
    /// <summary>
    /// 보스전 CLEAR/FAILED 결과 팝업.
    /// 코드에서 동적 생성, VisualElement 기반.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BossResultPopup : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            _doc.sortingOrder = 330;

#if UNITY_EDITOR
            if (_doc.panelSettings == null)
            {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    "Assets/UI Toolkit/GamePanelSettings.asset");
                if (ps != null) _doc.panelSettings = ps;
            }
#else
            if (_doc.panelSettings == null)
            {
                foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    if (doc != _doc && doc.panelSettings != null)
                    {
                        _doc.panelSettings = doc.panelSettings;
                        break;
                    }
                }
            }
#endif
        }

        public void ShowClear(string bossName)
        {
            BuildUI("CLEAR", bossName, new Color(1f, 0.84f, 0f), true);
            AutoCloseAsync().Forget();
        }

        public void ShowFailed(string bossName, System.Action onRetry = null, System.Action onExit = null)
        {
            BuildUI("FAILED", bossName, new Color(0.7f, 0.2f, 0.2f), false);
            AutoExitFailedAsync().Forget();
        }

        private async UniTaskVoid AutoExitFailedAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                await UniTask.Delay(5000, cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            // 5초 경과 — 아무 버튼도 누르지 않음 → 파밍 모드 진입
            var stageManager = FindFirstObjectByType<StageManager>();
            stageManager?.EnterFarmingMode();

            Close();
        }

        private void BuildUI(string title, string bossName, Color titleColor, bool isClear)
        {
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[BossResultPopup] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.Clear();

            // 딤 배경
            var dim = new VisualElement();
            dim.style.position = Position.Absolute;
            dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
            dim.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            dim.style.alignItems = Align.Center;
            dim.style.justifyContent = Justify.Center;
            _root.Add(dim);

            // 타이틀
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 48;
            titleLabel.style.color = titleColor;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 12;
            dim.Add(titleLabel);

            // 보스 이름
            var nameLabel = new Label(bossName);
            nameLabel.style.fontSize = 20;
            nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            nameLabel.style.marginBottom = 30;
            dim.Add(nameLabel);

            if (isClear)
            {
                // 나가기 버튼
                var exitBtn = CreateButton("나가기", new Color(0.2f, 0.5f, 0.8f));
                exitBtn.RegisterCallback<ClickEvent>(_ => Close());
                dim.Add(exitBtn);

                var autoLabel = new Label("3초 후 자동 닫힘");
                autoLabel.style.fontSize = 14;
                autoLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                autoLabel.style.marginTop = 8;
                dim.Add(autoLabel);
            }
            else
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;

                var exitBtn = CreateButton("나가기", new Color(0.4f, 0.4f, 0.4f));
                exitBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    var sm = FindFirstObjectByType<StageManager>();
                    sm?.EnterFarmingMode();
                    Close();
                });
                exitBtn.style.marginRight = 12;
                btnRow.Add(exitBtn);

                var retryBtn = CreateButton("다시하기", new Color(0.3f, 0.7f, 0.2f));
                retryBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    var sm = FindFirstObjectByType<StageManager>();
                    sm?.RetryStage();
                    Close();
                });
                btnRow.Add(retryBtn);

                dim.Add(btnRow);
            }
        }

        private Button CreateButton(string text, Color bgColor)
        {
            var btn = new Button { text = text };
            btn.style.width = 140;
            btn.style.height = 50;
            btn.style.fontSize = 20;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = bgColor;
            btn.style.borderTopLeftRadius = 8;
            btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8;
            btn.style.borderBottomRightRadius = 8;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            return btn;
        }

        private async UniTaskVoid AutoCloseAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.Delay(3000, cancellationToken: token);
            Close();
        }

        private void Close()
        {
            if (_root != null) _root.Clear();
            Destroy(gameObject);
        }
    }
}
