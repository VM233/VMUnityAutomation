Shader "VMUnityAutomation/Tests/Variant Layout"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            CBUFFER_START(LayoutFixture)
            float4 _LayoutOffset;
            float4 _LayoutScale;
            CBUFFER_END
            float4 Vert(float4 position : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(position * _LayoutScale + _LayoutOffset);
            }
            float4 Frag() : SV_Target { return 1; }
            ENDHLSL
        }
    }
}
