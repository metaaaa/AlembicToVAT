// 円周率
#define PI 3.141592653589
// degreeをradianに変換する定数
#define DIG2RAD 0.01745329251

// 回転
float2 rot(float2 p, float r)
{
    float c = cos(r);
    float s = sin(r);
    return mul(p, float2x2(c, -s, s, c));
}

// 3次元回転
float3 rand3D(float3 p)
{
    p = float3( dot(p,float3(127.1, 311.7, 74.7)),
    dot(p,float3(269.5, 183.3,246.1)),
    dot(p,float3(113.5,271.9,124.6)));
    return frac(sin(p)*43758.5453123);
}

// 疑似乱数
float rand(float2 co)
{
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
}

// 疑似乱数
float rand(float3 co)
{
    return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 56.787))) * 43758.5453);
}

//更新頻度を下げる奴、FPSを下げる？
float posterize(float f,float c)
{
    float pstr=floor(f*c)/(c-1);
    return pstr;
}

//更新頻度を下げる奴
float2 posterize(float2 f,float2 c)
{
    float2 pstr=floor(f*c)/(c-1);
    return pstr;
}

float ramp(float val, float vmin, float vmax, float rmin, float rmax)
{
    // inverese lerp
    val = clamp(vmin, vmax, val);
    float per = (val - vmin) / (vmax - vmin);
    return lerp(rmin, rmax, per);
}

float2 ramp(float2 val, float2 vmin, float2 vmax, float2 rmin, float2 rmax)
{
    // inverese lerp
    val = clamp(vmin, vmax, val);
    float2 per = (val - vmin) / (vmax - vmin);
    return lerp(rmin, rmax, per);
}

float3 ramp(float3 val, float3 vmin, float3 vmax, float3 rmin, float3 rmax)
{
    // inverese lerp
    val = clamp(vmin, vmax, val);
    float3 per = (val - vmin) / (vmax - vmin);
    return lerp(rmin, rmax, per);
}

float4 ramp(float4 val, float4 vmin, float4 vmax, float4 rmin, float4 rmax)
{
    // inverese lerp
    val = clamp(vmin, vmax, val);
    float4 per = (val - vmin) / (vmax - vmin);
    return lerp(rmin, rmax, per);
}

// lerpの逆
float inverseLerp(float val, float vmin, float vmax)
{
    val = clamp(vmin, vmax, val);
    return (val - vmin) / (vmax - vmin);
}

// カメラ位置取得(VR対応)
float3 getCameraPos()
{
    float3 cameraPos = _WorldSpaceCameraPos;
    #if defined(USING_STEREO_MATRICES)
    cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
    #endif
    return cameraPos;
}

#include "meta_function_ease.cginc"
#include "meta_function_quaternion.cginc"
#include "meta_function_color_space.cginc"
#include "meta_function_noise.cginc"