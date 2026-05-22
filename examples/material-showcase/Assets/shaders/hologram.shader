// Material Showcase: hologram-ish shader.
// Semi-transparent, with scrolling horizontal scan lines and Fresnel rim.
// Authored against s&box's standard shader scaffold from templates/unlit.shader.

HEADER
{
	Description = "Hologram with scrolling scan lines";
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

	RenderState( DepthWriteEnable, false );
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );

	float4 g_vHoloColor < UiType( Color ); Default4( 0.2, 0.8, 1.0, 1.0 ); >;
	float g_flScanSpeed < Default( 2.0 ); Range( 0.0, 20.0 ); >;
	float g_flScanDensity < Default( 80.0 ); Range( 1.0, 400.0 ); >;
	float g_flRimPower < Default( 2.0 ); Range( 0.1, 8.0 ); >;
	float g_flBaseAlpha < Default( 0.45 ); Range( 0.0, 1.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 vNormalWs = normalize( i.vNormalWs );
		float3 vToCamera = normalize( g_vCameraPositionWs - i.vPositionWithOffsetWs );

		// Fresnel rim — brighter at grazing angles.
		float ndotv = saturate( dot( vNormalWs, vToCamera ) );
		float rim = pow( 1.0 - ndotv, g_flRimPower );

		// Scrolling horizontal scan-line pattern keyed off world Z.
		float scan = 0.5 + 0.5 * sin( i.vPositionWithOffsetWs.z * g_flScanDensity * 0.01 - g_flTime * g_flScanSpeed );
		scan = pow( scan, 1.5 );

		float3 col = g_vHoloColor.rgb * ( 0.3 + 0.7 * scan ) + rim * g_vHoloColor.rgb * 1.5;
		float alpha = saturate( g_flBaseAlpha + rim * 0.6 ) * g_vHoloColor.a;

		return float4( col, alpha );
	}
}
