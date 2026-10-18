
using System;

public enum PowerUpKind { Speed, Shield, Heal }

// Per-run economy and combat state.
// Only completed runs can update the saved records.
public sealed class RunProgress
{
    public const int MaxHealth = 3;

    public int Coins { get; private set; }
    public int TotalCoins { get; private set; }
    public int Health { get; private set; } = MaxHealth;

    public float Elapsed { get; private set; }
    public float SpeedSeconds { get; private set; }
    public float ShieldSeconds { get; private set; }
    public float HurtSeconds { get; private set; }

    // Power-ups recuperados de la version antigua
    float speedFactor = 1.5f;

    public float DoubleJumpSeconds { get; private set; }
    public bool HasDoubleJump => DoubleJumpSeconds > 0;

    public float SpeedMultiplier =>
        SpeedSeconds > 0 ? speedFactor : 1f;

    public int Score =>
        Math.Max(0, 10000 - (int)Math.Min(10000, Elapsed * 20))
        + TotalCoins * 50;


    public void Advance(float delta)
    {
        if (float.IsNaN(delta) ||
            float.IsInfinity(delta) ||
            delta <= 0)
            return;

        Elapsed += delta;

        SpeedSeconds = Math.Max(0, SpeedSeconds - delta);
        ShieldSeconds = Math.Max(0, ShieldSeconds - delta);
        HurtSeconds = Math.Max(0, HurtSeconds - delta);
        DoubleJumpSeconds = Math.Max(0, DoubleJumpSeconds - delta);
    }

    public void AddCoins(int count)
    {
        if (count <= 0 || count > 1000000) return;

        Coins += count;
        TotalCoins += count;
    }

    public static int Price(PowerUpKind kind)
    {
        switch (kind)
        {
            case PowerUpKind.Speed: return 6;
            case PowerUpKind.Shield: return 8;
            case PowerUpKind.Heal: return 4;
            default: return -1;
        }
    }

    public bool CanBuy(PowerUpKind kind)
    {
        int price = Price(kind);

        return price > 0 &&
               Coins >= price &&
               (kind != PowerUpKind.Heal || Health < MaxHealth);
    }

    public bool TryBuy(PowerUpKind kind)
    {
        if (!CanBuy(kind)) return false;

        Coins -= Price(kind);

        switch (kind)
        {
            case PowerUpKind.Speed:
                SpeedSeconds = 10;
                speedFactor = 1.5f;
                break;

            case PowerUpKind.Shield:
                ShieldSeconds = 8;
                break;

            case PowerUpKind.Heal:
                Health = Math.Min(MaxHealth, Health + 1);
                break;
        }

        return true;
    }

    // Metodo recuperado para los items del laberinto
    public void ApplyItem(ItemType type, float duration, float value)
    {
        if (Health <= 0) return;

        switch (type)
        {
            case ItemType.Curacion:
                Health = Math.Min(
                    MaxHealth,
                    Health + Math.Max(1, (int)Math.Round(value))
                );
                break;

            case ItemType.Escudo:
                ShieldSeconds = Math.Max(ShieldSeconds, duration);
                break;

            case ItemType.Velocidad:
                speedFactor = Math.Max(1f, value);
                SpeedSeconds = Math.Max(SpeedSeconds, duration);
                break;

            case ItemType.SaltoDoble:
                DoubleJumpSeconds = Math.Max(
                    DoubleJumpSeconds, duration
                );
                break;
        }
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0 ||
            Health <= 0 ||
            ShieldSeconds > 0 ||
            HurtSeconds > 0)
            return false;

        Health = Math.Max(0, Health - amount);
        HurtSeconds = 1.2f;

        return true;
    }
}
