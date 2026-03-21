using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    public TMP_Text timerText;

    [Header("Road & Cyclist")]
    public RectTransform road;
    public RectTransform cyclist;
    public float cyclistStartOffset = 0f;

    [Header("Round Settings")]
    public float roundDuration = 30f;
    public float targetDistance = 100000f; // 100km in meters
    public int countdownSeconds = 3;

    [Header("Gifts")]
    public GiftEntry[] gifts;
    public float giftShakeDuration = 1f;
    public float giftShakeIntensity = 15f;
    public float giftConfettiDuration = 1f;
    public float giftOpenDuration = 0.6f;
    public float giftOpenTopMoveY = 150f;
    public float giftOpenTopRotation = 15f;
    public float giftOpenBottomMoveY = -100f;
    public float giftTypingSpeed = 0.05f;

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

        // Init and hide gifts
        if (gifts != null)
        {
            for (int i = 0; i < gifts.Length; i++)
            {
                gifts[i].Init();
                gifts[i].revealed = false;
                if (gifts[i].giftImage != null) gifts[i].giftImage.gameObject.SetActive(false);
                if (gifts[i].giftText != null) gifts[i].giftText.gameObject.SetActive(false);
                if (gifts[i].giftWrap != null) gifts[i].giftWrap.gameObject.SetActive(true);
                if (gifts[i].confetti != null) gifts[i].confetti.gameObject.SetActive(false);
            }
        }

        SetMoving(false);
        UpdateDistanceText(0f);

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (timerText != null) timerText.text = "";
    }

    public void OnStartGameReceived()
    {
        if (IsRoundActive || _countdownRunning) return;
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

        IsRoundActive = true;
        _roundTimer = roundDuration;
        _lastTotalDist = 0f;

        if (CycleDataManager.Instance != null)
            CycleDataManager.Instance.ResetAll();

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = Mathf.CeilToInt(roundDuration).ToString();
        }

        Debug.Log("[Game] Round started!");
    }

    private void Update()
    {

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

                if (timerText != null)
                    timerText.gameObject.SetActive(false);

                Debug.Log("[Game] Round ended!");
            }
            else if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(_roundTimer).ToString();
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
        CheckGifts(totalDist);

        // Cyclist movement & animation
        if (IsRoundActive)
        {
            bool moving = roundDist > _lastTotalDist;
            _lastTotalDist = roundDist;
            SetMoving(moving);
        }

        float progress = Mathf.Clamp01(totalDist / targetDistance);
        float x = Mathf.Lerp(_roadStartX, _roadEndX, progress);
        if (cyclist != null)
            cyclist.anchoredPosition = new Vector2(x, cyclist.anchoredPosition.y);

        // When round ends, add round distance to cumulative
        if (!IsRoundActive && roundDist > 0f)
        {
            _cumulativeDist += roundDist;
            CycleDataManager.Instance.ResetAll();
        }
    }

    private void UpdateDistanceText(float totalDistMeters)
    {
        if (distanceText == null) return;
        float km = totalDistMeters / 1000f;
        float targetKm = targetDistance / 1000f;
        distanceText.text = $"{km:F1}/{targetKm:F0} <size=50%>km</size>";
    }

    private void CheckGifts(float totalDist)
    {
        if (gifts == null) return;  

        float totalDistKm = totalDist / 1000f;
        for (int i = 0; i < gifts.Length; i++)
        {
            if (gifts[i].revealed) continue;
            if (totalDistKm >= gifts[i].revealAtDistanceKm)
            {
                gifts[i].revealed = true;
                StartCoroutine(RevealGift(gifts[i]));
            }
        }
    }

    private IEnumerator RevealGift(GiftEntry gift)
    {
        // Show gift wrap and shake it
        if (gift.giftWrap != null)
        {
            gift.giftWrap.gameObject.SetActive(true);
            Vector3 originalPos = gift.giftWrap.transform.localPosition;
            Quaternion originalRot = gift.giftWrap.transform.localRotation;

            float elapsed = 0f;
            while (elapsed < giftShakeDuration)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Sin(elapsed * 30f) * giftShakeIntensity * (1f - elapsed / giftShakeDuration);
                gift.giftWrap.transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            gift.giftWrap.transform.localRotation = originalRot;
        }

        // Show reveal image behind before opening
        if (gift.giftImage != null)
            gift.giftImage.gameObject.SetActive(true);

        // Open gift box - disable wrap image, animate top and bottom with confetti
        if (gift.giftWrap != null)
        {
            gift.giftWrap.enabled = false;

            Transform topCover = gift.giftWrap.transform.Find("TopCover");
            Transform bottomBox = gift.giftWrap.transform.Find("BottomBox");

            Vector3 topStart = topCover != null ? topCover.localPosition : Vector3.zero;
            Quaternion topRotStart = topCover != null ? topCover.localRotation : Quaternion.identity;
            Vector3 bottomStart = bottomBox != null ? bottomBox.localPosition : Vector3.zero;

            // Play confetti during opening
            if (gift.confetti != null)
            {
                gift.confetti.gameObject.SetActive(true);
                gift.confetti.Play();
            }

            float elapsed = 0f;
            while (elapsed < giftOpenDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / giftOpenDuration);
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                if (topCover != null)
                {
                    topCover.localPosition = topStart + Vector3.up * giftOpenTopMoveY * ease;
                    topCover.localRotation = topRotStart * Quaternion.Euler(0f, 0f, giftOpenTopRotation * ease);
                }
                if (bottomBox != null)
                {
                    bottomBox.localPosition = bottomStart + Vector3.up * giftOpenBottomMoveY * ease;
                }

                yield return null;
            }

            if (topCover != null) topCover.gameObject.SetActive(false);
            if (bottomBox != null) bottomBox.gameObject.SetActive(false);
        }

        // Typing text
        if (gift.giftText != null)
        {
            gift.giftText.gameObject.SetActive(true);
            string fullText = gift.giftText.text;
            gift.giftText.text = "";

            for (int c = 0; c < fullText.Length; c++)
            {
                gift.giftText.text = fullText.Substring(0, c + 1);
                yield return new WaitForSeconds(giftTypingSpeed);
            }
        }
    }

    private void SetMoving(bool moving)
    {
        if (_cyclistAnimator != null)
            _cyclistAnimator.enabled = moving;
    }
}

[Serializable]
public class GiftEntry
{
    public float revealAtDistanceKm;
    public GameObject giftObject;
    [HideInInspector] public bool revealed;
    [HideInInspector] public Image giftWrap;
    [HideInInspector] public RectTransform topCover;
    [HideInInspector] public RectTransform bottomBox;
    [HideInInspector] public ParticleSystem confetti;
    [HideInInspector] public Image giftImage;
    [HideInInspector] public TMP_Text giftText;

    public void Init()
    {
        if (giftObject == null) return;
        Transform t = giftObject.transform;
        giftImage = t.Find("Reveal").GetComponent<Image>();
        giftText = t.Find("Message").GetComponent<TMP_Text>();
        Transform wrapT = t.Find("GiftWrap");
        giftWrap = wrapT.GetComponent<Image>();
        topCover = wrapT.Find("TopCover") as RectTransform;
        bottomBox = wrapT.Find("BottomBox") as RectTransform;
        confetti = t.Find("Confetti").GetComponent<ParticleSystem>();
    }
}
