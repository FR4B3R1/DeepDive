using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text lostText; // Assegna da Inspector
    [SerializeField] private PlayerInventory p_i;
    [SerializeField] private PlayerBehaviour p_b;
    public int damageAmount = 0;

    public float Current { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    private void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (Current <= 0f || amount <= 0f) return;

        damageAmount++;

        Current = Mathf.Max(0f, Current - amount);
        OnHealthChanged?.Invoke(Current, maxHealth);

        LoseInventoryOnDamage();

        if (Current <= 0f)
            OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    private void Update()
    {
        if (healthText != null)
            healthText.text = $"Health:  {Current} / {maxHealth}";
    }

    public void LoseInventoryOnDamage()
    {
        if (p_i != null && damageAmount == 2)
        {
            p_i.ResetInventory();
            damageAmount = 0;
            if (p_b != null)
                p_b.ResetWeight();


            if (lostText != null)
                StartCoroutine(ShowLostText());
        }
    }

    private IEnumerator ShowLostText()
    {
        lostText.gameObject.SetActive(true);
        CanvasGroup cg = lostText.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = lostText.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = 0f;

        // Fade-in
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            cg.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cg.alpha = 1f;

        // Wait
        yield return new WaitForSeconds(1.5f);

        // Fade-out
        elapsed = 0f;
        while (elapsed < duration)
        {
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cg.alpha = 0f;

        lostText.gameObject.SetActive(false);

    }
}