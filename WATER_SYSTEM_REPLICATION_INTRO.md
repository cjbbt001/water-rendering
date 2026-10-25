# BoatAttack 水系统学习复刻项目介绍

## 项目目的

本项目用于学习和复刻 Unity BoatAttack 中的水面系统。重点不是完整复刻 BoatAttack 游戏，而是理解并重建它的水面渲染、波浪采样、浮力交互和水面特效链路。

目标是做出一个结构清晰、可逐步验证的 URP 水系统：

- 用 Gerstner Wave 生成动态海面。
- 在 GPU 上完成水面位移、法线、反射、折射、散射和泡沫。
- 在 CPU 上查询指定位置的水位和法线。
- 用查询结果实现浮动物体和船只的浮力。
- 用独立的 Render Feature 处理 WaterFX 和焦散。

## 参考项目

参考代码位于：

`E:/Project/Unity/BoatAttack`

水系统主要位于：

`Packages/com.verasl.water-system`

项目侧的使用代码主要位于：

- `Assets/Scripts/Boat/Engine.cs`
- `Assets/Scripts/Environment/BuoyManager.cs`
- `Assets/Scripts/Environment/WindsurferManager.cs`
- `Assets/Scripts/Effects/BoatFoamGenerator.cs`

当前参考项目使用 Unity 6000.3.16f1 和 URP 17.3.0。原水系统包较早，内部保留了一些旧版 URP、曲面细分和实验代码，因此复刻时应优先实现主链路。

## 整体架构

系统可以分成两条主链路。

### 1. GPU 视觉链路

`WaterSurfaceData` 和 `WaterSettingsData` 提供配置，`Water.cs` 将波浪参数、颜色渐变、泡沫资源、水深图和反射选项上传给 Shader。

主水面由 `Water.cs` 按相机位置提交网格，使用 `Water.shader` 绘制。`WaterCommon.hlsl` 负责组合：

- Gerstner 波浪位移和法线。
- 水深和浅水区域修饰。
- 细节法线。
- 屏幕空间折射。
- Fresnel 反射。
- 吸收、散射和主光照。
- 波峰泡沫、岸边泡沫和 WaterFX 泡沫。

`PlanarReflections.cs` 使用镜像相机生成平面反射贴图。`WaterSystemFeature.cs` 向 URP 添加两个辅助 Pass：

- WaterFX Pass：将带有 `WaterFX` Shader Pass 的粒子或网格编码成泡沫、法线扰动和位移纹理。
- Caustics Pass：根据场景深度和光照方向绘制水下焦散。

### 2. CPU 交互链路

`GerstnerWavesJobs.cs` 保存与 GPU 同源的波浪参数，并使用 Unity Jobs + Burst 批量计算采样点的波浪位移和法线。

`BuoyantObject.cs` 可以使用单点或体素采样：

- 非物理模式：直接调整物体高度和朝向。
- 物理模式：根据浸没程度施加浮力、阻尼和角阻尼。

`Engine.cs`、`BuoyManager.cs` 和 `WindsurferManager.cs` 都通过同一套波浪查询接口读取水面数据。

## 关键文件

### C# 运行时代码

- `Scripts/Water.cs`：水系统总控、初始化、全局 Shader 参数、水深捕获和水面网格提交。
- `Scripts/GerstnerWavesJobs.cs`：CPU 波浪采样和 Jobs 调度。
- `Scripts/BuoyantObject.cs`：浮力、体素采样和刚体交互。
- `Scripts/LocalToWorldJob.cs`：把局部采样点转换到世界空间。
- `Scripts/Rendering/PlanarReflections.cs`：镜像相机和平面反射。
- `Scripts/Rendering/WaterSystemFeature.cs`：URP 的 WaterFX 和焦散 Pass。
- `Scripts/MainCameraAlign.cs`：让水面相关对象跟随相机并进行位置量化。
- `Scripts/Data/WaterSurfaceData.cs`：波浪、吸收、散射和泡沫配置。
- `Scripts/Data/WaterSettingsData.cs`：反射和几何设置。
- `Scripts/Data/WaterResources.cs`：材质、网格和贴图资源引用。

### Shader/HLSL

- `Shaders/Water.shader`：默认水材质入口。
- `Shaders/WaterCommon.hlsl`：水面顶点和像素阶段的主体逻辑。
- `Shaders/GerstnerWaves.hlsl`：GPU Gerstner Wave。
- `Shaders/WaterInput.hlsl`：全局参数、纹理和采样器声明。
- `Shaders/WaterLighting.hlsl`：Fresnel、反射、软阴影和高光。
- `Shaders/CommonUtilities.hlsl`：噪声、映射和通用数学函数。
- `Shaders/WaterFXShader.shader`：水面特效数据编码。
- `Shaders/Caustics.shader`：焦散绘制。
- `Shaders/WaterTessellated.shader`、`WaterTessellation.hlsl`：曲面细分备用路径。
- `Shaders/InfiniteWater.shader`：实验性的无限水面路径，当前不是主要实现。

## 一帧中的主要数据流

1. `Water` 从 ScriptableObject 读取水面配置。
2. `Water` 生成或读取波浪数组。
3. `Water` 将波浪数组上传为 StructuredBuffer 或向量数组。
4. `Water` 捕获海底水深图，并准备颜色查找表。
5. URP 执行 WaterFX Pass，生成 `_WaterFXMap`。
6. `Water` 为当前相机提交水面网格。
7. `Water.shader` 在顶点阶段计算波浪和 WaterFX 位移。
8. `Water.shader` 在像素阶段读取场景深度、颜色、反射和水面贴图，合成最终水面颜色。
9. `GerstnerWavesJobs` 并行计算 CPU 采样点。
10. 浮力、船引擎、浮标和帆板读取采样结果。
11. URP 的 Caustics Pass 叠加水下焦散。

## 建议的复刻顺序

### 阶段一：静态水面

先建立一个 URP 场景和一块规则网格，实现基础水材质、透明排序、深度写入和相机跟随。

### 阶段二：GPU Gerstner Wave

实现单波和多波叠加，输出顶点位移与法线。先只做颜色和法线调试，不急着加入反射折射。

### 阶段三：水面着色

加入细节法线、Fresnel 反射、屏幕空间折射、吸收和散射。再接入水深图，处理浅水和岸边颜色。

### 阶段四：CPU 波浪查询

使用与 Shader 相同的波浪参数和公式，实现单点采样，再改为 Jobs + Burst 批量采样。验证 CPU 与 GPU 的波峰位置基本一致。

### 阶段五：浮力

先做单点浮力，再做多点或体素浮力；加入刚体阻尼、浸没比例和调试 Gizmo。

### 阶段六：水面辅助效果

实现平面反射、WaterFX 纹理和船尾泡沫。最后再加入焦散、性能优化和多平台兼容。

## 复刻时需要特别注意

- GPU 和 CPU 必须共享同一份波浪配置，否则视觉水面和物理水面会错位。
- WaterFX 只影响视觉水面，当前不会回写 CPU 浮力采样。
- 水深图和相机深度图用途不同：前者描述海底深度，后者描述当前视角下的场景深度。
- 平面反射会额外渲染一次场景，必须控制分辨率、反射层和阴影设置。
- 原项目使用全局 Shader 参数和单例 `Water.Instance`，复刻初期可以保留，后续再考虑改成多水域或显式数据上下文。
- 原包中有曲面细分、无限水面和旧 URP 兼容代码，学习时先忽略这些非主路径。

## 最小可行版本

如果只想快速验证核心理解，最小版本只需要：

- 一个 `Water` 控制器。
- 一个 `WaterSurfaceData`。
- 一个包含 4 到 6 个 Gerstner Wave 的水面 Shader。
- 一个 CPU 单点波浪采样器。
- 一个带 Rigidbody 的浮动物体。
- 一个基础平面反射或环境反射方案。

完成这个版本后，再逐步加入水深、折射、WaterFX、焦散和体素浮力。
