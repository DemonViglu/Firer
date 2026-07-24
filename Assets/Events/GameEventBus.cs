using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件总线 - 实现 IEventPublisher 接口
///
/// 特性：
/// - 类型安全的事件发布和订阅
/// - 防止重复订阅（同一个 handler 只能订阅一次）
/// - 支持多系统监听同一个事件
/// - 任何系统都可以取消自己的订阅，不影响其他系统
/// - 高效的重复检查（O(1) 而非 O(n)）
/// </summary>
public class GameEventBus : IEventPublisher
{
    private class EventSubscriptions
    {
        public Delegate combinedDelegate;
        public HashSet<Delegate> registeredHandlers = new();
    }

    private readonly Dictionary<Type, EventSubscriptions> m_Handlers = new();

    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (handler == null)
        {
            Debug.LogWarning("[EventBus] 尝试订阅 null handler");
            return;
        }

        var type = typeof(T);

        if (!m_Handlers.TryGetValue(type, out var subscription))
        {
            subscription = new EventSubscriptions();
            m_Handlers[type] = subscription;
        }

        if (subscription.registeredHandlers.Contains(handler))
        {
            Debug.LogWarning($"[EventBus] Handler 已订阅 {type.Name}，忽略重复订阅");
            return;
        }

        subscription.registeredHandlers.Add(handler);
        subscription.combinedDelegate = Delegate.Combine(subscription.combinedDelegate, handler);

        //Debug.Log($"[EventBus] ✓ 已订阅 {type.Name}，当前订阅者数: {GetSubscriberCount<T>()}");
    }

    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (handler == null)
        {
            Debug.LogWarning("[EventBus] 尝试取消订阅 null handler");
            return;
        }

        var type = typeof(T);

        if (!m_Handlers.TryGetValue(type, out var subscription))
        {
            Debug.LogWarning($"[EventBus] {type.Name} 没有任何订阅者");
            return;
        }

        if (!subscription.registeredHandlers.Remove(handler))
        {
            Debug.LogWarning($"[EventBus] Handler 未在 {type.Name} 中注册");
            return;
        }

        subscription.combinedDelegate = Delegate.Remove(subscription.combinedDelegate, handler);

        if (subscription.registeredHandlers.Count == 0)
        {
            m_Handlers.Remove(type);
            //Debug.Log($"[EventBus] ✓ 已取消订阅 {type.Name}，该事件类型现在没有订阅者");
        }
        else
        {
            //Debug.Log($"[EventBus] ✓ 已取消订阅 {type.Name}，当前订阅者数: {GetSubscriberCount<T>()}");
        }
    }

    public void Publish<T>(T gameEvent) where T : IGameEvent
    {
        if (gameEvent == null)
        {
            Debug.LogWarning("[EventBus] 尝试发布 null 事件");
            return;
        }

        var type = typeof(T);

        if (!m_Handlers.TryGetValue(type, out var subscription) || subscription.combinedDelegate == null)
        {
            //Debug.LogWarning($"[EventBus] {type.Name} 没有任何订阅者，事件被忽略");
            return;
        }

        var invocationList = subscription.combinedDelegate.GetInvocationList();
        Debug.Log($"[EventBus] 发布 {type.Name}，将通知 {invocationList.Length} 个订阅者");

        foreach (var invocation in invocationList)
        {
            try
            {
                var action = (Action<T>)invocation;
                action?.Invoke(gameEvent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBus] {type.Name} 处理异常: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public void Clear()
    {
        var count = m_Handlers.Count;
        m_Handlers.Clear();
        Debug.Log($"[EventBus] ⚠️ 已清除所有事件订阅 (共 {count} 个事件类型)");
    }

    public void ClearByType<T>() where T : IGameEvent
    {
        var type = typeof(T);
        if (m_Handlers.Remove(type))
        {
            Debug.Log($"[EventBus] ✓ 已清除 {type.Name} 的所有订阅");
        }
    }

    public int GetSubscriberCount<T>() where T : IGameEvent
    {
        var type = typeof(T);
        if (m_Handlers.TryGetValue(type, out var subscription))
        {
            return subscription.registeredHandlers.Count;
        }
        return 0;
    }

    public void DebugPrintAllSubscriptions()
    {
        Debug.Log("===== EventBus 订阅情况 =====");
        foreach (var kvp in m_Handlers)
        {
            var type = kvp.Key;
            var count = kvp.Value.registeredHandlers.Count;
            Debug.Log($"  {type.Name}: {count} 个订阅者");
        }
        Debug.Log("=============================");
    }
}
