using UnityEngine;

// Emulates the mouse for the Coffee Shop pack in VR.
// The pack reacts to the Unity messages OnMouseDown / OnMouseUp, which never fire on Quest.
// This ray-casts from the controller (trigger) or the tracked hand (index pinch)
// and sends those same messages to whatever collider is hit.
public class VRClickBridge : MonoBehaviour
{
    [Header("Ray origin")]
    [Tooltip("Controller anchor, e.g. RightHandAnchor of the OVRCameraRig. Used when no hand is tracked.")]
    public Transform controllerRayOrigin;

    [Tooltip("Optional. When assigned and tracked, its pointer pose and index pinch are used instead of the controller.")]
    public OVRHand hand;

    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Ray")]
    public float maxDistance = 10f;
    public LayerMask layerMask = ~0;

    [Tooltip("Optional line drawn from the origin to the hit point.")]
    public LineRenderer line;

    Collider pressedCollider;
    bool wasPressed;

    void Update()
    {
        Transform origin = GetOrigin(out bool pressed);
        if (origin == null)
            return;

        bool hitSomething = Physics.Raycast(origin.position, origin.forward, out RaycastHit hit,
            maxDistance, layerMask, QueryTriggerInteraction.Ignore);

        UpdateLine(origin, hitSomething ? hit.point : origin.position + origin.forward * maxDistance);

        if (pressed && !wasPressed && hitSomething)
        {
            pressedCollider = hit.collider;
            Send(pressedCollider, "OnMouseDown");
        }
        else if (!pressed && wasPressed && pressedCollider != null)
        {
            Send(pressedCollider, "OnMouseUp");
            if (hitSomething && hit.collider == pressedCollider)
                Send(pressedCollider, "OnMouseUpAsButton");
            pressedCollider = null;
        }

        wasPressed = pressed;
    }

    Transform GetOrigin(out bool pressed)
    {
        if (hand != null && hand.IsTracked && hand.IsPointerPoseValid && hand.PointerPose != null)
        {
            pressed = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            return hand.PointerPose;
        }

        pressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
        return controllerRayOrigin;
    }

    void UpdateLine(Transform origin, Vector3 end)
    {
        if (line == null)
            return;
        line.positionCount = 2;
        line.SetPosition(0, origin.position);
        line.SetPosition(1, end);
    }

    static void Send(Collider col, string message)
    {
        col.gameObject.SendMessage(message, SendMessageOptions.DontRequireReceiver);

        // Unity's real mouse messages also reach the Rigidbody that owns the collider.
        Rigidbody body = col.attachedRigidbody;
        if (body != null && body.gameObject != col.gameObject)
            body.gameObject.SendMessage(message, SendMessageOptions.DontRequireReceiver);
    }
}
