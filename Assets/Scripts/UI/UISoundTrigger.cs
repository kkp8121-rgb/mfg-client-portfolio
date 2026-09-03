using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI 사운드를 이벤트 기반으로 트리거한다.
    /// AudioManager와 연동하여 재화 획득, 레벨업, 가챠, 챌린지, 환생 등의 사운드를 재생한다.
    /// </summary>
    public class UISoundTrigger : MonoBehaviour
    {
        public static UISoundTrigger Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void SubscribeEvents()
        {
            EventBus<CurrencyChangedEvent>.Subscribe(OnCurrencyChanged);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<PrestigeExecutedEvent>.Subscribe(OnPrestigeExecuted);
        }

        private void UnsubscribeEvents()
        {
            EventBus<CurrencyChangedEvent>.Unsubscribe(OnCurrencyChanged);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<PrestigeExecutedEvent>.Unsubscribe(OnPrestigeExecuted);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.CurrentAmount > e.PreviousAmount)
            {
                AudioManager.Instance?.PlayUiSfx("sfx_gold_pickup");
            }
        }

        private void OnLevelUp(LevelUpEvent e)
        {
            AudioManager.Instance?.PlayUiSfx("sfx_levelup");
        }

        private void OnGachaResult(GachaResultEvent e)
        {
            string sfxName = e.Grade?.ToLower() switch
            {
                "legendary" or "mythic" => "sfx_gacha_reveal_legendary",
                "epic" or "unique" => "sfx_gacha_reveal_rare",
                _ => "sfx_gacha_reveal_normal"
            };

            AudioManager.Instance?.PlayUiSfx(sfxName);
        }

        private void OnPrestigeExecuted(PrestigeExecutedEvent e)
        {
            AudioManager.Instance?.PlayUiSfx("sfx_ui_confirm");
        }
    }
}
