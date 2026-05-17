
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Cainos.LucidEditor;

namespace Cainos.CustomizablePixelCharacter
{
    public class UIDemo : MonoBehaviour
    {
        [FoldoutGroup("UI")] public GameObject areaControl;
        [FoldoutGroup("UI")] public GameObject areaCustomization;
        [FoldoutGroup("UI")] public GameObject areaPreset;
        [Space]
        [FoldoutGroup("UI")] public UISelector selectorHat;
        [FoldoutGroup("UI")] public UISelector selectorFacewear;
        [FoldoutGroup("UI")] public UISelector selectorCloth;
        [FoldoutGroup("UI")] public UISelector selectorPants;
        [FoldoutGroup("UI")] public UISelector selectorSocks;
        [FoldoutGroup("UI")] public UISelector selectorShoes;
        [FoldoutGroup("UI")] public UISelector selectorBack;
        [Space]
        [FoldoutGroup("UI")] public UISelector selectorGender;
        [FoldoutGroup("UI")] public UISelector selectorHairstyle;
        [FoldoutGroup("UI")] public UISelector selectorHairColor;
        [FoldoutGroup("UI")] public UISelector selectorClipHair;
        [FoldoutGroup("UI")] public UISelector selectorEyeColor;
        [FoldoutGroup("UI")] public UISelector selectorExpression;
        [Space]
        [FoldoutGroup("UI")] public UISelector selectorWeapon;
        [FoldoutGroup("UI")] public UISelector selectorAttackAction;
        [FoldoutGroup("UI")] public UISelector selectorMeleeAction;
        [FoldoutGroup("UI")] public UISelector selectorProjectile;
        [Space]
        [FoldoutGroup("UI")] public TMP_Dropdown dropdownPreset;

        [FoldoutGroup("Material")] public List<Material> hairMaleMats;
        [FoldoutGroup("Material")] public List<Material> hairFemaleMats;
        [FoldoutGroup("Material")] public List<Texture> hairColorTexs;
        [FoldoutGroup("Material")] public List<Material> eyeMats;
        [FoldoutGroup("Material")] public List<Material> eyeBaseMats;
        [FoldoutGroup("Material")] public List<Material> hatMats;
        [FoldoutGroup("Material")] public List<Material> facewearMats;
        [FoldoutGroup("Material")] public List<Material> clothMats;
        [FoldoutGroup("Material")] public List<Material> pantsMats;
        [FoldoutGroup("Material")] public List<Material> socksMats;
        [FoldoutGroup("Material")] public List<Material> shoesMats;
        [FoldoutGroup("Material")] public List<Material> backMats;
        [FoldoutGroup("Material")] public List<Material> bodyMats;

        [FoldoutGroup("Weapon")] public List<Weapon> weapons;
        [FoldoutGroup("Weapon")] public List<GameObject> projectiles;

        [FoldoutGroup("Preset")] public List<PixelCharacter> presets;
        [FoldoutGroup("Preset")] public PixelCharacter startPreset;

        private bool isUIShown = true;
        private int hairstyleIndex = 0;

        public PixelCharacter Character
        {
            get
            {
                return PixelCharacter.instance;
            }
        }

        public void OnGenderChanged( int index)
        {
            //handle body material without gender, like zombie
            var genderIndex = GetGenderIndex(Character.BodyMaterial);
            if (genderIndex < 0) return;

            Character.BodyMaterial = bodyMats[index];
            Character.EyeBaseMaterial = eyeBaseMats[index];

            selectorHairstyle.Index = hairstyleIndex;
        }

        public void OnHairstyleChanged ( int index )
        {
            hairstyleIndex = index;

            if (selectorGender.Index == 0) Character.HairMaterial = hairMaleMats[index];
            else
            if (selectorGender.Index == 1) Character.HairMaterial = hairFemaleMats[index];
        }

        public void OnHairColorChanged ( int index)
        {
            Character.HairRampTexture = hairColorTexs[index];
        }

        public void OnClipHairChanged ( int index )
        {
            Character.ClipHair = (index == 0 ? false : true);
        }

        public void OnEyeColorChanged ( int index)
        {
            Character.EyeMaterial = eyeMats[index];
        }

        public void OnHatChanged(int index)
        {
            Character.HatMaterial = hatMats[index];
        }

        public void OnFacewearChanged(int index)
        {
            Character.FacewearMaterial = facewearMats[index];
        }

        public void OnClothChanged ( int index)
        {
            Character.ClothMaterial = clothMats[index];
        }

        public void OnPantsChanged ( int index )
        {
            Character.PantsMaterial = pantsMats[index];
        }

        public void OnSocksChanged(int index)
        {
            Character.SocksMaterial = socksMats[index];
        }

        public void OnShoesChanged(int index)
        {
            Character.ShoesMaterial = shoesMats[index];
        }

        public void OnBackChanged(int index)
        {
            Character.BackMaterial = backMats[index];
        }

        public void OnWeaponChanged ( int index)
        {
            GameObject weapon = null;
            if (index > 0)
            {
                weapon = weapons[index - 1].gameObject;
            }

            Character.AddWeapon(weapon, true);
        }

        public void OnProjectileChanged(int index)
        {
            GameObject projectile = null;
            if (index > 0) projectile = projectiles[index-1].gameObject;

            Character.GetComponent<PixelCharacterController>().projectilePrefab = projectile;
        }

        public void OnExpressionChanged ( int index)
        {
            Character.Expression = (PixelCharacter.ExpressionType) index;
        }

        public void OnAttackActionChanged ( int index)
        {
            Character.GetComponent<PixelCharacterController>().attackAction = GetAttackAction(index);
        }

        public void OnMeleeActionChanged(int index)
        {
            Character.GetComponent<PixelCharacterController>().attackActionMelee = GetMeleeAction(index);
        }

        public void OnDropWeapon()
        {
            Character.DetachWeapon();
            selectorWeapon.Index = 0;
        }

        public void OnKillRevive()
        {
            var cc = Character.GetComponent<PixelCharacterController>();
            cc.IsDead = !cc.IsDead;

            if (cc.IsDead == true) selectorWeapon.Index = 0;
        }

        public void OnInjureFront()
        {
            Character.InjuredFront();
        }

        public void OnInjureBack()
        {
            Character.InjuredBack();
        }

        public void OnPresetChanged ( int index)
        {

            Character.CopyFrom(presets[index]);

            //for non-human presets
            Character.eye.gameObject.SetActive( presets[index].eye.gameObject.activeSelf );
            Character.eyeBase.gameObject.SetActive(presets[index].eyeBase.gameObject.activeSelf);
            Character.expression.gameObject.SetActive(presets[index].expression.gameObject.activeSelf);
            Character.hair.gameObject.SetActive( presets[index].hair.gameObject.activeSelf );
            Character.hairClipped.gameObject.SetActive(presets[index].hairClipped.gameObject.activeSelf);

            selectorHat.Set( hatMats.IndexOf(Character.HatMaterial ));
            selectorFacewear.Set ( facewearMats.IndexOf(Character.FacewearMaterial));
            selectorCloth.Set ( clothMats.IndexOf (Character.ClothMaterial ));
            selectorPants.Set ( pantsMats.IndexOf(Character.PantsMaterial ));
            selectorSocks.Set ( socksMats.IndexOf(Character.SocksMaterial ));
            selectorShoes.Set ( shoesMats.IndexOf(Character.ShoesMaterial ));
            selectorBack.Set( backMats.IndexOf(Character.BackMaterial ));

            //hairstyle
            hairstyleIndex = GetHairstyleIndex(Character.HairMaterial);
            selectorHairstyle.Set (hairstyleIndex);

            selectorHairColor.Set ( GetHairColorIndex(Character.HairMaterial ));
            selectorClipHair.Set(Character.ClipHair ? 1 : 0);
            selectorEyeColor.Set(eyeMats.IndexOf(Character.EyeMaterial));
            selectorExpression.Set(0);

            //gender
            var genderIndex = GetGenderIndex(Character.BodyMaterial);
            if ( genderIndex >= 0 ) selectorGender.Set(genderIndex);

            //weapon
            selectorWeapon.Set(GetWeaponIndex( presets[index].Weapon ));
            //projectile
            selectorProjectile.Set(GetProjectileIndex(presets[index].GetComponent<PixelCharacterController>().projectilePrefab));


            //attack action
            selectorAttackAction.Set(GetAttackActionIndex());
            selectorMeleeAction.Set(GetMeleeActionIndex());
        }

        public void ToggleUI()
        {
            isUIShown = !isUIShown;

            areaControl.SetActive(isUIShown);
            areaCustomization.SetActive(isUIShown);
            areaPreset.SetActive(isUIShown);
        }

        public void Reset()
        {
            SceneManager.LoadScene(0);
        }

        private void Start()
        {
            //setup selectors
            for (int i = 0; i < hairColorTexs.Count; i++) selectorHairColor.items.Add(GetName(hairColorTexs[i].name));

            for ( int i = 0; i < eyeMats.Count; i++) selectorEyeColor.items.Add( GetName( eyeMats[i].name ));
            for ( int i = 0; i < hatMats.Count; i++) selectorHat.items.Add( GetName( hatMats[i].name ));
            for (int i = 0; i < facewearMats.Count; i++) selectorFacewear.items.Add(GetName(facewearMats[i].name));
            for ( int i = 0; i < clothMats.Count; i++) selectorCloth.items.Add( GetName( clothMats[i].name ));
            for (int i = 0; i < pantsMats.Count; i++) selectorPants.items.Add(GetName(pantsMats[i].name));
            for (int i = 0; i < socksMats.Count; i++) selectorSocks.items.Add(GetName(socksMats[i].name));
            for (int i = 0; i < shoesMats.Count; i++) selectorShoes.items.Add(GetName(shoesMats[i].name));
            for (int i = 0; i < backMats.Count; i++) selectorBack.items.Add(GetName(backMats[i].name));

            //weapon
            selectorWeapon.items.Add("NONE");
            for (int i = 0; i < weapons.Count; i++)
            {
                selectorWeapon.items.Add(GetName(weapons[i].name));
            }

            //projectile
            selectorProjectile.items.Add("NONE");
            for (int i = 0; i < projectiles.Count; i++)
            {
                selectorProjectile.items.Add(GetName(projectiles[i].name));
            }

            //setup preset dropdown
            List<string> presetOptions = new List<string>();
            for (int i = 0; i < presets.Count; i++) presetOptions.Add(GetName(presets[i].gameObject.name));
            dropdownPreset.AddOptions(presetOptions);

            //set start preset
            dropdownPreset.value = presets.IndexOf(startPreset);
        }

        //Get the last section from material or gameobject name
        private string GetName( string rawName )
        {
            string[] s = rawName.Split('-');
            return s[s.Length - 1].TrimStart (' ');
        }

        //get corresponding gender selector index from body material
        private int GetGenderIndex ( Material mat )
        {
            string[] s = mat.name.Split('-');
            string n = s[s.Length - 1].Trim(' ');

            if (n == "Male") return 0;
            else
            if (n == "Female") return 1;
            else return -1;
        }

        private int GetAttackActionIndex()
        {
            var attackAction = Character.GetComponent<PixelCharacterController>().attackAction;

            if (attackAction == PixelCharacterController.AttackActionType.None) return 0;
            if (attackAction == PixelCharacterController.AttackActionType.Swipe) return 1;
            if (attackAction == PixelCharacterController.AttackActionType.Stab) return 2;
            if (attackAction == PixelCharacterController.AttackActionType.PointAtTarget) return 3;
            if (attackAction == PixelCharacterController.AttackActionType.Summon) return 4;
            if (attackAction == PixelCharacterController.AttackActionType.Throw) return 5;
            if (attackAction == PixelCharacterController.AttackActionType.Cast) return 6;
            if (attackAction == PixelCharacterController.AttackActionType.Archery) return 7;

            return 0;
        }
        private PixelCharacterController.AttackActionType GetAttackAction( int index )
        {
            if( index == 0) return PixelCharacterController.AttackActionType.None;
            if (index == 1) return PixelCharacterController.AttackActionType.Swipe;
            if (index == 2) return PixelCharacterController.AttackActionType.Stab;
            if (index == 3) return PixelCharacterController.AttackActionType.PointAtTarget;
            if( index == 4) return PixelCharacterController.AttackActionType.Summon;
            if (index == 5) return PixelCharacterController.AttackActionType.Throw;
            if (index == 6) return PixelCharacterController.AttackActionType.Cast;
            if (index == 7) return PixelCharacterController.AttackActionType.Archery;

            return PixelCharacterController.AttackActionType.None;
        }

        private int GetMeleeActionIndex()
        {
            var meleeAction = Character.GetComponent<PixelCharacterController>().attackActionMelee;

            if (meleeAction == PixelCharacterController.AttackActionMeleeType.None) return 0;
            if (meleeAction == PixelCharacterController.AttackActionMeleeType.Swipe) return 1;
            if (meleeAction == PixelCharacterController.AttackActionMeleeType.Stab) return 2;

            return 0;
        }
        private PixelCharacterController.AttackActionMeleeType GetMeleeAction(int index)
        {
            if (index == 0) return PixelCharacterController.AttackActionMeleeType.None;
            if (index == 1) return PixelCharacterController.AttackActionMeleeType.Swipe;
            if (index == 2) return PixelCharacterController.AttackActionMeleeType.Stab;

            return PixelCharacterController.AttackActionMeleeType.None;
        }

        //get corresponding hairstyle selector index from hair material
        private int GetHairstyleIndex ( Material mat)
        {
            string[] s = mat.name.Split('-');
            string n = s[s.Length - 2].Trim(' ');
            s = n.Split(' ');
            n = s[s.Length - 1];

            return int.Parse(n) -1;
        }

        //get corresponding hair color selector index from hair material
        private int GetHairColorIndex(Material mat)
        {
            string[] s = mat.name.Split('-');
            string n = s[s.Length - 1].Trim(' ');

            int l = selectorHairColor.items.Count;
            for (int i = 0; i < l; i++)
            {
                if (n == selectorHairColor.items[i])
                {
                    return i;
                }
            }

            return 0;
        }

        private int GetWeaponIndex ( Weapon weapon)
        {
            if (weapon == null) return 0;
            for ( int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].name == weapon.name) return i+1;
            }

            return 0;
        }

        private int GetProjectileIndex(GameObject projectile)
        {
            if (projectile == null) return 0;

            return projectiles.IndexOf(projectile) +1;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.G)) OnDropWeapon();
            if (Input.GetKeyDown(KeyCode.K)) OnKillRevive();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) OnInjureFront();
            if (Input.GetKeyDown(KeyCode.RightBracket)) OnInjureBack();
        }
    }
}
