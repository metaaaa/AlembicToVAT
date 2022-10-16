//色空間変換関連

float3 RGB2HSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HSV2RGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// https://github.com/fuqunaga/ColorSpace/blob/master/ColorSpace.cginc
//--------------------------------------------------------------------------------
// ColorSpace Conversion
//  https://github.com/fuqunaga/ColorSpace
//
// Refs:
//  https://en.wikipedia.org/wiki/SRGB
//  https://en.wikipedia.org/wiki/CIELAB_color_space
//  https://github.com/mattharrison/colorspace.py/blob/master/colorspace.py
//--------------------------------------------------------------------------------

//------------------------------------------------------------
// CIEXYZ
//------------------------------------------------------------
float3 RGBLinearToXYZ(float3 rgb)
{
    float3x3 m = float3x3(
        0.41239080, 0.35758434, 0.18048079,
        0.21263901, 0.71516868, 0.07219232,
        0.01933082, 0.11919478, 0.95053215
    );

    return mul(m, rgb);
}

float3 XYZToRGBLinear(float3 xyz)
{
    float3x3 m = float3x3(
        +3.24096994, -1.53738318, -0.49861076,
        -0.96924364, +1.8759675,  +0.04155506,
        +0.05563008, -0.20397696, +1.05697151
    );

    return mul(m, xyz);
}


//------------------------------------------------------------
// CIELAB
// Note: the L* coordinate ranges from 0 to 100.
//------------------------------------------------------------
static const float LAB_Xn = 0.950489;
static const float LAB_Yn = 1.0;
static const float LAB_Zn = 1.088840;


float _LABFunc(float t)
{
    const float T = 0.00885645168; //pow(6/29,3);
    return t > T
    ? pow(t, 1.0/3.0)
    : 7.78703704 * t + 4.0/29.0;
}

float _LABFuncInv(float t)
{
    const float T = 6/29.0;
    return t > T
    ? t*t*t
    : 3*T*T*(t - 4/29.0);
}


float3 XYZToLAB(float3 xyz)
{
    float fx = _LABFunc(xyz.x / LAB_Xn);
    float fy = _LABFunc(xyz.y / LAB_Yn);
    float fz = _LABFunc(xyz.z / LAB_Zn);

    return float3(
        116*fy - 16,
        500*(fx-fy),
        200*(fy-fz)
    );
}

float3 LABToXYZ(float3 lab)
{
    float ltmp = (lab.x + 16)/116;
    return float3(
        LAB_Xn * _LABFuncInv(ltmp + lab.y / 500),
        LAB_Yn * _LABFuncInv(ltmp),
        LAB_Zn * _LABFuncInv(ltmp - lab.z / 200)
    );
}


//------------------------------------------------------------
// sRGB(D65)
//------------------------------------------------------------
float3 SRGBToRGBLinear(float3 rgb)
{
    const float t = 0.04045;
    float3 a = rgb / 12.92;
    float3 b = pow((rgb+0.055)/1.055, 2.4);
    return float3(
        rgb.r<=t ? a.r : b.r,
        rgb.g<=t ? a.g : b.g,
        rgb.b<=t ? a.b : b.b
    );
}

float3 RGBLinearToSRGB(float3 rgb)
{
    const float t = 0.031308;
    float3 a = rgb * 12.92;
    float3 b = 1.055*pow(rgb, 1/2.4) - 0.055;
    float3 srgb = float3(
        rgb.r<=t ? a.r : b.r,
        rgb.g<=t ? a.g : b.g,
        rgb.b<=t ? a.b : b.b
    );

    return saturate(srgb);
}


float3 RGBToXYZ(float3 rgb)
{
    return RGBLinearToXYZ(SRGBToRGBLinear(rgb));
}

float3 XYZToRGB(float3 xyz)
{
    float3 rgbl = XYZToRGBLinear(xyz);
    return RGBLinearToSRGB(rgbl);
}


// Note: the L* coordinate ranges from 0 to 100.
float3 RGBToLAB(float3 rgb)
{
    return XYZToLAB(RGBToXYZ(rgb));
}

float3 LABToRGB(float3 lab)
{
    return XYZToRGB(LABToXYZ(lab));
}
