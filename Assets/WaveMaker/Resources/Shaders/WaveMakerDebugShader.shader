Shader "WaveMaker/WaveMakerDebugShader"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float4 color : COLOR;
            };

            v2f vert (float4 pos : POSITION, float4 color : COLOR)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(pos);
				o.color = color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
