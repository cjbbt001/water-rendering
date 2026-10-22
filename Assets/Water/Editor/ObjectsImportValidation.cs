using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Editor
{
    internal static class ObjectsImportValidation
    {
        private const string Root = "Assets/Water/Objects";
        private const string Report = "Logs/Water/objects-validation.txt";

        [MenuItem("Water/Validate Scene Models")]
        public static void Validate()
        {
            var errors = new List<string>();
            int materials = 0, prefabs = 0;
            foreach (string path in Directory.GetFiles(Root + "/Island/Terrain", "Terrain_*.asset"))
            {
                var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(path.Replace('\\', '/'));
                if (!terrain || terrain.heightmapResolution < 2)
                    errors.Add("Invalid terrain data: " + path);
            }
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
                    foreach (var terrain in contents.GetComponentsInChildren<Terrain>(true))
                        if (!terrain.terrainData)
                            errors.Add("Missing terrain data: " + path + " / " + terrain.name);
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
            if (errors.Count == 0) Debug.Log(result);
            else Debug.LogError(result);
        }
    }
}
