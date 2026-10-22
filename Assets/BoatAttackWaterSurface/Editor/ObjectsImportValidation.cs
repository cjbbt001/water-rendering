using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoatAttackSurface
{
    // Runs once after asset cleanup; never opens or saves the user's scene.
    [InitializeOnLoad]
    internal static class ObjectsImportValidation
    {
        private const string Root = "Assets/BoatAttackWaterSurface/Objects";
        private const string Report = "Logs/ObjectsCleanup/validation.txt";

        static ObjectsImportValidation()
        {
            EditorApplication.delayCall += Validate;
        }

        [MenuItem("Water Example/BoatAttack Surface/Validate Scene Models")]
        private static void ValidateFromMenu()
        {
            Directory.CreateDirectory("Logs/ObjectsCleanup");
            File.WriteAllText("Logs/ObjectsCleanup/revalidate", "");
            Validate();
        }

        private static void Validate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Validate;
                return;
            }
            if (File.Exists(Report) && !File.Exists("Logs/ObjectsCleanup/revalidate"))
                return;
            if (File.Exists("Logs/ObjectsCleanup/reimport"))
            {
                File.Delete("Logs/ObjectsCleanup/reimport");
                foreach (string path in Directory.GetFiles(Root, "*.shadersubgraph", SearchOption.AllDirectories))
                    AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                    if (Path.GetExtension(path).Equals(".shadergraph", StringComparison.OrdinalIgnoreCase))
                        AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                foreach (string path in Directory.GetFiles(Root, "*.mat", SearchOption.AllDirectories))
                    AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            }
            var errors = new List<string>();
            int materials = 0, prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                materials++;
                if (!material || !material.shader || material.shader.name == "Hidden/InternalErrorShader")
                    errors.Add("Missing shader: " + path);
                else
                    foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                        if (message.severity.ToString() == "Error")
                            errors.Add("Shader error: " + path + " | " + message.message);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    prefabs++;
                    foreach (Transform node in contents.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                            errors.Add("Missing script: " + path + " / " + node.name);
                    foreach (var renderer in contents.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        foreach (var material in renderer.sharedMaterials)
                            if (!material)
                                errors.Add("Missing material: " + path + " / " + renderer.name);
                    }
                    foreach (var mesh in contents.GetComponentsInChildren<MeshFilter>(true))
                        if (!mesh.sharedMesh)
                            errors.Add("Missing mesh: " + path + " / " + mesh.name);
                }
                catch (Exception exception)
                {
                    errors.Add("Prefab failed: " + path + " | " + exception.Message);
                }
                finally
                {
                    if (contents) PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            string result = $"OBJECTS_VALIDATION: prefabs={prefabs}, materials={materials}, errors={errors.Count}\n" + string.Join("\n", errors);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, result);
            if (File.Exists("Logs/ObjectsCleanup/revalidate")) File.Delete("Logs/ObjectsCleanup/revalidate");
            if (errors.Count == 0) Debug.Log(result);
            else Debug.LogError(result);
        }
    }
}
