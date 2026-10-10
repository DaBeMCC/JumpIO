using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
// Física del jugador. El movimiento propio de cada modo se delega en PlayerMode (patrón State) y la entrada en PlayerInput.
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }
    [FormerlySerializedAs("velocidad")]
    [SerializeField] float speed = 10.386f;
    [FormerlySerializedAs("escalaGravedad")]
    [SerializeField] float gravityScale = 9f;
    [SerializeField] Transform groundCheck;
    [FormerlySerializedAs("radioGroundCheck")]
    [SerializeField] float groundCheckRadius = 0.45f;
    [FormerlySerializedAs("capaSuelo")]
    [SerializeField] LayerMask groundLayer;
    [FormerlySerializedAs("saltar")]
    [SerializeField] InputAction jumpAction = CreateJumpAction();
    [SerializeField] MusicConductor conductor;
    [FormerlySerializedAs("ventanaPulsacion")]
    [SerializeField] float orbPressWindow = 0.05f;
    [SerializeField, Range(0f, 1f)] float wallDeathBand = 0.3f;
    [SerializeField, Range(0f, 0.2f)] float coyoteTime = 0.08f;
    [SerializeField] CubeMode cube = new CubeMode();
    [SerializeField] ShipMode ship = new ShipMode();
    [SerializeField] BallMode ball = new BallMode();
    [SerializeField] UfoMode ufo = new UfoMode();

    Rigidbody2D rb;
    PlayerBounds bounds;
    Vector2 startPosition;
    float startSpeed;
    PlayerInput input;
    JumpOrb touchedOrb;
    bool dashing;
    float dashSlope;
    float lastGroundedAt = float.NegativeInfinity;
    bool groundJumpAvailable;

    public bool IsActive { get; private set; }
    public bool IsGrounded { get; internal set; }
    public bool IsOnCeiling { get; private set; }
    public bool Boosted { get; set; }
    public float GravityDirection { get; private set; } = 1f;
    public float Speed { get => speed; set => speed = value; }
    public float RelativeVerticalVelocity => rb.linearVelocity.y * GravityDirection;
    public float BaseGravity => Mathf.Abs(Physics2D.gravity.y) * gravityScale;
    public bool IsHeld => input.IsHeld;
    public Vector2 Velocity => rb.linearVelocity;
    public PlayerMode Mode { get; private set; }
    public PlayerModeType ModeType { get; private set; }
    public float FloorY { get; private set; } = float.NegativeInfinity;
    public float CeilingY { get; private set; } = float.PositiveInfinity;
    // Coyote time: justo después de dejar un borde todavía se acepta el salto, porque a esta velocidad el ojo va un poco por detrás del jugador.
    public bool CanGroundJump => IsGrounded || groundJumpAvailable && Time.fixedTime - lastGroundedAt <= coyoteTime;

    public event System.Action Landed;
    public event System.Action ModeChanged;

    static InputAction CreateJumpAction()
    {
        var action = new InputAction("Jump", InputActionType.Button);
        action.AddBinding("<Keyboard>/space");
        action.AddBinding("<Keyboard>/upArrow");
        action.AddBinding("<Keyboard>/w");
        action.AddBinding("<Mouse>/leftButton");
        action.AddBinding("<Touchscreen>/primaryTouch/press");
        return action;
    }

    void Awake()
    {
        Instance = this;
        input = GetComponent<PlayerInput>();
        if (input == null) input = gameObject.AddComponent<PlayerInput>();
        input.Configure(jumpAction, orbPressWindow);
        rb = GetComponent<Rigidbody2D>();
        startPosition = rb.position;
        startSpeed = speed;
        rb.simulated = false;
        bounds = new PlayerBounds(this, rb, GetComponent<Collider2D>(), groundLayer, wallDeathBand);
        foreach (var mode in new PlayerMode[] { cube, ship, ball, ufo }) mode.Bind(this);
        Mode = cube;
    }

    void OnEnable()
    {
        GameEvents.AttemptStarted += Restart;
        GameEvents.PlayerDied += Stop;
        GameEvents.LevelCompleted += Stop;
    }

    void OnDisable()
    {
        GameEvents.AttemptStarted -= Restart;
        GameEvents.PlayerDied -= Stop;
        GameEvents.LevelCompleted -= Stop;
    }

    public bool ConsumePress()
    {
        return input.Consume();
    }

    public void ClearPress() => input.Clear();

    public void UseGroundJump()
    {
        IsGrounded = false;
        groundJumpAvailable = false;
    }

    void FixedUpdate()
    {
        if (!IsActive) return;

        ClampToBounds(out bool onFloorBound, out bool onCeilingBound);
        bool touchingSolid = rb.IsTouchingLayers(groundLayer);
        bool touchingGround = RelativeVerticalVelocity <= 0.01f
                              && (onFloorBound || touchingSolid
                                  && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer));
        if (touchingGround && !IsGrounded)
        {
            Landed?.Invoke();
            GameEvents.RaiseLanded(transform.position);
        }
        IsGrounded = touchingGround;
        if (touchingGround)
        {
            lastGroundedAt = Time.fixedTime;
            groundJumpAvailable = true;
        }
        Vector2 head = 2f * rb.position - (Vector2)groundCheck.position;
        IsOnCeiling = RelativeVerticalVelocity >= -0.01f
                      && (onCeilingBound || touchingSolid && Physics2D.OverlapCircle(head, groundCheckRadius, groundLayer));

        if (dashing && !IsHeld) EndDash();
        if (touchedOrb != null && ConsumePress()) touchedOrb.Activate(this);
        else if (dashing) rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed * dashSlope);
        else rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mode.Step(RelativeVerticalVelocity, Time.fixedDeltaTime) * GravityDirection);
        touchedOrb = null;
        float vy = rb.linearVelocity.y;

        // La velocidad horizontal sigue al reloj de la música y no a la física libre, así la carrera nunca se desfasa de la canción.
        float vx = speed;
        if (conductor != null)
        {
            speed = conductor.CurrentSpeed;
            vx = (conductor.PositionX - rb.position.x) / Time.fixedDeltaTime;
        }
        rb.linearVelocity = new Vector2(vx, vy);
    }

    void ClampToBounds(out bool onFloor, out bool onCeiling) => bounds.Clamp(FloorY, CeilingY, GravityDirection, out onFloor, out onCeiling);

    PlayerMode Get(PlayerModeType type) => type switch
    {
        PlayerModeType.Ship => ship,
        PlayerModeType.Ball => ball,
        PlayerModeType.Ufo => ufo,
        _ => cube,
    };

    public void SetMode(PlayerModeType type, float portalY)
    {
        var next = Get(type);
        if (next.Bounds > 0f)
        {
            FloorY = Mathf.Max(0f, Mathf.Ceil(portalY - (next.Bounds / 2f + 1f)));
            CeilingY = FloorY + next.Bounds;
        }
        else
        {
            FloorY = float.NegativeInfinity;
            CeilingY = float.PositiveInfinity;
        }
        if (next == Mode) return;

        dashing = false;
        groundJumpAvailable = false;
        float vy = next.Enter(ModeType, RelativeVerticalVelocity);
        Mode = next;
        ModeType = type;
        SetGravity(GravityDirection < 0f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy * GravityDirection);
        ModeChanged?.Invoke();
    }

    public bool WallAhead() => bounds.WallAhead();

    void OnCollisionEnter2D(Collision2D collision) => bounds.SlidePastCorner(collision);
    void OnCollisionStay2D(Collision2D collision) => bounds.SlidePastCorner(collision);

    public void TouchOrb(JumpOrb orb) => touchedOrb = orb;

    public void Launch(float verticalVelocity, bool boost = false)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, verticalVelocity * GravityDirection);
        IsGrounded = false;
        groundJumpAvailable = false;
        if (boost) Boosted = true;
    }

    public void ReverseGravity()
    {
        FlipGravity();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        IsGrounded = false;
        groundJumpAvailable = false;
    }

    public void StartDash(float angle)
    {
        dashing = true;
        dashSlope = Mathf.Tan(angle * Mathf.Deg2Rad);
        Boosted = true;
        IsGrounded = false;
        groundJumpAvailable = false;
        rb.gravityScale = 0f;
    }

    void EndDash()
    {
        dashing = false;
        SetGravity(GravityDirection < 0f);
    }

    public void FlipGravity() => SetGravity(GravityDirection > 0f);

    public void SetGravity(bool inverted)
    {
        GravityDirection = inverted ? -1f : 1f;
        rb.gravityScale = dashing ? 0f : gravityScale * Mode.GravityMultiplier * GravityDirection;
        var p = groundCheck.localPosition;
        groundCheck.localPosition = new Vector3(p.x, -Mathf.Abs(p.y) * GravityDirection, p.z);
    }

    void Restart(int attempt)
    {
        speed = startSpeed;
        dashing = false;
        Boosted = false;
        var level = conductor != null ? conductor.Level : null;
        bool test = level != null && PlaytestStart.Active;
        Vector2 spawn = level != null ? new Vector2(startPosition.x, level.startY) : startPosition;
        if (test) spawn = new Vector2(level.XAtTime(PlaytestStart.Time), PlaytestStart.Y);
        transform.position = spawn;
        rb.position = spawn;
        rb.linearVelocity = Vector2.zero;
        SetGravity(false);
        SetMode(PlayerModeType.Cube, startPosition.y);
        if (level != null)
        {
            SetMode(test ? PlaytestStart.Mode : level.startMode, test ? PlaytestStart.CorridorY : level.startCorridorY);
            SetGravity(test ? PlaytestStart.GravityInverted : level.startGravityInverted);
        }
        ClampToBounds(out _, out _);
        transform.position = rb.position;
        rb.simulated = true;
        IsGrounded = false;
        IsOnCeiling = false;
        groundJumpAvailable = false;
        touchedOrb = null;
        input.Clear();
        IsActive = true;
    }

    void Stop()
    {
        IsActive = false;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearInstance() => Instance = null;
}
