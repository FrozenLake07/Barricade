using UnityEngine;

// Geometry-only print treatment: deterministic paper grain, cut corners and ink rules.
// All decoration is non-interactive; the original uGUI objects keep their references and controls.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ParisPrintGraphic : UnityEngine.UI.MaskableGraphic
{
    public int kind; // 0 settings, 1 pause sheet, 2 story sheet, 3 instructions, 4 button
    static readonly Color Ink=new Color(.065f,.061f,.053f),Paper=new Color(.13f,.12f,.10f),Line=new Color(.71f,.66f,.54f),Red=new Color(.53f,.18f,.14f);
    void Quad(UnityEngine.UI.VertexHelper vh,Rect r,Color c)
    {
        c.a*=color.a;int n=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero);vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n+2,n+3,n);
    }
    void Rule(UnityEngine.UI.VertexHelper vh,float x,float y,float w,float h,Color c){Quad(vh,new Rect(x,y,w,h),c);}
    void Frame(UnityEngine.UI.VertexHelper vh,Rect r,float unit,float alpha)
    {
        var c=Line;c.a=alpha;float cut=unit*6;
        Rule(vh,r.xMin+cut,r.yMin,r.width-cut*2,unit,c);Rule(vh,r.xMin+cut,r.yMax-unit,r.width-cut*2,unit,c);
        Rule(vh,r.xMin,r.yMin+cut,unit,r.height-cut*2,c);Rule(vh,r.xMax-unit,r.yMin+cut,unit,r.height-cut*2,c);
        foreach(int dx in new[]{0,1})foreach(int dy in new[]{0,1})
        {
            float x=dx==0?r.xMin:r.xMax-cut,y=dy==0?r.yMin:r.yMax-cut;
            Rule(vh,x+unit,y+unit,cut-unit*2,unit,c);Rule(vh,x+unit,y+unit,unit,cut-unit*2,c);
        }
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();var r=GetPixelAdjustedRect();if(r.width<1||r.height<1)return;
        float unit=Mathf.Max(1,r.width/(kind==4?440f:1920f));
        if(kind==4)
        {
            Quad(vh,r,new Color(.20f,.18f,.14f,.26f));Frame(vh,new Rect(r.x+unit,r.y+unit,r.width-2*unit,r.height-2*unit),unit,.34f);return;
        }
        Rect sheet=kind==2?new Rect(r.x+r.width*.15f,r.y+r.height*.12f,r.width*.70f,r.height*.76f)
            :kind==1?new Rect(r.x+unit*2,r.y+unit*2,r.width-unit*4,r.height-unit*4)
            :new Rect(r.x+r.width*.035f,r.y+r.height*.018f,r.width*.93f,r.height*.964f);
        Rect grain=kind==1||kind==2?sheet:r;
        Quad(vh,grain,Ink);
        // Grain stays inside the paper; no opaque rectangular rim extends past the border.
        int columns=kind==1?96:128,rows=kind==1?54:72;
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {
            uint seed=(uint)(x*374761393u+y*668265263u+19u);seed=(seed^(seed>>13))*1274126177u;
            if(seed%5!=0)continue;
            float a=.025f+(seed%7)*.003f;
            float px=grain.x+(x+(seed%7)/8f)*grain.width/columns;
            float py=grain.y+(y+((seed>>8)%7)/8f)*grain.height/rows;
            Quad(vh,new Rect(px,py,unit*(1+seed%3),unit),new Color(.62f,.57f,.46f,a));
        }
        Quad(vh,sheet,new Color(Paper.r,Paper.g,Paper.b,.48f));
        Frame(vh,sheet,unit,.36f);
        Rect inset=new Rect(sheet.x+unit*7,sheet.y+unit*7,sheet.width-unit*14,sheet.height-unit*14);
        Frame(vh,inset,unit,.10f);
        float header=kind==0?.875f:kind==1?.82f:kind==3?.91f:.76f;
        float yy=r.y+r.height*header;
        float left=sheet.x+sheet.width*.05f,right=sheet.xMax-sheet.width*.05f;
        Rule(vh,left,yy,right-left,unit,new Color(Line.r,Line.g,Line.b,.32f));
        Rule(vh,left,yy,Mathf.Min(sheet.width*.13f,125*unit),unit*2,Red);
        // Two printer's registration strokes form a restrained emblem at the lower edge.
        float cy=sheet.y+unit*12,cx=sheet.center.x;
        Rule(vh,cx-unit*30,cy,unit*22,unit,new Color(Line.r,Line.g,Line.b,.30f));
        Rule(vh,cx+unit*8,cy,unit*22,unit,new Color(Line.r,Line.g,Line.b,.30f));
        Rule(vh,cx-unit*2,cy-unit,unit*4,unit*3,Red);
    }
    public static ParisPrintGraphic Attach(UnityEngine.UI.Image surface,int kind)
    {
        var found=surface.transform.Find("PrintTreatment");ParisPrintGraphic graphic;
        if(found==null)
        {
            var go=new GameObject("PrintTreatment",typeof(RectTransform),typeof(CanvasRenderer),typeof(ParisPrintGraphic));go.transform.SetParent(surface.transform,false);go.transform.SetAsFirstSibling();
            graphic=go.GetComponent<ParisPrintGraphic>();ParisUI.Place((RectTransform)go.transform,0,0,1,1);
        }
        else graphic=found.GetComponent<ParisPrintGraphic>();
        if(kind!=4)foreach(Transform child in surface.transform)
            if(child.name.EndsWith("Rule",System.StringComparison.Ordinal))child.gameObject.SetActive(false);
        graphic.kind=kind;graphic.color=Color.white;graphic.raycastTarget=false;graphic.SetVerticesDirty();
        if(kind!=4)
        {
            float x0=kind==2?.60f:.68f,x1=kind==2?.80f:.92f,y0=kind==2?.80f:.93f,y1=kind==2?.85f:.97f;
            var meta=ParisUI.Label(graphic.transform,"Imprint","PARIS  /  1832",22,ParisUI.Style.muted,x0,y0,x1,y1);meta.alignment=TextAnchor.MiddleRight;meta.raycastTarget=false;
        }
        if(kind==4)surface.GetComponent<ParisMenuAccent>()?.RefreshStyle();
        return graphic;
    }
}
