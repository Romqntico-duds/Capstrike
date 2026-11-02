using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class VirtualJoystick : MonoBehaviour
{
    public RectTransform background;
    public RectTransform handle;
    public float handleRange = 70f;
    public Vector2 Value { get; private set; }
    int touchId = -1;
#if UNITY_EDITOR
    bool touchSimulationEnabled;
#endif

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        TouchSimulation.Enable();
        touchSimulationEnabled = true;
#endif
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
#if UNITY_EDITOR
        if (touchSimulationEnabled)
        {
            TouchSimulation.Disable();
            touchSimulationEnabled = false;
        }
#endif
        touchId = -1;
        Value = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        bool touchFound = false;

        foreach (var touch in Touch.activeTouches)
        {
            if (touchId < 0 && touch.phase == UnityEngine.InputSystem.TouchPhase.Began && RectTransformUtility.RectangleContainsScreenPoint(background, touch.screenPosition, null))
                touchId = touch.touchId;

            if (touch.touchId != touchId) continue;

            touchFound = true;
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                ResetStick();
            }
            else if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, touch.screenPosition, null, out Vector2 localPoint))
            {
                float radius = background.rect.width * 0.5f;
                Value = Vector2.ClampMagnitude(localPoint / radius, 1f);
                handle.anchoredPosition = Value * handleRange;
            }
        }

        if (!touchFound && touchId >= 0)
            ResetStick();
    }

    void ResetStick()
    {
        touchId = -1;
        Value = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}
