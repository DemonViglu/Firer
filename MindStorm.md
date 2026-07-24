FirePlay — Unity 水墨淡彩多人温馨游戏 架构规划 Context基于 Firer 项目已有的自研框架（GameInstanceSubSystem DI容器、GameEventBus 事件总线、FlowGraph 可视化脚本系统），开发一款火焰画笔 × 共享篝火的多人合作温馨游戏。
核心隐喻: 世界曾经是彩色的，后来褪色了。玩家手中的火苗是唯一的颜料——走过的地方，色彩复苏。篝火是世界的"心脏"，每个人添入的火焰让它跳动得更有力。
核心循环: 带火苗探暗世界（画笔作画）→ 回到篝火（汇入+看他人足迹）→ 接力探索更深处 → 世界越来越亮。
与《光遇》差异化: 小而美的共创空间、火苗是表达工具而非社交货币、水墨→彩色的视觉转变即玩法本身、无终点的陪伴而非史诗旅程。
 1. Unity 项目设置渲染管线: URP 3D（已安装）
新增包依赖:
com.unity.netcode.gameobjects — 多人网络（第3阶段启用）
com.unity.services.relay + com.unity.services.lobby — 房间匹配
com.unity.cinemachine — 第三人称相机
com.unity.inputsystem (已安装)
com.unity.textmeshpro — UI文本
com.unity.shadergraph — 水墨着色器（URP依赖，已安装）
com.unity.probuilder — 快速低模场景原型（可选）
 2. 文件夹结构Assets/
  Scenes/
    Boot.scene              # 启动场景 (GameBootstrapper)
    MainMenu.scene          # 主菜单
    Hub_Campfire.scene      # 篝火中心 (始终以 Single 加载)
    World_Forest.scene      # 示例暗世界关卡 (Additive 叠加)
    Loading.scene           # 过渡场景

  Scripts/
    Core/
      FirePlayBootstrapper.cs    # 初始化所有 FirePlay 系统到 GameInstanceSubsystem
    Systems/                     # 纯C#系统，接口+实现
      IFlameBrushSystem.cs       # 火苗状态：颜色、强度、历史
      FlameBrushSystem.cs
      ILightWorldSystem.cs       # 世界光照：区域水墨→彩色过渡
      LightWorldSystem.cs
      ICampfireHubSystem.cs      # 篝火：汇入、足迹展示、聚会
      CampfireHubSystem.cs
      IPlayerManager.cs          # 玩家注册表
      PlayerManager.cs
      IWorldMapSystem.cs         # 场景加载/区域解锁
      WorldMapSystem.cs
      IQuestSystem.cs            # FlowGraph 驱动的任务
      QuestSystem.cs
      ISaveSystem.cs             # 持久化（世界状态、玩家足迹）
      SaveSystem.cs
      IUISystem.cs               # UI面板管理
      UISystem.cs
      IAudioSystem.cs            # 自适应音乐
      AudioSystem.cs
    Gameplay/
      Flame/                     # 火苗 — 画笔/光源
        FlameBrush.cs            # 核心：火苗跟随玩家、投射光照、记录颜色
        FlameVisuals.cs          # Shader Graph 程序化火焰渲染
        FlameTrail.cs            # 走过留下的色彩拖尾
        FlameBeacon.cs           # 接力路标 — 留在路口供他人接续
      Light/                     # 光照机制
        LightEmitter.cs          # 发光体注册（火苗/篝火/水晶）
        LightInfluenceVolume.cs  # 区域触发器，绑定 areaId
        WorldBloom.cs            # 每区域一个，驱动 _LitAmount 着色器属性
        ColorRestorationOrder.cs # SO，定义区域色彩恢复的层次顺序
      Campfire/                  # 篝火中心
        Campfire.cs              # 中央篝火，汇入+展示
        CampfireChair.cs         # 座位交互点
        CampfireFootprints.cs    # 展示今日玩家足迹的视觉组件
      Player/                    # 玩家
        FirePlayPlayer.cs        # 玩家根组件
        PlayerMovement.cs        # 第三人称移动 (InputSystem)
        PlayerCamera.cs          # Cinemachine FreeLook
        PlayerInteraction.cs     # 世界交互
        PlayerEmote.cs           # 表情轮盘
      World/                     # 世界关卡
        WorldArea.cs             # 区域定义 (areaId, 解锁条件, 子区域列表)
        WorldPortal.cs           # 通往其他暗世界区域的传送门
        JointCanvas.cs           # 需要多人火焰颜色叠加的大型壁画
    Data/
      Events/
        FlameEvents.cs           # FlameColorChangedEvent, FlameMergedEvent …
        LightEvents.cs           # AreaIlluminatedEvent, WorldBloomProgressEvent …
        CampfireEvents.cs        # FlameOfferedEvent, FullCircleGatheredEvent …
        PlayerEvents.cs          # PlayerJoinedEvent, PlayerLeftEvent …
        WorldEvents.cs           # WorldAreaLoadedEvent, BeaconPlacedEvent …
      Config/
        FlameBrushConfig.cs      # SO: 火苗基础颜色、强度、光锥参数
        WorldAreaConfig.cs       # SO: 区域定义、色彩恢复层配置
        CampfireConfig.cs        # SO: 篝火规模阶段、粒子参数
      Rendering/
        InkLitShader.shadergraph       # 核心水墨Lit Shader
        InkFullscreen.shadergraph      # 全屏后期：宣纸纹理+柔边+飞白
        FlameProcedural.shadergraph    # 程序化火苗
        InkOutlineRenderFeature.cs     # 自定义描边RenderFeature

  FlowGraphs/                  # FlowGraph 资产
    Tutorials/
      Tutorial_FirstStep.asset
      Tutorial_LightWorld.asset
      Tutorial_Beacon.asset
      Tutorial_Campfire.asset
    Quests/
      Quest_RestoreBridge.asset
      Quest_MuralOfThree.asset
    Ambient/
      Ambient_CampfireGlow.asset

  Prefabs/                     # 可重用预制体
    FlameBrush.prefab
    Player.prefab
    Campfire.prefab
    LightInfluenceVolume.prefab
    Beacon.prefab
    UI/

  Art/                         # 美术资产
    Textures/                  # AI生成的贴图
      PaperFiber_Tiling.png    # 宣纸纤维纹理
      InkStroke_Atlas.png      # 墨笔笔触图谱
      FlyAway_Brush.png        # 飞白纹理
    Materials/
    Models/                    # 低模（Probuilder或Asset Store）

  Audio/
    Music/  SFX/
 3. 系统架构（全部注册到 GameInstanceSubsystem） 3.1 IFlameBrushSystem / FlameBrushSystem火苗是一个画笔+光源，不是宠物。不需要喂食、情绪、进化。
职责: 管理玩家火苗的颜色状态、强度、携带的历史颜色、接力路标。
关键方法:
// 颜色来自探索 — 触碰花则染粉、靠近水则染蓝
void AbsorbColor(ulong playerId, Color color, string source);
Color GetFlameColor(ulong playerId);
float GetFlameIntensity(ulong playerId);

// 持续增强 — 探索越多，火苗越亮
void BoostIntensity(ulong playerId, float amoun