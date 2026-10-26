Shader "Sprites/EnemyWallImpactBlur"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite Texture",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
  [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
  _BlurPixels("Local blur",Float)=0
  _SpriteUVRect("Sprite UV bounds",Vector)=(0,0,1,1)
  [PerRendererData] _AlphaTex("External Alpha",2D)="white" {}
  [PerRendererData] _EnableExternalAlpha("Enable External Alpha",Float)=0
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex SpriteVert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   float4 _MainTex_TexelSize,_SpriteUVRect;float _BlurPixels;
   fixed4 sampleSprite(float2 uv)
   {
    uv=clamp(uv,_SpriteUVRect.xy+_MainTex_TexelSize.xy*.5,_SpriteUVRect.zw-_MainTex_TexelSize.xy*.5);
    fixed4 c=SampleSpriteTexture(uv);
    c.rgb*=c.a;return c;
   }
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 sum=0;float weight=0;float2 stepUV=_MainTex_TexelSize.xy*_BlurPixels;
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
    {float w=(2-abs(x))*(2-abs(y));sum+=sampleSprite(i.texcoord+float2(x,y)*stepUV)*w;weight+=w;}
    fixed4 c=sum/weight;c.rgb*=i.color.rgb*i.color.a;c.a*=i.color.a;return c;
   }
   ENDCG
  }
 }
}
