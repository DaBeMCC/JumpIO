using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    [FormerlySerializedAs("controlador")]
    [SerializeField] PlayerController controller;
    [FormerlySerializedAs("salud")]
    [SerializeField] PlayerHealth health;
    [FormerlySerializedAs("estela")]
    [SerializeField] TrailRenderer trail;
    [FormerlySerializedAs("intervaloParpadeo")]
    [SerializeField] float blinkInterval = 0.1f;

    SpriteRenderer sr;
    CharacterSkin skin;
    Color tint = Color.white;
    float scale = 1f;
    float angle;
    bool visible;
    Coroutine blinking;
    Animator animator;
    RuntimeAnimatorController defaultAnimations;
    public bool UprightRunner => skin != null && skin.uprightRunner;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            defaultAnimations = animator.runtimeAnimatorController;
            if (defaultAnimations is AnimatorOverrideController overrides)
                defaultAnimations = overrides.runtimeAnimatorController;
        }
        Show(false);
    }

    void OnEnable()
    {
        controller.Landed += Snap;
        controller.ModeChanged += OnModeChanged;
        GameEvents.AttemptStarted += OnAttemptStarted;
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.PlayerDied += OnPlayerDied;
    }

    void OnDisable()
    {
        controller.Landed -= Snap;
        controller.ModeChanged -= OnModeChanged;
        GameEvents.AttemptStarted -= OnAttemptStarted;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.PlayerDied -= OnPlayerDied;
    }

    public void ApplySkin(CharacterSkin skin)
    {
        this.skin = skin;
        if (animator != null)
            animator.runtimeAnimatorController = skin.animationOverride != null
                ? skin.animationOverride : defaultAnimations;
        ShowModeSprite();
        tint = skin.tint;
        scale = skin.scale;
        trail.startColor = skin.trailColor;
        trail.endColor = new Color(skin.trailColor.r, skin.trailColor.g, skin.trailColor.b, 0f);
        trail.widthMultiplier = skin.scale;
        trail.enabled = skin.showTrail;
    }

    void Update()
    {
        if (!controller.IsActive) return;
        if (UprightRunner)
        {
            angle = 0f;
            sr.flipY = controller.GravityDirection < 0f;
            transform.localRotation = Quaternion.identity;
            return;
        }
        angle = controller.Mode.Rotate(angle, Time.deltaTime);
        sr.flipY = controller.GravityDirection < 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Snap()
    {
        angle = UprightRunner ? 0f : controller.Mode.Land(angle);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnModeChanged()
    {
        ShowModeSprite();
        angle = Mathf.Round(angle / 90f) * 90f;
    }

    void ShowModeSprite()
    {
        if (skin != null) sr.sprite = skin.SpriteFor(controller.ModeType);
    }

    void LateUpdate()
    {
        transform.localScale *= scale;
        sr.color *= tint;
    }

    void OnAttemptStarted(int attempt)
    {
        angle = 0f;
        transform.localRotation = Quaternion.identity;
        trail.Clear();
        Show(true);
    }

    void OnPlayerDamaged(int currentHealth, int maxHealth)
    {
        if (blinking != null) StopCoroutine(blinking);
        blinking = StartCoroutine(Blink());
    }

    IEnumerator Blink()
    {
        float end = Time.time + health.InvulnerabilityDuration;
        while (Time.time < end && visible)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
        sr.enabled = visible;
        blinking = null;
    }

    void OnPlayerDied()
    {
        if (blinking != null) StopCoroutine(blinking);
        blinking = null;
        Show(UprightRunner);
        trail.emitting = false;
    }

    void Show(bool show)
    {
        visible = show;
        sr.enabled = show;
        trail.emitting = show;
    }
}
