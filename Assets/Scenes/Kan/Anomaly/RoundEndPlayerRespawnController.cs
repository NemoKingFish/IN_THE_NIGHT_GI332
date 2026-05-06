using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RoundEndPlayerRespawnController : MonoBehaviour
{
    [SerializeField] private GameRoundManager gameRoundManager;
    [SerializeField] private PhotonScenePlayerSpawnManager spawnManager;
    [SerializeField] private bool debugLogRespawns;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeHoldDuration = 0.05f;
    [SerializeField] private float fadeInDuration = 0.35f;

    private Canvas fadeCanvas;
    private CanvasGroup fadeCanvasGroup;
    private Image fadeImage;
    private Coroutine respawnFadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RoundEndPlayerRespawnController>() != null)
        {
            return;
        }

        if (FindFirstObjectByType<GameRoundManager>() == null)
        {
            return;
        }

        var bootstrapObject = new GameObject("RoundEndPlayerRespawnController");
        bootstrapObject.AddComponent<RoundEndPlayerRespawnController>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (gameRoundManager == null)
        {
            gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        }

        if (spawnManager == null)
        {
            spawnManager = FindFirstObjectByType<PhotonScenePlayerSpawnManager>();
        }

        EnsureFadeOverlay();
    }

    private void Subscribe()
    {
        if (gameRoundManager != null)
        {
            gameRoundManager.gamePhase.OnValueChanged -= HandlePhaseChanged;
            gameRoundManager.gamePhase.OnValueChanged += HandlePhaseChanged;
        }
    }

    private void Unsubscribe()
    {
        if (gameRoundManager != null)
        {
            gameRoundManager.gamePhase.OnValueChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(int previousPhaseValue, int nextPhaseValue)
    {
        if (gameRoundManager == null)
        {
            return;
        }

        var previousPhase = (GameRoundManager.GamePhase)previousPhaseValue;
        var nextPhase = (GameRoundManager.GamePhase)nextPhaseValue;
        if (previousPhase != GameRoundManager.GamePhase.RoundTransition || nextPhase != GameRoundManager.GamePhase.Memorize)
        {
            return;
        }

        if (gameRoundManager.currentRound.Value <= 0)
        {
            return;
        }

        ResolveReferences();
        if (spawnManager == null)
        {
            return;
        }

        if (respawnFadeRoutine != null)
        {
            StopCoroutine(respawnFadeRoutine);
        }

        respawnFadeRoutine = StartCoroutine(FadeAndRespawnRoutine());
    }

    private System.Collections.IEnumerator FadeAndRespawnRoutine()
    {
        EnsureFadeOverlay();
        if (fadeCanvasGroup == null)
        {
            spawnManager.TeleportAllPlayersToAssignedSpawnPads();
            yield break;
        }

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.interactable = false;

        yield return FadeCanvasGroup(0f, 1f, Mathf.Max(0f, fadeOutDuration));

        spawnManager.TeleportAllPlayersToAssignedSpawnPads();
        if (debugLogRespawns)
        {
            Debug.Log($"[RoundEndPlayerRespawnController] Teleported all players to assigned spawn pads for round {gameRoundManager.currentRound.Value}.", this);
        }

        if (fadeHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(fadeHoldDuration);
        }

        yield return FadeCanvasGroup(1f, 0f, Mathf.Max(0f, fadeInDuration));

        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvas.gameObject.SetActive(false);
        respawnFadeRoutine = null;
    }

    private System.Collections.IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha = endAlpha;
            yield break;
        }

        fadeCanvasGroup.alpha = startAlpha;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = endAlpha;
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvasGroup != null && fadeCanvas != null && fadeImage != null)
        {
            return;
        }

        var existingCanvasGroup = FindFirstObjectByType<CanvasGroup>(FindObjectsInactive.Include);
        if (existingCanvasGroup != null && existingCanvasGroup.name == "RoundRespawnFadeOverlay")
        {
            fadeCanvasGroup = existingCanvasGroup;
            fadeCanvas = existingCanvasGroup.GetComponent<Canvas>();
            fadeImage = existingCanvasGroup.GetComponent<Image>();
        }

        if (fadeCanvasGroup != null && fadeCanvas != null && fadeImage != null)
        {
            return;
        }

        var overlayObject = new GameObject("RoundRespawnFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        overlayObject.transform.SetParent(null, false);

        fadeCanvas = overlayObject.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = short.MaxValue;

        var rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        fadeCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;

        fadeImage = overlayObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;

        overlayObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (fadeCanvas != null)
        {
            Destroy(fadeCanvas.gameObject);
        }
    }
}
