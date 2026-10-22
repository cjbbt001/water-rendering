# 水体渲染：保留的 12 篇

Zotero 位置：Shader → 水体渲染。核验日期：2026-10-14。

已将原导入清单中的其余 12 篇移入回收站；保留原有其他条目。12 篇精选均有本地附件，共 10 个 PDF、1 个 PPTX、2 个网页快照。

建议先读：GPU Gems → Alex Tardif → Portal 2 → Uncharted；再读 Tessendorf → Ocean Lighting → Whitecaps；模拟专题看 Wave Particles、Chentanez、Photon，工程体系看 Crest、Far Cry 5。

| 文献 / 讲义 | 年份与来源 | 本地附件 |
|---|---|---|
| [Effective Water Simulation from Physical Models](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) | 2004 · GPU Gems, Chapter 1 | 网页快照（53 张内嵌图片） |
| [Simulating Ocean Water](https://people.computing.clemson.edu/~jtessen/reports/papers_files/coursenotes2004.pdf) | 2001 · ACM SIGGRAPH Course Notes | PDF 26 页 |
| [Real-time Realistic Ocean Lighting using Seamless Transitions from Geometry to BRDF](https://doi.org/10.1111/j.1467-8659.2009.01618.x) | 2010 · Computer Graphics Forum / Eurographics 2010 | PDF 10 页 |
| [Real-time animation and rendering of ocean whitecaps](https://doi.org/10.1145/2407746.2407761) | 2012 · SIGGRAPH Asia 2012 Technical Briefs | PDF 4 页 |
| [Wave Particles](https://www.cemyuksel.com/research/waveparticles/) | 2007 · ACM Transactions on Graphics / SIGGRAPH 2007 | PDF 8 页；PDF 59 页 |
| [Real-time Simulation of Large Bodies of Water with Small Scale Details](https://matthias-research.github.io/pages/publications/hfFluid.pdf) | 2010 · Eurographics / ACM SIGGRAPH Symposium on Computer Animation 2010 | PDF 11 页 |
| [Water Flow in Portal 2](https://cdn.fastly.steamstatic.com/apps/valve/2010/siggraph2010_vlachos_waterflow.pdf) | 2010 · SIGGRAPH 2010 / Valve | PDF 54 页 |
| [Crest: Novel Ocean Rendering Techniques in an Open Source Framework](https://advances.realtimerendering.com/s2017/) | 2017 · SIGGRAPH 2017 / Studio Gobo | PPTX 103 页 |
| [The Challenges of Rendering an Open World in Far Cry 5](https://advances.realtimerendering.com/s2018/) | 2018 · SIGGRAPH 2018 / Ubisoft | PDF 195 页 |
| [Water Technology of Uncharted](https://gdcvault.com/play/1015309/Water-Technology-of) | 2012 · GDC 2012 / Naughty Dog | PDF 156 页 |
| [Advanced Graphics Summit: Open-World Water Rendering and Real-Time Simulation](https://gdcvault.com/play/1028915/Advanced-Graphics-Summit-Open-World) | 2023 · GDC 2023 / LIGHTSPEED STUDIOS | PDF 104 页 |
| [Water Walkthrough](https://alextardif.com/Water.html) |  · Alex Tardif technical blog | 网页快照（17 张内嵌图片） |

Crest 官方 PPTX 约 455 MB，已完整保存并通过 ZIP CRC 校验。网页快照含完整正文、代码与静态图片；嵌入视频保留在线链接。

## Effective Water Simulation from Physical Models

Gerstner / 正弦波叠加、解析切线与法线、几何波和动态法线贴图的分工。波浪实现的经典基础。

## Simulating Ocean Water

FFT 海洋的经典讲义：统计波谱、频域时间演化、位移、法线和尖锐波。

## Real-time Realistic Ocean Lighting using Seamless Transitions from Geometry to BRDF

将随距离消失的几何细节转入法线与 BRDF，解决海面远景闪烁和尺度过渡。

## Real-time animation and rendering of ocean whitecaps

FFT 海洋白浪与泡沫的实时生成和渲染。适合补齐海面白浪。

## Wave Particles

波粒子传播、边界反射和物体双向交互；附原作者预印本和 SIGGRAPH 实现幻灯片。

## Real-time Simulation of Large Bodies of Water with Small Scale Details

浅水高度场结合粒子：大范围河流、瀑布、浪花和泡沫，兼顾水平流动与小尺度细节。

## Water Flow in Portal 2

Flow Map 驱动法线 / 颜色流动，交错相位混合抑制拉伸；包括美术制作流程。

## Crest: Novel Ocean Rendering Techniques in an Open Source Framework

Unity 海洋架构：连续 LOD、位移 RenderTexture、Gerstner、波动方程和反馈泡沫。

## The Challenges of Rendering an Open World in Far Cry 5

水体系统及物理天空 / 昼夜光照的集成；附包含演讲备注的 195 页 PDF。演讲还包含非水体内容。

## Water Technology of Uncharted

神秘海域水体系统、网格生成、流动 Shader，以及 Uncharted 3 游轮场景的海洋 LOD 和波浪。

## Advanced Graphics Summit: Open-World Water Rendering and Real-Time Simulation

Photon Water System：统一高度 / 速度 / 泡沫数据，离线预计算与实时更新、自适应网格和 CDLOD。

## Water Walkthrough

完整 HLSL 水体案例：网格、Gerstner、细分、法线、高光、SSR、折射和泡沫。作者明确采用艺术化近似。
