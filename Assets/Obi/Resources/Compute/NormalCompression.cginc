#ifndef NORMALCOMPRESSION_INCLUDE
#define NORMALCOMPRESSION_INCLUDE

float2 octWrap( float2 v )
{
    return ( 1.0 - abs( v.yx ) ) * ( v.xy >= 0.0 ? 1.0 : -1.0 );
}

// use octahedral encoding to reduce to 2 coords, then pack them as two 16 bit values in a 32 bit float.
float encode( float3 n )
{
    n /= ( abs( n.x ) + abs( n.y ) + abs( n.z ) );
    n.xy = n.z >= 0.0 ? n.xy : octWrap( n.xy );
    n.xy = n.xy * 0.5 + 0.5;
    uint nx = (uint)(n.x * 0xffff);
    uint ny = (uint)(n.y * 0xffff);
    return asfloat((nx << 16) | (ny & 0xffff));
}
 
// unpack 32 bit float into two 16 bit ones, then use octahedral decoding.
float3 decode( float k )
{
    uint d = asuint(k);
    float2 f = float2((d >> 16) / 65535.0, (d & 0xffff) / 65535.0) * 2.0 - 1.0;
     
    float3 n = float3( f.x, f.y, 1.0 - abs( f.x ) - abs( f.y ) );
    float t = saturate( -n.z );
    n.xy += n.xy >= 0.0 ? -t : t;
    return normalize( n );
}

#endif