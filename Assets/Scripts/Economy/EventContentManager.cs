using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 이벤트 콘텐츠 매니저.
    /// 한정 던전, 특별 보스 레이드, 탑 침공 등 재사용 가능한 이벤트 프레임워크.
    /// </summary>
    public class EventContentManager : MonoBehaviour
    {
        public static EventContentManager Instance { get; private set; }

        [SerializeField] private List<EventContentDataSO> _eventCatalog = new();

        private readonly List<ActiveEvent> _activeEvents = new();
        private float _expiredCheckTimer;
        private const float EXPIRED_CHECK_INTERVAL = 60f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _expiredCheckTimer += Time.unscaledDeltaTime;
            if (_expiredCheckTimer >= EXPIRED_CHECK_INTERVAL)
            {
                _expiredCheckTimer = 0f;
                CheckExpiredEvents();
            }
        }

        /// <summary>
        /// 이벤트를 시작한다.
        /// </summary>
        public void StartEvent(EventContentDataSO data, DateTime startDate)
        {
            if (data == null)
            {
                Debug.LogWarning("[EventContentManager] data가 null");
                return;
            }

            // 이미 활성 중인 동일 이벤트 체크
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (_activeEvents[i].Data != null && _activeEvents[i].Data.EventId == data.EventId)
                {
                    Debug.LogWarning($"[EventContentManager] 이벤트 '{data.EventId}'가 이미 활성 중");
                    return;
                }
            }

            var activeEvent = new ActiveEvent
            {
                Data = data,
                StartDate = startDate,
                EndDate = startDate.AddDays(data.DurationDays),
                TodayEntries = 0,
                LastEntryDate = ""
            };

            _activeEvents.Add(activeEvent);

            // 일일 입장 횟수 로드
            LoadDailyEntries(activeEvent);

            EventBus<EventContentStartedEvent>.Publish(new EventContentStartedEvent
            {
                EventId = data.EventId,
                EventType = data.EventType,
                DurationDays = data.DurationDays
            });

            Debug.Log($"[EventContentManager] 이벤트 시작: {data.DisplayName} ({data.DurationDays}일)");
        }

        /// <summary>
        /// 이벤트를 종료한다.
        /// </summary>
        public void EndEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                if (_activeEvents[i].Data != null && _activeEvents[i].Data.EventId == eventId)
                {
                    var data = _activeEvents[i].Data;
                    ClearDailyEntries(eventId);
                    _activeEvents.RemoveAt(i);

                    EventBus<EventContentEndedEvent>.Publish(new EventContentEndedEvent
                    {
                        EventId = eventId,
                        EventType = data.EventType
                    });

                    Debug.Log($"[EventContentManager] 이벤트 종료: {data.DisplayName}");
                    return;
                }
            }

            Debug.LogWarning($"[EventContentManager] 종료할 이벤트를 찾을 수 없음: {eventId}");
        }

        /// <summary>
        /// 이벤트 입장 가능 여부를 반환한다.
        /// 기간 + 일일 횟수를 검사한다.
        /// </summary>
        public bool CanEnterEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;

            var activeEvent = FindActiveEvent(eventId);
            if (activeEvent == null) return false;
            if (!activeEvent.IsActive) return false;

            // 일일 제한 체크 (0 = 무제한)
            if (activeEvent.Data.DailyEntryLimit <= 0) return true;

            ResetDailyIfNeeded(activeEvent);
            return activeEvent.TodayEntries < activeEvent.Data.DailyEntryLimit;
        }

        /// <summary>
        /// 이벤트에 입장한다. 일일 횟수를 차감한다.
        /// </summary>
        public void EnterEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var activeEvent = FindActiveEvent(eventId);
            if (activeEvent == null)
            {
                Debug.LogWarning($"[EventContentManager] 활성 이벤트를 찾을 수 없음: {eventId}");
                return;
            }

            if (!CanEnterEvent(eventId))
            {
                Debug.LogWarning($"[EventContentManager] 입장 불가: {eventId}");
                return;
            }

            ResetDailyIfNeeded(activeEvent);
            activeEvent.TodayEntries++;
            activeEvent.LastEntryDate = DateTime.Now.ToString("yyyy-MM-dd");
            SaveDailyEntries(activeEvent);

            Debug.Log($"[EventContentManager] 이벤트 입장: {activeEvent.Data.DisplayName} (오늘 {activeEvent.TodayEntries}/{activeEvent.Data.DailyEntryLimit})");
        }

        /// <summary>
        /// 이벤트를 완료하고 보상을 지급한다.
        /// </summary>
        public void CompleteEvent(string eventId, float score)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var activeEvent = FindActiveEvent(eventId);
            if (activeEvent == null)
            {
                Debug.LogWarning($"[EventContentManager] 활성 이벤트를 찾을 수 없음: {eventId}");
                return;
            }

            // 보상 지급
            var data = activeEvent.Data;
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(data.RewardCurrency, data.RewardAmount);
            }
            else
            {
                Debug.LogWarning("[EventContentManager] CurrencyManager 인스턴스 없음 — 보상 지급 실패");
            }

            // 코스튬 보상 지급
            if (!string.IsNullOrEmpty(data.RewardCostumeId))
            {
                GrantCostumeReward(data.RewardCostumeId);
            }

            EventBus<EventContentCompletedEvent>.Publish(new EventContentCompletedEvent
            {
                EventId = eventId,
                RewardType = data.RewardCurrency,
                RewardAmount = data.RewardAmount
            });

            Debug.Log($"[EventContentManager] 이벤트 완료: {data.DisplayName}, 보상: {data.RewardCurrency} x{data.RewardAmount}, 점수: {score}");
        }

        /// <summary>
        /// 이벤트 보상으로 코스튬을 지급한다.
        /// </summary>
        public void GrantCostumeReward(string costumeId)
        {
            if (string.IsNullOrEmpty(costumeId)) return;

            if (Costume.CostumeManager.Instance != null)
            {
                Costume.CostumeManager.Instance.AddCostume(costumeId, costumeId, "Rare");
                Debug.Log($"[EventContentManager] 코스튬 보상 지급: {costumeId}");
            }
            else
            {
                Debug.LogWarning($"[EventContentManager] CostumeManager 없음, 코스튬 보상 스킵: {costumeId}");
            }
        }

        /// <summary>
        /// 현재 활성 이벤트 목록을 반환한다.
        /// </summary>
        public List<ActiveEvent> GetActiveEvents()
        {
            return new List<ActiveEvent>(_activeEvents);
        }

        /// <summary>
        /// 만료된 이벤트를 자동 종료한다.
        /// </summary>
        public void CheckExpiredEvents()
        {
            // 역순 순회로 안전하게 제거
            var expiredIds = new List<string>();
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (!_activeEvents[i].IsActive)
                {
                    expiredIds.Add(_activeEvents[i].Data.EventId);
                }
            }

            for (int i = 0; i < expiredIds.Count; i++)
            {
                EndEvent(expiredIds[i]);
            }
        }

        private ActiveEvent FindActiveEvent(string eventId)
        {
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (_activeEvents[i].Data != null && _activeEvents[i].Data.EventId == eventId)
                {
                    return _activeEvents[i];
                }
            }
            return null;
        }

        private void ResetDailyIfNeeded(ActiveEvent activeEvent)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (activeEvent.LastEntryDate != today)
            {
                activeEvent.TodayEntries = 0;
                activeEvent.LastEntryDate = today;
                SaveDailyEntries(activeEvent);
            }
        }

        private void LoadDailyEntries(ActiveEvent activeEvent)
        {
            string key = $"EventContent_{activeEvent.Data.EventId}_Entries";
            string dateKey = $"EventContent_{activeEvent.Data.EventId}_Date";
            string savedDate = PlayerPrefs.GetString(dateKey, "");
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (savedDate == today)
            {
                activeEvent.TodayEntries = PlayerPrefs.GetInt(key, 0);
                activeEvent.LastEntryDate = savedDate;
            }
            else
            {
                activeEvent.TodayEntries = 0;
                activeEvent.LastEntryDate = today;
            }
        }

        private void SaveDailyEntries(ActiveEvent activeEvent)
        {
            string key = $"EventContent_{activeEvent.Data.EventId}_Entries";
            string dateKey = $"EventContent_{activeEvent.Data.EventId}_Date";
            PlayerPrefs.SetInt(key, activeEvent.TodayEntries);
            PlayerPrefs.SetString(dateKey, activeEvent.LastEntryDate);
            PlayerPrefs.Save();
        }

        private void ClearDailyEntries(string eventId)
        {
            string key = $"EventContent_{eventId}_Entries";
            string dateKey = $"EventContent_{eventId}_Date";
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(dateKey);
        }
    }

    /// <summary>
    /// 활성 이벤트 런타임 데이터.
    /// </summary>
    [Serializable]
    public class ActiveEvent
    {
        public EventContentDataSO Data;
        public DateTime StartDate;
        public DateTime EndDate;
        public int TodayEntries;
        public string LastEntryDate;

        public bool IsActive => DateTime.Now < EndDate;
        public int RemainingDays => Mathf.Max(0, (int)(EndDate - DateTime.Now).TotalDays);
    }
}
