using System;
using UnityEngine;

public enum GameAction { BuildA, BuildD, BuildW, BuildS, LaneDown, LaneUp, AimUp, AimDown, Fire, Reload }
public enum GameDifficulty { Easy, Normal, Hard, Nightmare }

// Local preferences apply to new and resumed games; difficulty never changes damage.
public static class GameOptions
{
    const string DifficultyKey="Barricade.Difficulty";
    public static GameDifficulty Difficulty
    {
        get=>(GameDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey,1),0,3);
        set{PlayerPrefs.SetInt(DifficultyKey,Mathf.Clamp((int)value,0,3));PlayerPrefs.Save();CampaignTelemetry.Current?.Record("difficulty",value.ToString());}
    }
    public static bool PromptGraceEnabled=>Difficulty!=GameDifficulty.Nightmare;
    public static int DensityPercent(int chapter)=>GameDifficultyRules.DensityPercent(Difficulty,chapter);
}
public static class GameBindings
{
    public static readonly KeyCode[] Defaults={KeyCode.A,KeyCode.D,KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D,KeyCode.W,KeyCode.S,KeyCode.Space,KeyCode.R};
    public static readonly string[] Names={"建造按键 1","建造按键 2","建造按键 3","建造按键 4","向下换轨","向上换轨","瞄准空中","瞄准地面","开火","补弹"};
    static string Pref(GameAction action)=>"Barricade.Key."+action;
    public static KeyCode Key(GameAction action)
    {
        int stored=PlayerPrefs.GetInt(Pref(action),(int)Defaults[(int)action]);
        return Enum.IsDefined(typeof(KeyCode),stored)&&Allowed((KeyCode)stored)?(KeyCode)stored:Defaults[(int)action];
    }
    public static bool Down(GameAction action)=>!GameSettingsView.IsOpen&&Input.GetKeyDown(Key(action));
    public static string Label(GameAction action)=>Label(Key(action));
    public static string Label(KeyCode key)=>key==KeyCode.Space?"空格":key==KeyCode.LeftArrow?"←":key==KeyCode.RightArrow?"→":key==KeyCode.UpArrow?"↑":key==KeyCode.DownArrow?"↓":key.ToString();
    public static string CombatHint=>$"{Label(GameAction.LaneDown)}/{Label(GameAction.LaneUp)} 换轨   {Label(GameAction.AimUp)} 空中 / {Label(GameAction.AimDown)} 地面   {Label(GameAction.Fire)} 开火   {Label(GameAction.Reload)} 补弹";
    public static bool Allowed(KeyCode key)=>key!=KeyCode.None&&(int)key<(int)KeyCode.Mouse0&&key!=KeyCode.Escape&&key!=KeyCode.Return&&key!=KeyCode.KeypadEnter&&key!=KeyCode.BackQuote&&!(key>=KeyCode.F1&&key<=KeyCode.F15);
    public static bool TrySet(GameAction action,KeyCode key,out string reason)
    {
        if(!Allowed(key)){reason="此按键用于菜单或调试，请选择其他键。";return false;}
        for(int i=0;i<Defaults.Length;i++)
        {
            if(i==(int)action||(i<4)!=((int)action<4))continue;
            if(Key((GameAction)i)==key){reason="按键已用于"+Names[i]+"，请先修改该项。";return false;}
        }
        PlayerPrefs.SetInt(Pref(action),(int)key);PlayerPrefs.Save();reason="已保存";return true;
    }
    public static void Reset(){foreach(GameAction a in Enum.GetValues(typeof(GameAction)))PlayerPrefs.DeleteKey(Pref(a));PlayerPrefs.Save();}
}
