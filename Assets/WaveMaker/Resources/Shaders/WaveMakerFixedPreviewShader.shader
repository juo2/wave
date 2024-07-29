Shader "WaveMaker/WaveMakerFixedPreviewShader"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			Offset 0, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct vertice
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR0;
            };
            
            // vertex to fragment information
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };
            
            // Vertex shader
            v2f vert (vertice v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            } 
            
            // Fragment Shader 
            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG 
        }
    }
}
