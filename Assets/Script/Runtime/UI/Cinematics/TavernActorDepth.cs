using UnityEngine;

// Timeline animates this value so UI actors can cross the doorway's sorting layers.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class TavernActorDepth : MonoBehaviour
{
    public float sortingOrder = 20;
    Canvas targetCanvas;
    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }
    void OnDidApplyAnimationProperties() { Apply(); }
    public void Apply()
    {
        if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();
        if (targetCanvas != null) targetCanvas.sortingOrder = Mathf.RoundToInt(sortingOrder);
    }
}
