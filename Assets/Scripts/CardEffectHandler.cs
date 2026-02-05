using Fusion;
using UnityEngine;
using Vuforia;

public class CardEffectHandler : MonoBehaviour
{
    public enum CardSpeed
    {
        Slow,  // termina tura
        Fast,  // poate ataca/skip
        Burst  // poate orice
    }

    [Header("Card Config")]
    public string cardID;
    public string cardName = "Card";
    public int characterOwner = 0; // 0=Hornet, 1=Pure Vessel
    public CardSpeed cardSpeed = CardSpeed.Fast;
    [Range(0, 20)] public int energyCost = 1;

    [Header("Effects")]
    public int playerHPChange = 0;
    public int opponentHPChange = 0;
    public int playerEnergyChange = 0;
    public int opponentEnergyChange = 0;

    [Header("Settings")]
    public float cooldownDuration = 2f;

    [Header("Audio")]
    public AudioClip cardActivationSound;
    public AudioClip cardRejectedSound;
    public AudioClip notEnoughEnergySound;

    private NetworkRunner runner;
    private GameNetworkHandler networkHandler;
    private DefaultObserverEventHandler observerEventHandler;
    private AudioSource audioSource;
    private float lastUsedTime = -999f;
    private bool isTracking = false;

    private void Start()
    {
        observerEventHandler = GetComponent<DefaultObserverEventHandler>();
        if (!observerEventHandler)
        {
            Debug.LogError($"[{cardName}] DefaultObserverEventHandler not found!");
            return;
        }

        observerEventHandler.OnTargetFound.AddListener(OnCardDetected);
        observerEventHandler.OnTargetLost.AddListener(OnCardLost);

        runner = FindObjectOfType<NetworkRunner>();

        audioSource = GetComponent<AudioSource>();
        if (!audioSource && (cardActivationSound || cardRejectedSound || notEnoughEnergySound))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (!networkHandler && runner)
        {
            networkHandler = FindObjectOfType<GameNetworkHandler>();
        }
    }

    // detectie carte
    private void OnCardDetected()
    {
        isTracking = true;

        // cooldown
        if (Time.time - lastUsedTime < cooldownDuration) return;

        // verifica network
        if (!runner || !networkHandler || !networkHandler.IsReady()) return;

        // verifica ownership
        if (!CanLocalPlayerUseCard())
        {
            if (audioSource && cardRejectedSound) audioSource.PlayOneShot(cardRejectedSound);
            return;
        }

        // verifica energie
        if (!HasEnoughEnergy())
        {
            if (audioSource && notEnoughEnergySound) audioSource.PlayOneShot(notEnoughEnergySound);
            return;
        }

        ActivateCardEffect();
    }

    private void OnCardLost()
    {
        isTracking = false;
    }

    // verifica daca playerul are deck-ul corect
    private bool CanLocalPlayerUseCard()
    {
        PlayerRef localPlayer = runner.LocalPlayer;
        int playerDeck = networkHandler.GetPlayerDeckSelection(localPlayer);
        return playerDeck == characterOwner;
    }

    // verifica energie
    private bool HasEnoughEnergy()
    {
        PlayerRef localPlayer = runner.LocalPlayer;
        int currentEnergy = networkHandler.GetPlayerEnergy(localPlayer);
        return currentEnergy >= energyCost;
    }

    // activeaza efectul
    private void ActivateCardEffect()
    {
        PlayerRef localPlayer = runner.LocalPlayer;

        if (audioSource && cardActivationSound) audioSource.PlayOneShot(cardActivationSound);

        networkHandler.RPC_ApplyCardEffect(
            localPlayer, cardID, cardName,
            playerHPChange, opponentHPChange,
            playerEnergyChange, opponentEnergyChange,
            characterOwner, energyCost, (int)cardSpeed
        );

        lastUsedTime = Time.time;
    }

    private void OnDestroy()
    {
        if (observerEventHandler)
        {
            observerEventHandler.OnTargetFound.RemoveListener(OnCardDetected);
            observerEventHandler.OnTargetLost.RemoveListener(OnCardLost);
        }
    }

    // gizmos pentru visualizare
    private void OnDrawGizmos()
    {
        Gizmos.color = characterOwner == 0 ? Color.cyan : Color.magenta;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);

        Gizmos.color = GetSpeedColor(cardSpeed);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.03f);

        Gizmos.color = Color.yellow;
        float costSize = 0.02f + (energyCost * 0.005f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.08f, costSize);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = characterOwner == 0 ? Color.cyan : Color.magenta;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.15f);

#if UNITY_EDITOR
        string speedIcon = cardSpeed == CardSpeed.Slow ? "🐢" : cardSpeed == CardSpeed.Fast ? "⚡" : "💥";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.1f,
            $"{cardName}\n({GetCharacterName(characterOwner)})\nCost: {energyCost} ⚡\n{speedIcon} {cardSpeed}",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = GetSpeedColor(cardSpeed) },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            }
        );
#endif
    }

    private Color GetSpeedColor(CardSpeed speed)
    {
        switch (speed)
        {
            case CardSpeed.Slow: return Color.red;
            case CardSpeed.Fast: return Color.green;
            case CardSpeed.Burst: return Color.white;
            default: return Color.gray;
        }
    }

    private string GetCharacterName(int deckIndex)
    {
        if (deckIndex == 0) return "Hornet";
        if (deckIndex == 1) return "Pure Vessel";
        return "Unknown";
    }
}