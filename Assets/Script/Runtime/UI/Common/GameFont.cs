using UnityEngine;

// 通过资源配置引用项目像素字体；当前UI派生版只补齐“垒”，原始Silver保留。
[CreateAssetMenu(menuName="Barricade/UI/Game Font")]
public sealed class GameFont : ScriptableObject
{
    public Font font;
    static GameFont settings;
    public static Font Silver
    {
        get
        {
            if(settings==null) settings=Resources.Load<GameFont>("Campaign/GameFont");
            return settings!=null?settings.font:null;
        }
    }
}
