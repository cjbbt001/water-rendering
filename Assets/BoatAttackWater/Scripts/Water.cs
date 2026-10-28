using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Data;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace WaterSystem
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)] // 先于默认顺序的浮力组件准备波浪采样。
    public class Water : MonoBehaviour
    {
        // 单例入口；当前全局 Shader 参数按一个水面系统使用。
        public static Water Instance;

        // 配置与资源引用。
        [SerializeField]
        public WaterSettingsData settingsData;
        [SerializeField]
        public WaterSurfaceData surfaceData;
        [SerializeField]
        private WaterResources resources;

        // 波浪数据与 GPU 上传。
        public bool computeOverride;
        public bool customWaves; // 当前 SurfaceData 未提供 _customWaves，开关保留在主控上。
        [SerializeField]
        public Wave[] _waves;
        private bool _useComputeBuffer;
        private ComputeBuffer waveBuffer; // 运行时资源，不参与 Unity 序列化。
        private float _maxWaveHeight;
        private float _waveHeight;

        // 渐变查找纹理与水面细节纹理。
        private Texture2D _rampTexture;

        // 反射组件与环境贴图。
        private PlanarReflections _planarReflections;

        // 水深捕获与解码参数。
        [SerializeField] RenderTexture _depthTex;
        public Texture bakedDepthTex;
        private Camera _depthCam;
        private bool _needsDepthCapture;

        // Shader 参数 ID。
        private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
        private static readonly int WaveData = Shader.PropertyToID("waveData");
        private static readonly int AbsorptionScatteringRamp = Shader.PropertyToID("_AbsorptionScatteringRamp");
        private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        private static readonly int WaterDepthMap = Shader.PropertyToID("_WaterDepthMap");
        private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        private static readonly int DepthCamZParams = Shader.PropertyToID("_Water_DepthCamParams");

        private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
        private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");

        // 运行时查找场景中的水面组件，建立单例入口。
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInitializeOnLoad()
        {
            var found = FindObjectsByType<Water>();
            if (found.Length == 0) return;
            Debug.Assert(found.Length == 1); // Should be one and only one.
            Instance = found[0];

            // 兼容进入播放时没有重新触发 OnEnable 的情况，避免重复初始化。
            if (Instance.isActiveAndEnabled && !GerstnerWavesJobs.IsInitialized)
                Instance.Init();
        }

        // 选择波浪上传路径，初始化水面并订阅相机渲染事件。
        private void OnEnable()
        {
            Instance = this;
            // 不支持或主动禁用 Buffer 时，回退到 Vector4 数组上传。
            if (!computeOverride)
                _useComputeBuffer = SystemInfo.supportsComputeShaders &&
                                   Application.platform != RuntimePlatform.WebGLPlayer &&
                                   Application.platform != RuntimePlatform.Android;
            else
                _useComputeBuffer = false;
            Init();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }

        // 组件停用时清理临时资源和事件订阅。
        private void OnDisable()
        {
            Cleanup();
        }

        // 退出时完成并释放 CPU 采样；Cleanup 可重复调用，兼容随后发生的 OnDisable。
        private void OnApplicationQuit()
        {
            if (Instance == this) GerstnerWavesJobs.Cleanup();
        }

        // 等 URP 创建后再提交捕获请求，避免在 OnEnable 中过早渲染。
        private void Update()
        {
            if (_needsDepthCapture && RenderPipelineManager.currentPipeline != null)
                CaptureDepthMap();
        }

        // 取消回调并释放本组件创建的资源；允许反复启停。
        void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            if (Instance == this) GerstnerWavesJobs.Cleanup();
            if (_planarReflections) _planarReflections.enabled = false;
            if (_depthCam)
            {
                _depthCam.targetTexture = null;
                SafeDestroy(_depthCam.gameObject);
                _depthCam = null;
            }
            if (_depthTex)
            {
                _depthTex.Release();
                SafeDestroy(_depthTex);
                _depthTex = null;
            }

            waveBuffer?.Dispose();
            waveBuffer = null;
            if (_rampTexture) SafeDestroy(_rampTexture);
            _rampTexture = null;
            _needsDepthCapture = false;
            if (Instance == this) Instance = null;
        }

        // 每台相机渲染前更新视图参数，并绘制随相机移动的水面网格。
        private void BeginCameraRendering(ScriptableRenderContext src, Camera cam)
        {
            if (cam == _depthCam || cam.cameraType == CameraType.Preview) return;
            // 反射捕获期间不绘制水面；主动绘制也遵守相机的层遮罩。
            if (Shader.IsKeywordEnabled("_PLANAR_REFLECTION_CAMERA")) return;
            if ((cam.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (resources == null || resources.defaultWaterMeshes == null || resources.dafaultWaterMaterial == null) return;

            var roll = cam.transform.localEulerAngles.z;
            Shader.SetGlobalFloat(CameraRoll, roll);
            Shader.SetGlobalMatrix(InvViewProjection,
                (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            // 将水面放在相机前方，并按固定间距移动，减少网格连续滑动。
            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset;
            newPos.x = quantizeValue * (int)(newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int)(newPos.z / quantizeValue);

            var matrix = Matrix4x4.TRS(newPos + transform.position, Quaternion.identity, transform.localScale); // transform.localToWorldMatrix;

            foreach (var mesh in resources.defaultWaterMeshes)
            {
                Graphics.DrawMesh(mesh,
                    matrix,
                    resources.dafaultWaterMaterial,
                    gameObject.layer,
                    cam,
                    0,
                    null,
                    ShadowCastingMode.Off,
                    true,
                    null,
                    LightProbeUsage.Off);
            }
        }

        // 编辑模式立即销毁，运行模式延迟到帧末销毁。
        private static void SafeDestroy(Object o)
        {
            if (Application.isPlaying)
                Destroy(o);
            else
                DestroyImmediate(o);
        }

        // 初始化波浪、渐变纹理、反射和水深捕获状态。
        public void Init()
        {
            // 资源必须先于波浪和渐变初始化；优先使用 Inspector 指定的引用。
            if (resources == null)
                resources = Resources.Load<WaterResources>("WaterResources");
            if (resources == null || surfaceData == null || settingsData == null)
            {
                Debug.LogWarning("Water：请指定 Resources、Surface Data 和 Settings Data。", this);
                return;
            }

            SetWaves();
            // CPU 使用刚生成的同一份波浪；编辑模式仅准备 GPU 水面，不启动浮力采样。
            if (Application.IsPlaying(gameObject))
            {
                Instance = this;
                GerstnerWavesJobs.Init();
            }
            GenerateColorRamp();
            if (bakedDepthTex)
            {
                Shader.SetGlobalTexture(WaterDepthMap, bakedDepthTex);
            }

            // 保留原版平面反射组件，仅在选择该反射模式时启用。
            if (!gameObject.TryGetComponent(out _planarReflections))
            {
                _planarReflections = gameObject.AddComponent<PlanarReflections>();
            }
            _planarReflections.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            _planarReflections.m_settings = settingsData.planarSettings;
            _planarReflections.target = gameObject;
            _planarReflections.m_planeOffset = -0.25f; // 与上方绘制网格的 yOffset 一致。
            _planarReflections.enabled = settingsData.refType == ReflectionType.PlanarReflection;

            // 已有烘焙深度图时不重复捕获；保留原版 WebGL 限制。
            _needsDepthCapture = !bakedDepthTex && Application.platform != RuntimePlatform.WebGLPlayer;
        }

        // 浮力物体在 Update 提交采样点后，调度下一批 CPU 波浪计算。
        private void LateUpdate()
        {
            if (Application.IsPlaying(gameObject) && Instance == this)
                GerstnerWavesJobs.UpdateHeights();
        }

        // 原版保留接口；当前 Shader 未使用 GERSTNER_WAVES 关键词。
        public void FragWaveNormals(bool toggle)
        {
            var mat = GetComponent<Renderer>().sharedMaterial;
            if (toggle)
                mat.EnableKeyword("GERSTNER_WAVES");
            else
                mat.DisableKeyword("GERSTNER_WAVES");
        }

        // 汇总波浪、水位、纹理和反射模式，并选择 GPU 数据上传路径。
        private void SetWaves()
        {
            SetupWaves(customWaves);

            // set default resources
            Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);

            _maxWaveHeight = 0f;
            foreach (var w in _waves)
            {
                _maxWaveHeight += w.amplitude;
            }
            _maxWaveHeight = Mathf.Max(0.001f, _maxWaveHeight / Mathf.Max(1, _waves.Length));

            _waveHeight = transform.position.y;

            Shader.SetGlobalFloat(WaveHeight, _waveHeight);
            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, surfaceData._waterMaxVisibility);

            // 同步材质自身的关键词，避免旧的 Cubemap 选项覆盖当前反射模式。
            var material = resources.dafaultWaterMaterial;
            if (material != null)
            {
                material.DisableKeyword("_REFLECTION_CUBEMAP");
                material.DisableKeyword("_REFLECTION_PROBES");
                material.DisableKeyword("_REFLECTION_PLANARREFLECTION");
            }

            switch (settingsData.refType)
            {
                case ReflectionType.Cubemap:
                    Shader.EnableKeyword("_REFLECTION_CUBEMAP");
                    if (material != null) material.EnableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemap);
                    break;
                case ReflectionType.ReflectionProbe:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.EnableKeyword("_REFLECTION_PROBES");
                    if (material != null) material.EnableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                case ReflectionType.PlanarReflection:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.EnableKeyword("_REFLECTION_PLANARREFLECTION");
                    if (material != null) material.EnableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Shader.SetGlobalInt(WaveCount, _waves.Length);

            // 每条 Wave 为 6 个 float（24 字节），与 HLSL 的结构布局一致。
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer?.Dispose();
                waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
                if (_waves.Length > 0) waveBuffer.SetData(_waves);
                Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
            }
            else
            {
                Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
                Shader.SetGlobalVectorArray(WaveData, GetWaveData());
            }
        }

        // 数组路径：前 10 项存波浪参数，后 10 项存对应的全向波中心。
        private Vector4[] GetWaveData()
        {
            var waveData = new Vector4[20];
            for (var i = 0; i < _waves.Length; i++)
            {
                waveData[i] = new Vector4(_waves[i].amplitude, _waves[i].direction, _waves[i].wavelength, _waves[i].omniDir);
                waveData[i + 10] = new Vector4(_waves[i].origin.x, _waves[i].origin.y, 0, 0);
            }
            return waveData;
        }

        // 自动生成或读取自定义波浪；两条上传路径最多都容纳 10 条。
        private void SetupWaves(bool custom)
        {
            if (!custom)
            {
                // 固定种子保证生成可重复，结束后恢复 Unity 全局随机状态。
                var backupSeed = Random.state;
                Random.InitState(surfaceData.randomseed);
                var basicWaves = surfaceData._basicWaveSettings;
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = Mathf.Clamp(basicWaves.numWaves, 0, 10);
                _waves = new Wave[numWave];

                var r = 1f / Mathf.Max(1, numWave);

                for (var i = 0; i < numWave; i++)
                {
                    var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                    var amp = a * p * Random.Range(0.8f, 1.2f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.6f, 1.4f);
                    _waves[i] = new Wave(amp, dir, len, Vector2.zero, false);
                    Random.InitState(surfaceData.randomseed + i + 1);
                }
                Random.state = backupSeed;
            }
            else
            {
                _waves = surfaceData._waves?.ToArray() ?? Array.Empty<Wave>();
                if (_waves.Length > 10) Array.Resize(ref _waves, 10);
            }
        }

        // 生成 128×4 查找纹理：吸收、散射、泡沫；第 4 行保持原版留空。
        private void GenerateColorRamp()
        {
            if (_rampTexture == null)
                _rampTexture = new Texture2D(128, 4, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
            _rampTexture.wrapMode = TextureWrapMode.Clamp;

            var defaultFoamRamp = resources.defaultFoamRamp;

            var cols = new Color[512];
            for (var i = 0; i < 128; i++)
            {
                cols[i] = surfaceData._absorptionRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                cols[i + 128] = surfaceData._scatterRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                switch (surfaceData._foamSettings.foamType)
                {
                    case 0: // default
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(i / 128f, 0.5f);
                        break;
                    case 1: // simple
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(surfaceData._foamSettings.basicFoam.Evaluate(i / 128f), 0.5f);
                        break;
                    case 2: // custom
                        cols[i + 256] = Color.black;
                        break;
                }
            }
            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(AbsorptionScatteringRamp, _rampTexture);
        }

        // 岸边水深：用向下的正交相机捕获地形，供 Shader 计算竖直水深。
        [ContextMenu("Capture Depth")]
        public void CaptureDepthMap()
        {
            if (surfaceData == null) return;
            if (RenderPipelineManager.currentPipeline == null)
            {
                _needsDepthCapture = true;
                return;
            }
            // 深度相机仅由渲染请求驱动，不参与普通相机渲染。
            if (_depthCam == null)
            {
                var go =
                    new GameObject("depthCamera") { hideFlags = HideFlags.HideAndDontSave };
                _depthCam = go.AddComponent<Camera>();
            }

            var additionalCamData = _depthCam.GetUniversalAdditionalCameraData();
            additionalCamData.renderShadows = false;
            additionalCamData.requiresColorOption = CameraOverrideOption.Off;
            additionalCamData.requiresDepthOption = CameraOverrideOption.Off;

            var t = _depthCam.transform;
            var depthExtra = 4.0f;
            t.position = Vector3.up * (transform.position.y + depthExtra); // 水面上方 4 米。
            t.rotation = Quaternion.Euler(90f, 0f, 0f); // 朝下看，纹理方向与 HLSL 一致。

            _depthCam.enabled = false;
            _depthCam.orthographic = true;
            _depthCam.orthographicSize = 250; // 半宽 250 米，覆盖世界原点附近 500×500 米。
            _depthCam.aspect = 1f;
            _depthCam.nearClipPlane = 0.01f;
            _depthCam.farClipPlane = surfaceData._waterMaxVisibility + depthExtra;
            _depthCam.allowHDR = false;
            _depthCam.allowMSAA = false;
            _depthCam.cullingMask = (1 << 10); // 保留原版：仅捕获第 10 层地形。
            // 创建深度纹理。
            if (!_depthTex)
                _depthTex = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
            {
                _depthTex.filterMode = FilterMode.Point;
            }
            _depthTex.wrapMode = TextureWrapMode.Clamp;
            _depthTex.name = "WaterDepthMap";
            // URP 17 使用单相机渲染请求，替代内置管线的 Camera.Render()。
            if (!_depthTex.IsCreated()) _depthTex.Create();
            RenderPipeline.SubmitRenderRequest(_depthCam,
                new UniversalRenderPipeline.SingleCameraRequest { destination = _depthTex });
            _needsDepthCapture = false;
            Shader.SetGlobalTexture(WaterDepthMap, _depthTex);
            // x 为相机高度，y 为正交半宽，供水深解码和世界坐标映射使用。
            var _params = new Vector4(t.position.y, 250, 0, 0);
            //Vector4 zParams = new Vector4(1-f/n, f/n, (1-f/n)/f, (f/n)/f);//2015
            Shader.SetGlobalVector(DepthCamZParams, _params);

            /*            #if UNITY_EDITOR
                        Texture2D tex2D = new Texture2D(1024, 1024, TextureFormat.Alpha8, false);
                        Graphics.CopyTexture(_depthTex, tex2D);
                        byte[] image = tex2D.EncodeToPNG();
                        System.IO.File.WriteAllBytes(Application.dataPath + "/WaterDepth.png", image);
                        #endif*/

            _depthCam.enabled = false;
            _depthCam.targetTexture = null;
        }

        [Serializable]
        public enum DebugMode { none, stationary, screen };
    }
}
