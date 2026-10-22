using UnityEngine;
using UnityEngine.EventSystems;

// Focus is expressed through the typography and a small red rule, rather than another framed box.
public sealed class ParisMenuAccent : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    UnityEngine.UI.Button button;
    UnityEngine.UI.Image marker,rule;
    bool hovered;
    bool? previousFocus,previousEnabled;
    public void RefreshStyle()
    {
        if(button==null)button=GetComponent<UnityEngine.UI.Button>();
        marker=ParisUI.Panel(transform,"FocusMark",null,.025f,.33f,.032f,.67f);
        rule=ParisUI.Panel(transform,"MenuRule",null,.10f,.03f,.90f,.045f);
        previousFocus=previousEnabled=null;Refresh();
    }
    public void OnPointerEnter(PointerEventData data){hovered=true;Refresh();}
    public void OnPointerExit(PointerEventData data){hovered=false;Refresh();}
    void OnDisable(){hovered=false;previousFocus=null;}
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        if(button==null||marker==null)return;
        bool enabled=button.IsInteractable();bool focused=enabled&&(hovered||(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject==gameObject));
        if(previousFocus==focused&&previousEnabled==enabled)return;previousFocus=focused;previousEnabled=enabled;
        var s=ParisUI.Style;bool primary=name=="NewGame"||name=="Resume";
        foreach(var label in GetComponentsInChildren<UnityEngine.UI.Text>(true))label.color=enabled?(focused||primary?s.cream:s.muted):new Color(.38f,.38f,.36f,.8f);
        marker.color=new Color(s.red.r,s.red.g,s.red.b,focused?1:0);
        rule.color=focused?s.red:new Color(s.cream.r,s.cream.g,s.cream.b,enabled?.16f:.06f);
        rule.rectTransform.anchorMax=new Vector2(focused?.90f:.34f,.045f);
    }
}
