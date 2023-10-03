#include "lib/shared/hash-functions.hlsl"
#include "lib/shared/noise-functions.hlsl"
#include "lib/shared/point.hlsl"

cbuffer Params : register(b0)
{
    float4x4 TransformVolume;
    float4x4 InverseTransformVolume;

    float Strength;
    float Bounciness;
    float Attraction;
    float AttractionDecay;

    float Repulsion;
    float SpeedFactor;
    float InvertVolumeFactor;
}

cbuffer Params : register(b1)
{
    int VolumeShape;
} 


RWStructuredBuffer<Particle> Particles : u0; 

static const int VolumeSphere = 0;
static const int VolumeBox = 1;
static const int VolumePlane = 2;
static const int VolumeZebra = 3;
static const int VolumeNoise = 4;

[numthreads(64,1,1)]
void main(uint3 i : SV_DispatchThreadID)
{
    uint maxParticleCount, _;
    Particles.GetDimensions(maxParticleCount, _);
    int gi=i.x;
    if(gi >= maxParticleCount) 
        return;

    if (isnan(Particles[gi].birthTime))
        return;
    
    // return;
    if(isnan(TransformVolume._11) || TransformVolume._11 == 0) {
        return;
    }
        
    float3 pos = Particles[gi].p.position;
    float4 rot = Particles[gi].p.rotation;
    float3 velocity = Particles[gi].velocity;    

    float3 posInVolume = mul(float4(pos, 1), TransformVolume).xyz;
    float3 posInVolumeNext = mul(float4(pos + velocity * SpeedFactor * 0.01 * 2, 1), TransformVolume).xyz;

    //float s = 1;
    float distance = 0;
    float distanceNext =0;
    float3 surfaceN =0;
    if (VolumeShape == VolumeSphere)
    {
        float rUnitSphere = 0.5;
        distance = length(posInVolume) - rUnitSphere;
        distanceNext = length(posInVolumeNext) - rUnitSphere;
        surfaceN = normalize(posInVolume);
        // s = smoothstep(1 + FallOff, 1, distance);
    }
    else if (VolumeShape == VolumeBox)
    {
        float rUnitSphere = 0.5;
        float3 t1 = abs(posInVolume);
        distance = max(max(t1.x, t1.y), t1.z) - rUnitSphere;

        float3 t2 = abs(posInVolumeNext);
        distanceNext = max(max(t2.x, t2.y), t2.z) - rUnitSphere;

        surfaceN = t1.x > t1.y ? (t1.x > t1.z ? float3(sign(posInVolume.x),0,0) : float3(0,0,sign(posInVolume.z)))  
                               : (t1.y > t1.z ? float3(0,sign(posInVolume.y),0) : float3(0,0,sign(posInVolume.z)));

        //Particles[gi].w = distance;
        // s = smoothstep(1 + FallOff, 1, distance);
    }
    else if (VolumeShape == VolumePlane)
    {
        float distance = posInVolume.y;
        // s = smoothstep(FallOff, 0, distance);
    }
    else if (VolumeShape == VolumeZebra)
    {
        //float distance = 1 - abs(mod(posInVolume.y * 1 + Phase, 2) - 1);
        // s = smoothstep(Threshold + 0.5 + FallOff, Threshold + 0.5, distance);
    }
    else if (VolumeShape == VolumeNoise)
    {
        //float3 noiseLookup = (posInVolume * 0.91 + Phase);
        //float noise = snoise(noiseLookup);
        // s = smoothstep(Threshold + FallOff, Threshold, noise);
    }

    float3 force =0;

    surfaceN *= InvertVolumeFactor;
    float3 surfaceInWorld = normalize(mul(float4(surfaceN, 1), InverseTransformVolume).xyz);

    if(sign( distance * distanceNext) < 0  && distance * InvertVolumeFactor > 0) 
    {
        velocity = reflect(velocity, surfaceInWorld) * Bounciness;
    } 
    else 
    {
        if(distance * InvertVolumeFactor < 0) {
            force = surfaceInWorld * Repulsion;
        }
        else 
        {
            force = -surfaceInWorld * Attraction ;

        }
        velocity += force;
    }   

    Particles[gi].velocity = velocity;
}

