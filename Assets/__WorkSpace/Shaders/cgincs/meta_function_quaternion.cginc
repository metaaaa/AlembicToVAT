// クォータニオン関連

float4 quaternion(float rad, float3 axis)
{
    return float4(cos(rad / 2.0), axis * sin(rad / 2.0));
}

float4 mulQuaternion(float4 q, float4 r)
{
    // Q = (q; V)  R = (r; W)
    //QR = (qr - V ・ W; qW + rV + V × W)
    return float4(q.x*r.x - dot(q.yzw,r.yzw), q.x*r.yzw + r.x*q.yzw + cross(q.yzw, r.yzw));
}

float4 sLerpQuaternion(float4 q, float4 r, float t)
{
    float theta = acos(max(dot(q,r), dot(-q,r)));
    return sin((1 - t)*theta)*q/sin(theta) + r*sin(t*theta)/sin(theta);
}

float3 rotateQuaternion(float rad, float3 axis, float3 pos)
{
    float4 q = quaternion(rad, axis);
    return (q.x*q.x - dot(q.yzw, q.yzw)) * pos + 2.0 * q.yzw * dot(q.yzw, pos) + 2 * q.x * cross(q.yzw, pos);
}

float3 rotateQuaternion(float4 q, float3 pos)
{
    return (q.x*q.x - dot(q.yzw, q.yzw)) * pos + 2.0 * q.yzw * dot(q.yzw, pos) + 2 * q.x * cross(q.yzw, pos);
}

float3 angleToRadian(float3 angle)
{
    return PI*angle/180.0;
}

// Unityの回転順はZXY
float4 eulerToQuaternion(float3 rad)
{
    rad = rad*0.5;
    return float4(cos(rad.x)*cos(rad.y)*cos(rad.z) + sin(rad.x)*sin(rad.y)*sin(rad.z),
                  sin(rad.x)*cos(rad.y)*cos(rad.z) + cos(rad.x)*sin(rad.y)*sin(rad.z),
                  cos(rad.x)*sin(rad.y)*cos(rad.z) - sin(rad.x)*cos(rad.y)*sin(rad.z),
                  cos(rad.x)*cos(rad.y)*sin(rad.z) - sin(rad.x)*sin(rad.y)*cos(rad.z));
}

// YXZ
float4 eulerToQuaternionInvRotOrder(float3 rad)
{
    rad = rad*0.5;
    return float4(cos(rad.x)*cos(rad.y)*cos(rad.z) - sin(rad.x)*sin(rad.y)*sin(rad.z),
                  sin(rad.x)*cos(rad.y)*cos(rad.z) - cos(rad.x)*sin(rad.y)*sin(rad.z),
                  cos(rad.x)*sin(rad.y)*cos(rad.z) + sin(rad.x)*cos(rad.y)*sin(rad.z),
                  cos(rad.x)*cos(rad.y)*sin(rad.z) + sin(rad.x)*sin(rad.y)*cos(rad.z));
}

// xyz axis, w rad
float4 quaternionToAxisAngle(float4 qua)
{
    if(qua.w + qua.y + qua.z == 0.0) return float4(1.0, 0.0, 0.0, 0.0);
    float rad = 2.0 * acos(qua.x);
    float3 axis = float3(qua.y / sqrt(1.0 - qua.x * qua.x),
                         qua.z / sqrt(1.0 - qua.x * qua.x),
                         qua.w / sqrt(1.0 - qua.x * qua.x));
    return float4(axis, rad);
}

float3 quaternionToEuler(float4 qua)
{
    qua.xyzw = qua.yzwx;
    float sx = -(2.0 * qua.y * qua.z - 2.0 * qua.x * qua.w);
    float unlocked = abs(sx) < 0.99999;
    float3 euler = 0;
    euler.x = asin(-(2.0 * qua.y * qua.z - 2.0 * qua.x * qua.w));
    euler.y = unlocked ?
        atan2((2.0 * qua.x * qua.z + 2.0 * qua.y * qua.w), (2.0 * qua.w * qua.w + 2.0 * qua.z * qua.z - 1.0)) :
        atan2(-(2.0 * qua.x * qua.z - 2.0 * qua.y * qua.w), 2.0 * qua.w * qua.w + 2.0 * qua.x * qua.x - 1.0);
    euler.z = unlocked ? 
        atan2((2.0 * qua.x * qua.y + 2.0 * qua.z * qua.w), (2.0 * qua.w * qua.w + 2.0 * qua.y * qua.y - 1.0)) : 0.0;
    return euler;
}