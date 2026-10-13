// A drop-in replacement for the plugin's "ScratchCard/Mask" that also darkens the
// inside of the hole along its upper rim, the way the reference video does.
//
// The reference is a *directional* inner shadow, not an outline: measured off the
// video, the band sits only on the top edge of a scratched patch and is completely
// absent from its sides and bottom. That falls out of the model for free -- the
// shadow is found by sampling the mask straight up, so an edge that runs vertically
// has nothing but hole above it and stays clean.
//
// Everything is read from the same mask render texture the plugin already binds, so
// no second card, no extra draw call, and no plugin edits. ScratchCardManager only
// builds its own material when Card.SurfaceMaterial is empty, so assigning a
// material that uses this shader is enough to take over -- it still gets _MainTex,
// _MaskTex and _Offset filled in by the plugin.
Shader "Loopcase/ScratchCardInnerShadow"
{
    Properties
    {
        // Set by the plugin from the surface sprite. Only its dimensions matter
        // here: the sprite is the scratch canvas, and what the ground looks like
        // comes from _GroundTex instead. See the note on _GroundTiling.
        _MainTex ("Canvas (unused)", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _Offset ("Offset", Vector) = (0, 0, 1, 1)

        [Header(Ground)]
        // Tiled across the card rather than baked into one stretched image, so the
        // ground keeps a constant world scale whatever size arena it is fitted to.
        _GroundTex ("Ground", 2D) = "white" {}

        // Copies of the ground texture across the card, x and y. ArenaScratchCard
        // sets it to arena size over tile size, which is what holds a tile at its
        // drawn scale instead of stretching with the field. Needs the texture's
        // Wrap Mode on Repeat.
        _GroundTiling ("Ground Tiling", Vector) = (1, 1, 0, 0)

        // Multiplies the ground. The tiles provided are all the same earth colour,
        // so on its own the card would uncover ground the same shade as itself and
        // the scratching would be invisible. This is what separates the two layers.
        _GroundColor ("Ground Tint", Color) = (1, 1, 1, 1)

        [Header(Inner Shadow)]
        _ShadowColor ("Color", Color) = (0.20, 0.11, 0.05, 1)

        // Opacity of the band at its darkest.
        _ShadowStrength ("Strength", Range(0, 1)) = 0.9

        // How far the shadow reaches into the hole, in UV. ScratchInnerShadow sets
        // this from a world-unit field so it does not change with arena size.
        _ShadowDistance ("Distance (UV)", Range(0, 0.2)) = 0.012

        // Which way to look for the overhanging lip. (0,1) is up, matching a light
        // from above, which is what the reference uses.
        _ShadowDir ("Direction", Vector) = (0, 1, 0, 0)

        // Low values give the reference's near-solid band with a short tail; 1
        // spreads an even gradient across the whole distance instead.
        _ShadowSoftness ("Softness", Range(0.02, 1)) = 0.35
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // Eight keeps the band's inner edge from stepping at the distances this
            // is used at, and it is a fixed count so the loop unrolls.
            #define SHADOW_TAPS 8

            uniform sampler2D _MainTex;
            uniform sampler2D _MaskTex;
            uniform sampler2D _GroundTex;
            uniform float4 _MainTex_ST;
            uniform float4 _Offset;
            uniform float4 _GroundTiling;
            uniform fixed4 _GroundColor;

            uniform fixed4 _ShadowColor;
            uniform float _ShadowStrength;
            uniform float _ShadowDistance;
            uniform float4 _ShadowDir;
            uniform float _ShadowSoftness;

            struct app2vert
            {
                float4 position: POSITION;
                half4 color: COLOR;
                float2 texcoord: TEXCOORD0;
            };

            struct vert2frag
            {
                float4 position: SV_POSITION;
                half4 color: COLOR;
                float2 texcoord: TEXCOORD0;
            };

            vert2frag vert(app2vert input)
            {
                vert2frag output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                return output;
            }

            float4 frag(vert2frag input) : SV_Target
            {
                // The plugin's convention: 1 means the surface has been scratched
                // away here, 0 means it is still intact.
                float scratched = tex2D(_MaskTex, input.texcoord).r;

                // What fraction of the strip above this pixel is still unscratched.
                // Directly under a lip every tap lands on intact surface and this is
                // 1; deep inside a hole every tap is hole too and it falls to 0.
                //
                // Saturating the tap stops the arena's top row from wrapping around
                // and reading the mask at the bottom.
                float2 tapStep = _ShadowDir.xy * (_ShadowDistance / SHADOW_TAPS);
                float occlusion = 0.0;

                [unroll]
                for (int i = 1; i <= SHADOW_TAPS; i++)
                {
                    occlusion += 1.0 - tex2D(_MaskTex, saturate(input.texcoord + tapStep * i)).r;
                }

                occlusion /= SHADOW_TAPS;

                // Shaped rather than used raw: the raw average ramps linearly, while
                // the reference holds nearly solid across most of the band and then
                // drops off quickly.
                float shadow = smoothstep(0.0, _ShadowSoftness, occlusion) * _ShadowStrength;

                // Tiled off the card's own UV, so a bigger arena repeats the ground
                // further rather than magnifying it. The card is a rectangle laid
                // over the field, so it is opaque everywhere and the canvas sprite
                // is never sampled -- it exists to give the mask its resolution and
                // the brush its aspect, both of which are its pixel dimensions
                // rather than its contents.
                float4 surface = tex2D(_GroundTex, input.texcoord * _GroundTiling.xy);
                surface.rgb *= _GroundColor.rgb;
                surface.a = 1.0;
                surface *= input.color;

                // Composited by hand, because the two halves want different colours
                // through one alpha: the surface wherever it survives, and the shadow
                // in what the scratching opened up underneath it.
                float surfaceAlpha = surface.a * (1.0 - scratched);
                float shadowAlpha = shadow * scratched * (1.0 - surfaceAlpha);

                float outAlpha = surfaceAlpha + shadowAlpha;
                float3 outColor = (surface.rgb * surfaceAlpha + _ShadowColor.rgb * shadowAlpha)
                                  / max(outAlpha, 1e-5);

                return float4(outColor, outAlpha);
            }
            ENDCG
        }
    }

    Fallback "ScratchCard/Mask"
}
