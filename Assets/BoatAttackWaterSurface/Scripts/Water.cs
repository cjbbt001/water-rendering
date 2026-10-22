// Extracted from BoatAttack Water.cs.
using System;
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
        [Tooltip("Vector-array path is sufficient for up to ten study waves.")]
        public bool computeOverride = true;
        public Wave[] _waves;
        public LayerMask depthLayers = 1 << 10;
        public int depthRendererIndex = 1;
        public float depthAreaSize = 100;
        public int depthResolution = 1024;

        bool _useComputeBuffer, _refresh = true, _capturing;
        float _maxWaveHeight, _waveHeight;
        ComputeBuffer waveBuffer;
        Texture2D _rampTexture;
        RenderTexture _depthTex;
        Camera _depthCam;
        PlanarReflections _planarReflections;
        public RenderTexture TerrainDepthTexture => _depthTex;

        static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
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
            if (!settingsData || !surfaceData || !resources) return;
            if (surfaceData._customWaves && surfaceData._waves.Count == 0) return;
            _useComputeBuffer = !computeOverride && SystemInfo.supportsComputeShaders;
            SetWaves();
            GenerateColorRamp();
            Shader.SetGlobalVector("_SurfaceDepthBounds", new Vector4(transform.position.x,
                transform.position.z, 1f / depthAreaSize, 1f / depthAreaSize));
            if (!_planarReflections) _planarReflections = GetComponent<PlanarReflections>();
            if (!_planarReflections) _planarReflections = gameObject.AddComponent<PlanarReflections>();
            _planarReflections.target = gameObject;
            _planarReflections.m_settings = settingsData.planarSettings;
            _planarReflections.enabled = settingsData.refType == ReflectionType.PlanarReflection;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = resources.defaultSeaMaterial;
            foreach (string keyword in new[] { "_REFLECTION_CUBEMAP", "_REFLECTION_PROBES", "_REFLECTION_PLANARREFLECTION" })
                renderer.sharedMaterial.DisableKeyword(keyword);
            renderer.sharedMaterial.EnableKeyword(settingsData.refType == ReflectionType.Cubemap ? "_REFLECTION_CUBEMAP" :
                settingsData.refType == ReflectionType.ReflectionProbe ? "_REFLECTION_PROBES" : "_REFLECTION_PLANARREFLECTION");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // A dedicated orthographic camera captures the terrain once. Its renderer uses
        // SurfaceDepth.shader, which writes normalized height into an RFloat texture.
        [ContextMenu("Capture Depth")]
        public void CaptureDepthMap()
        {
            if (_capturing || !surfaceData) return;
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
                // Match SurfaceDepth.shader encoding: x = camera height, y = depth range.
                Shader.SetGlobalVector("_VeraslWater_DepthCamParams", new Vector4(_depthCam.transform.position.y, far, 0, 0));
            }
            finally { _capturing = false; }
        }

        void OnDisable()
        {
            waveBuffer?.Dispose(); waveBuffer = null;
            if (_planarReflections) _planarReflections.enabled = false;
            if (_depthCam) SafeDestroy(_depthCam.gameObject);
            if (_depthTex) { _depthTex.Release(); SafeDestroy(_depthTex); }
            if (_rampTexture) SafeDestroy(_rampTexture);
        }

        static void SafeDestroy(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void SetWaves()
        {
            SetupWaves(surfaceData._customWaves);

            // set default resources
            Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);

            _maxWaveHeight = 0f;
            foreach (var w in _waves)
            {
                _maxWaveHeight += w.amplitude;
            }
            _maxWaveHeight /= _waves.Length;

            _waveHeight = transform.position.y;

            Shader.SetGlobalFloat(WaveHeight, _waveHeight);
            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, Mathf.Max(0.1f, surfaceData._waterMaxVisibility));

            switch(settingsData.refType)
            {
                case ReflectionType.Cubemap:
                    Shader.EnableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);
                    break;
                case ReflectionType.ReflectionProbe:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.EnableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                case ReflectionType.PlanarReflection:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.EnableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Shader.SetGlobalInt(WaveCount, _waves.Length);

            //GPU side
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer?.Dispose();
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
            if(!custom)
            {
                //create basic waves based off basic wave settings
                var backupSeed = Random.state;
                Random.InitState(surfaceData.randomSeed);
                var basicWaves = surfaceData._basicWaveSettings;
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
        }

        private void GenerateColorRamp()
        {
            if(_rampTexture == null)
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
                switch(surfaceData._foamSettings.foamType)
                {
                    case 0: // default
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(i / 128f , 0.5f);
                        break;
                    case 1: // simple
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(surfaceData._foamSettings.basicFoam.Evaluate(i / 128f) , 0.5f);
                        break;
                    case 2: // custom
                        cols[i + 256] = new Color(surfaceData._foamSettings.denseFoam.Evaluate(i / 128f), surfaceData._foamSettings.mediumFoam.Evaluate(i / 128f), surfaceData._foamSettings.liteFoam.Evaluate(i / 128f));
                        break;
                }
            }
            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(AbsorptionScatteringRamp, _rampTexture);
        }

    }
}
