using UnityEngine;

public static class HeartHealthPresentation
{
    // These shipped 16x16 icons have opaque rows 2..13; uGUI reports zero importer padding.
    // Map health to the visible shape so the first 12.5% loss is not spent clipping transparent rows.
    public static float Fill(Sprite sprite,float ratio)
    {
        ratio=Mathf.Clamp01(ratio);
        if(ratio<=0f)return 0f;if(ratio>=1f)return 1f;
        if(sprite!=null&&sprite.name=="Heart-Classic-v4"&&sprite.rect.width==16&&sprite.rect.height==16)
            return .125f+.75f*ratio;
        return ratio;
    }
}
