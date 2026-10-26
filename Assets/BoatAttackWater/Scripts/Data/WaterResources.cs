using UnityEngine;

namespace WaterSystem
{
    // 保存默认引用的资源
    [System.Serializable]
    [CreateAssetMenu(
        fileName = "WaterResources",
        menuName = "WaterSystem/Resources",
        order = 0)]
    public class WaterResources : ScriptableObject
    {
        // texture
        public Texture2D defaultFoamRamp; // for basic foamsettings
        public Texture2D defaultFoamMap;
        public Texture2D defaultSurfaceMap; // noraml

        // material
        public Material dafaultWaterMaterial;

        //mesh
        public Mesh[] defaultWaterMeshes;
    }
}