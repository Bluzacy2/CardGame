namespace CardGame.ConsoleApp.Evolution.V1_Legacy
{
    public static class DNA
    {
        // 1-8: Macro Resources
        public const int SelfHealthWeight = 0;
        public const int EnemyHealthWeight = 1;
        public const int HeroPanicThreshold = 2;
        public const int LethalFeverWeight = 3;
        public const int BloodEfficiencyWeight = 4;
        public const int BloodConservationRatio = 5;
        public const int HandSizeUtility = 6;
        public const int OverdrawRiskPenalty = 7;

        // 9-15: Stats & Lines
        public const int FriendlyAtkWeight = 8;
        public const int FriendlyHpWeight = 9;
        public const int EnemyAtkWeight = 10;
        public const int EnemyHpWeight = 11;
        public const int EmptyLaneBonus = 12;
        public const int DefensiveBlockingValue = 13;
        public const int AdjacencyBonus = 14;

        // 16-23: Keywords
        public const int MarkedOnEnemyValue = 15;
        public const int MarkedOnSelfPenalty = 16;
        public const int ArmoredEfficiency = 17;
        public const int UnkillableValue = 18;
        public const int SoulGuardValue = 19;
        public const int StunnedPenalty = 20;
        public const int BurningPenalty = 21;
        public const int SplashDamageUtility = 22;

        // 24-27: Phase Logic
        public const int UnitOnly_Commitment = 23;
        public const int UnitAndAction_Flexibility = 24;
        public const int ActionOnly_ReactionWeight = 25;
        public const int CombatProjectionTrust = 26;

        // 28-33: Subtypes
        public const int MonsterAffinity = 27;
        public const int MercenaryAffinity = 28;
        public const int AnimalAffinity = 29;
        public const int MachineAffinity = 30;
        public const int HumanAffinity = 31;
        public const int DemonAffinity = 32;

        // 34-40: Tactical Actions
        public const int SacrificeValueThreshold = 33;
        public const int RecycleComboPreference = 34;
        public const int SilencePriority_Keywords = 35;
        public const int SilencePriority_Buffs = 36;
        public const int GlobalBuffValue = 37;
        public const int DiscardPileUtility = 38;
        public const int TutorPriority = 39;

        // 41-44: Psychology & Risk
        public const int OverkillPenalty = 40;
        public const int BoardPresenceSafety = 41;
        public const int VengeanceWeight = 42;
        public const int PredictiveOpponentFear = 43;

        // 45-49: Deep Info
        public const int OpponentHandSize_FearFactor = 44;
        public const int OpponentEmptyHand_Aggression = 45;
        public const int DeckTracking_FinisherFound = 46;
        public const int DeckThinning_Utility = 47;
        public const int LethalProbability_Multiplier = 48;

        // 50-54: Buff Logic
        public const int Buff_TallStrategy_Weight = 49;
        public const int Buff_WideStrategy_Weight = 50;
        public const int Buff_Priority_Unkillable = 51;
        public const int Buff_Priority_Armored = 52;
        public const int Buff_Priority_Splash = 53;

        // 55-58: Bonus Attacks
        public const int BonusAtk_LethalExecution = 54;
        public const int BonusAtk_FacePressure = 55;
        public const int BonusAtk_HighAtk_Preference = 56;
        public const int BonusAtk_TriggerHunting = 57;

        // 59-62: Kill Priority
        public const int Target_EngineUnit_Priority = 58;
        public const int Target_FodderUnit_Ignore = 59;
        public const int Target_HighAtk_LowHP_Snipe = 60;
        public const int Target_Bodyguard_Breaker = 61;

        // 63-65: Survival
        public const int Heal_Hero_vs_Unit_Ratio = 62;
        public const int Save_ValueTarget_Weight = 63;
        public const int Heal_Efficiency_Bonus = 64;

        // 66-71: Archetype Biases
        public const int Aggro_Bias = 65;
        public const int Control_Bias = 66;
        public const int Tempo_Bias = 67;
        public const int Value_Bias = 68;
        public const int Burn_Bias = 69;
        public const int Midrange_Bias = 70;

        // 72-74: Mulligan
        public const int Mulligan_KeepUnit_MaxCost = 71;
        public const int Mulligan_KeepSpell_Priority = 72;
        public const int Mulligan_ComboPiece_Retention = 73;

        // 75-77: Forecasting
        public const int Opponent_NextTurn_ManaAwareness = 74;
        public const int Fatigue_Panic_Factor = 75;
        public const int WinCondition_Proximity_Greed = 76;

        // 78-80: Geometry
        public const int Line_Concentration_Preference = 77;
        public const int L0_L3_Corner_Value = 78;
        public const int Line_Switching_Urgency = 79;

        // 81-82: Info Warfare
        public const int Card_Tracking_Probability = 80;
        public const int Bluff_Resistance = 81;

        // 83-87: Prediction
        public const int Enemy_Archetype_Sacrifice_Likelihood = 82;
        public const int Enemy_Archetype_Control_Likelihood = 83;
        public const int Enemy_Archetype_Aggro_Likelihood = 84;
        public const int Predict_AoE_Danger_Level = 85;
        public const int Predict_Silence_Danger_Level = 86;

        // 88-90: Counter-Play
        public const int Counter_Lethal_Buffer = 87;
        public const int Counter_Unkillable_Stall = 88;
        public const int Counter_Mark_Disruption = 89;

        // 91-94: Mental States
        public const int Desperation_Factor = 90;
        public const int Dominance_Greed = 91;
        public const int Bluff_Potential_Value = 92;
        public const int Tempo_Sacrifice_Tolerance = 93;

        // 95-97: Strategic Burst
        public const int Line_Depth_Strategic_Value = 94;
        public const int Resource_Burst_Potential = 95;
        public const int Draw_vs_Play_Priority = 96;

        // 98-100: Master DNA
        public const int Complexity_Bias = 97;
        public const int Win_Condition_Clarity = 98;
        public const int Adaptive_Learning_Rate = 99;

        public const int Line0_Priority = 100;
        public const int Line1_Priority = 101;
        public const int Line2_Priority = 102;
        public const int Line3_Priority = 103;

        public const int FlyingUtility = 104;         
        public const int ResourceGainValue = 105;     
        public const int TokenSynergyWeight = 106; 
        public const int PresenceOfMachineBonus = 107;

        public const int Combo_Bias = 108;

        public const int TOTAL_GENES = 109;
    }
}