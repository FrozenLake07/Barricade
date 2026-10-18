using UnityEngine;

public class TapSec : MonoBehaviour
{
    public SBullet sbullet;
    public static float beattime = 0.85f;
    public static float oribeattime = 0.85f;
    public static float Setbeattime = 0.85f;
    public static float perfecttime = 0.5f;
    public static float Setperfecttime = 0.5f;
    public static int cnt;
    public static string ShootStatu = "Down";
    int seenMusicVersion = -1;

    void Awake()
    {
        Setbeattime = oribeattime;
        beattime = Setbeattime;
        perfecttime = Setperfecttime;
        cnt = 0;
        ShootStatu = "Down";
        if (sbullet == null)
            Debug.LogWarning("[TapSec] 未指定 sbullet，场景 2 无法开枪。");
    }

    void Update()
    {
        // 选目标与换轨都是准备动作，不推进节拍；同帧切换后按 Space 也会禁射。
        if (Input.GetKeyDown(KeyCode.W)) SetAimMode("Up");
        else if (Input.GetKeyDown(KeyCode.S)) SetAimMode("Down");

        if (RhythmManager.TryGetMusicFrame(out MusicBeatFrame music))
        {
            Setbeattime = music.Interval;
            beattime = music.BeatTime;
            perfecttime = music.PerfectTime;
            if (!music.Started || !Input.GetKeyDown(KeyCode.Space)) return;
            if (!CanShoot()) return;
            if (music.IsPerfect && !RhythmManager.TryClaimPerfect(music.BeatIndex)) return;
            cnt++;
            sbullet.Fire(ShootStatu, music.IsPerfect);
            return;
        }

        bool handedOff = RhythmManager.ApplyHandoff(gameObject.scene.name, ref seenMusicVersion,
            ref beattime, ref Setbeattime, ref perfecttime, Setperfecttime);
        if (!handedOff) AdvanceBeat();
        if (!Input.GetKeyDown(KeyCode.Space) || !CanShoot()) return;

        bool perfect = beattime <= 0f && perfecttime > 0f;
        Setbeattime = perfect ? Mathf.Max(0.5f, Setbeattime - 0.05f) : oribeattime;
        cnt++;
        perfecttime = Setperfecttime;
        beattime = Setbeattime;
        sbullet.Fire(ShootStatu, perfect);
    }

    void SetAimMode(string mode)
    {
        if (ShootStatu == mode) return;
        ShootStatu = mode;
        CapsuleTrack.BeginAimAdjustment();
        SfxPlayer.Play("SFX-PLY-05", 0.35f);
    }

    // 打不出去时给一声空响，避免玩家以为掉帧。
    bool CanShoot()
    {
        if (sbullet != null && sbullet.CanFire) return true;
        SfxPlayer.Play("SFX-PLY-06", 0.3f);
        return false;
    }

    void AdvanceBeat()
    {
        // 禁射和换弹时心跳仍自然流逝，按 Space 不另扣拍、不重置速度或计数。
        if (beattime > 0f)
        {
            beattime = Mathf.Max(0f, beattime - Time.deltaTime);
        }
        else
        {
            perfecttime = Mathf.Max(0f, perfecttime - Time.deltaTime);
            if (perfecttime <= 0f)
            {
                Setbeattime = oribeattime;
                perfecttime = Setperfecttime;
                beattime = Setbeattime;
            }
        }
    }
}
