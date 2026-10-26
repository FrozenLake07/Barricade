using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Sprite-only local fade for a body reaching the barricade, including returning Gavroche.
public sealed class EnemyWallImpactVisual : MonoBehaviour
{
    public const float Duration=.3f;
    readonly List<SpriteRenderer> renderers=new List<SpriteRenderer>();
    readonly List<Sprite> sprites=new List<Sprite>();
    readonly List<Material> materials=new List<Material>();
    readonly List<Color> colors=new List<Color>();
    float elapsed;
    public int RendererCount=>renderers.Count;
    public float Progress=>Mathf.Clamp01(elapsed/Duration);
    public static EnemyWallImpactVisual Show(GameObject enemy)
    {
        if(!Application.isPlaying)return null;
        var shader=Resources.Load<Shader>("UI/EnemyWallImpactBlur");
        var root=new GameObject("CharacterArrivalFade");root.transform.position=enemy.transform.position;
        var visual=root.AddComponent<EnemyWallImpactVisual>();var group=enemy.GetComponent<SortingGroup>();
        if(group!=null){var sorting=root.AddComponent<SortingGroup>();sorting.sortingLayerID=group.sortingLayerID;sorting.sortingOrder=group.sortingOrder;}
        foreach(var source in enemy.GetComponentsInChildren<SpriteRenderer>())
        {
            if(!source.enabled||source.sprite==null||source.color.a<=0)continue;
            var child=new GameObject(source.name+"_ImpactVisual");child.transform.SetParent(root.transform,false);child.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);child.transform.localScale=source.transform.lossyScale;child.layer=source.gameObject.layer;
            var copy=child.AddComponent<SpriteRenderer>();
            // The original grenadier rig destroys its runtime sprites on exit; own an independent copy.
            var sprite=Instantiate(source.sprite);visual.sprites.Add(sprite);copy.sprite=sprite;
            copy.flipX=source.flipX;copy.flipY=source.flipY;copy.color=source.color;copy.sortingLayerID=source.sortingLayerID;copy.sortingOrder=source.sortingOrder;copy.spriteSortPoint=source.spriteSortPoint;
            if(shader!=null&&shader.isSupported)
            {
                var material=new Material(shader);var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);material.SetVector("_SpriteUVRect",uv);copy.sharedMaterial=material;visual.materials.Add(material);
            }
            visual.renderers.Add(copy);visual.colors.Add(source.color);source.enabled=false;
        }
        if(visual.renderers.Count==0){Destroy(root);return null;}return visual;
    }
    public void Sample(float progress)
    {
        float t=Mathf.Clamp01(progress),alpha=1-t*t;
        for(int i=0;i<renderers.Count;i++)if(renderers[i]!=null){var color=colors[i];color.a*=alpha;renderers[i].color=color;}
        foreach(var material in materials)material.SetFloat("_BlurPixels",t*1.8f);
    }
    void Update()
    {
        if(CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
        elapsed+=Time.deltaTime;Sample(Progress);if(elapsed>=Duration)Destroy(gameObject);
    }
    void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);foreach(var sprite in sprites)if(sprite!=null)Destroy(sprite);}
}
