using UnityEngine;

// Compatibility for older runtime/save references. Whole-camera impact blur is retired.
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class WallImpactBlur : MonoBehaviour
{
    public const float Duration=.32f;
    public float Strength=>0;
    public static void Trigger(float amount){}
    public void Pulse(float amount){}
    void OnEnable(){enabled=false;}
}
