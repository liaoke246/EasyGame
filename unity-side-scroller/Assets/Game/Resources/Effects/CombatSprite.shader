Shader "EasyGame/Combat Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _SeparateSlash ("Separate authored white slash", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CombatSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;
            float _SeparateSlash;
            fixed4 CombatSpriteFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord);
                // These uncompressed, unatlased 80x64 source frames bake a
                // pure-white VFX wedge/tail outside the body into frames 4/5.
                // Keep the body/weapon pixels (including the warrior's two
                // white grip pixels at x=36), remove only that authored wedge.
                float2 pixel = input.texcoord * _MainTex_TexelSize.zw;
                // Its lavender border is baked into the same strip; removing
                // only white leaves a dotted ghost arc during recovery.
                float3 border = float3(174.0, 161.0, 188.0) / 255.0;
                #ifndef UNITY_COLORSPACE_GAMMA
                    border = GammaToLinearSpace(border);
                #endif
                bool white = color.r > .999 && color.g > .999 && color.b > .999;
                bool outline = distance(color.rgb, border) < .008;
                if (_SeparateSlash > .5 && (pixel.x < 30 || pixel.y > 46) && (white || outline))
                    color.a = 0;
                color *= input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
