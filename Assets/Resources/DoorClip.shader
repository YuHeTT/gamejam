// 门裁剪着色器：把精灵中"高于 _ClipLineY（世界 Y）"的像素直接丢弃。
// 用途：门向上开启时，把越过门洞顶线的那部分从视觉上裁掉，落下来时自动露出。
//
// 为什么不用 SpriteMask：本工程所有精灵都在 Default 排序层且 order 0，
// SpriteMask 的生效依赖自定义排序区间与绘制顺序，容易整片失效；
// 而且它裁的是"区域内所有精灵"，会误伤天花板以上的其他物体。
// 用着色器按世界 Y 裁剪，作用对象精确等于"使用了这个材质的渲染器"，只裁门自己。
//
// 注意：裁剪值走 MaterialPropertyBlock（_ClipLineY），不需要为每扇门建独立材质。
Shader "Custom/DoorClip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ClipLineY ("Clip Line Y (world)", Float) = 100000
        _ClipFade ("Clip Fade", Range(0.0001, 1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float  worldY   : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _ClipLineY;
            float _ClipFade;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldY = mul(unity_ObjectToWorld, IN.vertex).y;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 高于裁剪线的部分丢弃；_ClipFade 给边缘一个很窄的软过渡，避免锯齿
                float d = IN.worldY - _ClipLineY;
                clip(_ClipFade - d);

                // 与 Sprites/Default 一致的预乘处理
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
