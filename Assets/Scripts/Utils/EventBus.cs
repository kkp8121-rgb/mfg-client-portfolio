using System;
using UnityEngine;

namespace MkLike.Utils
{
    /// <summary>
    /// 이벤트 마커 인터페이스.
    /// 모든 이벤트 구조체는 이 인터페이스를 구현해야 한다.
    /// </summary>
    public interface IEvent { }

    /// <summary>
    /// 제네릭 타입 기반 정적 이벤트 버스.
    /// 타입별로 독립적인 구독자 목록을 관리하여 시스템 간 느슨한 결합을 지원한다.
    /// GC 최소화를 위해 Action delegate를 사용한다.
    ///
    /// WebGL 안전: Publish 시 각 구독자를 개별 호출하여, 파괴된 오브젝트의
    /// 핸들러가 호출되더라도 다른 구독자에게 영향이 없도록 보호한다.
    /// (IL2CPP/AOT 환경에서 function signature mismatch 크래시 방지)
    ///
    /// Sticky 지원: PublishSticky로 발행된 마지막 이벤트는 캐시에 남아,
    /// 이후 SubscribeSticky로 구독하는 늦은 구독자에게도 즉시 재전달된다.
    /// 일회성 알림이 아닌 상태 이벤트(Load 완료, 세이브 준비 등)에만 사용할 것.
    /// </summary>
    /// <typeparam name="T">IEvent를 구현하는 이벤트 타입</typeparam>
    public static class EventBus<T> where T : IEvent
    {
        private static Action<T> _onEvent;

        // Sticky 캐시 — 타입별로 독립 (제네릭 정적 필드)
        private static T _lastPublished;
        private static bool _hasSticky;

        /// <summary>
        /// 이벤트를 구독한다. 이미 등록된 동일 핸들러는 중복 추가되지 않는다.
        /// </summary>
        /// <param name="handler">이벤트 발생 시 호출될 핸들러</param>
        public static void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            // 중복 구독 방지
            if (_onEvent != null)
            {
                var list = _onEvent.GetInvocationList();
                for (int i = 0; i < list.Length; i++)
                {
                    if ((Action<T>)list[i] == handler) return;
                }
            }
            _onEvent += handler;
        }

        /// <summary>
        /// Sticky 구독. 일반 Subscribe와 동일하게 등록하되,
        /// 이미 PublishSticky된 마지막 이벤트가 있으면 즉시 한 번 핸들러에 전달한다.
        /// OnEnable 시점에 초기 상태를 보장해야 하는 UI/시스템에 권장.
        /// </summary>
        /// <param name="handler">이벤트 발생 시 호출될 핸들러</param>
        public static void SubscribeSticky(Action<T> handler)
        {
            if (handler == null) return;
            Subscribe(handler);
            if (_hasSticky)
            {
                try
                {
                    handler.Invoke(_lastPublished);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[EventBus<{typeof(T).Name}>] sticky 재전달 중 예외: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        /// <param name="handler">구독 해제할 핸들러</param>
        public static void Unsubscribe(Action<T> handler)
        {
            _onEvent -= handler;
        }

        /// <summary>
        /// 이벤트를 발행한다. 등록된 모든 구독자에게 이벤트를 전달한다.
        /// 각 구독자를 개별 호출하여, 파괴된 오브젝트 핸들러가 있어도
        /// 나머지 구독자에게 영향이 없도록 보호한다.
        /// </summary>
        /// <param name="evt">발행할 이벤트 데이터</param>
        public static void Publish(T evt)
        {
            var handler = _onEvent;
            if (handler == null) return;

            // 멀티캐스트 delegate를 개별 호출하여 안전하게 실행
            var invocationList = handler.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    var action = (Action<T>)invocationList[i];

                    // Target이 Unity Object이면 파괴 여부 체크
                    if (action.Target is UnityEngine.Object unityObj && unityObj == null)
                    {
                        // 파괴된 오브젝트의 핸들러 — 자동 해제
                        _onEvent -= action;
                        Debug.LogWarning(
                            $"[EventBus<{typeof(T).Name}>] 파괴된 오브젝트의 핸들러를 자동 해제: {action.Method.DeclaringType?.Name}.{action.Method.Name}");
                        continue;
                    }

                    action.Invoke(evt);
                }
                catch (Exception ex)
                {
                    // 개별 핸들러 예외가 다른 구독자에 영향 주지 않도록 보호
                    Debug.LogWarning(
                        $"[EventBus<{typeof(T).Name}>] 핸들러 실행 중 예외 발생: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Sticky 발행. Publish와 동일하게 구독자에 전달하면서 마지막 이벤트를 캐시한다.
        /// 이후 SubscribeSticky로 구독하는 늦은 구독자는 캐시된 이벤트를 즉시 받는다.
        /// 상태 이벤트(Load 완료, BeforeSave 등)에만 사용할 것.
        /// </summary>
        /// <param name="evt">발행할 이벤트 데이터</param>
        public static void PublishSticky(T evt)
        {
            _lastPublished = evt;
            _hasSticky = true;
            Publish(evt);
        }

        /// <summary>
        /// Sticky 캐시만 제거. 구독자는 유지. 로그아웃/재시작 시 사용.
        /// </summary>
        public static void ClearSticky()
        {
            _lastPublished = default;
            _hasSticky = false;
        }

        /// <summary>
        /// Sticky 캐시 존재 여부.
        /// </summary>
        public static bool HasSticky => _hasSticky;

        /// <summary>
        /// 모든 구독자를 제거한다. 씬 전환이나 테스트 정리 시 사용한다.
        /// Sticky 캐시는 유지된다 (늦은 재구독을 위해).
        /// </summary>
        public static void Clear()
        {
            _onEvent = null;
        }
    }

    /// <summary>
    /// 타입 추론을 위한 편의 래퍼 클래스.
    /// EventBus&lt;T&gt;에 대한 단축 호출을 제공한다.
    /// </summary>
    public static class EventBus
    {
        /// <summary>
        /// 이벤트를 발행한다.
        /// </summary>
        public static void Publish<T>(T evt) where T : IEvent
        {
            EventBus<T>.Publish(evt);
        }

        /// <summary>
        /// Sticky 발행. 상태 이벤트용. 늦은 구독자에게 캐시된 이벤트 재전달.
        /// </summary>
        public static void PublishSticky<T>(T evt) where T : IEvent
        {
            EventBus<T>.PublishSticky(evt);
        }

        /// <summary>
        /// 이벤트를 구독한다.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            EventBus<T>.Subscribe(handler);
        }

        /// <summary>
        /// Sticky 구독. 캐시된 이벤트가 있으면 즉시 재전달.
        /// </summary>
        public static void SubscribeSticky<T>(Action<T> handler) where T : IEvent
        {
            EventBus<T>.SubscribeSticky(handler);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            EventBus<T>.Unsubscribe(handler);
        }

        /// <summary>
        /// 특정 이벤트 타입의 모든 구독자를 제거한다.
        /// </summary>
        public static void Clear<T>() where T : IEvent
        {
            EventBus<T>.Clear();
        }

        /// <summary>
        /// 특정 이벤트 타입의 sticky 캐시만 제거.
        /// </summary>
        public static void ClearSticky<T>() where T : IEvent
        {
            EventBus<T>.ClearSticky();
        }
    }
}
