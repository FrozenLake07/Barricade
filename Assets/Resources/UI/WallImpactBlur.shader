Shader "Hidden/WallImpactBlur"
{
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _Offset;
   fixed4 frag(v2f_img input):SV_Target
   {
    fixed4 color=tex2D(_MainTex,input.uv)*.227027;
    color+=(tex2D(_MainTex,input.uv+_Offset.xy*1.384615)+tex2D(_MainTex,input.uv-_Offset.xy*1.384615))*.316216;
    color+=(tex2D(_MainTex,input.uv+_Offset.xy*3.230769)+tex2D(_MainTex,input.uv-_Offset.xy*3.230769))*.070270;
    return color;
   }
   ENDCG
  }
 }
}
