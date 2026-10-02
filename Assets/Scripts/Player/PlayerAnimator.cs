using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int YVelocity = Animator.StringToHash("yVelocity");
    static readonly int Hurt = Animator.StringToHash("Hurt");
    static readonly int Mode = Animator.StringToHash("Mode");
    static readonly int Action = Animator.StringToHash("Action");
    static readonly int Dead = Animator.StringToHash("Dead");
    static readonly int Paused = Animator.StringToHash("Paused");

    [SerializeField] Animator animator;

    PlayerController controller;
    bool paused;
    AnimatorStateInfo resumeState;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.PlayerActed += OnPlayerActed;
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.AttemptStarted += OnAttemptStarted;
        GameEvents.PauseChanged += OnPauseChanged;
    }
    void OnDisable()
    {
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.PlayerActed -= OnPlayerActed;
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.AttemptStarted -= OnAttemptStarted;
        GameEvents.PauseChanged -= OnPauseChanged;
    }

    void Update()
    {
        animator.SetFloat(Speed, !paused && controller.IsActive ? controller.Speed : 0f);
        animator.SetBool(IsGrounded, paused || controller.IsGrounded);
        animator.SetInteger(Mode, paused ? 0 : (int)controller.ModeType);
        animator.SetFloat(YVelocity, !paused && controller.IsActive ? controller.RelativeVerticalVelocity : 0f);
    }

    void OnPlayerDamaged(int health, int maxHealth)
    {
        if (health > 0) animator.SetTrigger(Hurt);
    }
    void OnPlayerActed(Vector3 position)
    {
        if (controller.IsActive) animator.SetTrigger(Action);
    }
    void OnPlayerDied() => animator.SetBool(Dead, true);
    void OnAttemptStarted(int attempt)
    {
        animator.Rebind();
        animator.SetBool(Dead, false);
        animator.ResetTrigger(Hurt);
        animator.ResetTrigger(Action);
    }
    void OnPauseChanged(bool value)
    {
        if (value && !paused) resumeState = animator.GetCurrentAnimatorStateInfo(0);
        if (!value && paused) animator.Play(resumeState.fullPathHash, 0, resumeState.normalizedTime);
        paused = value;
        animator.SetBool(Paused, value);
        animator.updateMode = value ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
    }
}
