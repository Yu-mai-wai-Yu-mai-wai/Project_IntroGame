namespace TawanOS.EventEngine
{
    public enum EventEffectType
    {
        None,
        Heal,
        TakeDamage,
        ChangeMaxHp,
        GainIncense,
        LoseIncense,
        GainRelic,
        StartCombat,
        CardReward // opens the card reward screen (pick 1 of a few random cards) when the event ends
    }
}
