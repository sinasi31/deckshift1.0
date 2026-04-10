using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cainos.LucidEditor;

namespace Cainos.PixelArtMonster_Dungeon
{
    public class PixelMonster : MonoBehaviour
    {

        //reference to objects inside the character prefab
        #region REFERENCE
        [FoldoutGroup("Reference")] public List<Renderer> renderers;
        [Space]
        [FoldoutGroup("Reference")] public Animator animator;
        [FoldoutGroup("Reference")] public GameObject fx;
        [Space]
        [FoldoutGroup("Reference"), AssetsOnly] public GameObject dieFxPrefab;
        #endregion

        //those parameters should only be changed in runtime, mainly wrappers for animator parameters
        #region RUNTIME

        //used to work with Alpha
        private MaterialPropertyBlock MPB
        {
            get
            {
                if ( mpb == null)
                {
                    mpb = new MaterialPropertyBlock();
                }
                return mpb;
            }
        }
        private MaterialPropertyBlock mpb;

        //sorting layer does not work very well on the mesh, so it is hidden
        //[FoldoutGroup("Runtime"), ShowInInspector]
        public string SortingLayerName
        {
            get
            {
                if (renderers.Count <= 0) return "";
                return renderers[0].sortingLayerName;
            }
            set
            {
                if (renderers.Count <= 0) return;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.sortingLayerName = value;
                }
            }
        }

        //sorting layer does not work very well on the mesh, so it is hidden
        //[FoldoutGroup("Runtime"), ShowInInspector]
        public int SortingOrder
        {
            get
            {
                if (renderers.Count <= 0) return 0;
                return renderers[0].sortingOrder;
            }
            set
            {
                if (renderers.Count <= 0) return;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.sortingOrder = value;
                }
            }
        }

        //monster facing
        [FoldoutGroup("Runtime"), ShowInInspector]
        public FacingType Facing
        {
            get { return facing; }
            set
            {
                facing = value;
                if (facing == FacingType.None) return;

                animator.transform.localScale = new Vector3(1.0f, 1.0f, (int)facing);
            }
        }
        private FacingType facing = FacingType.None;

        //controls the transparancy of the entire monster
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public float Alpha
        {
            get { return alpha; }
            set
            {
                alpha = Mathf.Clamp01(value);

                if (renderers.Count <= 0) return;
                renderers[0].GetPropertyBlock(MPB);

                MPB.SetFloat("_Alpha", alpha);

                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.SetPropertyBlock(MPB);
                }
            }
        }
        private float alpha = 1.0f;

        //is the character hiding? only work for certain characters
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public bool IsHiding
        {
            get { return isHiding; }
            set
            {
                isHiding = value;
                animator.SetBool("IsHiding", isHiding);
            }
        }
        [SerializeField, HideInInspector]
        private bool isHiding;

        //is the character in jump prepare mode ( the animation played before the character actually jump)
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public bool IsInJumpPrepare
        {
            get { return isInJumpPrepare; }
            set
            {
                isInJumpPrepare = value;
                animator.SetBool("IsJumpPrepare", isInJumpPrepare);
            }
        }
        [SerializeField, HideInInspector]
        private bool isInJumpPrepare;

        //is the character on ground?
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public bool IsGrounded
        {
            get { return isGrounded; }
            set
            {
                isGrounded = value;
                animator.SetBool("IsGrounded", isGrounded);
            }
        }
        [SerializeField, HideInInspector]
        private bool isGrounded;

        //is the character dead?
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public bool IsDead
        {
            get { return isDead; }
            set
            {
                isDead = value;
                animator.SetBool("IsDead", isDead);

                if (fx) fx.SetActive(!isDead);
            }
        }
        [SerializeField, HideInInspector]
        private bool isDead;

        //moving animation blend
        //0.0:idle,  0.5:walk,  1.0:run
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public float MovingBlend
        {
            get
            {
                return movingBlend;
            }
            set
            {
                movingBlend = value;
                animator.SetFloat("MovingBlend", movingBlend);
            }
        }
        [SerializeField, HideInInspector]
        private float movingBlend;

        //vertical speed
        //determines whether the animation should be jumping or falling
        public float SpeedVertical
        {
            get { return speedVertical; }
            set
            {
                speedVertical = value;
                animator.SetFloat("SpeedVertical", speedVertical);
            }
        }
        private float speedVertical;

        //is the character in attack animation
        public bool IsAttacking
        {
            get
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex("Attack"));

                if (stateInfo.IsName("Attack")) return true;
                return false;
            }
        }

        //when character get injured from front or back
        [FoldoutGroup("Runtime"), HorizontalGroup("Runtime/Injure"), Button("Injured Front")]
        public void InjuredFront()
        {
            animator.SetTrigger("InjuredFront");
        }

        [FoldoutGroup("Runtime"), HorizontalGroup("Runtime/Injure"), Button("Injured Back")]
        public void InjuredBack()
        {
            animator.SetTrigger("InjuredBack");
        }

        //perform an attack
        [FoldoutGroup("Runtime"), Button("Attack")]
        public void Attack()
        {
            animator.SetTrigger("Attack");
        }

        //handle OnDieFx event
        //instantiate the die fx prefab if there is
        public void OnDieFx()
        {
            if (dieFxPrefab)
            {
                Instantiate(dieFxPrefab, transform.position, Quaternion.identity, transform.parent);
            }
        }

        private void Start()
        {
            //set a random offset for loop animation
            animator.SetFloat("LoopCycleOffset", Random.value);
        }

        #endregion

        public enum FacingType
        {
            None = 0,

            Left = -1,
            Right = 1,
        }

    }
}
