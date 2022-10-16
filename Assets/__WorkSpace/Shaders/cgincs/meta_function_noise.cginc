// ノイズ関連

float noise(float3 pos)
{
    float3 ip = floor(pos);
    float3 fp = smoothstep(0, 1, frac(pos));
    float4 a = float4(
    rand(ip + float3(0, 0, 0)),
    rand(ip + float3(1, 0, 0)),
    rand(ip + float3(0, 1, 0)),
    rand(ip + float3(1, 1, 0)));
    float4 b = float4(
    rand(ip + float3(0, 0, 1)),
    rand(ip + float3(1, 0, 1)),
    rand(ip + float3(0, 1, 1)),
    rand(ip + float3(1, 1, 1)));
    a = lerp(a, b, fp.z);
    a.xy = lerp(a.xy, a.zw, fp.y);
    return lerp(a.x, a.y, fp.x);
}

float perlin(float3 pos) {
    return  (noise(pos) * 32 +
    noise(pos * 2 ) * 16 +
    noise(pos * 4) * 8 +
    noise(pos * 8) * 4 +
    noise(pos * 16) * 2 +
    noise(pos * 32) ) / 63;
}

float3 perlin3d(float3 x)
{
    float s = perlin(x);
    float s1 = perlin(float3(x.y - 19.1, x.z + 33.4, x.x + 47.2));
    float s2 = perlin(float3(x.z + 74.2, x.x - 124.5, x.y + 99.4));
    float3 c = float3(s, s1, s2);
    return c;
}

float3 mod289(float3 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 mod289(float4 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 permute(float4 x)
{
    return mod289((x * 34.0 + 1.0) * x);
}

float4 taylorInvSqrt(float4 r)
{
    return 1.79284291400159 - 0.85373472095314 * r;
}

float snoise(float3 v)
{
    float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    // First corner
    float3 i  = floor(v + dot(v, C.yyy));
    float3 x0 = v   - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    // x1 = x0 - i1  + 1.0 * C.xxx;
    // x2 = x0 - i2  + 2.0 * C.xxx;
    // x3 = x0 - 1.0 + 3.0 * C.xxx;
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float4 p =
    permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
    + i.y + float4(0.0, i1.y, i2.y, 1.0))
    + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float4 j = p - 49.0 * floor(p * (1.0 / 49.0));  // mod(p,7*7)

    float4 x_ = floor(j * (1.0 / 7.0));
    float4 y_ = floor(j - 7.0 * x_ );  // mod(j,N)

    float4 x = x_ * (2.0 / 7.0) + 0.5 / 7.0 - 1.0;
    float4 y = y_ * (2.0 / 7.0) + 0.5 / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    //float4 s0 = float4(lessThan(b0, 0.0)) * 2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1, 0.0)) * 2.0 - 1.0;
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -1*step(h, float4(0.0,0.0,0.0,0.0));

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    // Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Mix final noise value
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    m = m * m;

    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
    return 42.0 * dot(m, px);
}


float3 snoise3d(float3 x)
{
    float s = snoise(x);
    float s1 = snoise(float3(x.y - 31.416, x.z + 47.853, x.x + 12.793));
    float s2 = snoise(float3(x.z + 233.145, x.x - 113.408, x.y + 185.31));
    float3 c = float3(s, s1, s2);
    return c;
}


float3 curlNoise(float3 p, float e = 0.009765625)
{
    // float e = 0.0009765625;
    // float e2 = 2.0 * e;

    float3 dx = float3( e   , 0.0 , 0.0 );
    float3 dy = float3( 0.0 , e   , 0.0 );
    float3 dz = float3( 0.0 , 0.0 , e   );

    float3 p_x0 = snoise3d( p - dx );
    float3 p_x1 = snoise3d( p + dx );
    float3 p_y0 = snoise3d( p - dy );
    float3 p_y1 = snoise3d( p + dy );
    float3 p_z0 = snoise3d( p - dz );
    float3 p_z1 = snoise3d( p + dz );

    float x = p_y1.z - p_y0.z - p_z1.y + p_z0.y;
    float y = p_z1.x - p_z0.x - p_x1.z + p_x0.z;
    float z = p_x1.y - p_x0.y - p_y1.x + p_y0.x;

    return normalize( float3( x , y , z ) );
}

float3 curlNoiseP(float3 p, float e = 0.009765625)
{
    // float e = 0.0009765625;
    // float e2 = 2.0 * e;

    float3 dx = float3( e   , 0.0 , 0.0 );
    float3 dy = float3( 0.0 , e   , 0.0 );
    float3 dz = float3( 0.0 , 0.0 , e   );

    float3 p_x0 = perlin3d( p - dx );
    float3 p_x1 = perlin3d( p + dx );
    float3 p_y0 = perlin3d( p - dy );
    float3 p_y1 = perlin3d( p + dy );
    float3 p_z0 = perlin3d( p - dz );
    float3 p_z1 = perlin3d( p + dz );

    float x = p_y1.z - p_y0.z - p_z1.y + p_z0.y;
    float y = p_z1.x - p_z0.x - p_x1.z + p_x0.z;
    float z = p_x1.y - p_x0.y - p_y1.x + p_y0.x;

    return normalize( float3( x , y , z ) );
}