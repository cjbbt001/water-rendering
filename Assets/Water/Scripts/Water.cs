using UnityEngine.Experimental.Rendering;
using Random = UnityEngine.Random;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Data;
using Object = UnityEngine.Object;

namespace WaterSystem
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Water : MonoBehaviour
    {
        public WaterSettingsData settingsData;
        public WaterSurfaceData surfaceData;
        public WaterResources resources;
        [Tooltip("Use a Vector array instead of a ComputeBuffer for wave data.")]
        public bool computeOverride = true;
        public Wave[] _waves;
        public LayerMask depthLayers = 1 << 10;
        public int depthRendererIndex = 1;
        public float depthAreaSize = 100;
        public int depthResolution = 1024;

        bool _useComputeBuffer, _refresh = true, _capturing;
        float _maxWaveHeight;
        ComputeBuffer waveBuffer;
        Texture2D _rampTexture;
        RenderTexture _depthTex;
        Camera _depthCam;
        PlanarReflections _planarReflections;
        public RenderTexture TerrainDepthTexture => _depthTex;

        static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
        static readonly int WaveData = Shader.PropertyToID("waveData");
        static readonly int AbsorptionScatteringRamp = Shader.PropertyToID("_AbsorptionScatteringRamp");

        void OnEnable() { _refresh = true; }
        void OnValidate() { _refresh = true; }
        void Update()
        {
            if (_refresh && settingsData && surfaceData && resources)
            {
                Init();
                CaptureDepthMap();
                _refresh = false;
            }
        }

        [ContextMenu("Refresh Water Parameters and Depth")]
        public void RefreshSurface() { _refresh = true; }

        public void Init()
        {
            if (!settingsData || !surfaceData || !resources || !resources.defaultSeaMaterial) return;
            depthAreaSize = Mathf.Max(0.1f, depthAreaSize);
            depthResolution = Mathf.Clamp(depthResolution, 1, SystemInfo.maxTextureSize);
            _useComputeBuffer = !computeOverride && SystemInfo.supportsComputeShaders;
            SetWaves();
            GenerateColorRamp();
            if (!_planarReflections) _planarReflections = GetComponent<PlanarReflections>();
            if (!_planarReflections) _planarReflections = gameObject.AddComponent<PlanarReflections>();
            _planarReflections.target = gameObject;
            _planarReflections.m_settings = settingsData.planarSettings ?? new PlanarReflections.PlanarReflectionSettings();
            _planarReflections.enabled = settingsData.refType == ReflectionType.PlanarReflection;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = resources.defaultSeaMaterial;
            foreach (string keyword in new[] { "_REFLECTION_CUBEMAP", "_REFLECTION_PROBES", "_REFLECTION_PLANARREFLECTION" })
                renderer.sharedMaterial.DisableKeyword(keyword);
            renderer.sharedMaterial.EnableKeyword(settingsData.refType == ReflectionType.Cubemap ? "_REFLECTION_CUBEMAP" :
                settingsData.refType == ReflectionType.ReflectionProbe ? "_REFLECTION_PROBES" : "_REFLECTION_PLANARREFLECTION");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        [ContextMenu("Capture Depth")]
        public void CaptureDepthMap()
        {
            if (_capturing || !surfaceData) return;
            depthAreaSize = Mathf.Max(0.1f, depthAreaSize);
            depthResolution = Mathf.Clamp(depthResolution, 1, SystemInfo.maxTextureSize);
            Shader.SetGlobalVector("_SurfaceDepthBounds", new Vector4(transform.position.x,
                transform.position.z, 1f / depthAreaSize, 1f / depthAreaSize));
            _capturing = true;
            try
            {
                if (!_depthCam)
                {
                    var go = new GameObject("Surface Depth Camera") { hideFlags = HideFlags.HideAndDontSave };
                    _depthCam = go.AddComponent<Camera>();
                    _depthCam.enabled = false;
                }
                const float extra = 4;
                float far = Mathf.Max(0.1f, surfaceData._waterMaxVisibility) + extra;
                _depthCam.transform.SetPositionAndRotation(
                    new Vector3(transform.position.x, transform.position.y + extra, transform.position.z),
                    Quaternion.Euler(90, 0, 0));
                _depthCam.orthographic = true;
                _depthCam.orthographicSize = depthAreaSize * 0.5f;
                _depthCam.aspect = 1;
                _depthCam.nearClipPlane = 0.01f;
                _depthCam.farClipPlane = far;
                _depthCam.clearFlags = CameraClearFlags.SolidColor;
                _depthCam.backgroundColor = Color.black;
                _depthCam.cullingMask = depthLayers;
                _depthCam.allowHDR = false;
                _depthCam.allowMSAA = false;
                var data = _depthCam.GetUniversalAdditionalCameraData();
                data.SetRenderer(depthRendererIndex);
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
                if (!_depthTex || _depthTex.width != depthResolution)
                {
                    if (_depthTex) { _depthTex.Release(); SafeDestroy(_depthTex); }
                    _depthTex = new RenderTexture(depthResolution, depthResolution, 24, RenderTextureFormat.RFloat,
                        RenderTextureReadWrite.Linear) { name = "Surface Terrain Depth", wrapMode = TextureWrapMode.Clamp };
                    _depthTex.Create();
                }
                Shader.SetGlobalVector("_SurfaceDepthEncode", new Vector4(transform.position.y + extra, 1f / far, 0, 0));
                RenderPipeline.SubmitRenderRequest(_depthCam,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = _depthTex });
                Shader.SetGlobalTexture("_WaterDepthMap", _depthTex);

                Shader.SetGlobalVector("_WaterDepthCamParams", new Vector4(_depthCam.transform.position.y, far, 0, 0));
            }
            finally { _capturing = false; }
        }

        void OnDisable()
        {
            waveBuffer?.Dispose(); waveBuffer = null;
            if (_planarReflections) _planarReflections.enabled = false;
            if (_depthCam) SafeDestroy(_depthCam.gameObject);
            _depthCam = null;
            if (_depthTex) { _depthTex.Release(); SafeDestroy(_depthTex); }
            _depthTex = null;
            if (_rampTexture) SafeDestroy(_rampTexture);
            _rampTexture = null;
        }

        static void SafeDestroy(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void SetWaves()
        {
            SetupWaves(surfaceData._customWaves);

            Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);

            _maxWaveHeight = 0f;
            foreach (var w in _waves)
            {
                _maxWaveHeight += w.amplitude;
            }
            _maxWaveHeight = Mathf.Max(0.0001f, _maxWaveHeight / _waves.Length);

            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, Mathf.Max(0.1f, surfaceData._waterMaxVisibility));

            Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);

            Shader.SetGlobalInt(WaveCount, _waves.Length);

            waveBuffer?.Dispose();
            waveBuffer = null;
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer = new ComputeBuffer(_waves.Length, (sizeof(float) * 6));
                waveBuffer.SetData(_waves);
                Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
            }
            else
            {
                Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
                Shader.SetGlobalVectorArray(WaveData, GetWaveData());
            }
        }

        private Vector4[] GetWaveData()
        {
            var waveData = new Vector4[20];
            for (var i = 0; i < _waves.Length; i++)
            {
                waveData[i] = new Vector4(_waves[i].amplitude, _waves[i].direction, _waves[i].wavelength, _waves[i].onmiDir);
                waveData[i+10] = new Vector4(_waves[i].origin.x, _waves[i].origin.y, 0, 0);
            }
            return waveData;
        }

        private void SetupWaves(bool custom)
        {
            if (!custom || surfaceData._waves == null || surfaceData._waves.Count == 0)
            {
                var backupSeed = Random.state;
                Random.InitState(surfaceData.randomSeed);
                var basicWaves = surfaceData._basicWaveSettings ?? new BasicWaves(1.5f, 45f, 5f);
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = Mathf.Clamp(basicWaves.numWaves, 1, 10);
                _waves = new Wave[numWave];

                var r = 1f / numWave;

                for (var i = 0; i < numWave; i++)
                {
                    var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                    var amp = a * p * Random.Range(0.8f, 1.2f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.6f, 1.4f);
                    _waves[i] = new Wave(amp, dir, len, Vector2.zero, false);
                    Random.InitState(surfaceData.randomSeed + i + 1);
                }
                Random.state = backupSeed;
            }
            else
            {
                _waves = surfaceData._waves.GetRange(0, Mathf.Min(10, surfaceData._waves.Count)).ToArray();
            }
            for (int i = 0; i < _waves.Length; i++)
            {
                _waves[i].amplitude = Mathf.Max(0f, _waves[i].amplitude);
                _waves[i].wavelength = Mathf.Max(0.01f, _waves[i].wavelength);
            }
        }

        private void GenerateColorRamp()
        {
            if(_rampTexture == null)
                _rampTexture = new Texture2D(128, 4, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
            _rampTexture.wrapMode = TextureWrapMode.Clamp;

            var defaultFoamRamp = resources.defaultFoamRamp;
            var foamSettings = surfaceData._foamSettings ?? new FoamSettings();
            var absorptionRamp = surfaceData._absorptionRamp ?? new Gradient();
            var scatterRamp = surfaceData._scatterRamp ?? new Gradient();
            if (foamSettings.basicFoam == null || foamSettings.denseFoam == null ||
                foamSettings.mediumFoam == null || foamSettings.liteFoam == null)
                foamSettings = new FoamSettings();

            var cols = new Color[512];
            for (var i = 0; i < 128; i++)
            {
                cols[i] = absorptionRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                cols[i + 128] = scatterRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                switch(foamSettings.foamType)
                {
                    case 0:
                        cols[i + 256] = (defaultFoamRamp ? defaultFoamRamp.GetPixelBilinear(i / 128f, 0.5f) : Color.white);
                        break;
                    case 1:
                        cols[i + 256] = (defaultFoamRamp ? defaultFoamRamp.GetPixelBilinear(foamSettings.basicFoam.Evaluate(i / 128f), 0.5f) : Color.white);
                        break;
                    case 2:
                        cols[i + 256] = new Color(foamSettings.denseFoam.Evaluate(i / 128f), foamSettings.mediumFoam.Evaluate(i / 128f), foamSettings.liteFoam.Evaluate(i / 128f));
                        break;
                }
            }
            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(AbsorptionScatteringRamp, _rampTexture);
        }
    }
}
