using TMPro;
using UnityEngine;

public class RunInfoDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text levelNameText;
    [SerializeField] TMP_Text attemptText;
    [SerializeField] TMP_Text progressText;
    [SerializeField] MusicConductor conductor;

    int shownPercent = -1;

    void OnEnable()
    {
        GameEvents.AttemptStarted += ShowAttempt;
        GameEvents.ProgressChanged += ShowProgress;
    }
    void OnDisable()
    {
        GameEvents.AttemptStarted -= ShowAttempt;
        GameEvents.ProgressChanged -= ShowProgress;
    }

    void Start() => levelNameText.text = conductor.Level.levelName;

    void ShowAttempt(int attempt)
    {
        attemptText.text = "Attempt " + attempt;
        shownPercent = -1;
        ShowProgress(0f);
    }

    void ShowProgress(float progress)
    {
        int percent = Mathf.Clamp(Mathf.FloorToInt(100f * progress), 0, 100);
        if (percent == shownPercent) return;
        shownPercent = percent;
        progressText.text = percent + "%";
    }
}
