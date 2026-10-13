# 简单水体渲染示例（Unity 6 / URP 17）

参考：[永雏塔菲《塔菲都看得懂的水体Shader渲染》](https://zhuanlan.zhihu.com/p/2081124056691105936)。这是按文章模块编写的简化学习示例，贴图全部由本项目生成。

## 使用

打开 `Assets/WaterExample/Scenes/SimpleWater.unity`，点击 Play 查看滚动水纹、焦散和泡沫。选中 Water Surface，修改 `Materials/Water.mat` 的参数。

菜单 `Water Example/Create or Rebuild Demo` 可重新生成贴图、材质和场景（覆盖本示例资源，先保存自己的修改）。`Open Demo` 打开场景；`Validate and Capture` 编译并将预览保存至 `Logs/WaterExample/preview.png`。

相机需使用 URP，并开启 Depth Texture 与 Opaque Texture；示例相机已显式开启。将 Shader 用在其他物体上时，需要带有法线、切线的水平水面 Mesh。

## 与文章对应

1. **深度**：场景线性眼空间深度减水面眼空间深度。它是沿视线的厚度，参数里的 meters 不是垂直水深。
2. **法线**：同一张法线图以两组 UV 滚动；先解码，再混合并转换到世界空间，避免直接乘编码 RGB 引起偏向。
3. **折射、水色**：采样 URP 不透明颜色，深度检查避免采到水面前方物体；用浅/深水色插值代替文章的 Ramp，指数吸收控制透底。
4. **焦散**：用逆视投影矩阵重建采样水底的位置，双层焦散取 min，再随水深衰减。这里是生成的光纹近似。
5. **反射、高光**：静态天空 Cubemap + 菲涅尔，独立叠加 Blinn-Phong 太阳高光。
6. **岸边白沫**：噪声和水深阈值比较，加抗锯齿与接触边缘淡出。

## 常用参数

| 参数 | 调整效果 |
| --- | --- |
| Shallow / Deep Color | 浅水和深水颜色 |
| Depth Color Range / Water Absorption | 变深速度与透底程度 |
| Normal Strength / First & Second Layer Speed | 波纹强度与运动方向 |
| Refraction Distortion | 水底扭曲程度 |
| Caustics Intensity | 水底光纹亮度 |
| Reflection Strength / Fresnel Power | 天空反光与角度变化 |
| Foam Width / Foam Contrast | 白沫范围与破碎程度 |

本例水面保持平面；没有顶点波浪、平面反射、SSR 或交互模拟。折射只包含不透明物体，Cubemap 只表示天空，不会实时反射岸边石头。相机适合在水面上方使用。
