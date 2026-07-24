using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏实例子系统（全局依赖注入容器）
///
/// 这是整个框架的核心依赖注入系统。
/// 所有系统通过这个容器来注册和获取依赖，类似 UE 的 Subsystem。
///
/// 设计优势：
/// - 其他同学不需要知道具体实现，只需依赖接口
/// - 框架易于扩展和替换实现
/// - 便于单元测试（可以注入 Mock）
/// - 支持懒加载，保证服务永远可用
///
/// 使用示例：
/// // 注册服务（推荐在 GameBootstrapper 中）
/// GameInstanceSubsystem.Register<IEventPublisher>(new GameEventBus());
///
/// // 获取服务（严格模式，必须已注册）
/// var eventBus = GameInstanceSubsystem.Get<IEventPublisher>();
///
/// // 获取或创建（懒加载模式，自动创建默认实例）
/// var saveSystem = GameInstanceSubsystem.GetOrCreate<ISaveSystem>(() => new SaveManager());
///
/// // 尝试获取（可能返回 null）
/// var service = GameInstanceSubsystem.TryGet<IMyService>();
/// </summary>
public static class GameInstanceSubsystem
{
    /// <summary>
    /// 存储所有注册的服务
    /// Key: 服务接口类型
    /// Value: 服务实例
    /// </summary>
    private static readonly Dictionary<Type, object> services = new();

    /// <summary>
    /// 存储服务工厂方法
    /// 用于懒加载时自动创建实例
    /// </summary>
    private static readonly Dictionary<Type, Func<object>> factories = new();

    /// <summary>
    /// 注册一个服务
    /// 通常在游戏启动时（GameBootstrapper）调用
    ///
    /// 特点：显式注册，确保初始化顺序明确
    /// </summary>
    public static void Register<T>(T implementation) where T : class
    {
        var type = typeof(T);

        if (services.ContainsKey(type))
        {
            Debug.LogWarning($"[GameInstanceSubsystem] 服务 {type.Name} 已注册，将被覆盖");
        }

        services[type] = implementation;
        //Debug.Log($"[GameInstanceSubsystem] ✓ 注册服务：{type.Name}");
    }

    /// <summary>
    /// 注册一个服务工厂（用于懒加载）
    ///
    /// 特点：定义了如何创建这个服务，但不立即创建
    /// 第一次 GetOrCreate 时才会调用工厂方法
    ///
    /// 示例：
    /// GameInstanceSubsystem.RegisterFactory<ISaveSystem>(() => new SaveManager());
    /// </summary>
    public static void RegisterFactory<T>(Func<T> factory) where T : class
    {
        var type = typeof(T);

        if (factories.ContainsKey(type))
        {
            Debug.LogWarning($"[GameInstanceSubsystem] 工厂 {type.Name} 已注册，将被覆盖");
        }

        factories[type] = () => factory();
        Debug.Log($"[GameInstanceSubsystem] ✓ 注册工厂：{type.Name}");
    }

    /// <summary>
    /// 获取一个服务（严格模式）
    ///
    /// 特点：
    /// - 服务必须已注册（通过 Register 或 GetOrCreate 过）
    /// - 未注册时抛异常，便于发现问题
    /// - 适合核心系统（EventBus、GameManager 等）
    ///
    /// 抛异常条件：
    /// - 服务未注册
    /// </summary>
    public static T Get<T>() where T : class
    {
        var type = typeof(T);

        if (!services.TryGetValue(type, out var service))
        {
            throw new Exception($"[GameInstanceSubsystem] ✗ 服务 {type.Name} 未注册");
        }

        return service as T;
    }

    /// <summary>
    /// 获取或创建一个服务（懒加载模式）
    ///
    /// 特点：
    /// - 如果已注册，直接返回
    /// - 如果未注册但有工厂，调用工厂创建并返回
    /// - 如果有默认工厂，使用它创建
    /// - 如果都没有，尝试用默认构造函数创建
    /// - 永远不会返回 null（如果创建失败会抛异常）
    ///
    /// 用途：保证服务"绝对可用"，最大化懒加载灵活性
    ///
    /// 示例：
    /// // 1. 提前注册
    /// var saveSystem = GameInstanceSubsystem.GetOrCreate<ISaveSystem>(() => new SaveManager());
    ///
    /// // 2. 或者直接调用，自动创建（如果有无参构造函数）
    /// var config = GameInstanceSubsystem.GetOrCreate<GameConfig>();  // 自动调用 new GameConfig()
    /// </summary>
    public static T GetOrCreate<T>(Func<T> defaultFactory = null) where T : class
    {
        var type = typeof(T);

        // 步骤 1：已经注册了，直接返回
        if (services.TryGetValue(type, out var existing))
        {
            return existing as T;
        }

        // 步骤 2：有工厂方法，调用创建
        if (factories.TryGetValue(type, out var factory))
        {
            var instance = factory() as T;
            if (instance != null)
            {
                services[type] = instance;
                Debug.Log($"[GameInstanceSubsystem] ✓ 通过工厂创建服务：{type.Name}");
                return instance;
            }
        }

        // 步骤 3：有默认工厂，使用它
        if (defaultFactory != null)
        {
            var instance = defaultFactory();
            if (instance != null)
            {
                services[type] = instance;
                Debug.Log($"[GameInstanceSubsystem] ✓ 通过默认工厂创建服务：{type.Name}");
                return instance;
            }
        }

        // 步骤 4：尝试用默认构造函数创建
        try
        {
            var instance = Activator.CreateInstance(type) as T;
            if (instance != null)
            {
                services[type] = instance;
                Debug.Log($"[GameInstanceSubsystem] ✓ 通过默认构造创建服务：{type.Name}");
                return instance;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameInstanceSubsystem] ⚠️ 默认构造失败 {type.Name}：{ex.Message}");
        }

        // 步骤 5：都失败了，抛异常
        throw new Exception($"[GameInstanceSubsystem] ✗ 无法创建服务 {type.Name}：没有工厂方法或默认创建器");
    }

    /// <summary>
    /// 尝试获取一个服务
    ///
    /// 特点：
    /// - 如果服务未注册返回 null
    /// - 不抛异常
    /// - 适合可选的服务
    ///
    /// 返回值：
    /// - 已注册的实例
    /// - null（如果未注册）
    /// </summary>
    public static T TryGet<T>() where T : class
    {
        var type = typeof(T);

        if (services.TryGetValue(type, out var service))
        {
            return service as T;
        }

        return null;
    }

    /// <summary>
    /// 检查服务是否已注册
    /// </summary>
    public static bool IsRegistered<T>() where T : class
    {
        return services.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 检查服务是否有工厂（可以被懒加载）
    /// </summary>
    public static bool HasFactory<T>() where T : class
    {
        return factories.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 注销一个服务
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        var type = typeof(T);
        if (services.Remove(type))
        {
            Debug.Log($"[GameInstanceSubsystem] ✓ 注销服务：{type.Name}");
        }
    }

    /// <summary>
    /// 注销一个服务工厂
    /// </summary>
    public static void UnregisterFactory<T>() where T : class
    {
        var type = typeof(T);
        if (factories.Remove(type))
        {
            Debug.Log($"[GameInstanceSubsystem] ✓ 注销工厂：{type.Name}");
        }
    }

    /// <summary>
    /// 清空所有注册的服务
    /// 场景卸载或游戏结束时调用
    /// </summary>
    public static void Clear()
    {
        services.Clear();
        Debug.Log("[GameInstanceSubsystem] ✓ 所有服务已注销");
    }

    /// <summary>
    /// 清空所有工厂
    /// </summary>
    public static void ClearFactories()
    {
        factories.Clear();
        Debug.Log("[GameInstanceSubsystem] ✓ 所有工厂已注销");
    }

    /// <summary>
    /// 获取已注册的服务数量（仅用于调试）
    /// </summary>
    public static int GetServiceCount()
    {
        return services.Count;
    }

    /// <summary>
    /// 获取已注册的工厂数量（仅用于调试）
    /// </summary>
    public static int GetFactoryCount()
    {
        return factories.Count;
    }

    /// <summary>
    /// 调试输出：显示所有已注册的服务和工厂
    /// </summary>
    public static void DebugPrintStatus()
    {
        Debug.Log("===== GameInstanceSubsystem 状态 =====");
        Debug.Log($"已注册服务: {GetServiceCount()}");
        foreach (var service in services)
        {
            Debug.Log($"  ✓ {service.Key.Name}: {service.Value.GetType().Name}");
        }
        Debug.Log($"已注册工厂: {GetFactoryCount()}");
        foreach (var factory in factories)
        {
            Debug.Log($"  ⚙️ {factory.Key.Name}");
        }
        Debug.Log("=====================================");
    }
}
