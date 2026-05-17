using System.Collections.Generic;
using UnityEngine;
using Cainos.LucidEditor;


namespace Cainos.CustomizablePixelCharacter
{
    public class Weapon : MonoBehaviour
    {
        //transform for marking the tip of the weapon
        //used for defining projectile launch position and weapon length
        //if no set, a default tip position will be used
        [FoldoutGroup("Reference")] public Transform tip; 


        private List<Renderer> Renderers
        {
            get
            {
                if (renderers == null)
                {
                    renderers = new List<Renderer>();
                    renderers.AddRange(GetComponentsInChildren<Renderer>(true));
                }
                return renderers;
            }
        }
        private List<Renderer> renderers;


        //work with Alpha to control transparency
        private MaterialPropertyBlock MPB
        {
            get
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                return mpb;
            }
        }
        private MaterialPropertyBlock mpb;


        //control the overall transparency
        [FoldoutGroup("Runtime"), ShowInInspector, DisableInEditMode]
        public float Alpha
        {
            get { return alpha; }
            set
            {
                alpha = Mathf.Clamp01(value);

                MPB.SetFloat("_Alpha", alpha);
                foreach (var r in Renderers)
                {
                    if (r == null) continue;
                    r.SetPropertyBlock(MPB);
                }
            }
        }
        private float alpha = 1.0f;

        //sorting layer does not work very well on the mesh, so it is hidden
        //[FoldoutGroup("Runtime"), ShowInInspector]
        public string SortingLayerName
        {
            get
            {
                if (Renderers.Count <= 0) return "";
                return Renderers[0].sortingLayerName;
            }
            set
            {
                if (Renderers.Count <= 0) return;
                foreach (var r in Renderers)
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
                if (Renderers.Count <= 0) return 0;
                return Renderers[0].sortingOrder;
            }
            set
            {
                if (Renderers.Count <= 0) return;
                foreach (var r in Renderers)
                {
                    if (r == null) continue;
                    r.sortingOrder = value;
                }
            }
        }


        public Vector3 TipPosition
        {
            get
            {
                if ( tip) return tip.position;
                return transform.position + transform.right * 0.7f;
            }
        }

        //length of the weapon
        public float Length
        {
            get
            {
                if (tip) return Vector3.Distance(tip.localPosition, Vector3.zero);
                return 1.0f;
            }
        }

        public Quaternion Rotation
        {
            get
            {
                return transform.rotation;
            }
        }
    }
}
