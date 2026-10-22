using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem;
using WaterSystem.Data;

namespace BoatAttackSurface
{
    public static class BoatAttackSurfaceBuilder
    {
        const string Root = "Assets/BoatAttackWaterSurface";
        public const string ScenePath = Root + "/Scenes/BoatAttackSurface.unity";
        [Serializable] class Geometry { public Prop[] objects; }
        [Serializable] class Prop { public string name; public int mesh, layer; public Vector3 position, scale; public Quaternion rotation; }

        [MenuItem("Water Example/BoatAttack Surface/Create or Rebuild")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildAndValidate() { Build(); Validate(); }

        // Validate the saved scene through normal play-mode lifecycle, without rebuilding assets.
        public static void ValidateSavedAndPlay()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.playModeStateChanged += WaitForPlay;
            EditorApplication.isPlaying = true;
        }

        static int _playFrames;
        static void WaitForPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= WaitForPlay;
            _playFrames = 0;
            EditorApplication.update += CapturePlay;
        }

        static void CapturePlay()
        {
            if (++_playFrames < 40) return;
            EditorApplication.update -= CapturePlay;
            try
            {
                Validate();
                Debug.Log("BOATATTACK_SURFACE_PLAYMODE_OK: saved scene loaded and rendered during Play.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(Root + "/" + path);
            if (!value) throw new InvalidOperationException("Missing surface asset: " + path);
            return value;
        }
        static Material Material(string name, string shader)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result) { result = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(result, path); }
            result.shader = Shader.Find(shader);
            EditorUtility.SetDirty(result);
            return result;
        }

        static UniversalRendererData Renderer(string name)
        {
            string path = Root + "/Data/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, path); }
            return data;
        }

        static void Build()
        {
            AssetDatabase.Refresh();
            var mainRenderer = Renderer("SurfaceRenderer");
            var depthRenderer = Renderer("DepthRenderer");
            SetForwardPlus(mainRenderer);
            SetForwardPlus(depthRenderer);
            if (mainRenderer.rendererFeatures.Count == 0)
            {
                var feature = ScriptableObject.CreateInstance<WaterCausticsFeature>();
                feature.name = "Surface Caustics";
                feature.material = Material("Caustics", "Hidden/BoatAttackSurface/Caustics");
                feature.material.SetTexture("_CausticMap", Load<Texture2D>("Textures/WaterSurface_single.tif"));
                feature.material.SetFloat("_Size", 0.25f);
                feature.material.SetFloat("_BlendDistance", 3);
                AssetDatabase.AddObjectToAsset(feature, mainRenderer);
                mainRenderer.rendererFeatures.Add(feature);
            }
            if (depthRenderer.rendererFeatures.Count != 0 && !(depthRenderer.rendererFeatures[0] is WaterTerrainDepthFeature))
            {
                foreach (var old in depthRenderer.rendererFeatures) UnityEngine.Object.DestroyImmediate(old, true);
                depthRenderer.rendererFeatures.Clear();
            }
            if (depthRenderer.rendererFeatures.Count == 0)
            {
                depthRenderer.opaqueLayerMask = 0;
                depthRenderer.transparentLayerMask = 0;
                var feature = ScriptableObject.CreateInstance<WaterTerrainDepthFeature>();
                feature.name = "Encode Terrain Height";
                feature.material = Material("TerrainDepth", "Hidden/BoatAttackSurface/TerrainDepth");
                AssetDatabase.AddObjectToAsset(feature, depthRenderer);
                depthRenderer.rendererFeatures.Add(feature);
            }
            EditorUtility.SetDirty(mainRenderer);
            EditorUtility.SetDirty(depthRenderer);
            // Keep the original renderer at index 0. Only the new scene's cameras
            // use the appended surface/depth renderers at indices 1 and 2.
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var serialized = new SerializedObject(asset);
                var list = serialized.FindProperty("m_RendererDataList");
                list.arraySize = 3;
                list.GetArrayElementAtIndex(1).objectReferenceValue = mainRenderer;
                list.GetArrayElementAtIndex(2).objectReferenceValue = depthRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var geometry = JsonUtility.FromJson<Geometry>(File.ReadAllText(Root + "/Editor/OriginalTestGeometry.json"));
            var ground = Material("TestGeometry", "Universal Render Pipeline/Lit");
            ground.SetColor("_BaseColor", new Color(0.58f, 0.56f, 0.48f));
            ground.SetFloat("_Smoothness", 0.15f);
            var parent = new GameObject("Original BoatAttack Test Geometry (static)");
            foreach (var prop in geometry.objects)
            {
                var type = prop.mesh == 10209 ? PrimitiveType.Plane : prop.mesh == 10208 ? PrimitiveType.Capsule :
                    prop.mesh == 10206 ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                var go = GameObject.CreatePrimitive(type);
                go.name = prop.name;
                go.layer = prop.layer;
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = prop.position;
                go.transform.localRotation = prop.rotation;
                go.transform.localScale = prop.scale;
                go.GetComponent<Renderer>().sharedMaterial = ground;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            RenderSettings.skybox = Load<Material>("Materials/ClearSky.mat");
            RenderSettings.skybox.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(RenderSettings.skybox);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.53f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.3f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.16f);
            RenderSettings.fog = false;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, 0.957f, 0.839f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32.6f, 208.5f, 0);
            RenderSettings.sun = sun;

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-1.14f, 6, -10);
            camera.transform.rotation = Quaternion.Euler(20, 0, 0);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 160;
            camera.fieldOfView = 60;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(1);
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            cameraData.requiresColorOption = CameraOverrideOption.On;

            var material = Load<Material>("Materials/Sea.mat");
            material.shader = Shader.Find("BoatAttackSurface/Water");
            material.shaderKeywords = new[] { "_REFLECTION_PLANARREFLECTION", "_DEBUG_OFF" };
            var resources = Load<WaterResources>("Data/WaterResources.asset");
            resources.defaultSeaMaterial = material;
            var mesh = Grid(100, 256);
            resources.defaultWaterMeshes = new[] { mesh };
            var settings = Load<WaterSettingsData>("Data/WaterSettingsData.asset");
            settings.refType = ReflectionType.PlanarReflection;
            settings.cubemapRefType = Load<Cubemap>("Textures/ReflectionProbe-0.exr");
            settings.planarSettings.m_ReflectLayers = ~(1 << 4);
            settings.planarSettings.m_ClipPlaneOffset = 0.07f;
            var surface = new GameObject("Water Surface - BoatAttack");
            surface.SetActive(false);
            surface.layer = 4;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = material;
            var water = surface.AddComponent<Water>();
            water.resources = resources;
            water.settingsData = settings;
            water.surfaceData = Load<WaterSurfaceData>("Data/WaterSurfaceData.asset");
            water.depthRendererIndex = 2;
            surface.SetActive(true);
            water.Init();
            water.GetComponent<PlanarReflections>().rendererIndex = 1;
            foreach (var asset in new UnityEngine.Object[] { material, resources, settings }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = surface;
            Debug.Log("BOATATTACK_SURFACE_BUILT: original static geometry and 256x256-cell grid.");
        }

        static void SetForwardPlus(UniversalRendererData renderer)
        {
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("m_RenderingMode").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Mesh Grid(float size, int cells)
        {
            int side = cells + 1;
            var vertices = new Vector3[side * side];
            var uv = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[cells * cells * 6];
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int i = z * side + x;
                vertices[i] = new Vector3(((float)x / cells - 0.5f) * size, 0, ((float)z / cells - 0.5f) * size);
                uv[i] = new Vector2((float)x / cells, (float)z / cells);
                normals[i] = Vector3.up;
                if (x == cells || z == cells) continue;
                int t = (z * cells + x) * 6;
                triangles[t] = i; triangles[t + 1] = i + side; triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 1; triangles[t + 4] = i + side; triangles[t + 5] = i + side + 1;
            }
            var mesh = new Mesh { name = "Surface Grid 100m 256x256", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size + 10, 10, size + 10));
            string path = Root + "/Meshes/SurfaceGrid.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            return mesh;
        }

        [MenuItem("Water Example/BoatAttack Surface/Open Scene")]
        public static void Open() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }

        [MenuItem("Water Example/BoatAttack Surface/Validate and Capture")]
        public static void Validate()
        {
            var camera = Camera.main;
            if (!camera || camera.gameObject.scene.path != ScenePath) throw new InvalidOperationException("Open the surface scene first.");
            var water = UnityEngine.Object.FindAnyObjectByType<Water>();
            water.Init(); water.CaptureDepthMap();
            var material = Load<Material>("Materials/Sea.mat");
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            var previousActive = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ShaderUtil.CompilePass(material, 0, true);
                rt.Create();
                var request = new RenderPipeline.StandardRequest { destination = rt };
                for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
                foreach (var shader in new[] { material.shader, Shader.Find("Hidden/BoatAttackSurface/Caustics"), Shader.Find("Hidden/BoatAttackSurface/TerrainDepth") })
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            throw new InvalidOperationException(shader.name + ": " + message.message + " line " + message.line);
                if (!material.shader.isSupported) throw new InvalidOperationException("Water shader unsupported.");
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs/BoatAttackSurface");
                File.WriteAllBytes("Logs/BoatAttackSurface/preview.png", pixels.EncodeToPNG());
                foreach (string keyword in new[] { "_DEBUG_NORMAL", "_DEBUG_FOAM", "_DEBUG_REFLECTION", "_DEBUG_REFRACTION", "_DEBUG_WATERDEPTH" })
                {
                    material.DisableKeyword("_DEBUG_OFF"); material.EnableKeyword(keyword);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Logs/BoatAttackSurface/" + keyword + ".png", pixels.EncodeToPNG());
                    material.DisableKeyword(keyword); material.EnableKeyword("_DEBUG_OFF");
                }
                RenderTexture.active = water.TerrainDepthTexture;
                var depthPixels = new Texture2D(water.TerrainDepthTexture.width, water.TerrainDepthTexture.height, TextureFormat.RGBAFloat, false, true);
                depthPixels.ReadPixels(new Rect(0, 0, depthPixels.width, depthPixels.height), 0, 0); depthPixels.Apply();
                var samples = depthPixels.GetPixels();
                float minimum = samples.Min(c => c.r), maximum = samples.Max(c => c.r);
                File.WriteAllBytes("Logs/BoatAttackSurface/depth.png", depthPixels.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(depthPixels);
                if (maximum - minimum < 0.001f) throw new InvalidOperationException("Terrain depth texture is uniform; capture failed.");
                Debug.Log("BOATATTACK_SURFACE_VALIDATION_OK: compiled and rendered, vertices=" + water.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync; RenderTexture.active = previousActive;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
