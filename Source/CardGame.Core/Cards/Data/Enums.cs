using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Data
{
    public enum CardType {  Unit, Spell }
    public enum  Keyword 
    { 
        None, 
        Armored,
        Armor2,
        DoubleStrike,
        SoulGuard, 
        Unkillable,
        Marked
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

        OnSacrificed
    }

    public enum ActionType
    {
        DealDamage,
        Heal,
        ConjureCard,
        DestroyUnit,
        DiscardCard,
        ShuffleDeck,
        Transform,
        SummonUnit,
        BuffStats,
        AddCardToHand,
        ApplyStatus,
        ModifyGlobalBuff,
        DrawCard,

        SacrificeUnit,
        AbsorbStats
    }

    public enum TargetType
    {
        Self,
        TargetEnemyUnit,
        TargetFriendlyUnit,
        AllEnemyUnits,
        ALlFriendlyUnits,
        EnemyHero,
        FriendlyHero,

        SelectedTarget

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
