using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player UI")]
    public TMP_Text[] playerTexts = new TMP_Text[6];

    [Header("Combined UI")]
    public TMP_Text combinedText;
    public TMP_Text distanceText;
    public TMP_Text countdownText;
    public GameObject timerBlock;

    [Header("Road & Cyclist")]
    public RectTransform road;
    public RectTransform cyclist;
    public float cyclistStartOffset = 0f;
    public bool fillRoad;
    public Image roadFillImage;
    [Tooltip("Fill offset at start (0-1)")]
    public float roadFillStartOffset = 0f;

    [Header("Round Settings")]
    public float roundDuration = 30f;
    public float targetDistance = 100000f; // 100km in meters
    public int countdownSeconds = 3;

    [Header("Milestones")]
    public MilestoneEntry[] milestones;
    public ParticleSystem milestoneRevealEffect;
    public float milestoneRevealScale = 3f;
    public float milestoneHoldDuration = 1f;
    public float milestoneTransitionDuration = 0.5f;
    public float lastMilestoneScale = 2f;
    public float messageBoxFadeDuration = 0.5f;

    [Header("Activity Pacing")]
    public float activityDurationMinutes = 180f;
    public float totalTargetDistanceKm = 100f;
    public int pacingSessions = 5;
    public float lastSessionCheckIntervalMinutes = 5f;
    [Tooltip("Min/max multiplier for wheel circumference adjustment")]
    public float minCircumferenceMultiplier = 0.5f;
    public float maxCircumferenceMultiplier = 2.0f;

    [Header("Thank You Screen")]
    public GameObject thankYouScreen;
    public float thankYouDuration = 5f;

    [Header("Result Display")]
    public GameObject resultVideo;

    [Header("Settings")]
    public float updateInterval = 0.1f;

    public bool IsRoundActive { get; private set; }

    private float _timer;
    private float _roundTimer;
    private float _roadStartX;
    private float _roadEndX;
    private float _lastTotalDist;
    private float _cumulativeDist;
    private Animator _cyclistAnimator;
    private TMP_Text _timerText;

    // Pacing state
    private bool _activityStarted;
    private float _activityStartTime;
    private float _baseWheelCircumference;
    private int _completedCheckpoints;
    private float _lastPaceCheckTime;
    private float _sessionDurationSec;
    private float _lastSessionStartDist;

    // Debug GUI
    private bool _showDebugGUI;
    private int _roundCount;

    // Team data
    private string _teamName = "";
    private Coroutine _resultCoroutine;
    private bool _finalMilestoneReached;
    private int _pendingReveals;
    private bool _roundEndedWaitingForReveals;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (road != null && cyclist != null)
        {
            _cyclistAnimator = cyclist.GetComponent<Animator>();
            float roadWidth = road.rect.width;
            float cyclistWidth = cyclist.rect.width;
            _roadStartX = -roadWidth / 2f + cyclistWidth / 2f + cyclistStartOffset;
            _roadEndX = roadWidth / 2f - cyclistWidth / 2f;
            cyclist.anchoredPosition = new Vector2(_roadStartX, cyclist.anchoredPosition.y);
        }

        if (fillRoad && roadFillImage != null)
        {
            //roadFillImage.color = new Color32(0xDE, 0x47, 0x45, 0xFF);
            roadFillImage.type = Image.Type.Filled;
            roadFillImage.fillMethod = Image.FillMethod.Horizontal;
            roadFillImage.fillAmount = roadFillStartOffset;
        }

        // Init milestones
        if (milestones != null)
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                milestones[i].Init();
                milestones[i].revealed = false;

                if (milestones[i].lockObject != null)
                {
                    milestones[i].lockObject.SetActive(true);
                    // Odd = #f79f00, Even = white (0-based: 0=odd, 1=even)
                    var lockImage = milestones[i].lockObject.GetComponent<Image>();
                    if (lockImage != null)
                        lockImage.color = (i % 2 == 0)
                            ? new Color32(0xF7, 0x9F, 0x00, 0xFF)
                            : Color.white;
                    var anim = milestones[i].lockObject.GetComponent<Animator>();
                    if (anim != null)
                        anim.enabled = false;
                    if (milestones[i].distanceText != null)
                        milestones[i].distanceText.gameObject.SetActive(true);
                }
                if (milestones[i].confetti != null)
                    milestones[i].confetti.gameObject.SetActive(false);
                if (milestones[i].messageBox != null)
                    milestones[i].messageBox.SetActive(false);
            }
        }

        SetMoving(false);
        UpdateDistanceText(0f);

        if (milestoneRevealEffect != null) milestoneRevealEffect.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (timerBlock != null)
        {
            _timerText = timerBlock.GetComponentInChildren<TMP_Text>();
            timerBlock.SetActive(false);
        }
        if (thankYouScreen != null) thankYouScreen.SetActive(false);
        if (resultVideo != null) resultVideo.SetActive(false);
    }

    public void OnStartGameReceived(string[] names)
    {
        if (IsRoundActive || _countdownRunning) return;

        // Hide any active screen immediately
        if (_resultCoroutine != null)
        {
            StopCoroutine(_resultCoroutine);
            _resultCoroutine = null;
        }
        if (thankYouScreen != null)
            thankYouScreen.SetActive(false);
        HideResult();

        // First value is team name, rest are player names
        _teamName = names.Length > 0 ? names[0] : "";
        if (CycleDataManager.Instance != null)
        {
            var players = CycleDataManager.Instance.Players;
            for (int i = 0; i < players.Length; i++)
                players[i].PlayerName = (i + 1 < names.Length) ? names[i + 1] : "";
        }

        // Start activity timer on first /start-game
        if (!_activityStarted)
        {
            _activityStarted = true;
            _activityStartTime = Time.time;
            _sessionDurationSec = (activityDurationMinutes * 60f) / pacingSessions;
            _completedCheckpoints = 0;
            _lastPaceCheckTime = Time.time;
            _lastSessionStartDist = 0f;

            if (CycleDataManager.Instance != null)
                _baseWheelCircumference = CycleDataManager.Instance.wheelCircumference;

            Debug.Log($"[Pacing] Activity started! Duration: {activityDurationMinutes}min, " +
                      $"Target: {totalTargetDistanceKm}km, Sessions: {pacingSessions}, " +
                      $"Session length: {_sessionDurationSec / 60f:F1}min");
        }

        StartCoroutine(CountdownAndStart());
    }

    private bool _countdownRunning;

    private IEnumerator CountdownAndStart()
    {
        _countdownRunning = true;

        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        for (int i = countdownSeconds; i > 0; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
        {
            countdownText.text = "<size=40%>GO!</size>";
            yield return new WaitForSeconds(0.5f);
            countdownText.gameObject.SetActive(false);
        }

        _countdownRunning = false;

        // Send 's' and start round
        if (OSCManager.Instance != null)
            OSCManager.Instance.SendSerialValue("s");

        StartRound();
    }

    public void StartRound()
    {
        if (IsRoundActive) return;

        _roundCount++;
        IsRoundActive = true;
        _roundTimer = roundDuration;
        _lastTotalDist = 0f;
        SetMoving(true);

        if (CycleDataManager.Instance != null)
            CycleDataManager.Instance.ResetAll();

        if (timerBlock != null)
        {
            timerBlock.SetActive(true);
            if (_timerText != null)
                _timerText.text = FormatTimer(roundDuration);
        }

        Debug.Log("[Game] Round started!");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
            _showDebugGUI = !_showDebugGUI;

        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        if (CycleDataManager.Instance == null) return;

        if (IsRoundActive)
        {
            _roundTimer -= Time.deltaTime;
            if (_roundTimer <= 0f)
            {
                _roundTimer = 0f;
                IsRoundActive = false;
                SetMoving(false);

                if (OSCManager.Instance != null)
                    OSCManager.Instance.SendSerialValue("f");

                if (timerBlock != null)
                    timerBlock.SetActive(false);

                _roundEndedWaitingForReveals = true;
                _resultCoroutine = StartCoroutine(WaitForRevealsAndShowThankYou());

                Debug.Log("[Game] Round ended!");
            }
            else if (_timerText != null)
            {
                _timerText.text = FormatTimer(_roundTimer);
            }
        }

        _timer += Time.deltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        var players = CycleDataManager.Instance.Players;

        // Per-player UI
        for (int i = 0; i < playerTexts.Length; i++)
        {
            if (playerTexts[i] == null || i >= players.Length) continue;

            var p = players[i];
            playerTexts[i].text =
                $"P{i + 1}\n" +
                $"Speed: {p.Speed:F1} m/s\n" +
                $"Top: {p.TopSpeed:F1} m/s\n" +
                $"Dist: {p.TotalDistance:F1} m\n" +
                $"Rotations: {p.TotalRotations}";
        }

        // Combined data (current round)
        float roundDist = 0f, totalSpeed = 0f, topSpeed = 0f;
        int totalRotations = 0, activeCount = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            roundDist += p.TotalDistance;
            totalSpeed += p.Speed;
            totalRotations += p.TotalRotations;
            if (p.TopSpeed > topSpeed) topSpeed = p.TopSpeed;
            if (p.Speed > 0f) activeCount++;
        }

        float totalDist = _cumulativeDist + roundDist;
        float avgSpeed = activeCount > 0 ? totalSpeed / activeCount : 0f;

        if (combinedText != null)
        {
            combinedText.text =
                $"Combined\n" +
                $"Total Dist: {totalDist:F1} m\n" +
                $"Avg Speed: {avgSpeed:F1} m/s\n" +
                $"Top Speed: {topSpeed:F1} m/s\n" +
                $"Rotations: {totalRotations}\n" +
                $"Active: {activeCount}/{players.Length}\n" +
                $"Time: {_roundTimer:F1}s";
        }

        UpdateDistanceText(totalDist);
        CheckMilestones(totalDist);
        CheckPacing(totalDist);

        // Cyclist position update
        _lastTotalDist = roundDist;

        float progress = Mathf.Clamp01(totalDist / targetDistance);
        float x = Mathf.Lerp(_roadStartX, _roadEndX, progress);
        if (cyclist != null)
            cyclist.anchoredPosition = new Vector2(x, cyclist.anchoredPosition.y);

        if (fillRoad && roadFillImage != null)
            roadFillImage.fillAmount = Mathf.Lerp(roadFillStartOffset, 1f, progress);

        // When round ends, add round distance to cumulative
        if (!IsRoundActive && roundDist > 0f)
        {
            _cumulativeDist += roundDist;
            CycleDataManager.Instance.ResetAll();
        }
    }

    private void CheckPacing(float totalDistMeters)
    {
        if (!_activityStarted || CycleDataManager.Instance == null) return;

        float elapsed = Time.time - _activityStartTime;
        float totalActivitySec = activityDurationMinutes * 60f;
        float targetDistMeters = totalTargetDistanceKm * 1000f;

        // Activity finished
        if (elapsed >= totalActivitySec)
            return;

        bool isLastSession = _completedCheckpoints >= (pacingSessions - 1);
        float checkInterval = isLastSession
            ? lastSessionCheckIntervalMinutes * 60f
            : _sessionDurationSec;

        float timeSinceLastCheck = Time.time - _lastPaceCheckTime;

        if (timeSinceLastCheck < checkInterval) return;

        // Time for a pace check
        _lastPaceCheckTime = Time.time;

        float remainingDist = targetDistMeters - totalDistMeters;
        float remainingTime = totalActivitySec - elapsed;

        if (remainingDist <= 0f || remainingTime <= 0f)
        {
            Debug.Log("[Pacing] Target distance reached!");
            return;
        }

        // Distance covered in this session/interval
        float sessionDist = totalDistMeters - _lastSessionStartDist;
        float expectedSessionDist = targetDistMeters / pacingSessions;

        if (!isLastSession)
            _completedCheckpoints++;

        // Calculate adjustment ratio
        // How much distance we need per second for the rest vs what we've been averaging
        float requiredRate = remainingDist / remainingTime; // m/s needed
        float currentRate = elapsed > 0f ? totalDistMeters / elapsed : 0f; // m/s actual

        float adjustment = 1f;
        if (currentRate > 0.001f)
        {
            adjustment = requiredRate / currentRate;
        }
        else
        {
            // No distance covered yet, increase circumference significantly
            adjustment = maxCircumferenceMultiplier / _baseWheelCircumference
                         * CycleDataManager.Instance.wheelCircumference;
        }

        // Clamp the adjustment
        float newCircumference = CycleDataManager.Instance.wheelCircumference * adjustment;
        float minCirc = _baseWheelCircumference * minCircumferenceMultiplier;
        float maxCirc = _baseWheelCircumference * maxCircumferenceMultiplier;
        newCircumference = Mathf.Clamp(newCircumference, minCirc, maxCirc);

        float oldCircumference = CycleDataManager.Instance.wheelCircumference;
        CycleDataManager.Instance.wheelCircumference = newCircumference;

        _lastSessionStartDist = totalDistMeters;

        string sessionType = isLastSession ? "Last session check" : $"Session {_completedCheckpoints}/{pacingSessions}";
        Debug.Log($"[Pacing] {sessionType} | " +
                  $"Elapsed: {elapsed / 60f:F1}min | " +
                  $"Distance: {totalDistMeters / 1000f:F2}km / {totalTargetDistanceKm:F0}km | " +
                  $"Session dist: {sessionDist / 1000f:F2}km (target: {expectedSessionDist / 1000f:F1}km) | " +
                  $"Circumference: {oldCircumference:F3} -> {newCircumference:F3}m | " +
                  $"Adjustment: {adjustment:F3}x");
    }

    private void UpdateDistanceText(float totalDistMeters)
    {
        if (distanceText == null) return;
        float km = totalDistMeters / 1000f;
        float targetKm = targetDistance / 1000f;
        distanceText.text = $"{km:F1}/{targetKm:F0} <size=50%>km</size>";
    }

    private void CheckMilestones(float totalDist)
    {
        if (milestones == null || _finalMilestoneReached) return;

        float totalDistKm = totalDist / 1000f;
        for (int i = 0; i < milestones.Length; i++)
        {
            if (milestones[i].revealed) continue;
            if (totalDistKm < milestones[i].unlockAtDistanceKm) continue;

            milestones[i].revealed = true;
            bool isLast = (i == milestones.Length - 1);

            if (isLast)
            {
                _finalMilestoneReached = true;
                StartCoroutine(HandleFinalMilestone(milestones[i]));
            }
            else
            {
                StartCoroutine(RevealMilestone(milestones[i]));
            }
        }
    }

    private IEnumerator RevealMilestone(MilestoneEntry milestone)
    {
        _pendingReveals++;

        if (milestone.lockObject != null)
        {
            var rt = milestone.lockObject.GetComponent<RectTransform>();
            Vector3 originalPos = rt != null ? rt.position : Vector3.zero;
            Vector3 originalScale = rt != null ? rt.localScale : Vector3.one;

            // Move to world center, scale up, hide distance text
            if (rt != null)
            {
                rt.position = Vector3.zero;
                rt.localScale = Vector3.one * milestoneRevealScale;
            }
            if (milestone.distanceText != null)
                milestone.distanceText.gameObject.SetActive(false);

            // Play common reveal effect, enable animator and play confetti
            if (milestoneRevealEffect != null)
            {
                milestoneRevealEffect.gameObject.SetActive(true);
                milestoneRevealEffect.Play();
            }
            var anim = milestone.lockObject.GetComponent<Animator>();
            if (anim != null)
                anim.enabled = true;
            if (milestone.confetti != null)
            {
                milestone.confetti.gameObject.SetActive(true);
                milestone.confetti.Play();
            }

            // Wait for animator to finish
            if (anim != null)
            {
                // Wait one frame for animator to start
                yield return null;
                var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                yield return new WaitForSeconds(stateInfo.length);
            }

            // Hold at center
            yield return new WaitForSeconds(milestoneHoldDuration);

            // Stop effects
            if (milestoneRevealEffect != null)
            {
                milestoneRevealEffect.Stop();
                milestoneRevealEffect.gameObject.SetActive(false);
            }
            if (milestone.confetti != null)
            {
                milestone.confetti.Stop();
                milestone.confetti.gameObject.SetActive(false);
            }

            // Smooth transition back to original position and scale
            if (rt != null)
            {
                Vector3 startPos = rt.position;
                Vector3 startScale = rt.localScale;
                float elapsed = 0f;
                while (elapsed < milestoneTransitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / milestoneTransitionDuration);
                    rt.position = Vector3.Lerp(startPos, originalPos, t);
                    rt.localScale = Vector3.Lerp(startScale, originalScale, t);
                    yield return null;
                }
                rt.position = originalPos;
                rt.localScale = originalScale;
            }

            // Show distance text
            if (milestone.distanceText != null)
                milestone.distanceText.gameObject.SetActive(true);
        }

        // Fade in messageBox
        if (milestone.messageBox != null)
            yield return FadeInMessageBox(milestone.messageBox);

        _pendingReveals--;
    }

    private IEnumerator HandleFinalMilestone(MilestoneEntry milestone)
    {
        // Stop processing cycle data
        IsRoundActive = false;
        SetMoving(false);

        if (OSCManager.Instance != null)
            OSCManager.Instance.SendSerialValue("f");
        if (timerBlock != null)
            timerBlock.SetActive(false);

        // Move lock to center and scale up
        if (milestone.lockObject != null)
        {
            var rt = milestone.lockObject.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.position = Vector3.zero;
                rt.localScale = Vector3.one * lastMilestoneScale;
            }
            if (milestone.distanceText != null)
                milestone.distanceText.gameObject.SetActive(false);

            if (milestoneRevealEffect != null)
            {
                milestoneRevealEffect.gameObject.SetActive(true);
                milestoneRevealEffect.Play();
            }

            var anim = milestone.lockObject.GetComponent<Animator>();
            if (anim != null)
                anim.enabled = true;
        }

        yield return new WaitForSeconds(5f);

        if (milestoneRevealEffect != null)
        {
            milestoneRevealEffect.Stop();
            milestoneRevealEffect.gameObject.SetActive(false);
        }

        // Show congratulation video
        _resultCoroutine = StartCoroutine(ShowResult());
    }

    private IEnumerator WaitForRevealsAndShowThankYou()
    {
        // Wait for any pending milestone reveals to finish
        while (_pendingReveals > 0)
            yield return null;

        _roundEndedWaitingForReveals = false;

        yield return new WaitForSeconds(1f);

        yield return ShowThankYou();
    }

    private IEnumerator FadeInMessageBox(GameObject messageBox)
    {
        messageBox.SetActive(true);
        var cg = messageBox.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = messageBox.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < messageBoxFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / messageBoxFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private string FormatTimer(float time)
    {
        int sec = (int)time;
        int ms = (int)((time - sec) * 100f);
        return $"{sec:D2} : {ms:D2}";
    }

    private IEnumerator ShowThankYou()
    {
        if (thankYouScreen == null) yield break;

        thankYouScreen.SetActive(true);
        yield return new WaitForSeconds(thankYouDuration);
        thankYouScreen.SetActive(false);
        _resultCoroutine = null;
    }

    private IEnumerator ShowResult()
    {
        VideoPlayer vp = null;

        if (resultVideo != null)
        {
            resultVideo.SetActive(true);
            vp = resultVideo.GetComponent<VideoPlayer>();
            if (vp != null)
                vp.Play();
        }

        // Wait for video to finish
        if (vp != null)
        {
            while (vp.isPlaying)
                yield return null;
        }

        HideResult();
        _resultCoroutine = null;
    }

    private void HideResult()
    {
        if (resultVideo != null)
        {
            var vp = resultVideo.GetComponent<VideoPlayer>();
            if (vp != null)
                vp.Stop();
            resultVideo.SetActive(false);
        }
    }

    private void OnGUI()
    {
        if (!_showDebugGUI) return;

        float w = 340f, h = 220f;
        Rect box = new Rect(10f, 10f, w, h);
        GUI.Box(box, "");

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        GUIStyle headerStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, fontSize = 18 };

        float x = 20f, y = 16f, lineH = 24f;

        // Activity countdown
        float totalSec = activityDurationMinutes * 60f;
        float elapsed = _activityStarted ? Time.time - _activityStartTime : 0f;
        float remaining = Mathf.Max(0f, totalSec - elapsed);
        int hrs = (int)(remaining / 3600f);
        int mins = (int)((remaining % 3600f) / 60f);
        int secs = (int)(remaining % 60f);

        GUI.Label(new Rect(x, y, w, lineH), "Activity Pacing", headerStyle);
        y += lineH + 4f;
        GUI.Label(new Rect(x, y, w, lineH), $"Time Remaining:  <b>{hrs:D2}:{mins:D2}:{secs:D2}</b>", labelStyle);
        y += lineH;

        // Session & adjustment
        int currentSession = _completedCheckpoints + (_activityStarted ? 1 : 0);
        float circumference = CycleDataManager.Instance != null
            ? CycleDataManager.Instance.wheelCircumference
            : _baseWheelCircumference;
        float multiplier = _baseWheelCircumference > 0f
            ? circumference / _baseWheelCircumference
            : 1f;

        GUI.Label(new Rect(x, y, w, lineH), $"Session:  <b>{currentSession} / {pacingSessions}</b>", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Circumference:  <b>{circumference:F3} m</b>  ({multiplier:F2}x)", labelStyle);
        y += lineH;

        // Distance progress
        float totalDist = _cumulativeDist;
        if (CycleDataManager.Instance != null)
        {
            var players = CycleDataManager.Instance.Players;
            for (int i = 0; i < players.Length; i++)
                totalDist += players[i].TotalDistance;
        }
        float distKm = totalDist / 1000f;
        GUI.Label(new Rect(x, y, w, lineH), $"Distance:  <b>{distKm:F2} / {totalTargetDistanceKm:F0} km</b>", labelStyle);
        y += lineH + 4f;

        // Round info
        GUI.Label(new Rect(x, y, w, lineH), "Round", headerStyle);
        y += lineH + 4f;
        string status = IsRoundActive ? $"Active  ({Mathf.CeilToInt(_roundTimer)}s left)" : "Idle";
        GUI.Label(new Rect(x, y, w, lineH), $"Round #:  <b>{_roundCount}</b>    {status}", labelStyle);
    }

    private void SetMoving(bool moving)
    {
        if (_cyclistAnimator != null)
            _cyclistAnimator.enabled = moving;
    }
}

[Serializable]
public class MilestoneEntry
{
    public float unlockAtDistanceKm;
    public GameObject milestoneObject;

    // Auto-resolved from milestoneObject children
    [HideInInspector] public GameObject lockObject;
    [HideInInspector] public TMP_Text distanceText;
    [HideInInspector] public ParticleSystem confetti;
    [HideInInspector] public GameObject messageBox;
    [HideInInspector] public TMP_Text messageText;
    [HideInInspector] public bool revealed;

    public void Init()
    {
        if (milestoneObject == null) return;
        Transform t = milestoneObject.transform;

        // lockObject is first child, messageBox is second child
        if (t.childCount > 0)
        {
            lockObject = t.GetChild(0).gameObject;
            distanceText = lockObject.GetComponentInChildren<TMP_Text>();
            confetti = lockObject.GetComponentInChildren<ParticleSystem>();
        }
        if (t.childCount > 1)
        {
            messageBox = t.GetChild(1).gameObject;
            messageText = messageBox.GetComponentInChildren<TMP_Text>();
        }
    }
}
