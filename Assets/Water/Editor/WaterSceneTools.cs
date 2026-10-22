using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaterSystem.Editor
{
    public static class WaterSceneTools
    {
        public const string ScenePath = "Assets/Water/Scenes/water.unity";
        const string OutputPath = "Logs/Water";
        static readonly string[] DebugKeywords =
        {
            "_DEBUG_OFF", "_DEBUG_SSS", "_DEBUG_REFRACTION", "_DEBUG_REFLECTION",
            "_DEBUG_NORMAL", "_DEBUG_FRESNEL", "_DEBUG_FOAM", "_DEBUG_WATERDEPTH"
        };

        [MenuItem("Water/Open Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void ValidateSaved()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
        }

        [MenuItem("Water/Validate and Capture")]
        public static void Validate()
        {
            var camera = Camera.main;
            if (!camera || camera.gameObject.scene.path != ScenePath)
                throw new InvalidOperationException("Open the water scene first.");
            var water = UnityEngine.Object.FindObjectsByType<Water>()
                .FirstOrDefault(value => value.gameObject.scene == camera.gameObject.scene && value.isActiveAndEnabled);
            if (!water || !water.settingsData || !water.surfaceData || !water.resources || !water.resources.defaultSeaMaterial)
                throw new InvalidOperationException("Water component or required resources are missing.");
            foreach (var root in camera.gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                        throw new InvalidOperationException("Missing script: " + node.name);

            water.Init();
            water.CaptureDepthMap();
            var material = water.resources.defaultSeaMaterial;
            var previousKeywords = material.shaderKeywords;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Texture2D depthPixels = null;
            try
            {
                Directory.CreateDirectory(OutputPath);
                ShaderUtil.allowAsyncCompilation = false;
                target.Create();
                foreach (string keyword in DebugKeywords) material.DisableKeyword(keyword);
                material.EnableKeyword("_DEBUG_OFF");
                Capture(camera, target, pixels, "preview");
                foreach (string keyword in DebugKeywords.Skip(1))
                {
                    material.DisableKeyword("_DEBUG_OFF");
                    material.EnableKeyword(keyword);
                    Capture(camera, target, pixels, keyword);
                    material.DisableKeyword(keyword);
                    material.EnableKeyword("_DEBUG_OFF");
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Water/Shaders" }))
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!shader || !shader.isSupported)
                        throw new InvalidOperationException("Unsupported water shader: " + AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            throw new InvalidOperationException(shader.name + ": " + message.message + " line " + message.line);
                }
                var depth = water.TerrainDepthTexture;
                if (!depth || !depth.IsCreated()) throw new InvalidOperationException("Terrain depth texture missing.");
                RenderTexture.active = depth;
                depthPixels = new Texture2D(depth.width, depth.height, TextureFormat.RGBAFloat, false, true);
                depthPixels.ReadPixels(new Rect(0, 0, depth.width, depth.height), 0, 0);
                depthPixels.Apply();
                var samples = depthPixels.GetPixels();
                if (samples.Any(value => float.IsNaN(value.r) || float.IsInfinity(value.r)))
                    throw new InvalidOperationException("Terrain depth contains invalid values.");
                float minimum = samples.Min(value => value.r), maximum = samples.Max(value => value.r);
                File.WriteAllBytes(OutputPath + "/depth.png", depthPixels.EncodeToPNG());
                string result = $"WATER_VALIDATION_OK: compiled and rendered, depth={minimum}..{maximum}, depthLayers={water.depthLayers.value}";
                File.WriteAllText(OutputPath + "/validation.txt", result);
                Debug.Log(result);
            }
            finally
            {
                material.shaderKeywords = previousKeywords;
                ShaderUtil.allowAsyncCompilation = previousAsync;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                if (depthPixels) UnityEngine.Object.DestroyImmediate(depthPixels);
            }
        }

        static void Capture(Camera camera, RenderTexture target, Texture2D pixels, string name)
        {
            for (int i = 0; i < 3; i++)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(OutputPath + "/" + name + ".png", pixels.EncodeToPNG());
        }
    }
}
