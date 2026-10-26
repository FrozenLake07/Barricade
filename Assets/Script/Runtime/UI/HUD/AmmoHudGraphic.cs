using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class AmmoHudGraphic : UnityEngine.UI.Image
{
    public Color tint=Color.white;
    readonly UnityEngine.UI.Image[] balls=new UnityEngine.UI.Image[6];
    UnityEngine.UI.Image stitch;
    public void SetState(AmmoHudState state)
    {
        ParisUI.Paint(this,null);color=Color.clear;
        for(int i=0;i<balls.Length;i++)
        {
            if(balls[i]==null){balls[i]=ParisUI.Panel(transform,"LeadBall"+(i+1),ParisUI.Style.leadBall,.30f+i*.085f,.51f,.365f+i*.085f,.715f);balls[i].type=Type.Simple;balls[i].preserveAspect=true;}
            balls[i].gameObject.SetActive(i<state.capacity);
            bool full=state.unlimited||i<state.ammo;
            balls[i].color=full?Color.white:new Color(.22f,.20f,.18f,.65f);
        }
        if(stitch==null){stitch=ParisUI.Panel(transform,"ReloadStitch",null,.30f,.46f,.62f,.475f);stitch.type=Type.Simple;}
        stitch.color=state.perfectFlash?ParisUI.Style.cream:ParisUI.Style.muted;
        stitch.rectTransform.anchorMax=new Vector2(.30f+.32f*(state.reloading?state.progress:1),.475f);
        stitch.gameObject.SetActive(state.reloading||state.perfectFlash);
    }
}
