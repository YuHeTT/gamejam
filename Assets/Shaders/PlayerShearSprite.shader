Shader "Custom/PlayerShearSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Shear ("Shear", Range(0, 1)) = 0
        _Flatten ("Flatten", Range(0, 1)) = 0
        _Reveal ("Reveal", Range(0, 1)) = 1
        _ShearMaxOffset ("Shear Max Offset", Float) = 1
        _PivotBottomY ("Pivot Bottom Y (object space)", Float) = 0
        _SpriteBottomY ("Sprite Bottom Y", Float) = 0
        _SpriteTopY ("Sprite Top Y", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
                float localY : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Shear;
            float _Flatten;
            float _Reveal;
            float _ShearMaxOffset;
            float _PivotBottomY;
            float _SpriteBottomY;
            float _SpriteTopY;

            v2f vert (appdata v)
            {
                float3 pos = v.vertex.xyz;
                float yRel = pos.y - _PivotBottomY;
                float flatten = saturate(_Flatten);
                float shear = saturate(_Shear);

                pos.x += yRel * shear * _ShearMaxOffset;
                pos.y = _PivotBottomY + yRel * (1.0 - flatten);

                v2f o;
                o.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.localY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float span = max(_SpriteTopY - _SpriteBottomY, 0.0001);
                float y01 = saturate((i.localY - _SpriteBottomY) / span);
                float threshold = 1.0 - saturate(_Reveal);
                clip(y01 - threshold - 0.0001);

                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
