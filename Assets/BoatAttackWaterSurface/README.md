# BoatAttack 水面本体拆解

打开 `Scenes/BoatAttackSurface.unity`，点击 Play。菜单：`Water Example > BoatAttack Surface > Open Scene`。

本目录复制自本机 BoatAttack 的 `Packages/com.verasl.water-system` 和 `Assets/scenes/Testing/Water.unity`，针对 Unity 6000.6.2f1 / URP 17.6.0 提取并适配。

## 内容与代码入口

| 模块 | 文件 |
| --- | --- |
| 顶点与最终合成 | `Shaders/WaterCommon.hlsl` |
| Gerstner 波浪 | `Shaders/GerstnerWaves.hlsl` |
| 菲涅尔、反射、光照 | `Shaders/WaterLighting.hlsl` |
| 波浪参数、吸收/散射/泡沫 LUT | `Scripts/Water.cs` |
| 水面组件、俯视水底纹理 | `Scripts/Water.cs` |
| 平面反射相机 | `Scripts/Rendering/PlanarReflections.cs` |
| 水底焦散 | `Shaders/Caustics.shader`、`Scripts/Rendering/WaterCausticsFeature.cs` |
| 静态俯视高度编码 | `Shaders/SurfaceDepth.shader`、`Scripts/Rendering/WaterTerrainDepthFeature.cs` |

`Reference` 保存未经提取的原始源码文本与原测试场景文本，用于对照。`.txt` 后缀防止 Unity 编译重复脚本和 Shader；不要直接删掉后缀。

## 场景与网格

- 复制原测试场景中 26 个静态基本体的位置、旋转、缩放和地形层设置，保留用于观察遮挡、反射与岸线的测试布局。场景没有船、浮力、刚体模拟或交互。
- 天空、水纹、泡沫、Foam Ramp、反射 Cubemap 和两份原水面 FBX 已复制。测试物体的原材质 GUID 在源工程中没有对应资产，因此新建 URP Lit 测试材质；原烘焙光照与后处理不迁移。
- 当前水面使用 `Meshes/SurfaceGrid.asset`：100×100 米、256×256 格、66,049 顶点、131,072 三角形、间距约 0.391 米，使用 UInt32 索引并扩大包围盒容纳顶点位移。
- 已有足够细分的平面，不需要自行导入。`OriginalWater.fbx`、`OriginalSeaVertDisp.fbx` 保留原网格供后续比较；暂不使用原来的跟随相机分块绘制。
- 网格密度依据最短波长选择：练习可先以每波长约 8–16 个采样点为目标，再检查轮廓；扩大水面时应考虑分块与 LOD，不能无限增加均匀细分。

## 调参和分层查看

选择水面对象，编辑 `surfaceData`：`_basicWaveSettings` 控制波浪，`_absorptionRamp` / `_scatterRamp` 控制水色，`_foamSettings` 控制泡沫。编辑这些数据资产会刷新水面。

选择 `Materials/Sea.mat`，修改 `Debug mode` 查看 Normal、Foam、Refraction、Reflection、SSS、WaterDepth。`Static` 冻结动画便于比较。

`Data/WaterSettingsData.asset` 切换 PlanarReflection / Cubemap / ReflectionProbe。默认使用平面反射；探针模式需在场景中另设探针。

修改地形后，在水面组件右键菜单选 `Capture Depth`，或 `Refresh Water Parameters and Depth`。第 10 层仅用于需要影响浅水波浪的地形；不要把水面或所有悬空测试物体放到第 10 层。

## 提取时的修改

- 去掉 `_WaterFXMap` 的顶点位移、法线扰动和交互泡沫，保留波峰与岸边泡沫。CPU 波浪采样、Jobs/Burst、浮力、尾迹和飞溅粒子不参与运行。
- 原 `Water.cs` 波浪生成和 LUT 算法保留在 `Water.cs`；运行部分改为显式 MeshRenderer 和资源引用，避免迁移整个游戏的分块/资源系统。
- 原焦散投影和混合算法改为 RenderGraph 全屏 Pass，使用 URP 标准位置重建；原兼容模式 Pass 保存在 Reference。
- 俯视相机通过独立 Renderer 将世界高度编码进 RFloat 纹理，供原水深解码使用；替代旧 SRP 向原生 Depth RT 直接渲染的路径。捕获时显式绘制地形网格，避免 GPU Resident Drawer 缓存材质与 override-material 绘制不一致。
- 平面反射改用 SubmitRenderRequest，显式选择新 Renderer、排除水面、保护剔除/画质状态并按画面尺寸更新纹理。
- 在 PC/Mobile 管线资产尾部加入 SurfaceRenderer、DepthRenderer，原默认 Renderer 仍为索引 0，新场景相机使用索引 1/2。
- 原 Gerstner 位移/法线和水体光照近似保留，便于源代码拆解；不将这些美术近似视为完整物理模型。

## 验证

菜单 `Validate and Capture` 检查 Shader 编译、生成整体与分层截图，并确认俯视深度图存在有效变化。输出在项目 `Logs/BoatAttackSurface`。

本次已在 Unity 6000.6.2f1 验证：C#/Shader 编译、1280×720 实际渲染、保存场景重新加载并进入 Play、有效俯视深度，以及法线/泡沫/反射/折射/水深分层输出。日志为 `unity-validation.log`、`unity-playmode.log`。

`Create or Rebuild` 会重建本场景、测试材质及规则网格，覆盖这些资产的手动调整；日常使用只需 Open Scene。

源代码与资源许可见本目录 `LICENSE.md`。

## 场景模型目录

水面代码保持在 `Shaders`、`Scripts`、`Editor`；场景素材统一放在 `Objects`：

- `Environment`：岩壁、石头、树木，按模型/贴图/材质/预制体存放。
- `Buildings`：房屋、码头及其模型、材质和贴图。
- `Props`：布景道具，预制体已去掉缺失的游戏脚本和粒子效果。
- `Island/Groups`：原岛屿的建筑和环境组合；`Island/Environment.prefab` 为整体静态环境。
- `Island/Terrain`：TerrainGroup、地形数据和 TerrainLayer。
- `Boats`：船的静态 FBX、贴图和 URP Lit 材质；不保留驾驶、浮力、音效和尾迹预制体。
- `Shared`：上述场景材质所需的 Shader Graph、子图、HLSL 和公共贴图。它们用于场景物体，水面 Shader 仍在外层 `Shaders`。

UI、菜单、比赛逻辑、游戏相机、音效、云系统和旧版游戏依赖已从 Assets 移走；保留的资源随 `.meta` 一起移动，维持 GUID 引用。旧素材及被修改的原始预制体备份在项目 `Logs/ObjectsCleanup/20261014-160755`，不参与 Unity 导入。

通过菜单 `Water Example > BoatAttack Surface > Validate Scene Models` 检查静态预制体、网格、材质和 Shader，报告在 `Logs/ObjectsCleanup/validation.txt`。本次 Unity 编辑器检查 71 个预制体、27 个材质，错误数为 0；不会打开或保存当前场景。
