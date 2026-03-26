
public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Dashing,
    KnockedBack,
    WallSliding,
    InCannon,
    CometDiving,
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
    CometDive,
    Adrenaline,
    Stagger
}

public enum SkillType
{
    None,
    InfinitySeal,   // (Eski favorimiz)
    EchoChamber,    // %50 Çift Etki
    SpectralWings,  // Bedava Air Jump
    Overclock,
    KineticDiscount// Kill = Sonraki Kart Bedava
}
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}