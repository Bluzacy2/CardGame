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
        Marked,
        SoulGuardDepleted
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
        Passive
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
        TutorCard
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

        SelectedTarget,

        AllFriendlyUnits,
        OtherFriendlyUnits,
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
