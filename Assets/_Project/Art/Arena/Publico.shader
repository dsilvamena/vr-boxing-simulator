// Público de las gradas: todas las siluetas van en una sola malla (una draw call), sin iluminación (el color sale del
// vértice, ya atenuado según la distancia al ring) y recortadas por alfa. El vértice las anima:
// - en calma se mecen un poco, cada una a su ritmo (la fase va en el alfa del color del vértice);
// - cuando el público se anima (_PublicoAnimo, global, lo pone AmbienteMultitud en una ovación) saltan;
// - los destellos de cámara son cuadraditos que solo se abren un instante de vez en cuando: su vértice guarda el centro
//   y la normal el desplazamiento de cada esquina, así que cerrados no ocupan ni un píxel.
// Lleva las macros de estéreo: en el Quest los dos ojos se dibujan en una sola pasada (multiview).
Shader "Boxeo/Publico"
{
    Properties
    {
        _MainTex ("Siluetas (alfa = recorte)", 2D) = "white" {}
        _Cutoff ("Recorte", Range(0, 1)) = 0.5
        _Brillo ("Brillo", Range(0, 3)) = 1
        _Vaiven ("Vaivén en calma (m)", Float) = 0.02
        _Salto ("Salto con el público animado (m)", Float) = 0.14
        _Destellos ("Frecuencia de los destellos", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;     // destellos: desplazamiento de la esquina desde el centro; siluetas: cero
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;       // rgb = tinte, a = fase
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed3 color : COLOR;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;
            half _Brillo, _Vaiven, _Salto, _Destellos;
            half _PublicoAnimo;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float fase = v.color.a;
                float esDestello = step(0.0001, dot(v.normal, v.normal));

                // Siluetas: se estiran un poco al mecerse (la base no se mueve) y saltan enteras con la ovación
                float calma = sin(_Time.y * (0.9 + fase * 1.3) + fase * 6.2832) * _Vaiven * v.uv.y;
                float salto = abs(sin(_Time.y * (4.5 + fase * 3.0) + fase * 6.2832)) * _Salto * _PublicoAnimo;
                v.vertex.y += (calma + salto) * (1.0 - esDestello);

                // Destellos: cada uno prueba suerte unas 7 veces por segundo; con el público animado hay muchos más
                float tramo = floor(_Time.y * 7.0 + fase * 13.0);
                float azar = frac(sin(tramo * 12.9898 + fase * 78.233) * 43758.5453);
                float abierto = step(1.0 - (0.012 + 0.12 * _PublicoAnimo) * _Destellos, azar);
                v.vertex.xyz += v.normal * abierto;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color.rgb * _Brillo;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);
                c.rgb *= i.color;
                UNITY_APPLY_FOG(i.fogCoord, c);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
