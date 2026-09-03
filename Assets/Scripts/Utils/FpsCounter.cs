using UnityEngine;

namespace MkLike.Utils
{
    /// <summary>
    /// 디버그용 FPS 카운터. 에디터 및 개발 빌드에서만 표시.
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        [SerializeField] private bool _showInEditor = true;

        private float _deltaTime;
        private float _fps;
        private float _updateInterval = 0.5f;
        private float _timer;
        private int _frameCount;

        private void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _frameCount++;
            _timer += Time.unscaledDeltaTime;

            if (_timer >= _updateInterval)
            {
                _fps = _frameCount / _timer;
                _frameCount = 0;
                _timer = 0f;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static GUIStyle _fpsStyle;
        private static GUIStyle _fpsShadowStyle;

        private void OnGUI()
        {
            if (!_showInEditor) return;

            if (_fpsStyle == null)
            {
                _fpsStyle = new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold };
                _fpsShadowStyle = new GUIStyle(_fpsStyle);
                _fpsShadowStyle.normal.textColor = Color.black;
            }

            float msec = _deltaTime * 1000f;
            string text = $"FPS: {_fps:0.0} ({msec:0.0}ms)";

            _fpsStyle.normal.textColor = _fps >= 55f ? Color.green : _fps >= 30f ? Color.yellow : Color.red;

            GUI.Label(new Rect(11, 11, 200, 40), text, _fpsShadowStyle);
            GUI.Label(new Rect(10, 10, 200, 40), text, _fpsStyle);
        }
#endif
    }
}
