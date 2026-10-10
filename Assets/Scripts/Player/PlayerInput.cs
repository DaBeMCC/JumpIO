using UnityEngine;
using UnityEngine.InputSystem;

// Guarda la pulsación de salto para que cuente aunque llegue un poco antes de aterrizar o de tocar un orbe.
public sealed class PlayerInput : MonoBehaviour
{
    [SerializeField] private InputActionReference actionReference;
    InputAction jump;
    InputAction action;
    float window;
    float pressTime = float.NegativeInfinity;
    public event System.Action ActionRequested;
    public bool IsHeld => Time.timeScale > 0f && jump != null && jump.IsPressed();
    public void Configure(InputAction jumpAction, float pressWindow)
    {
        jump = jumpAction;
        window = pressWindow;
        if (isActiveAndEnabled) jump.Enable();
    }
    void Awake()
    {
        if (actionReference != null) action = actionReference.action.Clone();
        else
        {
            action = new InputAction("Action", InputActionType.Button);
            action.AddBinding("<Keyboard>/e");
            action.AddBinding("<Keyboard>/x");
            action.AddBinding("<Gamepad>/buttonWest");
        }
    }
    void OnEnable()
    {
        jump?.Enable();
        action.performed += RequestAction;
        action.Enable();
    }
    void OnDisable()
    {
        jump?.Disable();
        action.performed -= RequestAction;
        action.Disable();
        Clear();
    }
    void OnDestroy() => action.Dispose();
    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (jump != null && jump.WasPressedThisFrame()) pressTime = Time.time;
    }
    public bool Consume()
    {
        bool recent = Time.time - pressTime <= window;
        if (Time.timeScale == 0f || !(recent || pressTime > float.NegativeInfinity && IsHeld)) return false;
        Clear();
        return true;
    }
    public bool ActionPressed => Time.timeScale > 0f && action.WasPressedThisFrame();
    void RequestAction(InputAction.CallbackContext context)
    {
        if (Time.timeScale > 0f) ActionRequested?.Invoke();
    }
    public void Clear() => pressTime = float.NegativeInfinity;
}
