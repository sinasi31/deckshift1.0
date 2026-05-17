using Cainos.LucidEditor;
using UnityEngine;

namespace Cainos.CustomizablePixelCharacter
{
    public class ProjectileArrow : Projectile
    {
        [Space]

        [Tooltip("If the angle between arrow direction and hit surface normal is below this angle, the arrow can insert into the surface.")]
        [FoldoutGroup("Params")] public float insertMaxAngle = 60.0f;

        [Tooltip("The minimum speed required for the arrow to insert into the surface when hit.")]
        [FoldoutGroup("Params")] public float insertMinSpeed = 20.0f;

        [Tooltip("Arrow insert depth when hit.")]
        [FoldoutGroup("Params")] public float insertDepth = 0.1f;


        private Vector2 hitVel;
        private bool isAttachedToTarget;
        private float curInsertDepth;

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (HasHit == true) return;

            hitVel = -collision.relativeVelocity;

            //try insert into surface
            if ( Vector2.Angle(collision.contacts[0].normal, -transform.right) < insertMaxAngle && hitVel.magnitude > insertMinSpeed)
            {
                isAttachedToTarget = true;

                transform.SetParent(collision.collider.transform, true);
                Rigidbody2D.simulated = false;
            }

            base.OnCollisionEnter2D(collision);
        }

        protected override void Update()
        {
            if (IsLaunched == false) return;

            base.Update();

            if (hasHit == false)
            {
                float angle = Mathf.Atan2(Rigidbody2D.linearVelocity.y, Rigidbody2D.linearVelocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if ( isAttachedToTarget)
            {
                if (curInsertDepth < insertDepth)
                {
                    transform.Translate(hitVel * Time.deltaTime, Space.World);
                    curInsertDepth += hitVel.magnitude * Time.deltaTime;
                }


            }
        }
    }
}
