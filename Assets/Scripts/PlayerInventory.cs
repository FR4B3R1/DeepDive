using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine.SceneManagement;

public class PlayerInventory : MonoBehaviour
{
    [Header("Valori")]
    public double currentMoney = 0;      // Denaro potenziale in mano (si azzera alla vendita/morte)
    public int forzieriCollected = 0;
    public int cristalliCollected = 0;
    public int spadeCollected = 0;
    public int legnoCollected = 0;
    private double earnedMoney = 0;      // Denaro effettivamente guadagnato (persistente)
    public int targetMoney = 50000;    // Obiettivo di denaro per vincere

    [Header("UI (assegna da Inspector)")]
    [SerializeField] private TMP_Text soldiText;        // Mostra currentMoney
    [SerializeField] private TMP_Text forzieriText;
    [SerializeField] private TMP_Text cristalliText;
    [SerializeField] private TMP_Text spadeText;
    [SerializeField] private TMP_Text legnoText;
    [SerializeField] private TMP_Text earnedMoneyText;  // <<< NUOVO: mostra earnedMoney

    [SerializeField] private TMP_Text earnedMoneyText_overlay;      // mostra sempre glie arned money in overlay
    [SerializeField] private TMP_Text targetMoneyText_overlay;      // mostra il target money in overlay
    [SerializeField] private TMP_Text actualMoneyInInventory_overlay; // mostra il current money in overlay

    [Header("Riferimenti UI")]
    [SerializeField] private Button vendiButton;        // <<< Opzionale: assegna il tuo bottone "Vendi"
    [SerializeField] private GameObject minimap;    // riferimento alla minimappa

    Collectible collectible;

    // private CultureInfo it = new CultureInfo("it-IT");

    private void Awake()
    {
        // Collega il click del bottone al metodo, se assegnato
        if (vendiButton != null)
            vendiButton.onClick.AddListener(SellAllTreasures);
    }

    private void OnDestroy()
    {
        if (vendiButton != null)
            vendiButton.onClick.RemoveListener(SellAllTreasures);
    }

    private void Start()
    {
        // Valori iniziali (se servono)
        currentMoney = 0;
        forzieriCollected = 0;
        cristalliCollected = 0;
        spadeCollected = 0;
        legnoCollected = 0;


        actualMoneyInInventory_overlay.text = $"In Inventory:  0";
        targetMoneyText_overlay.text = $"Target:  {targetMoney}";
        earnedMoneyText_overlay.text = $"Current:  0";

        AggiornaUI();
    }

    private void Update()
    {
        // Aggiorna sempre il testo del totale guadagnato in overlay
        if (earnedMoneyText_overlay)
            earnedMoneyText_overlay.text = $"Current: {earnedMoney}";
    }

    public void AddMoney(Collider2D other)
    {
        collectible = other.GetComponent<Collectible>();

        if (other == null) return;
        if (!other.TryGetComponent(out Collectible c)) return;

        currentMoney += collectible.GetValue();

        if (other.CompareTag("Forziere")) forzieriCollected++;
        else if (other.CompareTag("Cristallo")) cristalliCollected++;
        else if (other.CompareTag("Spada")) spadeCollected++;
        else if (other.CompareTag("Legno")) legnoCollected++;

        AggiornaUI();
    }

    public void SellAllTreasures()
    {
        // Somma il denaro in mano al totale guadagnato
        earnedMoney += currentMoney;
        if(earnedMoney >= targetMoney)
        {
            Time.timeScale = 1f;
            SellMenu sell = GetComponent<SellMenu>();
            if (sell != null)
            {
                sell.CloseMenu(); // Chiudi il menu di vendita se aperto
            }
            SceneManager.LoadScene("WinScene");
            return;
        }

        // Azzera il denaro in mano e i contatori degli oggetti
        currentMoney = 0;
        forzieriCollected = 0;
        cristalliCollected = 0;
        spadeCollected = 0;
        legnoCollected = 0;

        // Aggiorna SUBITO la UI
        AggiornaUI();

        PlayerBehaviour player = GetComponent<PlayerBehaviour>();
        if (player != null)
        {
            player.ResetWeight(); // Resetta il peso trasportato
        }

        SellMenu menu = GetComponent<SellMenu>();
        if (menu != null)
        {
            menu.CloseMenu(); // Chiudi il menu di vendita se aperto

        }

        Debug.Log($"Totale venduto: {earnedMoney}");
    }

    public void ResetInventory()
    {
        currentMoney = 0;
        forzieriCollected = 0;
        cristalliCollected = 0;
        spadeCollected = 0;
        legnoCollected = 0;
        earnedMoney = 0;
        AggiornaUI();
    }

    private void AggiornaUI()
    {
        if (soldiText) soldiText.text = $"Valore Oggetti: {currentMoney}";  
        if (forzieriText) forzieriText.text = $"Forzieri: {forzieriCollected}";
        if (cristalliText) cristalliText.text = $"Cristalli: {cristalliCollected}";
        if (spadeText) spadeText.text = $"Spade: {spadeCollected}";
        if (legnoText) legnoText.text = $"Legno: {legnoCollected}";
        if (earnedMoneyText) earnedMoneyText.text = $"Totale Guadagnato: {earnedMoney}";
        if (actualMoneyInInventory_overlay) actualMoneyInInventory_overlay.text = $"In Inventory: {currentMoney}";
    }

    public void SetOverlayVisible(bool visible)
    {
        if (earnedMoneyText_overlay) earnedMoneyText_overlay.gameObject.SetActive(visible);
        if (targetMoneyText_overlay) targetMoneyText_overlay.gameObject.SetActive(visible);
        if (actualMoneyInInventory_overlay) actualMoneyInInventory_overlay.gameObject.SetActive(visible);
        if (minimap) minimap.SetActive(visible);
    }

}
