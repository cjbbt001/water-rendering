using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WaterSystem.Data;

namespace WaterSystem
{
    /// <summary>
    /// CPU 波浪查询：接收世界采样点，用共享的 Gerstner 波浪参数计算位移和法线。
    /// 不读取 GPU 网格，也不施加浮力；浮力由 BuoyantObject 根据查询结果计算。
    /// </summary>
    public class GerstnerWavesJobs
    {
        // static 数据由所有浮力物体共用，一次批处理多个物体的采样点。
        private static bool _firstFrame = true; // 还没有提交过第一批波浪 Job。
        private static bool _processing; // 已提交的批次尚未通过 CompleteJobs 回收。
        private static int _waveCount; // 初始化时从 Water 复制的波浪数量。
        private static NativeArray<Wave> _waveData; // CPU 波浪快照；Water 后续改参不会自动更新此数组。

        // 输入和输出使用相同索引，每个索引对应一个世界采样点。
        private static NativeArray<float3> _positions; // 输入：所有物体提交的世界坐标。
        private static int _positionCount; // 已注册的点数；数组容量为 4096，调度时仅计算已注册部分。
        private static NativeArray<float3> _wavePos; // 输出：波浪相对位移，不包含基础水位和输入点坐标。
        private static NativeArray<float3> _waveNormal; // 输出：与位移对应的单位法线。
        private static JobHandle _waterHeightHandle; // 代表一批波浪任务，用 Complete 等待完成。
        // 物体 ID → [起点, 终点)；例如 (8, 12) 表示使用索引 8～11 的四个点。
        static readonly Dictionary<EntityId, int2> Registry = new();

        // 供调度者和浮力物体判断输入/输出数组是否已经准备好。
        public static bool IsInitialized => _waveData.IsCreated && _positions.IsCreated;

        // 在 Water 已生成 _waves 后调用：复制波浪参数，分配跨帧保留的输入/输出缓冲区。
        // 重复初始化时先回收旧任务和数组，支持运行中重新调用 Water.Init。
        public static void Init()
        {
            Cleanup();
            _waterHeightHandle.Complete();
            _positionCount = 0;
            _waveCount = 0;
            _firstFrame = true;
            _processing = false;
            Registry.Clear();

            if (Debug.isDebugBuild)
                Debug.Log("Initializing Gerstner Waves Jobs");
            // NativeArray 让 Job/Burst 直接访问连续原生内存；Persistent 数组需显式 Dispose。
            _waveCount = Water.Instance._waves.Length;
            _waveData = new NativeArray<Wave>(_waveCount, Allocator.Persistent);
            for (var i = 0; i < _waveData.Length; i++)
            {
                _waveData[i] = Water.Instance._waves[i];
            }

            _positions = new NativeArray<float3>(4096, Allocator.Persistent);
            _wavePos = new NativeArray<float3>(4096, Allocator.Persistent);
            _waveNormal = new NativeArray<float3>(4096, Allocator.Persistent);
        }

        // 先完成正在使用数组的 Job，再释放波浪、采样点和结果数组；须与 Init 配对。
        public static void Cleanup()
        {
            if (Debug.isDebugBuild)
                Debug.Log("Cleaning up Gerstner Wave Jobs");
            _waterHeightHandle.Complete();

            //Cleanup native arrays
            if (_waveData.IsCreated) _waveData.Dispose();
            if (_positions.IsCreated) _positions.Dispose();
            if (_wavePos.IsCreated) _wavePos.Dispose();
            if (_waveNormal.IsCreated) _waveNormal.Dispose();
            Registry.Clear();
            _positionCount = 0;
            _processing = false;
            _firstFrame = true;
        }

        // 提交某物体的世界采样点；先完成上一批任务，避免主线程写入 Job 正在读取的数组。
        // 已注册时复制坐标；原版首次调用只分配区间，下一次调用才真正复制，保留此行为。
        public static void UpdateSamplePoints(ref NativeArray<float3> samplePoints, EntityId guid)
        {
            if (!IsInitialized) return;
            CompleteJobs();

            if (Registry.TryGetValue(guid, out var offsets))
            {
                for (var i = offsets.x; i < offsets.y; i++) _positions[i] = samplePoints[i - offsets.x];
            }
            else
            {
                if (_positionCount + samplePoints.Length >= _positions.Length) return;

                offsets = new int2(_positionCount, _positionCount + samplePoints.Length);
                Registry.Add(guid, offsets);
                _positionCount += samplePoints.Length;
            }
        }

        // 按物体 ID 将本物体区间的位移/法线复制回普通 C# 数组；outNorm 可为 null。
        // 本函数不自行等待 Job；调用方应先调用 UpdateSamplePoints，保证上一批结果可读。
        public static void GetData(EntityId guid, ref float3[] outPos, ref float3[] outNorm)
        {
            if (!Registry.TryGetValue(guid, out var offsets)) return;

            _wavePos.Slice(offsets.x, offsets.y - offsets.x).CopyTo(outPos);
            if (outNorm != null)
                _waveNormal.Slice(offsets.x, offsets.y - offsets.x).CopyTo(outNorm);
        }

        // 由 Water.LateUpdate 调度：输入波浪、时间和采样点，提交下一批计算。
        // 提交后不立即等待，后续由 CompleteJobs 同步；结果因此可能落后一批。
        public static void UpdateHeights()
        {
            if (!IsInitialized || _processing) return;

            _processing = true;

#if STATIC_EVERYTHING
            var t = 0.0f; // CPU 编译宏冻结时间，与材质的 _STATIC_SHADER 不是同一个开关。
#else
            var t = Time.time;
#endif

            // Buoyant Object Job
            var waterHeight = new HeightJob()
            {
                WaveData = _waveData,
                Position = _positions,
                OffsetLength = new int2(0, _positions.Length),
                Time = t,
                OutPosition = _wavePos,
                OutNormal = _waveNormal
            };

            _waterHeightHandle = waterHeight.Schedule(_positionCount, 32); // 点数为总迭代次数，32 为批大小参数。

            JobHandle.ScheduleBatchedJobs();

            _firstFrame = false;
        }

        // 将上一批 Job 完成并同步回主线程，之后可安全读结果、写下一批输入。
        private static void CompleteJobs()
        {
            if (_firstFrame || !_processing) return;

            _waterHeightHandle.Complete();
            _processing = false;
        }

        // 每个 Execute(i) 独立处理一个点，可并行；Burst 将数值计算编译为本机代码。
        // Job 内只读写数组和数值，不访问 Transform/Rigidbody，也不调用 Unity 场景 API。
        [BurstCompile]
        private struct HeightJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Wave> WaveData; // 输入：振幅、方向、波长、全向标记和中心位置。
            [ReadOnly]
            public NativeArray<float3> Position; // 输入：采样点世界坐标，公式主要使用 xz。

            [WriteOnly]
            public NativeArray<float3> OutPosition; // 输出：各波累加后的相对位移。
            [WriteOnly]
            public NativeArray<float3> OutNormal; // 输出：归一化法线。

            [ReadOnly]
            public float Time; // 调度当刻的时间快照，所有采样点使用相同时间。
            [ReadOnly]
            public int2 OffsetLength; // 原版处理区间参数，当前调度始终传入 (0, 数组容量)。

            // 对第 i 个点遍历所有波浪：投影传播方向 → 算相位 → 累加位移和法线。
            // 这里不包含 GPU 浅水衰减、WaterFX 或细节法线；保留原版公式和边界行为。
            public void Execute(int i)
            {
                if (i < OffsetLength.x || i >= OffsetLength.y - OffsetLength.x) return;

                var waveCountMulti = 1f / WaveData.Length; // 波数倒数，用于原版叠加权重。
                var wavePos = new float3(0f, 0f, 0f);
                var waveNorm = new float3(0f, 0f, 0f);

                for (var wave = 0; wave < WaveData.Length; wave++) // for each wave
                {
                    // Wave data vars
                    var pos = Position[i].xz;

                    var amplitude = WaveData[wave].amplitude;
                    var direction = WaveData[wave].direction;
                    var wavelength = WaveData[wave].wavelength;
                    var omniPos = WaveData[wave].origin;
                    ////////////////////////////////wave value calculations//////////////////////////
                    var w = 6.28318f / wavelength; // 波数 k=2π/波长，控制空间周期。
                    var wSpeed = math.sqrt(9.8f * w); // 角频率 ω=√(gk)，控制时间相位；不是线速度。
                    const float peak = 0.8f; // 波峰陡峭度参数。
                    var qi = peak / (amplitude * w * WaveData.Length);

                    var windDir = new float2(0f, 0f);

                    direction = math.radians(direction); // 配置中的角度转为三角函数使用的弧度。
                    var windDirInput = new float2(math.sin(direction), math.cos(direction)) * (1 - WaveData[wave].omniDir); // omniDir=0 时使用固定方向。
                    var windOmniInput = (pos - omniPos) * WaveData[wave].omniDir; // omniDir=1 时使用中心向外的径向方向。

                    windDir += windDirInput;
                    windDir += windOmniInput;
                    windDir = math.normalize(windDir);
                    var dir = math.dot(windDir, pos - (omniPos * WaveData[wave].omniDir)); // 点沿传播方向的投影距离。

                    ////////////////////////////position output calculations/////////////////////////
                    var calc = dir * w + -Time * wSpeed; // 相位 = 空间距离×波数 - 时间×角频率。
                    var cosCalc = math.cos(calc); // cosine version(used for horizontal undulation)
                    var sinCalc = math.sin(calc); // sin version(used for vertical undulation)

                    // calculate the offsets for the current point
                    wavePos.x += qi * amplitude * windDir.x * cosCalc;
                    wavePos.z += qi * amplitude * windDir.y * cosCalc;
                    wavePos.y += sinCalc * amplitude * waveCountMulti; // the height is divided by the number of waves 

                    ////////////////////////////normal output calculations/////////////////////////
                    var wa = w * amplitude;
                    // normal vector
                    var norm = new float3(-(windDir.xy * wa * cosCalc),
                        1 - (qi * wa * sinCalc));
                    waveNorm += (norm * waveCountMulti) * amplitude;
                }
                // 每个任务只写自己索引，多个点的并行计算不会争写同一个结果。
                OutPosition[i] = wavePos;
                OutNormal[i] = math.normalize(waveNorm.xzy);
            }
        }
    }
}
