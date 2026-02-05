using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Characters")]
    public GameObject Player_Hornet, Player_Pure, Opponent_Hornet, Opponent_Pure;
    public Animator Player_Hornet_Animator, Player_Pure_Animator, Opponent_Hornet_Animator, Opponent_Pure_Animator;

    [Header("UI")]
    public Button skipButton, attackButton;
    public TMP_Text roundText, myEnergyText, opponentEnergyText, myHp, opponentHp, myAttackText, opponentAttackText;
    public TMP_Text myDeckText, opponentDeckText;
    public Text turnInfoText;
    public GameObject ResultScreen;

    private NetworkRunner runner;
    private GameNetworkHandler networkHandler;
    private bool isInitialized = false;
    private int lastMyDeckIndex = -1, lastOpponentDeckIndex = -1;
    private Animator currentPlayerAnimator, currentOpponentAnimator;

    private void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (skipButton) skipButton.onClick.AddListener(OnSkipPressed);
        if (attackButton) attackButton.onClick.AddListener(OnAttackPressed);

        if (Player_Hornet) Player_Hornet.SetActive(false);
        if (Player_Pure) Player_Pure.SetActive(false);
        if (Opponent_Hornet) Opponent_Hornet.SetActive(false);
        if (Opponent_Pure) Opponent_Pure.SetActive(false);

        AutoAssignAnimators();
    }

    // gaseste automat animators daca nu sunt setati
    private void AutoAssignAnimators()
    {
        if (!Player_Hornet_Animator && Player_Hornet) Player_Hornet_Animator = Player_Hornet.GetComponent<Animator>();
        if (!Player_Pure_Animator && Player_Pure) Player_Pure_Animator = Player_Pure.GetComponent<Animator>();
        if (!Opponent_Hornet_Animator && Opponent_Hornet) Opponent_Hornet_Animator = Opponent_Hornet.GetComponent<Animator>();
        if (!Opponent_Pure_Animator && Opponent_Pure) Opponent_Pure_Animator = Opponent_Pure.GetComponent<Animator>();
    }

    private void Update()
    {
        if (!isInitialized && networkHandler == null)
        {
            networkHandler = FindObjectOfType<GameNetworkHandler>();
            if (networkHandler) isInitialized = true;
        }

        if (!networkHandler?.IsReady() == true) return;

        UpdateDeckSelections();
        if (runner && skipButton && attackButton)
        {
            UpdateButtonState();
            DisplayHp();
            DisplayAttack();
        }
    }

    // actualizeaza deck-urile si activeaza modelele corecte
    private void UpdateDeckSelections()
    {
        if (!networkHandler || !runner) return;

        int myDeck = networkHandler.GetPlayerDeckSelection(runner.LocalPlayer);
        if (myDeck != lastMyDeckIndex && myDeck >= 0)
        {
            lastMyDeckIndex = myDeck;
            if (myDeckText) myDeckText.text = $"My Deck: {GetDeckName(myDeck)}";

            if (myDeck == 0)
            {
                Player_Hornet?.SetActive(true);
                Player_Pure?.SetActive(false);
                currentPlayerAnimator = Player_Hornet_Animator;
            }
            else
            {
                Player_Hornet?.SetActive(false);
                Player_Pure?.SetActive(true);
                currentPlayerAnimator = Player_Pure_Animator;
            }
        }

        PlayerRef opponent = GetOpponentPlayerRef();
        if (opponent != PlayerRef.None)
        {
            int oppDeck = networkHandler.GetPlayerDeckSelection(opponent);
            if (oppDeck != lastOpponentDeckIndex && oppDeck >= 0)
            {
                lastOpponentDeckIndex = oppDeck;
                if (opponentDeckText) opponentDeckText.text = $"Opponent: {GetDeckName(oppDeck)}";

                if (oppDeck == 0)
                {
                    Opponent_Hornet?.SetActive(true);
                    Opponent_Pure?.SetActive(false);
                    currentOpponentAnimator = Opponent_Hornet_Animator;
                }
                else
                {
                    Opponent_Hornet?.SetActive(false);
                    Opponent_Pure?.SetActive(true);
                    currentOpponentAnimator = Opponent_Pure_Animator;
                }
            }
        }
    }

    // afiseaza HP
    private void DisplayHp()
    {
        if (!networkHandler || !runner) return;
        if (myHp) myHp.text = $"{networkHandler.GetPlayerStats(runner.LocalPlayer).HP}";

        PlayerRef opp = GetOpponentPlayerRef();
        if (opp != PlayerRef.None && opponentHp) opponentHp.text = $"{networkHandler.GetPlayerStats(opp).HP}";
    }

    // afiseaza attack cu bonus permanent
    private void DisplayAttack()
    {
        if (!networkHandler || !runner) return;

        if (myAttackText)
        {
            int baseAtk = networkHandler.GetPlayerStats(runner.LocalPlayer).Attack;
            int bonus = networkHandler.StatsManager.GetPermanentAttackBonus(runner.LocalPlayer);
            int total = baseAtk + bonus;
            myAttackText.text = bonus > 0 ? $"{total} <color=green>(+{bonus})</color>" : $"{total}";
        }

        PlayerRef opp = GetOpponentPlayerRef();
        if (opp != PlayerRef.None && opponentAttackText)
        {
            int baseAtk = networkHandler.GetPlayerStats(opp).Attack;
            int bonus = networkHandler.StatsManager.GetPermanentAttackBonus(opp);
            int total = baseAtk + bonus;
            opponentAttackText.text = bonus > 0 ? $"{total} <color=green>(+{bonus})</color>" : $"{total}";
        }
    }

    // afiseaza energia cu bonus per runda
    private void DisplayEnergyWithBonus()
    {
        if (!networkHandler || !runner) return;

        if (myEnergyText)
        {
            int energy = networkHandler.GetPlayerEnergy(runner.LocalPlayer);
            int bonus = networkHandler.StatsManager.GetEnergyPerRoundBonus(runner.LocalPlayer);
            myEnergyText.text = bonus > 0 ? $"{energy} <size=16><color=yellow>(+{bonus}/rnd)</color></size>" : $"{energy}";
        }

        PlayerRef opp = GetOpponentPlayerRef();
        if (opp != PlayerRef.None && opponentEnergyText)
        {
            int energy = networkHandler.GetPlayerEnergy(opp);
            int bonus = networkHandler.StatsManager.GetEnergyPerRoundBonus(opp);
            opponentEnergyText.text = bonus > 0 ? $"{energy} <size=16><color=yellow>(+{bonus}/rnd)</color></size>" : $"{energy}";
        }
    }

    // actualizeaza butoanele
    private void UpdateButtonState()
    {
        bool isMyTurn = networkHandler.IsPlayerTurn(runner.LocalPlayer);

        if (skipButton && skipButton.interactable != isMyTurn)
        {
            skipButton.interactable = isMyTurn;
            var text = skipButton.GetComponentInChildren<Text>();
            if (text) text.text = isMyTurn ? "SKIP" : "WAITING...";
        }

        if (attackButton) attackButton.interactable = isMyTurn;
        if (roundText) roundText.text = $"Round {networkHandler.CurrentRound}";
        DisplayEnergyWithBonus();
    }

    public void OnSkipPressed()
    {
        if (!runner || !networkHandler || !networkHandler.IsPlayerTurn(runner.LocalPlayer)) return;
        string role = runner.IsServer ? "HOST" : "CLIENT";
        networkHandler.RPC_SkipTurn(runner.LocalPlayer, role);
    }

    public void OnAttackPressed()
    {
        if (!runner || !networkHandler || !networkHandler.IsPlayerTurn(runner.LocalPlayer)) return;
        string role = runner.IsServer ? "HOST" : "CLIENT";
        int atk = networkHandler.GetPlayerStats(runner.LocalPlayer).Attack;
        networkHandler.RPC_AttackOpponent(runner.LocalPlayer, role, atk);
    }

    // afiseaza ecranul de game over
    public void ShowGameOver(string resultText)
    {
        Debug.Log($"[GameManager] ShowGameOver called: {resultText}");
        
        // Caută GameObject-ul ResultScreen în scenă dacă nu e setat
        if (ResultScreen == null)
        {
            ResultScreen = GameObject.Find("ResultScreen");
            if (ResultScreen == null)
            {
                Debug.LogError("[GameManager] ResultScreen GameObject not found in scene!");
                return;
            }
        }
        
        ResultScreen.SetActive(true);
        Debug.Log("[GameManager] ResultScreen activated!");
        
        // Caută TMP_Text pentru rezultat
        TMP_Text resultTextComponent = ResultScreen.GetComponentInChildren<TMP_Text>();
        if (resultTextComponent != null)
        {
            resultTextComponent.text = resultText;
            Debug.Log($"[GameManager] Result text set to: {resultText}");
        }
        else
        {
            Debug.LogError("[GameManager] TMP_Text component not found in ResultScreen!");
        }
        
        // Așteaptă 3 secunde apoi mergi la meniu
        StartCoroutine(ReturnToMenuAfterDelay(3f));
    }

    // așteaptă și încarcă meniul principal
    private System.Collections.IEnumerator ReturnToMenuAfterDelay(float delay)
    {
        Debug.Log($"[GameManager] Returning to menu in {delay} seconds...");
        yield return new WaitForSeconds(delay);
        
        Debug.Log("[GameManager] Loading main menu...");
        
        // Oprește runner-ul Photon Fusion
        if (runner != null)
        {
            runner.Shutdown();
        }
        
        // Încarcă scena de meniu (index 0)
        SceneManager.LoadScene(0);
    }

    // triggere animatii
    public void TriggerPlayerAttackAnimation() => PlayAnim(currentPlayerAnimator, "attack");
    public void TriggerPlayerDamageAnimation() => PlayAnim(currentPlayerAnimator, "damage");
    public void TriggerPlayerSkillAnimation() => PlayAnim(currentPlayerAnimator, "skill");
    public void TriggerOpponentAttackAnimation() => PlayAnim(currentOpponentAnimator, "attack");
    public void TriggerOpponentDamageAnimation() => PlayAnim(currentOpponentAnimator, "damage");
    public void TriggerOpponentSkillAnimation() => PlayAnim(currentOpponentAnimator, "skill");

    private void PlayAnim(Animator anim, string param)
    {
        if (anim) StartCoroutine(PlayAnimationAndReset(anim, param));
    }

    // joaca animatie si reseteaza
    private System.Collections.IEnumerator PlayAnimationAndReset(Animator animator, string parameterName)
    {
        animator.SetBool(parameterName, true);
        yield return new WaitForSeconds(GetAnimationLength(animator, parameterName));
        animator.SetBool(parameterName, false);
    }

    // gaseste durata animatiei
    private float GetAnimationLength(Animator animator, string animationName)
    {
        if (!animator?.runtimeAnimatorController) return 1f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name.ToLower().Contains(animationName.ToLower())) return clip.length;
        return 1f;
    }

    private PlayerRef GetOpponentPlayerRef()
    {
        if (!runner) return PlayerRef.None;
        foreach (var p in runner.ActivePlayers)
            if (p != runner.LocalPlayer) return p;
        return PlayerRef.None;
    }

    private string GetDeckName(int idx) => idx == 0 ? "Hornet" : idx == 1 ? "Pure Vessel" : $"Deck {idx}";
}