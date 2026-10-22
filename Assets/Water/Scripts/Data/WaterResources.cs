using UnityEngine;

namespace WaterSystem
{
    [System.Serializable][CreateAssetMenu(fileName = "WaterResources", menuName = "WaterSystem/Resource", order = 0)]
    public class WaterResources : ScriptableObject
    {
        public Texture2D defaultFoamRamp;
        public Texture2D defaultFoamMap;
        public Texture2D defaultSurfaceMap;
        public Material defaultSeaMaterial;
    }
}
