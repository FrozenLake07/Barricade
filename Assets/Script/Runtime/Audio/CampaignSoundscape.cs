using UnityEngine;
public sealed class CampaignSoundscape : MonoBehaviour
{
    StoryDirector owner;SelectedSoundLoop wind,fire;
    int chapter=-1;bool resting;int cinematicChapter;string cinematicImage="";
    void Awake(){owner=GetComponent<StoryDirector>();wind=Child("Night wind");fire=Child("Visible fire");}
    SelectedSoundLoop Child(string name){var go=new GameObject(name);go.transform.SetParent(transform,false);return go.AddComponent<SelectedSoundLoop>();}
    public void Picture(int number,string image){cinematicChapter=number;cinematicImage=image??"";}
    public void HidePicture(){cinematicChapter=0;cinematicImage="";}
    void LateUpdate()
    {
        if(owner==null||CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
        if(chapter!=RunState.Chapter){SfxPlayer.StopAll();chapter=RunState.Chapter;}
        wind.Set("ambience_wind_loop",StoryDirector.GameplayRunning&&chapter==6,.35f);
        var film=owner.GetComponent<CampaignCinematicView>();
        bool picture=chapter==6&&cinematicChapter==6&&film!=null&&film.Active&&!film.IsBlackCardVisible;
        bool visible=picture&&(cinematicImage.StartsWith("CH06_")||cinematicImage.Contains("Fire")||cinematicImage.Contains("Torch")||cinematicImage.Contains("火"));
        fire.Set("ambience_fire_detail_loop",visible,.12f);
        bool now=owner.IsConstructionResting;if(now&&!resting)SfxPlayer.Play("action_rest_breath",.5f);resting=now;
    }
    public static void WallDamage(int before,int after,int maximum)
    {
        if(before<=0||after>=before||maximum<=0)return;
        if(after<=0)SfxPlayer.Play("structure_collapse",.65f);
        else if(before>maximum/3f&&after<=maximum/3f)SfxPlayer.Play("structure_crack_heavy",.55f);
        else if(before>maximum*2f/3f&&after<=maximum*2f/3f)SfxPlayer.Play("structure_crack_light",.5f);
    }
    public static void BulletImpact(Collider2D surface,Vector2 hit)
    {
        // Current wall is one mixed-material image: low strikes hit its foundation.
        bool stone=surface!=null&&hit.y<=surface.bounds.min.y+surface.bounds.size.y*.28f;
        SfxPlayer.Play(stone?"structure_bullet_hit_stone":"structure_bullet_hit_wood",.4f);
    }
}
