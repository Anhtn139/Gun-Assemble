using UnityEngine;
using UnityEngine.UI;

public class InverseMask : Mask
{
    public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return !base.IsRaycastLocationValid(sp, eventCamera);
    }
}