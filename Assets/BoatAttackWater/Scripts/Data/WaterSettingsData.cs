using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem.Data
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "WaterSettingsData",
        menuName = "WaterSystem/Settings")]
    public class WaterSettingsData : ScriptableObject
    {
        public GeometryType waterGeomType;
        public ReflectionType refType = ReflectionType.PlanarReflection;
        public PlanarReflections.PlanarReflectionSettings planarSettings;
        public Cubemap cubemap;
    }

    // 反射模式
    [System.Serializable]
    public enum ReflectionType
    {
        Cubemap,
        ReflectionProbe,
        PlanarReflection
    }

    // 顶点位移 or 曲面细分
    [System.Serializable]
    public enum GeometryType
    {
        VertexOffset,
        Tesselation
    }
}

