# BoatAttack 水体系统复现清单

目标：完整复现水面渲染、反射折射、水深、焦散、CPU 采样、浮力、尾迹及粒子交互。

- 参考：`E:/Project/Unity/BoatAttack/Packages/com.verasl.water-system/`。
- 目标：`Assets/BoatAttackWater/`，参考原工程实现；允许主动精简未使用或未完成的分支，检查以实际功能和引用一致性为准。
- 代码由使用者自行编写和修改；答疑检查时只更新清单，除非明确要求修改代码。
- 按编号推进；每块完成后检查代码、编译和效果。互相引用的文件需一起完成。
- 进度更新只记录完成度、修改内容，不新增待办项。
- 勾选表示本轮代码已完成（允许已注明的简化和少量差异）；效果验证和暂缓学习单独记录，不阻止代码项勾选。
- 对话中发送 `1` 表示：检查当前项代码、更新进度记录，再分块总览下一项参考源码。
- 下列路径默认相对参考包；标注 `Assets/` 的内容位于 BoatAttack 主工程。

## 0. 环境准备

- [ ] 安装兼容当前 Unity 的 Mathematics、Burst、Collections；保留当前 URP 版本。
- [ ] 尾迹使用原工程 Addressables 方案时，安装并配置 Addressables。
- [ ] 保持 `Wave` 的 C#/HLSL 数据布局一致；CPU/GPU 时间、水位和波浪参数一致。

## 1. 配置与反射基础

- [x] **01** `Scripts/Data/WaterSurfaceData.cs`：完整 Wave、定向/全向波、自动生成、颜色渐变和泡沫配置。
  - 完成度（2026-10-26）：本轮数据代码已完成，允许下述简化和差异；C# 编译检查通过，未验证效果。
  - 差距：尚无自动/自定义波浪切换字段 `_customWaves`。
  - 特殊处理：主动省略未接入运行时的 `liteFoam`、`mediumFoam`、`denseFoam`，不作为缺项；原版仅有对应 Editor 界面，custom 泡沫分支直接输出黑色。
  - 命名差异：使用 `randomseed`、`omniDir`，原版为 `randomSeed`、`onmiDir`；后续引用保持一致即可。`foamType` 注释 custom=2 符合实际索引，修正了原版注释的笔误。
- [x] **02** `Scripts/Data/WaterResources.cs`：纹理、材质、水面网格引用。
  - 完成度（2026-10-26）：资源字段和创建菜单已写齐，C# 编译检查通过；未验证资源加载。
  - 修改：默认资源名已修正为 `WaterResources`，泡沫字段已修正为 `defaultFoamMap`。
  - 命名差异：材质字段仍为 `dafaultWaterMaterial`，原版为 `defaultSeaMaterial`；后续自写调用保持一致即可。菜单改为 `WaterSystem/Resources` 不影响功能。
- [x] **03** `Scripts/Rendering/PlanarReflections.cs`：反射相机、裁剪平面、反射纹理及设置类型。
  - 最新接入（2026-10-28）：启用平面反射链路，Water.Init 自动绑定水面 target，平面偏移 -0.25 与 DrawMesh 一致；同步材质反射关键词，反射捕获期间跳过水面主动绘制，反射相机使用当前工程 Renderer 0，并在 finally 中恢复渲染状态。场景水面改用 Water 层，关闭重复 MeshRenderer，保留 DrawMesh 绘制；现有 Half 分辨率、0.07 裁剪偏移、排除 Water 的层遮罩及关闭阴影保持不变。当前 C# 编译通过，无诊断；本次实际反射画面未验证。下述暂缓说明为此前状态。
  - 完成度（2026-10-26）：已直接复制参考源码，差异仅为格式与编码；C# 编译检查通过，未验证反射效果。
  - 特殊处理：暂缓学习和启用平面反射，后续作为独立专题；当前已接入 Cubemap 环境反射。复制脚本本身不代表已启用平面反射。
  - 01～03 编译依据：使用当前 Unity 6000.6.2f1 自带 Roslyn，复用项目 Bee 生成的 `Assembly-CSharp.rsp`（相同引用、宏和分析器），对当前全部四个 C# 源文件重新编译，输出定向到临时目录；退出码 0，无诊断输出。本检查不包含 Unity 导入、域重载和运行效果。
- [x] **04** `Scripts/Data/WaterSettingsData.cs`：反射模式、几何模式等设置，依赖第 03 项。
  - 完成度（2026-10-27）：设置资源、反射模式枚举、几何模式枚举已写；当前 C# 源码编译检查通过，未验证 Inspector 和实际反射效果。
  - 特殊处理：暂时保留 `PlanarReflection` 与参数类型以兼容后续专项学习；当前用 `Cubemap` 路径时由配置选择 Cubemap，并可省略原版未使用的 `isInfinite`、`originOffset`。

## 2. GPU 水面

- [x] **05** `Shaders/GerstnerWaves.hlsl`：完整公式，保留 omni、origin、静态开关、Buffer/数组两条路径和衰减参数。
  - 完成度（2026-10-27）：单波位移、法线、多波叠加、定向/全向波、静态开关和浅水衰减已写；已修正 Wave 定义顺序，qi 分母使用 max(..., 0.01) 避免零振幅除零；数组和 Buffer 路径的临时 D3D 顶点入口均编译通过。尚未接入当前 WaterCommon.hlsl，不代表 Unity 水面效果已验证。
  - 边界处理（2026-10-28）：零波浪数时返回向上法线，全向波中心使用安全归一化，已消除 normalize(0)；分母下限会改变极小振幅时的陡峭度，属于当前稳定性处理。
  - 接入状态（2026-10-28）：已接入 WaterCommon；Water.cs 上传完整 omni/origin，修正 Update 并逐帧刷新参数，默认冻结波浪时间；完整 HLSL 十组顶点/片元共 20 次编译通过，并通过临时 Unity 场景渲染验证。
  - 特殊处理：省略原版 `saturate(amplitude * 10000)` 位移缩放；零振幅问题应在除法前处理，事后缩放不能可靠修复 NaN。
- [x] **06** `Shaders/WaterInput.hlsl`：参数、纹理和采样器声明。
  - 完成度（2026-10-27）：材质常量、全局参数、反射/屏幕/水面纹理及着色中间结构已写齐；独立声明编译检查通过，尚未接入当前 WaterCommon.hlsl。
  - 命名差异：深度相机参数使用 `_Water_DepthCamParams`，原版为 `_VeraslWater_DepthCamParams`；后续 HLSL 读取和 C# 上传需统一。
  - 特殊处理：当前省略原版未使用的 HLSL WaterSurfaceData 中间结构，不影响现有调用。
  - 05～06 验证：使用项目 URP 的 D3D11 API 宏，临时以 float 映射 half 精度并提供 _Time，组合 WaterInput 声明和 Gerstner 顶点调用；数组/Buffer × 动态/静态 × 无反射关键字/Cubemap/平面反射，共 12 种组合经 D3DCompile vs_5_0 编译通过。此检查覆盖声明及公式编译，不包含完整 Core.hlsl、Unity Shader 导入、纹理采样效果或移动端精度。
- [x] **07** `Shaders/CommonUtilities.hlsl`：公共计算。
  - 完成度（2026-10-27）：random、noise 与原版公式一致，未发现代码问题；临时顶点和片元入口调用实际函数，经 D3DCompile vs_5_0、ps_5_0 编译通过，尚未接入当前水面 Shader。
  - 特殊处理：主动省略原包水面 Shader 未调用的 Remap、HeightToNormal，仅保留当前使用的二维梯度噪声及其伪随机辅助函数。
- [x] **08** `Shaders/WaterLighting.hlsl`：光照、菲涅耳和反射采样。
  - 完成度（2026-10-27）：菲涅耳、软阴影和三种反射分支已写；按使用者授权修正 SmapleReflections → SampleReflections、SAMPLER_TEXTURECUBE → SAMPLE_TEXTURECUBE，并统一 Cubemap 分支使用 reflectionVector。
  - 验证：临时片元入口使用项目 D3D11 宏、WaterInput 声明和 half→float 映射，关闭主光源阴影后，无反射与 Cubemap 分支均通过 D3DCompile ps_5_0。尚未验证完整 URP Lighting.hlsl 集成、阴影/探针/平面反射分支及运行效果。
  - 特殊处理：省略未调用的 VertexLightingAndFog、Highlights；保留原版软阴影和暂缓学习的其他反射分支。
- [x] **09** `Shaders/WaterCommon.hlsl`：位移、法线、水深、吸收散射、折射、反射、泡沫和调试输出。
  - 完成度（2026-10-28）：顶点波浪、细节法线、水深、折射、反射、散射、泡沫、高光及调试输出已写；按授权修复 Scattering 大小写与 RGB 截取，并将 AdjustedDepth/WaterDepth 返回值统一为 float/float2，修复深度调试输出维数。
  - 特殊处理：深度图使用实际 near=0.01、far=_MaxDepth+4 解码，加入 UNITY_REVERSED_Z 分支并使用 LOD 0，恢复非零水面高度下的竖直水深；主控参数已接入，仍使用朝下正交相机和固定 XZ 捕获范围。临时 Unity 测试中，水底 Y=0、水面 Y=10/20 时读回水深分别为 10.00001/20.00001。
  - 特殊处理：WaterFX 位移、法线和泡沫交互暂时注释；对应调试分支输出中性值，避免引用未定义的 waterFX。水深仅保留视线厚度和竖直水深两个分量。
  - 验证：使用完整项目 Core/Lighting HLSL 引用，经 D3DCompile 对基础、Cubemap、主光阴影、级联与附加灯、探针、平面反射、Buffer+静态、水深调试、WaterFX 调试九组的顶点/片元共 18 次编译，全部通过。临时兼容 Unity 预处理（half 映射 min16float、空 PopMarker 宏）；存在精度转换提示，不代表完整 Unity 导入与效果验证。Shader 属性/关键字及主控资源绑定仍由 10、12 项完成。
- [x] **10** `Shaders/Water.shader`：属性、关键字、Pass 和渲染状态。
  - 完成度（2026-10-28）：材质属性、反射/Buffer/静态/调试变体、灯光阴影、实例化、雾和顶点/片元入口已写齐；已修复标签误置，将 RenderType 设为 Transparent、Queue 设为 Transparent-100。
  - 验证：实际项目 URP 引用下的基础、三种反射、阴影/附加灯、Buffer/静态、雾/实例化和调试共十组顶点/片元（20 次）独立 D3DCompile 编译通过。使用者刷新 Unity 后，Water.shader 已重新导入，新增 Editor.log 无 Shader 错误或警告，最新 UnityShaderCompiler 的 Cubemap 顶点/片元记录均为 ok=1；运行效果和构建变体保留情况尚未验证。

## 3. CPU 采样与主控

- [ ] **11** `Scripts/GerstnerWavesJobs.cs`：采样注册、CPU 波浪计算、Jobs 调度和结果读取。
  - 接入更新（2026-10-28）：已在 Water.Init 生成波浪后初始化 CPU 采样，LateUpdate 调度，停用/退出时释放；增加初始化状态检查和重复释放保护，浮力组件未就绪时暂停更新。当前八个 C# 源文件编译通过，仍有原有两条 autoSyncTransforms 警告；浮力公式、模式和场景配置未改，尚未验证本次运行效果。下条记录中的“CPU 调用仍注释”为本次接入前状态。
  - 进度（2026-10-28）：已复制参考 GerstnerWavesJobs.cs，补充函数、数组、采样注册及波浪计算注释；仅修复 onmiDir→omniDir 和 Unity 6000.6 对象标识迁移（GetEntityId/EntityId，三个脚本统一注册表键与参数类型）。当前全部八个 C# 源文件使用 Unity Roslyn 与项目 Bee 响应文件编译通过，无错误；保留 BuoyantObject 中两条 Physics.autoSyncTransforms 弃用警告，未改浮力公式、Job 调度或其他运行逻辑。Water.cs 的 CPU 初始化、更新与释放仍注释，尚未验证 Unity 本次导入及浮力运行，第 11 项暂不勾选。
- [ ] **12** `Scripts/Water.cs`：参数生成与上传、渐变纹理、水深捕获、反射初始化、相机跟随绘制、CPU 调度。
  - 最新修改（2026-10-28）：使用者改回接近原版的 Water.cs 后，补充功能分块注释；更新无排序参数的 FindObjectsByType、无 LightProbeProxyVolume 参数的 DrawMesh、URP SingleCameraRequest 和泛型 Resources.Load。统一目标工程字段及水深参数名，提前加载资源，CPU 调用按暂缓状态保留注释；补充波浪数量边界和临时资源释放处理。
  - 当前状态：保留原版相机跟随绘制、Buffer/数组上传和反射模式分支；自定义波浪开关位于 Water.customWaves。字段按功能归组，所有 Shader.PropertyToID 集中声明；方法内容和顺序保持一致。下述静态 GPU 版本的效果记录属于此前版本，不代表当前代码。当前全部 C# 使用 Unity 6000.6.2f1 的 Roslyn 和项目 Bee 响应文件编译通过，无错误、无警告；尚未验证本次 Unity 导入和运行效果，第 12 项仍未勾选。
  - 完成度（2026-10-28）：静态 GPU 水面链路已打通；完成自动/自定义波浪、完整数组上传、吸收/散射/泡沫渐变、水深捕获、Cubemap 环境反射、资源清理及场景引用。整体项保留未勾选，原版相机跟随绘制、Buffer 上传及 CPU 调度尚未实现。
  - 特殊处理：沿用现有 MeshRenderer 和网格，避免重复绘制；默认 staticWater=true 冻结波浪与 UV 时间，保留 Gerstner 波形，取消勾选可恢复运动。平面反射和 WaterFX 不启用，CPU/交互相关调用已注释。
  - 特殊处理：水深相机位于 (0, 水面高度+4, 0)，正交半宽 250，使用 URP 17 的 SingleCameraRequest 捕获；临时隐藏水面避免自身写入水深，捕获完成立即重新绑定纹理。无指定 Cubemap 时回退到天空环境贴图。
  - 验证：当前 C# 编译通过；隔离临时 Unity 项目使用 D3D11 实际渲染，水面可见、生成 6 条波浪、静态关键字生效、水深读回正确，禁用/重启通过，测试错误数为 0。尚未人工确认主工程岛屿场景的最终画面，也未验证打包。

本轮开始第 11 项 CPU 波浪采样的源码阅读与复现，后续与第 12 项主控联合检查。

- [ ] 对照 `Textures/`、`Meshes/` 配齐纹理、网格及导入设置。
- [x] 创建 `Materials/Water.mat`，配置 Water Shader。
- [x] 创建并连接 `WaterSurfaceData.asset`、`WaterSettingsData.asset`、`WaterResources.asset`。
  - 特殊处理（2026-10-28）：资源资产实际名为 `WaterResoureces.asset`，场景直接引用，不依赖 Resources.Load；已配置默认吸收/散射渐变及 Cubemap 模式。
- [ ] 使用 `Resources.Load("WaterResources")` 时，将对应配置资产放在 `Resources/` 下。
- [ ] 对照 `SeaVisual.prefab` 装配测试水面，配置相机、灯光和水下地形。
- [x] 启用 URP Depth Texture、Opaque Texture。

## 4. Editor 工具

- [ ] **13** `Editor/PlanarSettingsDrawer.cs`：平面反射设置界面。
- [ ] **14** `Editor/WaterSettingsDataEditor.cs`：渲染配置界面。
- [ ] **15** `Editor/WaterSurfaceDataEditor.cs`：波浪编辑、默认吸收/散射渐变初始化。
- [ ] **16** `Editor/WaterEditor.cs`：水组件界面、编辑后调用 Init 刷新。

检查：波浪运动与调参、反射折射、水深颜色、泡沫正常。

## 5. WaterFX 与焦散

- [ ] **17** `Shaders/WaterFXShader.shader`：写入泡沫、法线扰动和位移。
- [ ] **18** `Shaders/WaterFXFoamOnly.shader`：只写泡沫。
- [ ] **19** `Shaders/Caustics.shader`：水下焦散。
- [ ] **20** `Scripts/Rendering/WaterSystemFeature.cs`：WaterFX 和焦散 Pass。
- [ ] 在 URP Renderer 添加 Feature，配置焦散纹理，核对 `Materials/Caustics.mat` 设置。
- [ ] 核对原包路径、Shader.Find 名称和 RenderGraph API；迁移到目标目录后同步调整引用。

检查：`_WaterFXMap` 的泡沫、法线、位移通道生效，水下出现焦散。

## 6. 浮力

- [ ] **21** `Scripts/LocalToWorldJob.cs`：局部采样点转世界坐标。
  - 进度（2026-10-28）：已复制参考脚本，注册标识类型同步为 EntityId；补充矩阵转换、共享数组、Job 调度和释放注释，C# 编译通过，尚未验证运行。
- [ ] **22** `Scripts/BuoyantObject.cs`：体素化、浸没量、浮力、阻力和非物理跟随。
  - 进度（2026-10-28）：已复制参考脚本，GetInstanceID 改为 GetEntityId；补充全部函数、变量、浸水近似、阿基米德浮力、阻尼和受力转矩注释。除对象标识迁移外不改运行代码；编译通过，保留两条 autoSyncTransforms 弃用警告，未接入/验证浮力效果。
- [ ] **23** `Editor/BuoyantObjectEditor.cs`：配置与调试绘制。
- [ ] 用立方体检查 Rigidbody、Collider、质量、体素及重心，再接船体。
- [ ] 对照 CPU/GPU 公式差异，检查物体与水面贴合、Job 完成和 NativeArray 释放。

## 7. 尾迹、泡沫与粒子交互

以下参考路径位于 BoatAttack 主工程；脚本与资产在目标目录按用途组织。

- [ ] **24** `Assets/Scripts/Effects/WakeGenerator.cs`：双侧尾迹生成、扩散、消退。
- [ ] **25** `Assets/Scripts/Effects/BoatFoamGenerator.cs`：泡沫粒子位置和方向。
- [ ] **26** `Assets/Objects/misc/Wake.prefab`：尾迹 LineRenderer 配置。
- [ ] **27** `Assets/Materials/_dispWake_add.mat`、`_dispWake_sub.mat`：尾迹位移材质。
- [ ] **28** `Assets/Materials/_waterFoam.mat`、`_foam.mat`：泡沫材质。
- [ ] **29** `Assets/Objects/boats/_BoatBase.prefab`：船体、水花粒子、发射点和引用配置。
- [ ] 配齐相关纹理、粒子曲线、Renderer 设置和 Addressables 引用。
- [ ] 需要可驾驶测试船时，依次接 `Assets/Scripts/Boat/Engine.cs`、`Boat.cs`、`BaseController.cs`、`HumanController.cs`，处理竞速/输入等外部依赖。

检查：移动产生尾迹和泡沫，WaterFX 扰动可见，粒子正常发射和消退。

## 8. 其他几何分支与工程收尾

- [ ] **30** `Shaders/WaterTessellation.hlsl`：曲面细分计算。
- [ ] **31** `Shaders/WaterTessellated.shader`：曲面细分 Shader。
- [ ] **32** `Scripts/MainCameraAlign.cs`：独立水面物体随相机对齐，避免与 Water.cs 重复移动。
- [ ] **33** `Shaders/InfiniteWater.shader`、`Materials/InfiniteWater.mat`：远处水面；原工程标注为未完成分支，单独验证。
- [ ] **34** `Shaders/Graphs/Direction.shadersubgraph`、`GestnerWaves.shadersubgraph`：Shader Graph 子图，保留原拼写。
- [ ] **35** `Scripts/WaterSystem.Runtime.asmdef`、`Editor/WaterSystem.editor.asmdef`：程序集与依赖引用。
- [ ] 核对场景/Prefab、材质关键字、网格子资源、纹理导入和资源加载路径。
- [ ] 检查反复启停、场景切换、内存释放及实际打包运行。

说明：这是完整迁移顺序，不保证原工程每个分支本身都已完善；遇到错误或当前 URP 不兼容的 API 时，说明原因后修正。
