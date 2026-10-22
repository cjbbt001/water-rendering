using UnityEditor;
using UnityEngine;
using WaterSystem;
using WaterSystem.Data;

namespace BoatAttackSurface
{
    [CustomEditor(typeof(WaterSurfaceData))]
    public class SurfaceDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector()) return;
            foreach (var water in Object.FindObjectsByType<Water>())
                if (water.surfaceData == target) water.RefreshSurface();
        }
    }

    [CustomEditor(typeof(WaterSettingsData))]
    public class SurfaceSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector()) return;
            foreach (var water in Object.FindObjectsByType<Water>())
                if (water.settingsData == target) water.RefreshSurface();
        }
    }
}
