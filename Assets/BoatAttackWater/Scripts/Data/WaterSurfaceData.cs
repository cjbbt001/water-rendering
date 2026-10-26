using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

namespace WaterSystem.Data
{

    // 波浪数据
    [System.Serializable]
    public struct Wave
    {
        public float amplitude;
        public float direction;
        public float wavelength;
        public float2 origin;
        public float omniDir;
        public Wave(float amp, float dir, float length, float2 org, bool omni)
        {
            amplitude = amp;
            direction = dir;
            wavelength = length;
            origin = org;
            omniDir = omni ? 1 : 0;
        }

    }

    // 基础波
    [System.Serializable]
    public class BasicWaves
    {
        public int numWaves = 6;
        public float amplitude;
        public float direction;
        public float wavelength;
        public BasicWaves(float amp, float dir, float len)
        {
            numWaves = 6;
            amplitude = amp;
            direction = dir;
            wavelength = len;
        }
    }

    // 泡沫设置
    [System.Serializable]
    public class FoamSettings
    {

        public int foamType; // 0 = default, 1 = simple, 2 = custom
        public AnimationCurve basicFoam;
        public FoamSettings()
        {
            foamType = 0;
            basicFoam = new AnimationCurve(new Keyframe[2] {new Keyframe(0.25f, 0f),
                                                            new Keyframe(1f, 1f)});
        }
    }

    // 水面配置
    [System.Serializable]
    [CreateAssetMenu(
        fileName = "WaterSurfaceData",
        menuName = "WaterSystem/Surface Data",
        order = 0 )]
    public class WaterSurfaceData : ScriptableObject
    {
        public List<Wave> _waves = new List<Wave>();

        public float _waterMaxVisibility = 40.0f;
        public Gradient _absorptionRamp;
        public Gradient _scatterRamp;
        public int randomseed = 3234;
        public BasicWaves _basicWaveSettings = new BasicWaves(1.5f, 45.0f, 5.0f);
        public FoamSettings _foamSettings = new FoamSettings();
        public bool _init = false;
    }
}
