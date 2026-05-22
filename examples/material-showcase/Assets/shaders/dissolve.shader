// Material Showcase: dissolve / wireframe-ish shader.
// Uses a procedural 3D noise (interference pattern) to discard pixels below a
// threshold and emits an emissive "burn" rim around the dissolving edge.

HEADER
{
	Description = "Procedural dissolve effect with emissive burn edge";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( CullMode, NONE );

	float4 g_vBaseColor < UiType( Color ); Default4( 0.9, 0.3, 0.1, 1.0 ); >;
	float4 g_vEdgeColor < UiType( Color ); Default4( 4.0, 1.5, 0.2, 1.0 ); >;
	float g_flThreshold < Default( 0.45 ); Range( 0.0, 1.0 ); >;
	float g_flEdgeWidth < Default( 0.08 ); Range( 0.0, 0.5 ); >;
	float g_flNoiseScale < Default( 0.06 ); Range( 0.001, 1.0 ); >;
	float g_flPulse < Default( 0.15 ); Range( 0.0, 1.0 ); >;

	// Cheap hash + value-noise from world position.
	float hash3( float3 p )
	{
		p = frac( p * 0.3183099 + 0.1 );
		p *= 17.0;
		return frac( p.x * p.y * p.z * ( p.x + p.y + p.z ) );
	}

	float noise3( float3 p )
	{
		float3 i = floor( p );
		float3 f = frac( p );
		f = f * f * ( 3.0 - 2.0 * f );

		float n000 = hash3( i + float3(0,0,0) );
		float n100 = hash3( i + float3(1,0,0) );
		float n010 = hash3( i + float3(0,1,0) );
		float n110 = hash3( i + float3(1,1,0) );
		float n001 = hash3( i + float3(0,0,1) );
		float n101 = hash3( i + float3(1,0,1) );
		float n011 = hash3( i + float3(0,1,1) );
		float n111 = hash3( i + float3(1,1,1) );

		float nx00 = lerp( n000, n100, f.x );
		float nx10 = lerp( n010, n110, f.x );
		float nx01 = lerp( n001, n101, f.x );
		float nx11 = lerp( n011, n111, f.x );

		float nxy0 = lerp( nx00, nx10, f.y );
		float nxy1 = lerp( nx01, nx11, f.y );

		return lerp( nxy0, nxy1, f.z );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 p = i.vPositionWithOffsetWs * g_flNoiseScale;
		float n = noise3( p );

		// Animate the threshold so the dissolve breathes.
		float t = g_flThreshold + sin( g_flTime * 1.5 ) * g_flPulse;

		if ( n < t - g_flEdgeWidth )
			discard;

		// Emissive edge inside the burn band.
		float edgeT = saturate( ( n - ( t - g_flEdgeWidth ) ) / max( g_flEdgeWidth, 0.001 ) );
		float3 col = lerp( g_vEdgeColor.rgb, g_vBaseColor.rgb, edgeT );

		return float4( col, 1.0 );
	}
}
