using UnityEngine;

// Quiet untextured scrims. Geometry follows the RectTransform; no generated bitmap or material required.
public sealed class ParisBackdropGraphic : UnityEngine.UI.Image
{
    [Tooltip("0 左侧暗场，1 中央暗场，2 顶部渐隐，3 底部渐隐，4 纯色")]
    public int fadeMode=4;
    public static float Opacity(int mode,float t)
    {
        t=Mathf.Clamp01(t);
        if(mode==0)return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1,t));
        if(mode==1)return Mathf.SmoothStep(0,1,Mathf.Min(t,1-t)*4);
        if(mode==2)return Mathf.SmoothStep(0,1,t);
        if(mode==3)return 1-Mathf.SmoothStep(0,1,t);
        return 1;
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();var rect=GetPixelAdjustedRect();bool vertical=fadeMode==2||fadeMode==3;
        const int count=32;
        for(int i=0;i<count;i++)
        {
            float t0=i/(float)count,t1=(i+1)/(float)count;
            float x0=vertical?rect.xMin:Mathf.Lerp(rect.xMin,rect.xMax,t0),x1=vertical?rect.xMax:Mathf.Lerp(rect.xMin,rect.xMax,t1);
            float y0=vertical?Mathf.Lerp(rect.yMin,rect.yMax,t0):rect.yMin,y1=vertical?Mathf.Lerp(rect.yMin,rect.yMax,t1):rect.yMax;
            var c0=color;c0.a*=Opacity(fadeMode,t0);var c1=color;c1.a*=Opacity(fadeMode,t1);int first=vh.currentVertCount;
            vh.AddVert(new Vector3(x0,y0),c0,Vector2.zero);vh.AddVert(new Vector3(x0,y1),vertical?c1:c0,Vector2.zero);
            vh.AddVert(new Vector3(x1,y1),c1,Vector2.zero);vh.AddVert(new Vector3(x1,y0),vertical?c0:c1,Vector2.zero);
            vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first+2,first+3,first);
        }
    }
}
