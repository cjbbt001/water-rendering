using UnityEditor;
using UnityEngine;
using WaterSystem.Data;

namespace WaterSystem.Editor
{
    [CustomEditor(typeof(WaterSurfaceData))]
    public class SurfaceDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector()) return;
            foreach (var water in Object.FindObjectsByType<Water>())
                if (water.surfaceData == target) water.RefreshSurface();
        }
    }

    [CustomEditor(typeof(WaterSettingsData))]
    public class SurfaceSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector()) return;
            foreach (var water in Object.FindObjectsByType<Water>())
                if (water.settingsData == target) water.RefreshSurface();
        }
    }
}
