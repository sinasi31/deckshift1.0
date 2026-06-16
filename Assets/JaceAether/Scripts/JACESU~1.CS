using UnityEngine;

[RequireComponent(typeof(JaceDensityObject))]
public class JaceSupportBeam : MonoBehaviour
{
    [Tooltip("Units above start position the beam must rise to trigger victory.")]
    public float heightToWin = 4f;

    private Vector3           startPosition;
    private JaceDensityObject densityObj;
    private bool              triggered;

    void Start()
    {
        startPosition = transform.position;
        densityObj    = GetComponent<JaceDensityObject>();
    }

    void Update()
    {
        if (triggered) return;
        if (densityObj.State != JaceDensityState.Lightened) return;
        if (transform.position.y - startPosition.y >= heightToWin)
        {
            triggered = true;
            JaceGameManager.Instance?.Win();
        }
    }
}
