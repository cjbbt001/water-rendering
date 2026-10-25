# BoatAttack 环境资源

Unity 6000.6.2f1 / URP 17.6.0。

本工程保留了 BoatAttack 的岛屿环境、船只、建筑、植被、地形、贴图、材质和 Shader Graph，以及搭建好的场景 `Scenes/water.unity`。

当前**没有水系统实现**：没有水面脚本、水面 Shader/HLSL、波浪、浮力、平面反射、焦散或专用 Renderer Feature。后续水体由使用者从零编写。

## 目录

| 路径 | 内容 |
| --- | --- |
| `Scenes/water.unity` | 环境场景（岛屿、地形、植被、建筑、船、灯光、相机、天空盒） |
| `Objects/` | 模型、预制体、材质、贴图、地形图层 |
| `Objects/Shared/Shaders/` | 环境 Shader Graph 与通用 HLSL 光照函数 |
| `Shaders/Skybox-Procedural-Universal.shader` | 程序化天空盒 Shader |
| `Materials/ClearSky.mat`、`Sky.mat` | 天空盒材质 |
| `Textures/` | （已清理旧水系统专用贴图） |

URP 配置位于 `Assets/Settings/`，使用 `PC_RPAsset` / `PC_Renderer` 和 `Mobile_RPAsset` / `Mobile_Renderer`。

## 来源与许可

环境模型、贴图与材质来自 Unity BoatAttack，许可见同目录 `LICENSE.md`。

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
