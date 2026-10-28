// Buoyancy.cs
// by Alex Zhdankin
// Version 2.1
//
// https://discussions.unity.com/t/buoyancy-script/432786
//
// Terms of use: do whatever you like
//
// Further tweaks by Andre McGrail
//
//

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

namespace WaterSystem
{
    // 浮力使用者：准备物体上的采样点，查询 CPU 波浪，再跟随水面或对刚体施力。
    // 数据链：局部体素 → 世界坐标 → 波浪高度 → 浸水程度 → 浮力和阻尼。
    public class BuoyantObject : MonoBehaviour
    {
        public BuoyancyType _buoyancyType; // 直接跟随 / 物理浮力，以及是否使用多个体素采样点。
        public float density; // 物体密度，体素化时按 Rigidbody.mass / volume 计算。
        public float volume; // 排水体积的近似值；单点物理模式需要自行提供有效体积。
        public float voxelResolution = 0.51f; // 体素化时是网格步长、体积计算的边长；浸水判断中原版也直接拿它作中心下偏移。
        private Bounds _voxelBounds; // 体素网格覆盖的包围盒，供采样、重心和调试绘制使用。
        public Vector3 centerOfMass = Vector3.zero; // 局部重心偏移；单点模式也用它作为唯一采样点。
        public float waterLevelOffset = 0f; // 基础水位：CPU 返回波浪位移，调用者再加这个偏移得到水面高度。

        private const float Dampner = 0.005f; // 采样点速度阻尼系数，抑制持续上下振荡。
        private const float WaterDensity = 1000; // 水密度，约 1000 kg/m³；场景长度按米理解。

        private float _baseDrag; // 刚体原始线性阻尼，浸水后在此基础上增加。
        private float _baseAngularDrag; // 刚体原始角阻尼。
        private EntityId _guid; // Unity 对象标识，用作波浪和坐标转换注册表的键；不是 System.Guid。
        private float3 _localArchimedesForce; // 总排水浮力均分到每个采样点后的向上力，单位 N。

		private Vector3[] _voxels; // 固定在物体上的局部采样点，随物体一起平移和旋转。
        private NativeArray<float3> _samplePoints; // Job 输出的世界坐标，也是 CPU 波浪查询的输入。
        [NonSerialized] public float3[] Heights; // 名称虽为 Heights，实际存 float3 波浪位移；本脚本主要读取 y。
        private float3[] _normals; // CPU 波浪法线，非物理模式据此调整物体朝向。
        private float3[] _velocity; // 刚体每个采样点的世界速度，包含平移和旋转造成的速度。
        [SerializeField] Collider[] colliders; // 用碰撞体筛选物体内部体素，不使用渲染网格作判断。
        private Rigidbody _rb; // Unity 物理系统负责积分力、速度和旋转。
        private DebugDrawing[] _debugInfo; // 每点的水面高度、位置和力，供 Gizmos 查看。
        [NonSerialized] public float PercentSubmerged; // 平滑后的浸水指标，来自各点 k 的平均值，并非精确排水体积比。

        // 根据模式创建采样点和结果数组；物理模式还准备 Rigidbody 与每点浮力。
        // NonPhysicalVoxel 是原版预留分支，Update 中尚未实现多点跟随。
        [ContextMenu("Initialize")]
        private void Init()
        {
            _voxels = null;

            switch (_buoyancyType)
            {
                case BuoyancyType.NonPhysical:
                    SetupVoxels();
                    SetupData();
                    break;
                case BuoyancyType.NonPhysicalVoxel:
                    SetupColliders();
                    SetupVoxels();
                    SetupData();
                    break;
                case BuoyancyType.Physical:
                    SetupVoxels();
                    SetupData();
                    SetupPhysical();
                    break;
                case BuoyancyType.PhysicalVoxel:
                    SetupColliders();
                    SetupVoxels();
                    SetupData();
                    SetupPhysical();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 多点模式体素化碰撞体；单点模式只在 centerOfMass 放一个采样点。
        private void SetupVoxels()
        {
            if (_buoyancyType == BuoyancyType.NonPhysicalVoxel || _buoyancyType == BuoyancyType.PhysicalVoxel)
            {
                SliceIntoVoxels();
            }
            else
            {
                _voxels = new Vector3[1];
                _voxels[0] = centerOfMass;
            }
        }

        // 所有输入、结果与调试数组按同一个体素数量分配，索引 i 始终代表同一个点。
        // NativeArray 是可供 Job 使用的原生内存；Persistent 表示跨帧保留，须主动 Dispose。
        private void SetupData()
        {
            _debugInfo = new DebugDrawing[_voxels.Length];
            Heights = new float3[_voxels.Length];
            _normals = new float3[_voxels.Length];
            _samplePoints = new NativeArray<float3>(_voxels.Length, Allocator.Persistent);
        }

        // 获取物体标识，建立浮力数据，并提交第一批局部到世界坐标转换。
        private void OnEnable()
        {
            _guid = gameObject.GetEntityId();
            Init();
            LocalToWorldConversion();
        }

        // 收集自身及子物体碰撞体；完全没有碰撞体时添加 BoxCollider 并提示。
        private void SetupColliders()
        {
            // The object must have a Collider
            colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length != 0) return;
            
            colliders = new Collider[1];
            colliders[0] = gameObject.AddComponent<BoxCollider>();
            Debug.LogError($"Buoyancy:Object \"{name}\" had no coll. BoxCollider has been added.");
        }

        // 每帧非物理跟随，或更新刚体各点速度；随后提交采样位置、取回上一批波浪结果。
        // 注意当前顺序是先使用 Heights，再取新结果，因此存在批次/帧延迟。
        private void Update()
        {
            if (!GerstnerWavesJobs.IsInitialized) return; // 水面未启用或已释放时暂停查询与跟随。
#if STATIC_EVERYTHING
            var dt = 0.0f;
#else
            var dt = Time.deltaTime;
#endif
            switch (_buoyancyType)
            {
                case BuoyancyType.NonPhysical:
                {
                    var t = transform;
                    var vec  = t.position;
                    vec.y = Heights[0].y + waterLevelOffset; // 波浪竖直位移 + 基础水位。
                    t.position = vec;
                    t.up = Vector3.Slerp(t.up, _normals[0], dt); // 平滑靠近水面法线，不施加物理力。
                    break;
                }
                case BuoyancyType.NonPhysicalVoxel:
                    // do the voxel non-physical
                    break;
                case BuoyancyType.Physical:
                    LocalToWorldJob.CompleteJob(_guid);
                    GetVelocityPoints();
                    break;
                case BuoyancyType.PhysicalVoxel:
                    LocalToWorldJob.CompleteJob(_guid);
                    GetVelocityPoints();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // 两次查询必须保持这个顺序：UpdateSamplePoints 会先完成上一批波浪 Job。
            GerstnerWavesJobs.UpdateSamplePoints(ref _samplePoints, _guid);
            GerstnerWavesJobs.GetData(_guid, ref Heights, ref _normals);
        }

        // 固定物理步中根据已取得的水面高度，对浸水采样点施力；多点受力自然产生转矩。
        // 波浪查询在 Update 中进行，这里使用最近一次拿到的结果。
        private void FixedUpdate()
        {
            if (!GerstnerWavesJobs.IsInitialized) return; // 没有有效采样系统时不继续施加浮力。
            var submergedAmount = 0f; // 本次物理步累计各点 k / 点数，之后用于调整阻力。
            
            switch (_buoyancyType)
            {
                case BuoyancyType.PhysicalVoxel:
                {
                    LocalToWorldJob.CompleteJob(_guid);
                    //Debug.Log("new pass: " + gameObject.name);
                    Physics.autoSyncTransforms = false;

                    for (var i = 0; i < _voxels.Length; i++)
                        BuoyancyForce(_samplePoints[i], _velocity[i], Heights[i].y + waterLevelOffset, ref submergedAmount, ref _debugInfo[i]);
                    Physics.SyncTransforms();
                    Physics.autoSyncTransforms = true;
                    UpdateDrag(submergedAmount);
                    break;
                }
                case BuoyancyType.Physical:
                    // 保留原版单点分支：此处传的是世界原点，不能把它理解为物体重心。
                    //LocalToWorldJob.CompleteJob(_guid);
                    BuoyancyForce(Vector3.zero, _velocity[0], Heights[0].y + waterLevelOffset, ref submergedAmount, ref _debugInfo[0]);
                    //UpdateDrag(submergedAmount);
                    break;
                case BuoyancyType.NonPhysical:
                    break;
                case BuoyancyType.NonPhysicalVoxel:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 使用本帧最终 Transform 提交坐标转换，供后续采样和施力读取。
        private void LateUpdate() { LocalToWorldConversion(); }
        
        // 物体销毁时释放该物体持有或委托管理的 NativeArray。
        private void OnDestroy()
        {
            CleanUp();
        }

        // 物理模式由 LocalToWorldJob 释放共享的世界坐标数组；其余模式在这里释放。
        void CleanUp()
        {
            if (_buoyancyType == BuoyancyType.Physical || _buoyancyType == BuoyancyType.PhysicalVoxel)
            {
                LocalToWorldJob.Cleanup(_guid);
            }
            else
            {
                _samplePoints.Dispose();
            }
        }

        // 将物体当前 localToWorldMatrix 交给 Job，把局部体素转换为世界采样点。
        // 原版仅对两种物理模式执行；本轮保留这一条件。
        private void LocalToWorldConversion()
        {
            if (_buoyancyType != BuoyancyType.Physical && _buoyancyType != BuoyancyType.PhysicalVoxel) return;
            
            var transformMatrix = transform.localToWorldMatrix;
            LocalToWorldJob.ScheduleJob(_guid, transformMatrix);
        }

        // 单点浮力：输入世界位置、该点速度、水面高度；输出施加到刚体的力及浸水统计。
        // ref 参数会直接修改调用者传入的变量；debug 仅记录结果供观察，不参与物理计算。
        private void BuoyancyForce(Vector3 position, float3 velocity, float waterHeight, ref float submergedAmount, ref DebugDrawing debug)
        {
            debug.Position = position;
            debug.WaterHeight = waterHeight;
            debug.Force = Vector3.zero;

            if (!(position.y - voxelResolution < waterHeight)) return; // 点的下方还未入水，则本点不施力。
            
            // k 是原版将浸入深度限制到 0～1 的经验指标，没有按体素高度归一化。
            var k = math.clamp(waterHeight - (position.y - voxelResolution), 0f, 1f);

            submergedAmount += k / _voxels.Length;

            var localDampingForce = Dampner * _rb.mass * -velocity; // 反向于点速度，减小入水振荡。
            // sqrt(k) 调节当前点的浮力强度，是近似模型，不是严格的浸没体积积分。
            var force = localDampingForce + math.sqrt(k) * _localArchimedesForce;
            _rb.AddForceAtPosition(force, position); // 偏离重心施力会产生转矩，形成俯仰和侧倾。

            debug.Force = force; // For drawing force Gizmos
            //Debug.Log(string.Format("Position: {0:f1} -- Force: {1:f2} -- Height: {2:f2}\nVelocity: {3:f2} -- Damp: {4:f2} -- Mass: {5:f1} -- K: {6:f2}", wp, force, waterLevel, velocity, localDampingForce, RB.mass, localArchimedesForce));
        }

        // 平滑浸水指标，再增加刚体线性/角阻尼，近似水阻力；当前仅体素物理模式调用。
        private void UpdateDrag(float submergedAmount)
        {
            PercentSubmerged = math.lerp(PercentSubmerged, submergedAmount, 0.25f);
            _rb.linearDamping = _baseDrag + _baseDrag * (PercentSubmerged * 10f);
            _rb.angularDamping = _baseAngularDrag + PercentSubmerged * 0.5f;
        }

        // 查询每个世界采样点的刚体速度，用来计算该点的阻尼力。
        private void GetVelocityPoints()
        {
            for (var i = 0; i < _voxels.Length; i++) { _velocity[i] = _rb.GetPointVelocity(_samplePoints[i]); }
        }

        // 按网格遍历包围盒，只保留落在碰撞体内的中心点，并估算体积和密度。
        // 当前原版算法按特定坐标/缩放假设工作；不是任意碰撞体布局下的精确体积计算。
        private void SliceIntoVoxels()
        {
            _voxels = null;
            var points = new List<Vector3>();

            var rawBounds = VoxelBounds();
            _voxelBounds = rawBounds;
            _voxelBounds.size = RoundVector(rawBounds.size, voxelResolution);
            for (var ix = -_voxelBounds.extents.x; ix < _voxelBounds.extents.x; ix += voxelResolution)
            {
                for (var iy = -_voxelBounds.extents.y; iy < _voxelBounds.extents.y; iy += voxelResolution)
                {
                    for (var iz = -_voxelBounds.extents.z; iz < _voxelBounds.extents.z; iz += voxelResolution)
                    {
                        var x = (voxelResolution * 0.5f) + ix;
                        var y = (voxelResolution * 0.5f) + iy;
                        var z = (voxelResolution * 0.5f) + iz;

                        var p = new Vector3(x, y, z) + _voxelBounds.center;

                        var inside = false;
                        foreach (var t1 in colliders)
                        {
                            if (PointIsInsideCollider(t1, p))
                            {
                                inside = true;
                            }
                        }
                        if(inside)
                            points.Add(p);
					}
				}
            }

            _voxels = points.ToArray();
            var voxelVolume = Mathf.Pow(voxelResolution, 3f) * _voxels.Length; // 每格边长³ × 有效格数。
            var rawVolume = rawBounds.size.x * rawBounds.size.y * rawBounds.size.z;
            volume = Mathf.Min(rawVolume, voxelVolume);
            density = gameObject.GetComponent<Rigidbody>().mass / volume;
        }

        // 合并碰撞体尺寸得到体素遍历范围；原版把各 bounds.center 置零，未保留子物体中心偏移。
		private Bounds VoxelBounds()
		{
            var bounds = new Bounds();
            foreach (var nextCollider in colliders)
            {
                var b = nextCollider.bounds;
                b.center = Vector3.zero;
                bounds.Encapsulate(b);
            }
            return bounds;
		}

        // 各轴向上取整到网格步长的整数倍，使包围盒能铺满采样网格。
        private static Vector3 RoundVector(Vector3 vec, float rounding)
		{
            return new Vector3(Mathf.Ceil(vec.x / rounding) * rounding, Mathf.Ceil(vec.y / rounding) * rounding, Mathf.Ceil(vec.z / rounding) * rounding);
        }

        // 最近点与输入点几乎重合，就把该点视为位于碰撞体内部；保留原版传入的零位置/单位旋转。
        private bool PointIsInsideCollider(Collider c, Vector3 p)
        {
            var cp = Physics.ClosestPoint(p, c, Vector3.zero, Quaternion.identity);
			return Vector3.Distance(cp, p) < 0.01f;
        }

        // 缓存刚体、重心和原始阻尼；用 F=水密度×重力×体积算总浮力，再均分给体素。
        // 同时把局部采样点和世界坐标输出数组注册到 LocalToWorldJob。
        private void SetupPhysical()
        {
            if (!TryGetComponent(out _rb))
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                Debug.LogError($"Buoyancy:Object \"{name}\" had no Rigidbody. Rigidbody has been added.");
            }
            _rb.centerOfMass = centerOfMass + _voxelBounds.center;
            _baseDrag = _rb.linearDamping;
            _baseAngularDrag = _rb.angularDamping;
            
            _velocity = new float3[_voxels.Length];
            var archimedesForceMagnitude = WaterDensity * Mathf.Abs(Physics.gravity.y) * volume;
            _localArchimedesForce = new float3(0, archimedesForceMagnitude, 0) / _voxels.Length;
            LocalToWorldJob.SetupJob(_guid, _voxels, ref _samplePoints);
        }

        // 选中物体时绘制体素范围、重心、水面高度和受力方向，帮助观察采样与浮力。
        private void OnDrawGizmosSelected()
        {
			const float gizmoSize = 0.05f;
            var t = transform;
            var matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);

            if (_voxels != null)
            {
                Gizmos.color = Color.yellow;

                foreach (var p in _voxels)
                {
                    Gizmos.DrawCube(p, new Vector3(gizmoSize, gizmoSize, gizmoSize));
                }
            }

            Gizmos.matrix = matrix;
            if (voxelResolution >= 0.1f)
			{
                Gizmos.DrawWireCube(_voxelBounds.center, _voxelBounds.size);
                Vector3 center = _voxelBounds.center;
                float y = center.y - _voxelBounds.extents.y;
                for (float x = -_voxelBounds.extents.x; x < _voxelBounds.extents.x; x += voxelResolution)
				{
                    Gizmos.DrawLine(new Vector3(x, y, -_voxelBounds.extents.z + center.z), new Vector3(x, y, _voxelBounds.extents.z + center.z));
                }
				for (float z = -_voxelBounds.extents.z; z < _voxelBounds.extents.z; z += voxelResolution)
                {
					Gizmos.DrawLine(new Vector3(-_voxelBounds.extents.x, y, z + center.z), new Vector3(_voxelBounds.extents.x, y, z + center.z));
                }
            }
			else
                _voxelBounds = VoxelBounds();

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_voxelBounds.center + centerOfMass, 0.2f);

            Gizmos.matrix = Matrix4x4.identity;Gizmos.matrix = Matrix4x4.identity;

            if (_debugInfo != null)
            {
                foreach (DebugDrawing debug in _debugInfo)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(debug.Position, new Vector3(gizmoSize, gizmoSize, gizmoSize)); // drawCenter
                    var water = debug.Position;
                    water.y = debug.WaterHeight;
                    Gizmos.DrawLine(debug.Position, water); // draw the water line
                    Gizmos.DrawSphere(water, gizmoSize * 4f);
                    if(_buoyancyType == BuoyancyType.Physical || _buoyancyType == BuoyancyType.PhysicalVoxel)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawRay(debug.Position, debug.Force / _rb.mass); // draw force
                    }
                }
            }

        }

        // 一个采样点的调试快照：施力、世界位置与该点查询到的水面高度。
        private struct DebugDrawing
        {
            public Vector3 Force;
            public Vector3 Position;
            public float WaterHeight;
        }

        public enum BuoyancyType
        {
            NonPhysical, // 单点直接修改位置和朝向，不通过 Rigidbody 施力。
            NonPhysicalVoxel, // 原版预留的多点非物理分支，目前未实现跟随。
            Physical, // 原版单点刚体分支，按其现有实现保留。
            PhysicalVoxel // 多体素刚体浮力，每点查询水面并独立施力。
        }
    }
}
