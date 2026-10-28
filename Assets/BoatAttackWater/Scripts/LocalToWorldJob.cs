using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

// 坐标转换服务：把每个物体固定不变的局部采样点，转换为当前世界位置。
// 不计算波浪或浮力；结果数组与 BuoyantObject._samplePoints 共享底层内存。
public static class LocalToWorldJob
{
    // 每个物体 ID 对应一份局部点、世界点、任务和运行状态。
    private static readonly Dictionary<EntityId, TransformLocalToWorld> Data = new Dictionary<EntityId, TransformLocalToWorld>();

    // 一个 Job 转换一个物体的全部点；Burst 负责加速这里的数值运算。
    [BurstCompile]
    struct LocalToWorldConvertJob : IJob
    {
        [WriteOnly] public NativeArray<float3> PositionsWorld; // 输出：世界坐标。
        [ReadOnly] public Matrix4x4 Matrix; // 调度当刻的 localToWorldMatrix，包含平移、旋转和缩放。
        [ReadOnly] public NativeArray<float3> PositionsLocal; // 输入：固定在物体上的局部坐标。

        // 对每个点执行 世界位置 = localToWorldMatrix × float4(局部位置, 1)。
        public void Execute()
        {
            for (var i = 0; i < PositionsLocal.Length; i++)
            {
                var pos = float4.zero;
                pos.xyz = PositionsLocal[i];
                pos.w = 1f; // w=1 表示位置，矩阵中的平移会生效；方向向量通常用 w=0。
                pos = Matrix * pos;
                PositionsWorld[i] = pos.xyz;
            }
        }
    }

    // 注册物体：复制局部点，引用调用者已分配的世界坐标数组；此处不创建另一份 output。
    // output 是 NativeArray 句柄，赋值后仍指向同一块原生内存。
    public static void SetupJob(EntityId guid, Vector3[] positions, ref NativeArray<float3> output)
    {
        var jobData = new TransformLocalToWorld
        {
            PositionsWorld = output,
            PositionsLocal = new NativeArray<float3>(positions.Length, Allocator.Persistent)
        };
        
        for (var i = 0; i < positions.Length; i++)
            jobData.PositionsLocal[i] = positions[i];
        
        Data.Add(guid, jobData);
    }

    // 根据当前变换矩阵提交任务；上一任务未回收时直接返回，避免重复写共享数组。
    public static void ScheduleJob(EntityId guid, Matrix4x4 localToWorld)
    {
        if (Data[guid].Processing)
            return;
        
        Data[guid].Job = new LocalToWorldConvertJob()
        {
            PositionsWorld = Data[guid].PositionsWorld,
            PositionsLocal = Data[guid].PositionsLocal,
            Matrix = localToWorld
        };
        
        Data[guid].Handle = Data[guid].Job.Schedule();
        Data[guid].Processing = true;
        JobHandle.ScheduleBatchedJobs();
    }

    // 等待该物体坐标转换完成；读取世界采样点前调用，之后允许再次调度。
    public static void CompleteJob(EntityId guid)
    {
        Data[guid].Handle.Complete();
        Data[guid].Processing = false;
    }

    // 等待任务结束，释放局部与世界数组并注销物体；调用者不要再重复释放 output。
    public static void Cleanup(EntityId guid)
    {
        if (!Data.ContainsKey(guid)) return;
        Data[guid].Handle.Complete();
        Data[guid].PositionsWorld.Dispose();
        Data[guid].PositionsLocal.Dispose();
        Data.Remove(guid);
    }

    // 每个物体的一组运行数据；class 为引用类型，修改 Data[guid] 的字段会保留在注册表中。
    class TransformLocalToWorld
    {
        public NativeArray<float3> PositionsLocal; // 原始局部点。
        public NativeArray<float3> PositionsWorld; // 与浮力脚本共享的世界坐标输出。
        public JobHandle Handle; // 用于等待本物体任务完成。
        public LocalToWorldConvertJob Job; // 本次提交的输入、输出和矩阵。
        public bool Processing; // 是否有尚未 CompleteJob 的任务。
    }
}
