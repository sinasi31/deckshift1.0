using System;

[Flags]
public enum ConflictFlags
{
    None                 = 0,
    GravityScale         = 1,
    TimeScale            = 2,
    MoveSpeed            = 4,
    LayerCollisionMatrix = 8,
    VisualTransform      = 16,
    PlayerVelocity       = 32,
    Invincibility        = 64,
    AnimatorAttackState  = 128,
}
