using UnityEngine;
/// <summary>
/// 游戏启动器
///
/// 职责：
/// - 初始化所有核心系统
/// - 注册所有依赖到 ServiceLocator
/// - 保证系统加载顺序
///
/// 使用方式：
/// 1. 在最开始的场景（如 Splash 或 Loading）放一个空 GameObject
/// 2. 挂载这个脚本
/// 3. 设置依赖系统的引用
/// 4. 脚本会自动初始化一切
/// </summary>
public class GameBootstrapper : MonoBehaviour
{


    private void Awake()
    {
        // 确保只有一个 Bootstrapper
        var existingBootstrapper = FindAnyObjectByType<GameBootstrapper>();
        if (existingBootstrapper != this)
        {
            Debug.LogWarning("[GameBootstrapper] 场景中已有 Bootstrapper，销毁重复的");
            Destroy(gameObject);
            return;
        }

        // 不销毁该对象（跨场景保持）
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameBootstrapper] 启动游戏...");

        // 初始化核心系统
        InitializeCoreSystems();
    }

    /// <summary>
    /// 初始化核心系统
    /// </summary>
    private void InitializeCoreSystems()
    { 
        // 1. 注册事件系统
        var eventBus = new GameEventBus();
        GameInstanceSubsystem.Register<IEventPublisher>(eventBus);
        //Debug.Log("[GameBootstrapper] ✓ 事件系统初始化");


        var gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("[GameBootstrapper] 场景中未找到 GameManager，自动创建");
            var go = new GameObject("GameManager");
            gameManager = go.AddComponent<GameManager>();
        }
    }
}
