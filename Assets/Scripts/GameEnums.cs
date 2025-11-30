/* Bu script'i Unity'de herhangi bir objeye eklemene GEREK YOK.
Sadece "Scripts" klasöründe durmasý, diðer tüm script'lerin 
bu tanýmlarý görmesini saðlayacaktýr.
*/

public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Dashing,
    KnockedBack,
    WallSliding
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