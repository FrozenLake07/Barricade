using UnityEngine;

// 通过资源配置引用原始 Silver.ttf，不复制字体，也不退回系统字体。
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
