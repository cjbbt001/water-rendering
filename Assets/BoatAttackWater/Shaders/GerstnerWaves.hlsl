#ifndef GERSTNER_WAVES_INCLUDED
#define GERSTNER_WAVES_INCLUDED

uniform uint _WaveCount;
half4 waveData[20]; // 0-9 amplitude, direction, wavelength, omni, 10-19 origin.xy

struct Wave
{
    float amplitude;
    float direction;
    float wavelength;
    float2 origin;
    float omni;
};

struct WaveStruct
{
    float3 position;
    float3 normal;
    
};

WaveStruct GerstnerWave(
    half2 pos, float waveCountMulti,
    half amplitude, half direction,
    half wavelength, half omni, half2 omniPos)
{
    WaveStruct waveOut;
#if defined(_STATIC_SHADER)
	float time = 0;
#else
    float time = _Time.y;
#endif
    
    // wave value
    half k = 6.283185 / wavelength; // 波数 k = 2pi / L 
    half w = sqrt(9.8 * k); // 频率 ω = √(gk)
    half peak = 1.5;
    half qi = peak / (amplitude * k * _WaveCount); // 陡峭度
    
    // direction
    direction = radians(direction);
    half2 dirWave = half2(sin(direction), cos(direction)) * (1 - omni); 
    half2 omniWave = (pos - omniPos) * omni; //omni = 0, 定向波; omni = 1, 圆形波
    half2 windDir = normalize(dirWave + omniWave);
    half d = dot(windDir, pos - (omniPos * omni));
    
    half phase = k * d - time * w; // 相位 φ = kd - tω
    
    half sinPhase = sin(phase);
    half cosPhase = cos(phase);
    
    
    half horizontalAmplitude = qi * amplitude; // 水平振幅 qi * A
    half verticalAmplitude = amplitude * waveCountMulti; // 竖直振幅 A / N
    
    // displacement
    half3 displacement; 
    displacement.xz = horizontalAmplitude * windDir * cosPhase;
    displacement.y = verticalAmplitude * sinPhase;
    
    // normal
    half ka = k * amplitude;
    half3 normal = half3(
        -windDir.x * ka * cosPhase,
        1 - qi * ka * sinPhase,
        -windDir.y * ka * cosPhase
    );

    // output
    waveOut.position = displacement;
    waveOut.normal = normal * waveCountMulti;
    
    return waveOut;
}

void SampleWaves(float3 position, out WaveStruct waveOut)
{
    half2 pos = position.xz;
    waveOut.position = 0;
    waveOut.normal = 0;

    half waveCountMulti = 1.0 / max(_WaveCount, 1);

    UNITY_LOOP
    for (uint i = 0; i < _WaveCount; i++)
    {
        Wave w;
        w.amplitude = waveData[i].x;
        w.direction = waveData[i].y;
        w.wavelength = waveData[i].z;
        w.omni = waveData[i].w;
        w.origin = waveData[i + 10].xy;

        WaveStruct wave = GerstnerWave(pos,
								waveCountMulti,
								w.amplitude,
								w.direction,
								w.wavelength,
								w.omni,
								w.origin);
        
        waveOut.position += wave.position;
        waveOut.normal += wave.normal;
    }

    waveOut.normal = normalize(waveOut.normal);
}

#endif
