using System;
using UnityEngine;

[CreateAssetMenu(menuName="街垒/对话/酒馆演出台词")]
public sealed class TavernDialogueLines : ScriptableObject
{
    [Serializable] public sealed class Line
    {
        public string speaker="";
        [TextArea(2,6),Tooltip("支持 {playerName}；留空时跳过此对白节点。")]
        public string text="";
    }
    public Line first=new Line{speaker="格朗泰尔"};
    [Header("第二段按姓名从 NameResponses.asset 选择")]
    public Line third=new Line{speaker="昂若拉"};
    public Line fourth=new Line{speaker="昂若拉"};
}
