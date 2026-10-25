using UnityEditor;
using UnityEngine;

namespace WaterSystem.Editor
{
    /// <summary>
    /// Re-serializes assets whose importer metadata was written by an older Unity version.
    /// Unity logs warnings such as:
    /// "contains a TextureImporter object at version 6, below the supported minimum (10).
    ///  Open and re-save the file to upgrade."
    /// <see cref="AssetImporter.SaveAndReimport"/> alone does not rewrite the metadata; the
    /// importer has to be marked dirty first, otherwise the old serialized version is kept.
    /// </summary>
    internal static class UpgradeLegacyTextureImporters
    {
        static readonly string[] Paths =
        {
            // Textures (TextureImporter)
            "Assets/Water/Textures/DefaultFoamRamp.tif",
            "Assets/Water/Textures/PeakMap.tif",
            "Assets/Water/Objects/Shared/Textures/Island/BushTest.tif",
            "Assets/Water/Objects/Shared/Textures/Island/Rock_normal.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/Bush_basecolor.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/Bush_normal.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/RockPlants_basecolor.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/RockPlants_normal.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/SandRocks_basecolor.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/SandRocks_normal.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/BeachSand_normal.tif",
            "Assets/Water/Objects/Shared/Textures/Terrain/BeachSand_wet_basecolor.tif",

            // ShaderGraph (ScriptedImporter)
            "Assets/Water/Objects/Shared/Shaders/UtilityGraphs/PBR_HUE.ShaderGraph",
            "Assets/Water/Objects/Shared/Shaders/CliffShader.ShaderGraph",

            // Mesh (ModelImporter)
            "Assets/Water/Meshes/OriginalSeaVertDisp.fbx"
        };

        [MenuItem("Water/Upgrade Legacy Asset Importers")]
        static void Upgrade()
        {
            var upgraded = 0;
            foreach (var path in Paths)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    Debug.LogWarning($"Asset importer not found: {path}");
                    continue;
                }

                // Mark dirty so SaveAndReimport re-serializes the importer at the current
                // version instead of writing the stale serialized data back out unchanged.
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                upgraded++;
            }

            Debug.Log($"Upgraded legacy importer metadata for {upgraded}/{Paths.Length} assets.");
        }
    }
}
