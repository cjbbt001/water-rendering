using UnityEngine;

namespace WaterSystem.Data
{
    [System.Serializable][CreateAssetMenu(fileName = "WaterSettingsData", menuName = "WaterSystem/Settings", order = 0)]
    public class WaterSettingsData : ScriptableObject
    {
        public ReflectionType refType = ReflectionType.PlanarReflection;

        public PlanarReflections.PlanarReflectionSettings planarSettings;

        public Cubemap cubemapRefType;
    }

    [System.Serializable]
    public enum ReflectionType
    {
        Cubemap,
        ReflectionProbe,
        PlanarReflection
    }
}
