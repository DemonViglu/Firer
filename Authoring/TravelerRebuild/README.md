# 新雪地旅人源资产

这是从零制作的独立角色，不依赖旧角色网格或旧绑定。当前是第一版可编辑制作资产，尚未替换 SnowValley 或 Player Prefab。按用户要求没有继续运行玩法/网络/变形验收；不能据此宣称已达到最终生产质量。

## 文件

- `Traveler_Modular.blend`：人体分部件、衣装、头发、面部配件、完整骨架、动作与摄影棚。
- `../../Assets/FirePlay/Art/TravelerRebuild/Traveler_Modular.fbx`：Unity 交付网格、骨架和动作。（从工程根目录定位 Assets 路径。）
- `Preview/Traveler_Portrait.png`：造型预览，非游戏截图。
- `build_traveler.py`：重建源资产的作者脚本。再次运行会重新生成本目录的 blend/预览与新 FBX；手工修改前应另存副本，脚本不会合并手工修改。

## 部件与绑定

Blender 以米为单位，Z 向上、-Y 朝前；导出 FBX 使用 Unity 的 Y Up / -Z Forward 转换。人体约 1.7 米。

集合按 Skeleton、Body、Clothing、Hair、Face and Accessories 分组。头、躯干、四肢、手掌与手指网格可分别编辑；外套衣身/衣袖、裤装、靴子、围巾和背包独立。部件共享同一副骨架，不建立各自独立的骨骼。

骨架包含 root、hips、spine、chest、neck、head、双侧锁骨/上臂/前臂/手、每手五指三节、大腿/小腿/脚/脚趾。主要对应 Unity Humanoid 的 Hips、Spine、Chest、Neck、Head、Left/Right Shoulder、UpperArm、LowerArm、Hand、UpperLeg、LowerLeg、Foot、Toes。

权重由部位和关节环明确生成。肘、膝、腕附近使用连续两骨权重；头发和面部绑定 head，背包绑定 chest。没有全身距离自动加权，不让邻近的另一条腿或手臂获得错误权重。Body 和对应衣袖/裤腿使用同一权重函数。

## 动作

NLA 中有 Idle、Walk、Run、Jump、Wave、Thanks、Rest、Marshmallow、Fishing、Guitar、Stargaze。静止/移动/持续活动动作可循环；Jump、Wave、Thanks 为单次动作。每条 Action 有独立 NLA Track；在 Action Editor 选择动作查看和继续编辑，勿同时取消所有 Track 的静音进行预览。

动作使用四元数旋转、少量 hips 平移，无骨骼缩放动画。导出的网格保留休息骨架；绑定姿态为 T Pose，源文件默认展示 Idle。

## 当前仍需制作

- 肩袖及胯部目前采用分部件重叠接缝，需继续细化衣装接缝与关节外形；不可把第一版程序构形误认为最终人工精修拓扑。
- 手部抓握、脚底接触、起坐过渡和各活动一次性动作仍需进一步精修。没有面部表情 BlendShape。
- 尚未制作 Unity Humanoid Avatar、Animator Controller、装备挂点 Prefab 或替换正式角色。应在新目录完成接入后才切换正式 Player 的视觉引用。
- Android 材质/Renderer 合批、LOD 和性能验收尚未完成。

现有 Gameplay 动画参数继续复用，不因角色重建复制活动或资源状态机。
