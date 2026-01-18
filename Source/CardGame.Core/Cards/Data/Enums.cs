namespace CardGame.Core.Cards.Data
{
    #region Enums

    /// <summary>
    /// Represents the type of a card.
    /// </summary>
    public enum CardType
    {
        Unit,
        Spell
    }

    /// <summary>
    /// Represents the type of resource used in the game.
    /// </summary>
    public enum ResourceType
    {
        Blood
    }

    /// <summary>
    /// Represents special abilities or statuses that can be applied to cards.
    /// </summary>
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
        BurnSource,
        Stunned,
        Flying
    }

    /// <summary>
    /// Represents the timing or condition when an ability activates.
    /// </summary>
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
        OnOtherUnitSacrificed,
        OnKill,
        OnOpponentCardDrawn,
        OnDamagedEnemyUnit,
        OnStatusApplied,
        OnPreCombatLine,
        OnDamagedEnemyHero,
        OnFriendlyCardDrawn,
        OnFriendlyActionPlayed,
        OnSummoned
    }

    /// <summary>
    /// Represents the type of action a card can perform.
    /// </summary>
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
        DrawFromDiscard,
        BonusAttack,
        HealToFull,
        MoveRight,
        GiveToOpponent,
        Silence,
        AddResource,
        MakeAUnit,
        MoveLeft
    }

    /// <summary>
    /// Represents the valid targets for a card's action.
    /// </summary>
    public enum TargetType
    {
        Self,
        TargetEnemyUnit,
        TargetFriendlyUnit,
        AllEnemyUnits,
        AllFriendlyUnits,
        EnemyHero,
        FriendlyHero,
        SelectedTarget,
        OtherFriendlyUnits,
        AdjacentEnemyUnits,
        AllUnitsOnBoard,
        Choice,
        FriendlySpellsInHand,
        EnemySpellsInHand,
        EmptyLane,
        RandomEmptyLane,
        OppositeEnemyUnit,
        RandomFriendlyUnit
    }

    /// <summary>
    /// Represents logical conditions for targeting or effect activation.
    /// </summary>
    public enum ConditionType
    {
        None,
        And,
        Or,
        IsEnemy,
        IsUnit,
        IsType,
        IsStatus,
        IsSubtype,
        IsSelf,
        HasSubtypeOnBoard,
        Not,
        IsFriendly
    }

    /// <summary>
    /// Represents the game zones where effects can occur.
    /// </summary>
    public enum EffectZone
    {
        Board,
        Hand,
        Graveyard,
        Any
    }

    #endregion
}