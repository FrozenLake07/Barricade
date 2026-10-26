Shader "UI/TitleFrenchTricolor"
{
 Properties
 {
  [PerRendererData] _MainTex("Title Artwork",2D)="white" {}
  _Blue("Bleu France",Color)=(0,0,0.56862745,1)
  _White("White",Color)=(1,1,1,1)
  _Red("Rouge Marianne",Color)=(0.88235294,0,0.05882353,1)
  _OutlinePixels("Fine outline width",Range(0,8))=3
  _ReliefPixels("Relief depth",Range(0,12))=5
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float4 local:TEXCOORD1;};
   sampler2D _MainTex;fixed4 _Blue,_White,_Red,_TextureSampleAdd;float4 _ClipRect,_MainTex_TexelSize;float _OutlinePixels,_ReliefPixels;
   v2f vert(appdata v){v2f o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
    // Recolor the existing warm letter faces, retaining their exact alpha and light relief edges.
    float face=step(.035,c.g-c.b)*step(.035,c.r-c.b)*step(.25,c.g);
    // Follow the interlocking glyph tips rather than cutting letters with vertical stripes.
    float2 pixel=float2(i.uv.x*2172,(1-i.uv.y)*724);
    bool first=pixel.x<503||(pixel.x<515&&(pixel.y<271||(pixel.y>420&&pixel.y<460)));
    bool last=pixel.x>=1432||(pixel.x>=1416&&pixel.y>400&&pixel.y<480);
    fixed3 tint=first?_Blue.rgb:last?_Red.rgb:_White.rgb;
    float neutral=dot(c.rgb,float3(.299,.587,.114));
    c.rgb=lerp(neutral.xxx,tint,face);
    // Alpha silhouette keeps the existing lettering; light upper-left rim and dark lower-right relief add depth.
    float2 edge=_MainTex_TexelSize.xy*_OutlinePixels;
    float rim=0;
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
     rim=max(rim,tex2D(_MainTex,i.uv+float2(x,y)*edge).a);
    float highlight=tex2D(_MainTex,i.uv+float2(edge.x,-edge.y)).a;
    float shadow=tex2D(_MainTex,i.uv+float2(-1,1)*_MainTex_TexelSize.xy*_ReliefPixels).a;
    float behind=max(rim*.95,shadow*.72);
    fixed3 relief=lerp(fixed3(.025,.035,.055),fixed3(.80,.82,.85),saturate(highlight-rim*.15));
    float alpha=c.a+behind*(1-c.a);
    c.rgb=(c.rgb*c.a+relief*behind*(1-c.a))/max(alpha,.0001);
    c.a=alpha;
    c*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
     c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
     clip(c.a-.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
