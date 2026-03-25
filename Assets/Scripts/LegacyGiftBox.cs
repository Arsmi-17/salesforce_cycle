using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LegacyGiftBox : MonoBehaviour
{
    public float revealAtDistanceKm;

    [Header("Animation Settings")]
    public float shakeDuration = 1f;
    public float shakeIntensity = 15f;
    public float confettiDuration = 1f;
    public float openDuration = 0.6f;
    public float openTopMoveY = 150f;
    public float openTopRotation = 15f;
    public float openBottomMoveY = -100f;
    public float typingSpeed = 0.05f;

    private bool _revealed;
    private Image _giftWrap;
    private RectTransform _topCover;
    private RectTransform _bottomBox;
    private ParticleSystem _confetti;
    private Image _giftImage;
    private TMP_Text _giftText;

    private void Start()
    {
        Transform t = transform;
        _giftImage = t.Find("Reveal").GetComponent<Image>();
        _giftText = t.Find("Message").GetComponent<TMP_Text>();
        Transform wrapT = t.Find("GiftWrap");
        _giftWrap = wrapT.GetComponent<Image>();
        _topCover = wrapT.Find("TopCover") as RectTransform;
        _bottomBox = wrapT.Find("BottomBox") as RectTransform;
        _confetti = t.Find("Confetti").GetComponent<ParticleSystem>();

        if (_giftImage != null) _giftImage.gameObject.SetActive(false);
        if (_giftText != null) _giftText.gameObject.SetActive(false);
        if (_giftWrap != null) _giftWrap.gameObject.SetActive(true);
        if (_confetti != null) _confetti.gameObject.SetActive(false);
    }

    public void TryReveal(float totalDistKm)
    {
        if (_revealed) return;
        if (totalDistKm < revealAtDistanceKm) return;

        _revealed = true;
        StartCoroutine(RevealGift());
    }

    private IEnumerator RevealGift()
    {
        // Shake
        if (_giftWrap != null)
        {
            _giftWrap.gameObject.SetActive(true);
            Quaternion originalRot = _giftWrap.transform.localRotation;

            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Sin(elapsed * 30f) * shakeIntensity * (1f - elapsed / shakeDuration);
                _giftWrap.transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            _giftWrap.transform.localRotation = originalRot;
        }

        // Show reveal image
        if (_giftImage != null)
            _giftImage.gameObject.SetActive(true);

        // Open gift box
        if (_giftWrap != null)
        {
            _giftWrap.enabled = false;

            Transform topCover = _giftWrap.transform.Find("TopCover");
            Transform bottomBox = _giftWrap.transform.Find("BottomBox");

            Vector3 topStart = topCover != null ? topCover.localPosition : Vector3.zero;
            Quaternion topRotStart = topCover != null ? topCover.localRotation : Quaternion.identity;
            Vector3 bottomStart = bottomBox != null ? bottomBox.localPosition : Vector3.zero;

            if (_confetti != null)
            {
                _confetti.gameObject.SetActive(true);
                _confetti.Play();
            }

            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                if (topCover != null)
                {
                    topCover.localPosition = topStart + Vector3.up * openTopMoveY * ease;
                    topCover.localRotation = topRotStart * Quaternion.Euler(0f, 0f, openTopRotation * ease);
                }
                if (bottomBox != null)
                {
                    bottomBox.localPosition = bottomStart + Vector3.up * openBottomMoveY * ease;
                }

                yield return null;
            }

            if (topCover != null) topCover.gameObject.SetActive(false);
            if (bottomBox != null) bottomBox.gameObject.SetActive(false);
        }

        // Typing text
        if (_giftText != null)
        {
            _giftText.gameObject.SetActive(true);
            string fullText = _giftText.text;
            _giftText.text = "";

            for (int c = 0; c < fullText.Length; c++)
            {
                _giftText.text = fullText.Substring(0, c + 1);
                yield return new WaitForSeconds(typingSpeed);
            }
        }
    }
}
