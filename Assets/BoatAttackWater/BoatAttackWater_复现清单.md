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
  - 完成度（2026-10-26）：已直接复制参考源码，差异仅为格式与编码；C# 编译检查通过，未验证反射效果。
  - 特殊处理：暂缓学习和启用平面反射，后续作为独立专题；当前计划先走 Cubemap 环境反射，尚未接入。复制脚本本身不代表已启用平面反射。
  - 01～03 编译依据：使用当前 Unity 6000.6.2f1 自带 Roslyn，复用项目 Bee 生成的 `Assembly-CSharp.rsp`（相同引用、宏和分析器），对当前全部四个 C# 源文件重新编译，输出定向到临时目录；退出码 0，无诊断输出。本检查不包含 Unity 导入、域重载和运行效果。
- [x] **04** `Scripts/Data/WaterSettingsData.cs`：反射模式、几何模式等设置，依赖第 03 项。
  - 完成度（2026-10-27）：设置资源、反射模式枚举、几何模式枚举已写；当前 C# 源码编译检查通过，未验证 Inspector 和实际反射效果。
  - 特殊处理：暂时保留 `PlanarReflection` 与参数类型以兼容后续专项学习；当前用 `Cubemap` 路径时由配置选择 Cubemap，并可省略原版未使用的 `isInfinite`、`originOffset`。

## 2. GPU 水面

- [ ] **05** `Shaders/GerstnerWaves.hlsl`：完整公式，保留 omni、origin、静态开关、Buffer/数组两条路径和衰减参数。
- [ ] **06** `Shaders/WaterInput.hlsl`：参数、纹理和采样器声明。
- [ ] **07** `Shaders/CommonUtilities.hlsl`：公共计算。
- [ ] **08** `Shaders/WaterLighting.hlsl`：光照、菲涅耳和反射采样。
- [ ] **09** `Shaders/WaterCommon.hlsl`：位移、法线、水深、吸收散射、折射、反射、泡沫和调试输出。
- [ ] **10** `Shaders/Water.shader`：属性、关键字、Pass 和渲染状态。

## 3. CPU 采样与主控

- [ ] **11** `Scripts/GerstnerWavesJobs.cs`：采样注册、CPU 波浪计算、Jobs 调度和结果读取。
- [ ] **12** `Scripts/Water.cs`：参数生成与上传、渐变纹理、水深捕获、反射初始化、相机跟随绘制、CPU 调度。

第 11、12 项互相引用，一起完成后检查编译。

- [ ] 对照 `Textures/`、`Meshes/` 配齐纹理、网格及导入设置。
- [ ] 创建 `Materials/Sea.mat`，配置 Water Shader。
- [ ] 创建并连接 `WaterSurfaceData.asset`、`WaterSettingsData.asset`、`WaterResources.asset`。
- [ ] 使用 `Resources.Load("WaterResources")` 时，将对应配置资产放在 `Resources/` 下。
- [ ] 对照 `SeaVisual.prefab` 装配测试水面，配置相机、灯光和水下地形。
- [ ] 启用 URP Depth Texture、Opaque Texture。

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
- [ ] **22** `Scripts/BuoyantObject.cs`：体素化、浸没量、浮力、阻力和非物理跟随。
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
