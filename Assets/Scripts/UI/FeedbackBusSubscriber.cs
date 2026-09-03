using UnityEngine;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// FeedbackEvent 구독자. EventBus에서 이벤트를 받아 3-tier 피드백으로 발화한다.
    /// Tier 1 Toast (HudPanel.ShowToast) / Tier 2 ScreenShake / Tier 3 DamageText.
    /// 씬에 싱글턴으로 1개 배치. DontDestroyOnLoad 불필요 (HUD 라이프사이클과 동일).
    /// </summary>
    public class FeedbackBusSubscriber : MonoBehaviour
    {
        private static FeedbackBusSubscriber _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            EventBus<FeedbackEvent>.Subscribe(OnFeedback);
        }

        private void OnDisable()
        {
            EventBus<FeedbackEvent>.Unsubscribe(OnFeedback);
            if (_instance == this) _instance = null;
        }

        private void OnFeedback(FeedbackEvent evt)
        {
            // Tier 1: Toast (메시지 있을 때만)
            if (!string.IsNullOrEmpty(evt.Message))
                HudPanel.ShowToast(evt.Message);

            // Tier 2: ScreenShake (강도 지정 시)
            if (evt.ShakeIntensity > 0 && ScreenShakeManager.Instance != null)
            {
                switch (evt.ShakeIntensity)
                {
                    case 1: ScreenShakeManager.Instance.ShakeLight(); break;
                    case 2: ScreenShakeManager.Instance.ShakeMedium(); break;
                    case 3: ScreenShakeManager.Instance.ShakeHeavy(); break;
                }
            }

            // Tier 3: 수치 텍스트 (AmountText + WorldPosition 둘 다 있을 때)
            if (!string.IsNullOrEmpty(evt.AmountText)
                && evt.WorldPosition.HasValue
                && DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowBuffText(evt.WorldPosition.Value, evt.AmountText);
            }
        }
    }
}
