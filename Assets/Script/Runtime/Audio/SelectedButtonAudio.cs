using UnityEngine;
using UnityEngine.EventSystems;
[DisallowMultipleComponent]
public sealed class SelectedButtonAudio : MonoBehaviour,IPointerClickHandler,ISubmitHandler
{
    public static void Attach(UnityEngine.UI.Button button)
    {if(button!=null&&button.GetComponent<SelectedButtonAudio>()==null)button.gameObject.AddComponent<SelectedButtonAudio>();}
    void Click()
    {
        var button=GetComponent<UnityEngine.UI.Button>();if(button==null)return;
        bool back=name.IndexOf("Resume",System.StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Cancel",System.StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Return",System.StringComparison.OrdinalIgnoreCase)>=0;
        SfxPlayer.Play(!button.IsInteractable()?"ui_unavailable":back?"ui_cancel":"ui_confirm",.8f);
    }
    public void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)Click();}
    public void OnSubmit(BaseEventData data)=>Click();
}
