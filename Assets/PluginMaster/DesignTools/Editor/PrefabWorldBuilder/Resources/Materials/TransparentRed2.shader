Shader "PluginMaster/TransparentRed2"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2g
            {
                float4 clipPos  : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
            };

            struct g2f
            {
                float4 clipPos : SV_POSITION;
            };

            v2g vert (appdata v)
            {
                v2g o;
                o.localPos = v.vertex.xyz;

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos      = worldPos.xyz;
                o.clipPos       = UnityObjectToClipPos(v.vertex);

                return o;
            }


            inline bool IsAxisAligned(float3 dir, float tolerance)
            {
                float3 ndir = normalize(dir);

                float ax = abs(ndir.x);
                float ay = abs(ndir.y);
                float az = abs(ndir.z);

                if (ax > (1.0 - tolerance) && ay < tolerance && az < tolerance)
                    return true;
                if (ay > (1.0 - tolerance) && ax < tolerance && az < tolerance)
                    return true;
                if (az > (1.0 - tolerance) && ax < tolerance && ay < tolerance)
                    return true;

                return false;
            }

            inline void DrawEdge(float3 pA, float3 pB, inout LineStream<g2f> lineStream)
            {
                g2f vA, vB;
                vA.clipPos = UnityWorldToClipPos(pA);
                vB.clipPos = UnityWorldToClipPos(pB);

                lineStream.Append(vA);
                lineStream.Append(vB);
                lineStream.RestartStrip();
            }

            [maxvertexcount(6)]
            void geom(triangle v2g input[3], inout LineStream<g2f> lineStream)
            {
                float3 w0 = input[0].worldPos;
                float3 w1 = input[1].worldPos;
                float3 w2 = input[2].worldPos;

                float3 l0 = input[0].localPos;
                float3 l1 = input[1].localPos;
                float3 l2 = input[2].localPos;

                float3 edge01_local = l1 - l0;
                if (IsAxisAligned(edge01_local,0.01))
                    DrawEdge(w0, w1, lineStream);

                float3 edge12_local = l2 - l1;
                if (IsAxisAligned(edge12_local, 0.01))
                    DrawEdge(w1, w2, lineStream);

                float3 edge20_local = l0 - l2;
                if (IsAxisAligned(edge20_local, 0.01))
                    DrawEdge(w2, w0, lineStream);
            }

            float4 frag(g2f IN) : SV_Target
            {
               return float4(1,0,0,0.5);
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
