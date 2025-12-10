
public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Dashing,
    KnockedBack,
    WallSliding,
    InCannon
}

public enum CardActionType
{
    Jump,
    DashForward,
    DashBackward,
    WallCling,
    DrawCards,
    GainJumpCharges,
    PlatformCreate,
    Fireball,
    Portal,
    VampiricBite,
    GlassWail,
    Phase,

}

public enum SkillType
{
    None,
    Recycle,        // Düþman ölünce +1 Shift
    VampiricAura,   // Düþman ölünce Can
    KineticDiscount,// Kart maliyetleri -1
    MaxShiftBonus,  // Max Shift +1 (YENÝ)
    InfinitySeal    // Bir karta sonsuz kullaným (YENÝ)
}
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}