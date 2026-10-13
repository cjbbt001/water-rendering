using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterExample
{
    public static class WaterExampleBuilder
    {
        const string Root = "Assets/WaterExample";
        public const string ScenePath = Root + "/Scenes/SimpleWater.unity";

        [MenuItem("Water Example/Create or Rebuild Demo")]
        public static void CreateDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildDemo();
        }

        // CLI entry point: Unity -batchmode -projectPath ... -executeMethod WaterExample.WaterExampleBuilder.BuildAndValidate -quit
        public static void BuildAndValidate()
        {
            BuildDemo();
            ValidateAndCapture();
        }

        static void BuildDemo()
        {
            foreach (string folder in new[] { "Materials", "Textures", "Meshes", "Scenes" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var shader = Shader.Find("WaterExample/Simple Water");
            if (shader == null) throw new InvalidOperationException("Simple Water shader was not imported.");

            Texture2D normal = MakeTexture("WaveNormal", 256, (u, v) =>
            {
                // Periodic height derivatives give a seamless, neutral-centered normal map.
                float a = (u * 3 + v * 2) * Mathf.PI * 2;
                float b = (u * -2 + v * 4) * Mathf.PI * 2;
                float c = (u * 7 + v * -5) * Mathf.PI * 2;
                float dx = 0.32f * Mathf.Cos(a) - 0.16f * Mathf.Cos(b) + 0.1f * Mathf.Cos(c);
                float dy = 0.22f * Mathf.Cos(a) + 0.32f * Mathf.Cos(b) - 0.07f * Mathf.Cos(c);
                Vector3 n = new Vector3(-dx, -dy, 1).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
            }, true);
            Texture2D caustics = MakeTexture("Caustics", 256, (u, v) =>
            {
                // Warped periodic ridges provide a lightweight illustrative caustics texture.
                float x = u * Mathf.PI * 2, y = v * Mathf.PI * 2;
                float a = Mathf.Sin(x * 3 + 0.85f * Mathf.Sin(y * 2));
                float b = Mathf.Sin(y * 3 + 0.85f * Mathf.Sin(x * 2));
                float line = Mathf.Exp(-Mathf.Abs(a * b) * 24);
                return new Color(line, line, line);
            });
            Texture2D foam = MakeTexture("FoamNoise", 256, (u, v) =>
            {
                float value = PeriodicNoise(u, v, 7) * 0.65f + PeriodicNoise(u, v, 19) * 0.35f;
                return new Color(value, value, value);
            });
            Texture2D sand = MakeTexture("Sand", 256, (u, v) =>
            {
                float value = PeriodicNoise(u, v, 8) * 0.7f + PeriodicNoise(u, v, 43) * 0.3f;
                return Color.Lerp(new Color(0.55f, 0.43f, 0.27f), new Color(0.88f, 0.8f, 0.59f), value);
            }, false, true);
            var water = GetMaterial("Water", shader);
            water.SetTexture("_NormalMap", normal);
            water.SetTexture("_CausticsMap", caustics);
            water.SetTexture("_FoamMap", foam);
            water.SetTexture("_ReflectionTex", MakeSkyCube());
            var sandMat = GetMaterial("Sand", Shader.Find("Universal Render Pipeline/Lit"));
            sandMat.SetTexture("_BaseMap", sand);
            sandMat.SetColor("_BaseColor", Color.white);
            sandMat.SetFloat("_Smoothness", 0.15f);
            var rockMat = GetMaterial("Rock", Shader.Find("Universal Render Pipeline/Lit"));
            rockMat.SetColor("_BaseColor", new Color(0.32f, 0.36f, 0.36f));
            rockMat.SetFloat("_Smoothness", 0.22f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.37f, 0.43f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.21f);
            RenderSettings.fog = false;
            var sky = GetMaterial("Sky", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.72f));
            sky.SetFloat("_Exposure", 1.15f);
            RenderSettings.skybox = sky;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.color = new Color(1, 0.95f, 0.84f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            RenderSettings.sun = sun;

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(13, 12, -16);
            camera.transform.LookAt(new Vector3(0, -0.6f, 0));
            camera.fieldOfView = 48;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            cameraData.requiresColorOption = CameraOverrideOption.On;
            cameraData.renderPostProcessing = false;

            CreateMeshObject("Sand Bank and Sloping Bottom", MakeBed(), sandMat);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "Water Surface - select material to tune";
            surface.transform.localScale = new Vector3(2.4f, 1, 2);
            surface.GetComponent<Renderer>().sharedMaterial = water;
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());

            CreateRock("Shore Rock A", new Vector3(-6.5f, -0.1f, 3.8f), new Vector3(3, 2.9f, 2.6f), rockMat);
            CreateRock("Shore Rock B", new Vector3(-4.4f, -0.25f, 5.1f), new Vector3(1.8f, 1.8f, 2.1f), rockMat);
            CreateRock("Shore Rock C", new Vector3(6.1f, 0.1f, 3.7f), new Vector3(2.1f, 2.7f, 2.5f), rockMat);
            CreateRock("Submerged Rock A", new Vector3(-2.8f, -1.7f, -1.3f), new Vector3(1.9f, 1.2f, 1.5f), rockMat);
            CreateRock("Submerged Rock B", new Vector3(2.3f, -2.1f, -0.6f), new Vector3(2.2f, 1, 1.6f), rockMat);
            CreateRock("Contact Foam Rock", new Vector3(3.4f, -0.2f, -4.3f), new Vector3(1.3f, 1.7f, 1.5f), rockMat);
            EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, -0.5f, 0), camera.transform.rotation, 25);
            Selection.activeGameObject = surface;
            Debug.Log("Water example created: " + ScenePath);
        }

        static Material GetMaterial(string name, Shader shader)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        static float PeriodicNoise(float u, float v, float scale)
        {
            // Blend opposite edges so the repeating texture has no seams.
            float a = Mathf.PerlinNoise(u * scale, v * scale);
            float b = Mathf.PerlinNoise((u - 1) * scale, v * scale);
            float c = Mathf.PerlinNoise(u * scale, (v - 1) * scale);
            float d = Mathf.PerlinNoise((u - 1) * scale, (v - 1) * scale);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static Texture2D MakeTexture(string name, int size, Func<float, float, Color> sample, bool normal = false, bool srgb = false)
        {
            string path = Root + "/Textures/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false, !srgb);
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    colors[y * size + x] = sample((float)x / size, (float)y / size);
            texture.SetPixels(colors);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Cubemap MakeSkyCube()
        {
            const int size = 64;
            string path = Root + "/Textures/SkyReflection.asset";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null)
            {
                cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "SkyReflection" };
                AssetDatabase.CreateAsset(cube, path);
            }
            for (int face = 0; face < 6; face++)
            {
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = 2f * (x + 0.5f) / size - 1, v = 2f * (y + 0.5f) / size - 1;
                        Vector3 d;
                        switch ((CubemapFace)face)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                            default: d = new Vector3(-u, -v, -1); break;
                        }
                        d.Normalize();
                        Color color = Color.Lerp(new Color(0.66f, 0.81f, 0.86f), new Color(0.16f, 0.4f, 0.7f), Mathf.Sqrt(Mathf.Max(d.y, 0)));
                        if (d.y < 0) color = new Color(0.25f, 0.28f, 0.25f);
                        pixels[y * size + x] = color;
                    }
                cube.SetPixels(pixels, (CubemapFace)face);
            }
            cube.Apply(true);
            EditorUtility.SetDirty(cube);
            return cube;
        }

        static Mesh MakeBed()
        {
            const int cells = 96;
            int side = cells + 1;
            var vertices = new Vector3[side * side];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[cells * cells * 6];
            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                {
                    float wx = (float)x / cells * 32 - 16;
                    float wz = (float)z / cells * 28 - 14;
                    float r = Mathf.Sqrt(wx * wx / 110 + wz * wz / 70);
                    float irregularity = Mathf.Sin(wx * 0.6f + wz * 0.4f) * 0.1f;
                    float height = -2.8f + 3.6f * Mathf.Pow(Mathf.Min(r, 1f), 3) + irregularity;
                    int i = z * side + x;
                    vertices[i] = new Vector3(wx, height, wz);
                    uv[i] = new Vector2(wx * 0.25f, wz * 0.25f);
                    if (x == cells || z == cells) continue;
                    int t = (z * cells + x) * 6;
                    triangles[t] = i; triangles[t + 1] = i + side; triangles[t + 2] = i + 1;
                    triangles[t + 3] = i + 1; triangles[t + 4] = i + side; triangles[t + 5] = i + side + 1;
                }
            var mesh = new Mesh { name = "Sloping Sand Bed", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = Root + "/Meshes/SandBed.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        static void CreateMeshObject(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void CreateRock(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(position.x * 17, position.z * 23, 12);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        [MenuItem("Water Example/Open Demo")]
        public static void OpenDemo()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Water Example/Validate and Capture")]
        public static void ValidateAndCapture()
        {
            var camera = Camera.main;
            if (camera == null || camera.gameObject.scene.path != ScenePath)
                throw new InvalidOperationException("Open the SimpleWater scene before validation.");
            var water = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Water.mat");
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            RenderTexture previousActive = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ShaderUtil.CompilePass(water, 0, true);
                rt.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                // Warm up pipeline and camera resources, then capture the fully initialized frame.
                for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
                foreach (var message in ShaderUtil.GetShaderMessages(water.shader))
                {
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        throw new InvalidOperationException(message.message + " at line " + message.line);
                    Debug.Log("Water shader: " + message.message);
                }
                if (!water.shader.isSupported) throw new InvalidOperationException("Water shader is unsupported.");
                RenderTexture.active = rt;
                capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                capture.Apply();
                Directory.CreateDirectory("Logs/WaterExample");
                File.WriteAllBytes("Logs/WaterExample/preview.png", capture.EncodeToPNG());
                Debug.Log("WATER_EXAMPLE_VALIDATION_OK: compiled and rendered 1280x720 preview.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                RenderTexture.active = previousActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(capture);
            }
        }
    }
}
