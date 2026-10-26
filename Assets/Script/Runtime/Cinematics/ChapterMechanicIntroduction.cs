using UnityEngine;

// Only first appearances of gameplay rules receive a chapter-entry page.
public static class ChapterMechanicIntroduction
{
    public static bool TryGet(int chapter,float chordGap,out string title,out string body)
    {
        title=body=null;
        switch(chapter)
        {
            case 2:
                title="修筑街垒";
                body="按下画面提示的按键。"+(GameOptions.Difficulty==GameDifficulty.Nightmare?"噩梦难度下，普通成功不加血，只有完美建造才能恢复生命。":"普通成功增加5点生命。心跳静止的间隙按对，可获得完美建造。")+"\n\n出现两个字母时，同时按下两键，先后间隔不超过 "+Mathf.RoundToInt(chordGap*1000)+" 毫秒。\n\n按错会短暂休息。"+(GameOptions.PromptGraceEnabled?"字母刚切换时，旧字母有短暂保护，但不会重复增加街垒耐久。":"噩梦难度不提供换字保护，请按当前显示的字母。");break;
            case 3:
                title="守住街垒";
                body=GameBindings.CombatHint+"。\n\n尽量在心跳静止的间隙命中。普通命中手榴弹不会削减它的耐久，完美命中才能引爆。\n\n同组的三枚手榴弹会连锁爆炸：拦截其中一枚，就能消除整组威胁。";break;
            case 4:
                title="掩护Gavroche";
                body="Gavroche会跑出街垒拾取弹药，再沿原路返回。\n\n优先清理他所在轨道的士兵，阻止士兵接触他。玩家子弹不会伤到他。\n\n本章三次护送全部成功，从第五章起可额外携带一发弹药。";break;
            case 5:
                title="弹药管理";
                body="从本章起，弹药数量有限。留意剩余弹药，按 "+GameBindings.Label(GameAction.Reload)+" 补弹。\n\n在心跳静止的间隙补弹更快。换轨或换弹尚未结束时，无法开火。\n\n留意弹药余量，提前安排补弹，为下一轮进攻做好准备。";break;
            case 8:
                title="拦截炮弹";
                body="火炮会向街垒发射高速炮弹，命中会造成大量伤害。\n\n切到炮弹所在轨道，按 "+GameBindings.Label(GameAction.AimUp)+" 瞄准空中，再按 "+GameBindings.Label(GameAction.Fire)+" 射击。优先抓住心跳静止的间隙命中。\n\n炮弹不属于手榴弹组，不会随手榴弹连锁消除，需要单独拦截。";break;
            case 9:
                title="最后的防线";
                body="这是最后总攻，心跳从较快的节奏开始。\n\n继续利用此前学会的换轨、空中拦截与补弹，尽量保持街垒耐久。\n\n本章街垒倒下会直接进入结局，不再提供此前的临危挽救机会。";break;
        }
        return title!=null;
    }
}
