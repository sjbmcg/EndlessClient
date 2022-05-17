﻿namespace EndlessClient.Audio
{
    // These are 0 based indexes even though the files start at sfx001
    // sfx001 will be id 0
    // sfx060 will be id 59
    public enum SoundEffectID
    {
        LayeredTechIntro,
        ButtonClick,
        DialogButtonClick,
        TextBoxFocus, //also the sound when opening chest?
        Login = 4, //also the sound from a server message?
        DeleteCharacter,
        UnknownStaticSound,
        ScreenCapture,
        PMReceived = 8,
        PunchAttack,
        UnknownWarpSound,
        UnknownPingSound,
        HudStatusBarClick = 12,
        AdminAnnounceReceived,
        MeleeWeaponAttack,
        UnknownClickSound2,
        TradeAccepted = 16, // also join party
        GroupChatReceived,
        UnknownWhooshSound,
        InventoryPickup,
        InventoryPlace = 20,
        Earthquake,
        DoorClose,
        DoorOpen,
        UnknownClickSound3 = 24,
        BuySell,
        Craft,
        UnknownBuzzSound,
        AdminChatReceived = 28,
        UnknownAttackLikeSound,
        PotionOfFlamesEffect,
        AdminWarp,
        NoWallWalk = 32,
        PotionOfEvilTerrorEffect,
        PotionOfFireworksEffect,
        PotionOfSparklesEffect,
        LearnNewSpell = 36,
        AttackBow,
        LevelUp,
        Dead,
        JumpStone = 40,
        Water,
        Heal,
        Harp1,
        Harp2 = 44,
        Harp3,
        Guitar1,
        Guitar2,
        Guitar3 = 48,
        Thunder,
        UnknownTimerSound,
        UnknownFanfareSound,
        Gun = 52,
        UltimaBlastSpell,
        ShieldSpell,
        RingOfFireSpell,
        IceBlastSpell1 = 56,
        EnergyBallSpell,
        WhirlSpell,
        BouldersSpell,
        AuraSpell = 60,
        HeavenSpell,
        IceBlastSpell2,
        MapAmbientNoiseWater,
        MapAmbientNoiseDrone1 = 64,
        UnknownMapAmbientNoise1,
        UnknownMapAmbientNoise2,
        UnknownMapAmbientNoise3,
        UnknownMapAmbientNoise4 = 68,
        MapEffectHPDrain,
        MapEffectTPDrain,
        Spikes,
        UnknownClick = 72,
        UnknownBoing,
        UnknownMapAmbientNoise5,
        DarkHandSpell,
        TentaclesSpell = 76,
        MagicWhirlSpell,
        PowerWindSpell,
        FireBlastSpell,
        UnknownBubblesNoise = 80,
    }
}
