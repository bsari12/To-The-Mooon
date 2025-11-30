using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RocketTapGame : MonoBehaviour
{
    [Header("UI - Bar")]
    public Slider powerBar;
    public Image barFillImage;
    [Header("Rounds Info")]
    public TMP_Text roundsLeftText;  
    [Header("High Score")]
    public TMP_Text highScoreText; 
    private int highScore = 0;
    [Header("Stars")]
    public GameObject starPrefab;      
    public Transform starParent;   
    public float starMoveDistance = 30f;
    private GameObject currentStar;  

    [Header("Info Texts")]
    public TMP_Text instructionText;
    public TMP_Text roundsText;
    public TMP_Text summaryText;

    [Header("Restart")]
    public Button restartButton;

    [Header("Rocket Visuals")]
    public Transform rocket;
    public GameObject fireEffect;
    public float launchHeightFactor = 0.05f;

    [Header("Audio")]
    public AudioSource sfxIgnitionSource;
    public AudioSource sfxPerfectSource;
    public AudioSource audioPlayer;
    public AudioClip clipIgnition;
    public AudioClip clipPerfect;

    [Header("Gameplay")]
    public int totalRounds = 5;
    public float fillSpeedPerSec = 180f;
    public bool pingPongMode = false;
    public float betweenRoundsDelay = 0.6f;

    [Header("Feedback")]
    public bool vibrateOnPerfect = true;
    public float perfectEpsilon = 0.5f;
    public Image fillFlashImage;

    [Header("Score Feedback UI")]
    public TMP_Text feedbackText;
    public float feedbackShowSeconds = 0.8f;
    public float feedbackPulseScale = 1.05f;
    public float feedbackPulseTime = 0.22f;

    [Header("Launch Effects")]
    public Camera mainCamera;
    public float shakeDuration = 0.22f;
    public float shakeMagnitude = 0.12f;
    public float perfectBonusMultiplier = 1.12f;

    public float fireScaleUp = 1.2f;
    public float fireScaleTime = 0.12f;
    public float fireKeepTime = 0.55f;

    private Coroutine feedbackPulseCoroutine;
    private Coroutine cameraShakeCoroutine;
    private Coroutine fireScaleCoroutine;

    private enum State { Idle, Filling, WaitingStop, RoundEnd, Summary }
    private State state = State.Idle;

    private int currentRound = 0;
    private float dir = 1f;
    private float totalMeters = 0f;
    private bool inputArmed = true;

    private Vector3 rocketStartPos;
    private Vector3 camOriginalLocalPos;

    void Awake()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
            restartButton.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (highScoreText != null)
        highScoreText.gameObject.SetActive(false);

        if (powerBar != null)
        {
            powerBar.minValue = 0f; powerBar.maxValue = 100f; powerBar.wholeNumbers = false; powerBar.value = 0f;
        }
        if (barFillImage != null) barFillImage.fillAmount = 0f;

        if (summaryText) summaryText.gameObject.SetActive(false);
        if (fireEffect) fireEffect.SetActive(false);
        if (feedbackText) feedbackText.gameObject.SetActive(false);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Inject Fuel\nTap again to Launch";
        }
        if (roundsText != null) roundsText.gameObject.SetActive(true);
        if (roundsText != null) roundsText.text = $"Rounds: 0/{totalRounds}";

        if (rocket != null) rocketStartPos = rocket.position;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) camOriginalLocalPos = mainCamera.transform.localPosition;

        ResetBar();
    }

    void Update()
    {
        bool tapped = false;
#if UNITY_EDITOR || UNITY_STANDALONE
        tapped = Input.GetMouseButtonDown(0);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) tapped = true;
#endif
        if (tapped && inputArmed)
        {
            inputArmed = false;
            Invoke(nameof(ArmInput), 0.05f);
            OnTap();
        }

        if (state == State.Filling || state == State.WaitingStop) TickBar();
        
    }

    void ArmInput() => inputArmed = true;

    void TickBar()
    {
        if (state == State.Filling)
        {
            float v = GetBarValue() + dir * fillSpeedPerSec * Time.deltaTime;

            if (!pingPongMode)
            {
                if (v >= 100f) { v = 100f; ApplyBarValue(v, true); state = State.WaitingStop; return; }
                else if (v <= 0f) v = 0f;
            }
            else
            {
                if (v >= 100f) { v = 100f; dir = -1f; }
                else if (v <= 0f) { v = 0f; dir = 1f; }
            }

            ApplyBarValue(v, false);
        }
        else if (state == State.WaitingStop)
        {
            ApplyBarValue(100f, true);
        }
    }

    void OnTap()
    {
        if (instructionText != null && instructionText.gameObject.activeSelf)
        {
            instructionText.gameObject.SetActive(false);
            NextRound();
            state = State.Filling;
            PlayIgnitionCue(false);
            return;
        }

        switch (state)
        {
            case State.Idle:
                state = State.Filling;
                PlayIgnitionCue(false);
                break;
            case State.Filling:
            case State.WaitingStop:
                StopAndScore();
                break;
        }
    }

    void StopAndScore()
    {
        float val = Mathf.Clamp(GetBarValue(), 0f, 100f);
        ApplyBarValue(val, val >= 100f - 0.0001f);

        bool isPerfect = (val >= 100f - perfectEpsilon);
        float appliedVal = val;
        if (isPerfect) appliedVal *= perfectBonusMultiplier;

        totalMeters += Mathf.Round(appliedVal);

        if (fireEffect != null)
        {
            fireEffect.SetActive(true);
            if (fireScaleCoroutine != null) StopCoroutine(fireScaleCoroutine);
            fireScaleCoroutine = StartCoroutine(FireScaleRoutine());
            Invoke(nameof(StopFire), fireKeepTime);
        }

        if (rocket != null)
        {
            Vector3 to = rocket.position;
            to.y += appliedVal * launchHeightFactor;
            StartCoroutine(SmoothMove(rocket, rocket.position, to, 0.4f));
        }

        if (mainCamera != null)
        {
            if (cameraShakeCoroutine != null) StopCoroutine(cameraShakeCoroutine);
            cameraShakeCoroutine = StartCoroutine(ShakeCamera(shakeDuration, shakeMagnitude));
        }

        // Audio oynatma
        if (sfxIgnitionSource != null)
        {
            if (sfxIgnitionSource.clip != null) sfxIgnitionSource.Play();
            else if (audioPlayer != null && clipIgnition != null) audioPlayer.PlayOneShot(clipIgnition, 1f);
        }
        else if (audioPlayer != null && clipIgnition != null) audioPlayer.PlayOneShot(clipIgnition, 1f);

        if (isPerfect)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (vibrateOnPerfect) Handheld.Vibrate();
#endif
            if (sfxPerfectSource != null)
            {
                if (sfxPerfectSource.clip != null) sfxPerfectSource.Play();
                else if (audioPlayer != null && clipPerfect != null) audioPlayer.PlayOneShot(clipPerfect, 1f);
            }
            else if (audioPlayer != null && clipPerfect != null) audioPlayer.PlayOneShot(clipPerfect, 1f);

            if (fillFlashImage != null) StartCoroutine(FlashFillCoroutine());
        }

        ShowScoreFeedback(val);
        SpawnStar();
        state = State.RoundEnd;
        Invoke(nameof(NextRound), betweenRoundsDelay);
    }

    void NextRound()
    {
        currentRound++;
        if (currentRound > totalRounds) { ShowSummary(); return; }

        if (roundsText != null) roundsText.text = $"Rounds: {currentRound}/{totalRounds}";
        UpdateRoundsLeftUI();

        ResetBar();
        state = State.Idle;
    }


    void ResetBar()
    {
        dir = 1f;
        ApplyBarValue(0f, true);
    }

    void ShowSummary()
    {
        state = State.Summary;

        // Slider gizle
        if (powerBar != null) powerBar.gameObject.SetActive(false);
        if (barFillImage != null) barFillImage.gameObject.SetActive(false);

        int roundedTotal = Mathf.RoundToInt(totalMeters);
        bool isNewHighScore = false;

        if (roundedTotal > highScore)
        {
            highScore = roundedTotal;
            PlayerPrefs.SetInt("RocketHighScore", highScore);
            PlayerPrefs.Save();
            isNewHighScore = true;
        }

        if (summaryText)
        {
            summaryText.gameObject.SetActive(true);
            summaryText.text = $"Distance: {roundedTotal} m";
        }

        if (highScoreText)
        {
            highScoreText.gameObject.SetActive(true);
            if (isNewHighScore)
                highScoreText.text = $"New High Score! {highScore} m";
            else
                highScoreText.text = $"High Score: {highScore} m";
        }

        if (restartButton != null)
            restartButton.gameObject.SetActive(true);

        if (roundsText != null)
            roundsText.text = $"Rounds: {Mathf.Min(currentRound, totalRounds)}/{totalRounds}";
    }


    private bool tapRegistered = false; 
    public void RestartGame()
    {
        StopAllCoroutines();
        CancelInvoke();

        if (summaryText) summaryText.gameObject.SetActive(false);
        if (restartButton) restartButton.gameObject.SetActive(false);
        if (highScoreText) highScoreText.gameObject.SetActive(false);
        if (fireEffect) fireEffect.SetActive(false);
        if (feedbackText) feedbackText.gameObject.SetActive(false);

        currentRound = 0;
        totalMeters = 0f;
        state = State.Idle;
        dir = 1f;

        ResetBar();
        if (powerBar) powerBar.gameObject.SetActive(true);
        if (barFillImage) barFillImage.gameObject.SetActive(true);

        if (rocket) rocket.position = rocketStartPos;

        if (instructionText) instructionText.gameObject.SetActive(true);

        inputArmed = true;
        tapRegistered = false;

        UpdateRoundsLeftUI();
    }

    void ShowScoreFeedback(float val)
    {
        if (feedbackText == null) return;

        string label; Color col; int added = Mathf.RoundToInt(val);
        if (val >= 100f - perfectEpsilon) { label = $"Perfect! +{added}m"; col = Color.green; }
        else if (val >= 75f) { label = $"Good! +{added}m"; col = new Color(0.2f, 0.6f, 1f); }
        else if (val >= 40f) { label = $"Late... +{added}m"; col = new Color(1f, 0.6f, 0f); }
        else { label = $"Too Early! +{added}m"; col = Color.red; }

        feedbackText.text = label;
        feedbackText.color = col;
        feedbackText.gameObject.SetActive(true);

        if (feedbackPulseCoroutine != null) StopCoroutine(feedbackPulseCoroutine);
        feedbackPulseCoroutine = StartCoroutine(FeedbackPulseRoutine());

        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), feedbackShowSeconds);
    }

    IEnumerator FeedbackPulseRoutine()
    {
        if (feedbackText == null) yield break;

        Vector3 baseScale = Vector3.one;
        float elapsed = 0f;
        float half = feedbackPulseTime * 0.5f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(1f, feedbackPulseScale, Mathf.SmoothStep(0f, 1f, t));
            feedbackText.transform.localScale = baseScale * s;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(feedbackPulseScale, 1f, Mathf.SmoothStep(0f, 1f, t));
            feedbackText.transform.localScale = baseScale * s;
            yield return null;
        }

        feedbackText.transform.localScale = baseScale;
    }

    void HideFeedback()
    {
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (feedbackPulseCoroutine != null) { StopCoroutine(feedbackPulseCoroutine); feedbackPulseCoroutine = null; }
    }

    IEnumerator ShakeCamera(float dur, float mag)
    {
        if (mainCamera == null) yield break;
        Vector3 original = camOriginalLocalPos;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / dur);
            float x = (Random.value * 2f - 1f) * mag * damper;
            float y = (Random.value * 2f - 1f) * mag * damper;
            mainCamera.transform.localPosition = original + new Vector3(x, y, 0f);
            yield return null;
        }
        mainCamera.transform.localPosition = original;
        cameraShakeCoroutine = null;
    }

    IEnumerator FireScaleRoutine()
    {
        if (fireEffect == null) yield break;
        Transform f = fireEffect.transform;
        Vector3 baseScale = f.localScale;
        Vector3 target = baseScale * fireScaleUp;
        float elapsed = 0f;
        float half = fireScaleTime;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            f.localScale = Vector3.Lerp(baseScale, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        f.localScale = target;
        yield return new WaitForSeconds(Mathf.Max(0f, fireKeepTime - fireScaleTime));
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            f.localScale = Vector3.Lerp(target, baseScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        f.localScale = baseScale;
        fireScaleCoroutine = null;
    }

    float GetBarValue()
    {
        if (barFillImage != null) return (barFillImage.fillAmount * 100f);
        if (powerBar != null) return powerBar.value;
        return 0f;
    }

    void ApplyBarValue(float value, bool forceFullVisual)
    {
        value = Mathf.Clamp(value, 0f, 100f);

        if (powerBar != null) powerBar.value = value;
        if (barFillImage != null) barFillImage.fillAmount = value / 100f;
    }

    IEnumerator SmoothMove(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            t.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        t.position = to;
    }

    void StopFire()
    {
        if (fireEffect != null) fireEffect.SetActive(false);
    }

    IEnumerator FlashFillCoroutine()
    {
        if (fillFlashImage == null) yield break;
        Color baseCol = fillFlashImage.color;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float a = Mathf.PingPong(t * 8f, 1f);
            var c = baseCol; c.a = Mathf.Lerp(1f, 0.3f, a);
            fillFlashImage.color = c;
            yield return null;
        }
        fillFlashImage.color = baseCol;
    }

    void PlayIgnitionCue(bool play)
    {
        if (audioPlayer == null) return;
        if (!play)
        {
            if (clipIgnition != null) audioPlayer.PlayOneShot(clipIgnition, 0.7f);
        }
        else
        {
            if (clipIgnition != null) audioPlayer.PlayOneShot(clipIgnition, 1f);
        }
    }
    void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = $"High Score: {highScore} m";
    }
    void UpdateRoundsLeftUI()
    {
        if (roundsLeftText != null)
            roundsLeftText.text = $"Rounds Left: {Mathf.Max(totalRounds - currentRound, 0)}";
    }
    void SpawnStar()
    {
        if (starPrefab == null || starParent == null) return;

        if (currentStar != null)
        {
            Vector3 pos = currentStar.transform.localPosition;
            pos.y -= starMoveDistance;  
            currentStar.transform.localPosition = pos;
        }

        currentStar = Instantiate(starPrefab, starParent);
        currentStar.transform.localPosition = Vector3.zero; 
        currentStar.transform.localScale = Vector3.one;
    }

}
