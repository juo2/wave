Shader "WaveMaker/WaveMakerWaveShader"
{
	Properties
	{
		_MaxColor("Max Color", Color) = (1,1,1,1)
		_MinColor("Min Color", Color) = (0,0,0,1)
		_Size("Size Around 0", float) = 0.30
		_Offset("Offset From 0", float) = -0.15
	}
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

			//#include "UnityCG.cginc"

			float4 _MinColor;
			float4 _MaxColor;
			float _Offset;
			float _Size;

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float4 color : COLOR;
            };

            v2f vert (float4 pos : POSITION)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(pos);

				// Minimum is 0
				_Size = max(_Size, 0);

				// Bring height in object space -size to size, clamp to it
				float clampedNormalizedHeight = clamp(pos.y + _Offset + _Size, -_Size, _Size) / (_Size * 2);

				// Interpolate min and max colors by that normalization
				o.color = lerp(_MinColor, _MaxColor, clampedNormalizedHeight);
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
