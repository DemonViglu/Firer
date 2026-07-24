using System;

/// <summary>
/// 事件发布者接口
/// 用于解耦具体实现（EventBus / EventSystem）
/// </summary>
public interface IEventPublisher
{
    void Publish<T>(T gameEvent) where T : IGameEvent;
    void Subscribe<T>(Action<T> handler) where T : IGameEvent;
    void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;

    /// <summary>
    /// 清除所有订阅（谨慎使用）
    /// </summary>
    void Clear();

    /// <summary>
    /// 清除指定事件类型的所有订阅
    /// </summary>
    void ClearByType<T>() where T : IGameEvent;

    /// <summary>
    /// 获取指定事件的订阅者数量
    /// </summary>
    int GetSubscriberCount<T>() where T : IGameEvent;
}
