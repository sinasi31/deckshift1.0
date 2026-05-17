using UnityEditor;
using UnityEngine;
using System.Collections;
using Cainos.LucidEditor;


namespace Cainos.CustomizablePixelCharacter
{
    [CustomEditor(typeof(PixelCharacter))]
    public class PixelCharacterEditor : Cainos.LucidEditor.LucidEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            SetTooltip("blinkInterval", "The interval range for the character to play an eye blink animation.");

            SetTooltip("HatMaterial", "Material of the character's hat.");
            SetTooltip("HairMaterial", "Material of the character's hair.");
            SetTooltip("EyeMaterial", "Material of the character's eye.");
            SetTooltip("EyeBaseMaterial", "Material of the character's eye shape. You only need to change this when changing the character's gender.");
            SetTooltip("FacewearMaterial", "Material of the character's facewear.");
            SetTooltip("ClothMaterial", "Material of the character's cloth.");
            SetTooltip("PantsMaterial", "Material of the character's pants.");
            SetTooltip("SocksMaterial", "Material of the character's socks.");
            SetTooltip("ShoesMaterial", "Material of the character's shoes. This will be displayed behind the character's pants.");
            SetTooltip("BackMaterial", "Material of the object the character carries on the back.");
            SetTooltip("BodyMaterial", "Material of the character's body. You need to change this when changing the character's gender. Also, special character presets would use their specific body material, like Vampire or Zombie.");

            SetTooltip("ClipHair", "Hide part of the character's hair. When wearing certain hats, you need to enable this.");
            SetTooltip("HideHair", "Completely hide the character's hair.");
            SetTooltip("ShoesInFront", "Whether to display shoes in front of the character's pants.");
            SetTooltip("Facing", "The character's facing. Can be changed in editor to set the character's initial facing.");

            SetTooltip("Alpha", "Controls the transparency of the entire character.");
            SetTooltip("IsDead", "Whether the character is dead. Turning this on will only make the character appear dead on the graphic side. For making the character truly dead, turn on the Is Dead on the Pixel Character Controller script.");
            SetTooltip("Expression", "The character's expression.");

        }
    }
}
