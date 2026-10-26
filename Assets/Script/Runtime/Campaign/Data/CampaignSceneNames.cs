// Keep these names stable: build settings and existing saves store the original scene identifiers.
public static class CampaignSceneNames
{
    public const string Title = "Title";
    public const string Construction = "1";
    public const string Defense = "2";
    public const string Tavern = "3";
    public static string AssetPath(string sceneName) => "Assets/Scenes/" + sceneName + ".unity";
}
