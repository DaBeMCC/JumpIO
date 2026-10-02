using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] float popScale = 1.35f;
    [SerializeField] float popDuration = 0.25f;

    int shown;
    Coroutine pop;

    void OnEnable() => GameEvents.ScoreChanged += Show;
    void OnDisable() => GameEvents.ScoreChanged -= Show;

    void Show(int score)
    {
        text.text = score.ToString();
        if (score - shown > 1)
        {
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(Pop());
        }
        shown = score;
    }

    IEnumerator Pop()
    {
        for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
        {
            text.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, t / popDuration);
            yield return null;
        }
        text.transform.localScale = Vector3.one;
        pop = null;
    }
}
