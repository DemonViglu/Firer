# SnowValley 环境重建

正式场景保持 `Assets/Scenes/SnowValley_Playable.unity`。原角色未替换。

新版环境位于场景根 `SnowValley_RebuiltEnvironment`：约 1024 米见方的连续山谷网格、北向雪地路线、松林群和岩石坡地。地形分块保存到 `Assets/FirePlay/Art/SnowValleyRebuild/Meshes`，具有显式 MeshCollider 与 CampfirePlacementSurface。现有 WarmthSnowReceiver 的目标列表改为新地形 Renderer，继续只读取原热场事实。

作者入口：Unity 菜单 `FirePlay/Environment/Rebuild Snow Valley Landscape`。这是编辑器生成器，重复运行会重建它自己的环境根与网格资产；手工深化生成根之前应另存作者脚本或停止重复执行，避免覆盖手工摆放。

`SnowValley_BeforeRebuild.unity.bak` 是首次重建前的场景副本，不参与 Unity Build。旧环境分组仍留在原 Envir 内并停用；原角色、Gameplay 服务、活动 Anchor、公共篝火和世界对象保留。高台湖区域停用，主湖和主湖玩法保留。

`SnowValley_Overview.png` 为作者总览渲染，不是游戏运行或性能验收。当前是第一轮环境构形：近景营地细节、自然湖岸、路径材质过渡、植被密度和移动端 LOD 仍需继续深化。按用户要求未运行玩法测试；地形通行、交互距离、碰撞与 Android 性能待用户验收。
