# Water

Unity 6000.6.2f1 / URP 17.6.0 水体学习工程。打开 `Scenes/water.unity`，或使用菜单 `Water > Open Scene`。

## 代码入口

| 模块 | 入口 |
| --- | --- |
| 水面顶点与片元 | `Shaders/Water.shader`、`WaterCommon.hlsl` |
| Gerstner 波浪 | `Shaders/GerstnerWaves.hlsl` |
| 光照与反射 | `Shaders/WaterLighting.hlsl` |
| 参数上传、波浪、颜色表与深度捕获 | `Scripts/Water.cs` |
| 平面反射 | `Scripts/Rendering/PlanarReflections.cs` |
| 水底焦散 | `WaterCausticsFeature.cs`、`Shaders/Caustics.shader` |
| 地形高度捕获 | `WaterTerrainDepthFeature.cs`、`Shaders/SurfaceDepth.shader` |

## 调整与验证

- `WaterSurfaceData` 控制波浪、吸收、散射和泡沫。修改配置资产后，编辑器会刷新使用该配置的水面。
- `WaterSettingsData` 保留 PlanarReflection、Cubemap、ReflectionProbe 三种反射模式，当前默认使用平面反射。
- 水面组件的 `computeOverride` 勾选时使用 Vector 数组；取消勾选且设备支持时使用 ComputeBuffer。两条路径都支持最多 10 个波浪。
- `Materials/Sea.mat` 的 `Static` 冻结水面动画；`Debug mode` 可查看散射、折射、反射、法线、菲涅尔、泡沫和水深。
- 修改地形后，使用水面组件的 `Capture Depth` 或 `Refresh Water Parameters and Depth`。`depthLayers` 为空时深度图保持清空值；需要浅水波浪衰减时，指定要捕获的地形层，排除水面。
- 菜单 `Water > Validate and Capture` 检查当前水体场景的脚本、Shader 和深度纹理，输出整体及分层截图到 `Logs/Water`。验证会恢复材质关键字，不保存场景或配置。
- 菜单 `Water > Validate Scene Models` 检查 `Objects` 中的预制体、材质和网格，报告输出到 `Logs/Water/objects-validation.txt`。
- 本工程不再提供重建场景功能。修改后的场景、共享配置和网格由使用者自行维护。

`Objects` 保留场景模型、材质、贴图和 `milkfrog` 资源。水体不包含交互、浮力、尾迹或粒子系统。原版源码请在本机 `E:\Project\Unity\BoatAttack` 中查看，本工程不再存放 `Reference` 副本。

## 来源与许可

水体代码和相关场景素材基于 Unity BoatAttack 的 `Packages/com.verasl.water-system` 与测试场景提取、适配，许可见同目录 `LICENSE.md`。本工程保留原波浪、水色、散射、泡沫等主要算法；Unity 6 适配采用 RenderGraph 焦散、显式地形网格高度捕获与 `SubmitRenderRequest` 平面反射。

噪声辅助函数沿用原源码标注的 The Book of Shaders 和 Morgan McGuire 示例：<https://www.shadertoy.com/view/4dS3Wd>。

`Skybox-Procedural-Universal.shader` 原始声明为：Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license。

MIT License（上述天空 Shader）：

Copyright (c) 2016 Unity Technologies

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
