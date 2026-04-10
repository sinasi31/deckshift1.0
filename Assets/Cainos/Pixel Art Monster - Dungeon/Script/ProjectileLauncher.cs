using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Cainos.PixelArtMonster_Dungeon
{
    //attached to character that can shoot something out, like an arrow or magic missle

    [RequireComponent(typeof(PixelMonster))]
    public class ProjectileLauncher : MonoBehaviour
    {
        public GameObject projectilePrefab;                     //the object to shoot out
        public GameObject launchFxPrefab;                       //the fx effect to instantiate at launch
        public Transform muzzle;                                //start position
        public float speed;                                     //start speed
        public float zPos;                                      //z value for projectile's position
        public Vector2 angleOffsetRange;                                                     

        private PixelMonster Monster
        {
            get
            {
                if ( monster == null) monster = GetComponent<PixelMonster>();
                return monster;
            }
        }
        private PixelMonster monster;

        public void Launch()
        {
            if (Monster == null) return;

            if (!projectilePrefab) return;
            var projectile = Instantiate(projectilePrefab);

            float angleOffset = Random.Range(angleOffsetRange.x, angleOffsetRange.y);


            //position
            var pos = muzzle.position;
            pos.z = zPos;
            projectile.transform.position = pos;

            //rotation
            float rotZ = (Monster.Facing  == PixelMonster.FacingType.Right) ? 0.0f : 180.0f;
            rotZ += (Monster.Facing == PixelMonster.FacingType.Right) ? angleOffset : -angleOffset;
            projectile.transform.rotation = Quaternion.Euler(0.0f, 0.0f, rotZ);

            var rb2d = projectile.GetComponent<Rigidbody2D>();
            if ( rb2d)
            {
                rb2d.linearVelocity = projectile.transform.right * speed;
            }

            //fx
            if (launchFxPrefab)
            {
                var launchFx = Instantiate(launchFxPrefab);
                launchFx.transform.position = muzzle.position;
                launchFx.transform.rotation = Quaternion.Euler(0.0f, 0.0f, rotZ);
            }
        }
    }
}
