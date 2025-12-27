namespace CardGame.Core.Cards.Data
{
    public enum CardType { Unit, Spell }

    public enum ResourceType
    {
        Blood
    }

    public enum Keyword
    {
        None,
        Armored,
        Armor2,
        DoubleStrike,
        SoulGuard,
        Unkillable,
        Marked,
        SoulGuardDepleted,
        Burning,
        SplashDamage,
        BurnSource
    }

    public enum TriggerType
    {
        OnPlayed,
        OnDeath,
        OnDamagTaken,
        OnTurnStart,
        OnTurnEnd,
        WhileOnBoard,
        OnFriendlyUnitDied,
        OnSacrificed,
        Passive,
        OnOtherUnitSacrificed
    }

    public enum ActionType
    {
        DealDamage,
        Heal,
        ConjureCard,
        DestroyUnit,
        DiscardCard,
        Transform,
        SummonUnit,
        BuffStats,
        AddCardToHand,
        ApplyStatus,
        ModifyGlobalBuff,
        DrawCard,
        SacrificeUnit,
        AbsorbStats,
        ShuffleDeck,
        ReturnToHand,
        TutorCard,
        DrawFromDiscard
    }

    public enum TargetType
    {
        Self,
        TargetEnemyUnit,
        TargetFriendlyUnit,
        AllEnemyUnits,
        AllFriendlyUnits, // Poprawiona literówka
        EnemyHero,
        FriendlyHero,
        SelectedTarget,
        OtherFriendlyUnits,
        AdjacentEnemyUnits,
        AllUnitsOnBoard,
        Choice
    }

    public enum ConditionType
    {
        None,
        And,
        Or,
        IsEnemy,
        IsUnit,
        IsType
    }

    public enum EffectZone
    {
        Board,
        Hand,
        Graveyard,
        Any
    }
}