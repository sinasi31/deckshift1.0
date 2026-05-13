using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Cainos.LucidEditor;

namespace Cainos.CustomizablePixelCharacter
{
    [CustomEditor(typeof(PixelCharacterController))]
    public class CharacterControllerEditor : Cainos.LucidEditor.LucidEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            SetTooltip("groundCheckLayerMask", "Objects under this layer mask will be consider ground the character can stand on.");

            SetTooltip("walkSpeedMax", "Max walking speed.");
            SetTooltip("walkAcc", "Walking Acceleration.");

            SetTooltip("runSpeedMax", "Max running speed.");
            SetTooltip("runAcc", "Running Acceleration.");

            SetTooltip("crouchSpeedMax", "Max move speed while crouching.");
            SetTooltip("crouchAcc", "Crouching acceleration.");

            SetTooltip("crawlSpeedMax", "Max move speed while crawling.");
            SetTooltip("crawlAcc", "Crawling acceleration.");

            SetTooltip("airSpeedMax", "Max move speed while in air.");
            SetTooltip("airAcc", "Air acceleration.");

            SetTooltip("groundDrag", "Braking acceleration (from movement to still) while on ground.");
            SetTooltip("airDrag", "Braking acceleration (from movement to still) while in air.");

            SetTooltip("jumpEnabled", "Whether the character can jump.");
            SetTooltip("jumpSpeed", "Speed applied to the character when jump.");
            SetTooltip("jumpCooldown", "Time needed to be able to jump again after landing.");
            SetTooltip("jumpTolerance", "When character is off ground and inside this time, the character is still able to jump.");
            SetTooltip("jumpGravityMul", "Gravity multiplier when character is jumping.");
            SetTooltip("fallGravityMul", "Gravity multiplier when character is falling.");

            SetTooltip("dashEnabled", "Whether the character can dash.");
            SetTooltip("dashSpeedStart", "Speed when dash starts.");
            SetTooltip("dashSpeedMax", "Max move speed while dashing.");
            SetTooltip("dashAcc", "Dash acceleration.");
            SetTooltip("dashTime", "Time the dash state will last.");
            SetTooltip("dashCooldown", "Time it takes for the character to be able to dash again after exiting dash state.");

            SetTooltip("dodgeEnabled", "Whether the character can dodge.");
            SetTooltip("dodgeSpeedMul", "Dodge speed multiplier.");
            SetTooltip("dodgeCooldown", "Time it takes for the character to be able to dodge again after exiting dodge state.");
            
            SetTooltip("swimEnabled", "Whether the character can swim.");
            SetTooltip("swimSpeedMax", "Max swimming speed.");
            SetTooltip("swimSpeedFastMax", "Max swimming speed when in fast moving mode (when the run key is down).");
            SetTooltip("swimAcc", "Swim acceleration.");
            SetTooltip("swimBuoyancySpeed", "The speed applied to the character as buoyancy when in water.");
            SetTooltip("keepInWaterDepthWhenDiving", "Whether to automatically keep current in water depth when diving.");
            
            SetTooltip("ladderClimbEnabled", "Whether the character can climb ladder.");
            SetTooltip("ladderClimbSpeed", "Ladder climbing speed.");
            SetTooltip("ladderClimbSpeedFast", "Ladder climbing speed when the run key (by default is SHIFT) is pressed.");

            SetTooltip("ledgeClimbEnabled", "Whether the character can climb ledge.");

            SetTooltip("attackAction", "Attack action to perform whether primary attack key (by default is LMB) is pressed.");
            SetTooltip("attackActionMelee", "Attack action to perform whether melee attack key (by default is V) is pressed, or when the character is in a state when primary attack action is not available, like when crawling or climbing ladder.");
            SetTooltip("attackSpeedMul", "Speed multiplier of the attack animation.");
            SetTooltip("attackCooldown", "Time needed to be able to attack again after last attack action ended.");

            SetTooltip("throwForce", "When using Throw attack action, the character will throw out the weapon it is holding. This defines the force applied to the thrown weapon.");
            SetTooltip("throwAngularSpeed", "The angular speed applied to the thrown weapon.");

            SetTooltip("projectileSpeed", "The speed applied the projectile when using Cast or Archery attack action.");
            SetTooltip("projectilePrefab", "The prefab of the projectile to be instantiate when using Cast or Archery attack action.");

            SetTooltip("inputMove", "movement input, x for horizontal, y for vertical, x and y should be in [-1.0, 1.0]");
            SetTooltip("inputRun", "Run input, when enabled the character will run instead of walk.");
            SetTooltip("inputDash", "Dash input, when triggered the character will enter dash state.");
            SetTooltip("inputDodge", "Dodge input, when triggered the character will enter dodge state.");
            SetTooltip("inputCrouch", "Crouch input, when enabled the character will be crouching.");
            SetTooltip("inputCrawl", "Crawl input, when enabled the character will be crawling. Will override crouch input.");
            SetTooltip("inputJump", "Jump input, when triggered the character will start to jump. When continuously enabled the character will jump higher.");
            SetTooltip("inputAttack", "Primary attack input, will trigger the attack action when available.");
            SetTooltip("inputMelee", "Melee attack input, will trigger the melee attack action.");
            SetTooltip("inputLook", "Look input, when enabled the character will look at the position defined by Input Target.");
            SetTooltip("inputTarget", "This is a world space Vector2 position that defines the target the character should look at or point at when performing curtain attack action.");
        }
    }
}
