Shader "Custom/TargetPreviewGlow"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (1, 0, 0, 1)
        _Alpha ("Alpha", Range(0,1)) = 0.35
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2.0
        _GlowIntensity ("Glow Intensity", Range(0, 8)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _GlowColor;
            float _Alpha;
            float _FresnelPower;
            float _GlowIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);

                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);

                float fresnel = 1.0 - saturate(dot(n, v));
                fresnel = pow(fresnel, _FresnelPower);

                fixed3 glow = _GlowColor.rgb * fresnel * _GlowIntensity;
                fixed alpha = _Alpha * saturate(fresnel + 0.15);

                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }
}