using UnityEngine;

// Follow the existing sprite silhouette; no separate target box or new timing cue.
[DisallowMultipleComponent]
public sealed class TargetMarkerContrast : MonoBehaviour
{
    SpriteRenderer source;
    SpriteRenderer[] outlines;
    Vector3 baseScale;
    MaterialPropertyBlock originalProperties,highlightProperties;
    public bool HighlightVisible=>outlines!=null&&outlines.Length>0&&outlines[0].enabled;
    public Vector3 OriginalScale=>baseScale;
    public void Initialize(SpriteRenderer sprite)
    {
        if(source!=null)return;
        source=sprite;baseScale=transform.localScale;
        originalProperties=new MaterialPropertyBlock();highlightProperties=new MaterialPropertyBlock();
        source.GetPropertyBlock(originalProperties);source.GetPropertyBlock(highlightProperties);
        highlightProperties.SetColor("_Color",new Color(1.6f,1.6f,1.6f,1));
        outlines=new SpriteRenderer[8];int index=0;
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
        {
            if(x==0&&y==0)continue;
            var node=new GameObject("ReticleOutline_"+index);node.transform.SetParent(transform,false);
            var edge=node.AddComponent<SpriteRenderer>();edge.sharedMaterial=source.sharedMaterial;edge.color=new Color(.02f,.02f,.025f,1);edge.enabled=false;
            outlines[index++]=edge;
        }
    }
    public void Apply(bool hasTarget)
    {
        if(source==null)return;
        bool visible=isActiveAndEnabled&&hasTarget&&source.enabled&&source.gameObject.activeInHierarchy;
        transform.localScale=baseScale*(visible?1.25f:1f);
        source.SetPropertyBlock(visible?highlightProperties:originalProperties);
        float pixel=source.sprite!=null?1f/source.sprite.pixelsPerUnit:1f/64f;int index=0;
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
        {
            if(x==0&&y==0)continue;
            var edge=outlines[index++];edge.transform.localPosition=new Vector3(x*pixel,y*pixel,0);
            edge.sprite=source.sprite;edge.flipX=source.flipX;edge.flipY=source.flipY;
            edge.sortingLayerID=source.sortingLayerID;edge.sortingOrder=source.sortingOrder-1;edge.enabled=visible;
        }
    }
    void OnDisable()
    {
        if(source!=null){transform.localScale=baseScale;source.SetPropertyBlock(originalProperties);}
        if(outlines!=null)foreach(var edge in outlines)if(edge!=null)edge.enabled=false;
    }
}
