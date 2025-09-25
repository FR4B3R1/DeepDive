using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    public float Current { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    private void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (Current <= 0f || amount <= 0f) return;

        Current = Mathf.Max(0f, Current - amount);
        OnHealthChanged?.Invoke(Current, maxHealth);

        if (Current <= 0f)
            OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        OnHealthChanged?.Invoke(Current, maxHealth);
    }
}

