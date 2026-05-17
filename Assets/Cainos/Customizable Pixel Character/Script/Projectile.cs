using UnityEngine;
using Cainos.LucidEditor;
using UnityEngine.Events;

namespace Cainos.CustomizablePixelCharacter
{
    //the projectile the character shoots, like arrows, magic missiles
    public class Projectile : MonoBehaviour
    {
        [Tooltip("When lifetime is reached, destroy the projectile game object.")]
        [FoldoutGroup("Params")] public float lifeTime = 10.0f;

        [Space]
        [Tooltip("Whether to change the projectile's z position when launched.")]
        [FoldoutGroup("Params")] public bool setZPosOnLaunch = false;

        [Tooltip("is z position on launch in local space or world space.")]
        [FoldoutGroup("Params")] public bool isZPosLaunchLocal = true;

        [Tooltip("The z position in world space to set when launched.")]
        [FoldoutGroup("Params")] public float zPosLaunch = 0.0f;


        [Space]
        [Tooltip("Whether to change the projectile's z position when hit.")]
        [FoldoutGroup("Params")] public bool setZPosOnHit = false;

        [Tooltip("is z position on hit in local space or world space.")]
        [FoldoutGroup("Params")] public bool isZPosHitLocal = true;

        [Tooltip("The z position in world space to set when hit.")]
        [FoldoutGroup("Params")] public float zPosHit = 0.0f;

        [FoldoutGroup("Event")] public UnityEvent onLaunch;
        [FoldoutGroup("Event")] public UnityEvent<Collision2D> onHit;


        private float timer;

        //is the projectile launched
        //for arrow projectile, it is consider launched when released from bow
        [FoldoutGroup("Runtime"), ShowInInspector]
        public bool IsLaunched
        {
            get { return isLaunched; }
            set
            {
                if (isLaunched == value) return;
                isLaunched = value;

                if (isLaunched)
                {
                    Rigidbody2D.simulated = true;

                    //set launch z pos
                    if (setZPosOnLaunch)
                    {
                        var pos = isZPosLaunchLocal ? transform.localPosition: transform.position;
                        pos.z = zPosLaunch;

                        if (isZPosLaunchLocal) transform.localPosition = pos;
                        else transform.position = pos;
                    }

                    OnLaunched();
                }
            }
        }
        private bool isLaunched;

        //has the projectile hit anything
        [FoldoutGroup("Runtime"), ShowInInspector]
        public virtual bool HasHit
        {
            get { return hasHit; }
            protected set
            {
                if (hasHit == value) return;
                hasHit = value;

                Rigidbody2D.gravityScale = 1.0f;

                //set hit z pos
                if (setZPosOnHit)
                {
                    var pos = isZPosHitLocal ? transform.localPosition : transform.position;
                    pos.z = zPosHit;

                    if(isZPosHitLocal) transform.localPosition = pos;
                    else transform.position = pos;
                }
            }
        }
        protected bool hasHit;

        public Vector2 Velocity
        {
            get
            {
                return Rigidbody2D.linearVelocity;
            }
            set
            {
                Rigidbody2D.linearVelocity = value;
            }
        }

        protected Rigidbody2D Rigidbody2D
        {
            get
            {
                if (rigidbody2D == null) rigidbody2D = GetComponent<Rigidbody2D>();
                return rigidbody2D;
            }
        }
        protected new Rigidbody2D rigidbody2D;

        protected virtual void OnLaunched()
        {
            onLaunch?.Invoke();
        }

        private void Start()
        {
            if (!IsLaunched )Rigidbody2D.simulated = false;
        }

        protected virtual void Update()
        {
            if (IsLaunched == false) return;

            timer += Time.deltaTime;
            if ( timer > lifeTime)
            {
                Destroy();
            }
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (HasHit == false)
            {
                HasHit = true;
                onHit?.Invoke(collision);
            }
        }

        protected virtual void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
