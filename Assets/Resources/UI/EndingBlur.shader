Shader "UI/CampaignEndingBlur"
{
    Properties { [PerRendererData] _MainTex("Picture",2D)="white" {} _BlurPixels("Blur",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
            sampler2D _MainTex;float4 _MainTex_TexelSize;float _BlurPixels;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 stepUV=_MainTex_TexelSize.xy*_BlurPixels*.5;
                fixed4 sum=0;float weight=0;
                [unroll] for(int y=-2;y<=2;y++) [unroll] for(int x=-2;x<=2;x++)
                {float w=(3-abs(x))*(3-abs(y));sum+=tex2D(_MainTex,saturate(i.uv+float2(x,y)*stepUV))*w;weight+=w;}
                return sum/weight*i.color;
            }
            ENDCG
        }
    }
}
