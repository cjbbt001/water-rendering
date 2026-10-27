using UnityEngine;
using WaterSystem.Data;

namespace WaterSystem
{
    [ExecuteAlways]
    public class Water : MonoBehaviour
    {
        public WaterSurfaceData surfaceData;
        private const int MaxWaves = 10;
        private readonly Vector4[] waveData = new Vector4[20];
        private static readonly int WaveCount =
            Shader.PropertyToID("_WaveCount");
        private static readonly int WaveData =
            Shader.PropertyToID("waveData");

        private void OnEnable()
        {
            SetWaves();
        }

        private void Update()
        {
            SetWaves();
        }

        private void SetWaves()
        {
            if (surfaceData == null || surfaceData._waves == null)
            {
                Shader.SetGlobalInt(WaveCount, 0);
                return;
            }

            int count = Mathf.Min(surfaceData._waves.Count, MaxWaves);

            GetWaveData(count);

            Shader.SetGlobalInt(WaveCount, count);
            Shader.SetGlobalVectorArray(WaveData, waveData);
        }

        private void GetWaveData(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Wave wave = surfaceData._waves[i];

                waveData[i] = new Vector4(
                    Mathf.Max(0, wave.amplitude),
                    wave.direction,
                    Mathf.Max(0.01f, wave.wavelength),
                    0f);
            }
        }
    }
}
