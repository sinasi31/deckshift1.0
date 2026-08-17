using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Owns the LOOK of one map node you can act on: its breathing pen ring, and its response to the
// cursor. The map screen used to have no hover state at all — the only feedback for picking a
// branch was a soft-red ring becoming a slightly larger solid-red ring, which is not a signal.
//
// ⚠️ THE PULSE LIVES HERE, NOT IN RunMapScreen.TickMotion, because hovering has to STOP it.
// Two owners writing the same Image.color in undefined script order is the classic way for one of
// them to silently win. TickMotion still drives the trail strokes; a node's ring is this
// component's alone.
public class MapNodeMark : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public RectTransform body;
    [HideInInspector] public Image ring;
    [HideInInspector] public TextMeshProUGUI label;

    // A branch you MAY take breathes. The one you have chosen does not — it is decided, and
    // stillness is what says so. Same reasoning as the quest board, where hovering a slip stops
    // its sway and that is the whole selection signal.
    [HideInInspector] public bool pulses;

    [HideInInspector] public Color restCol, hotCol, labelRest, labelHot;

    private bool hovered;
    private float t;

    private const float GROW = 1.10f;
    private const float SPEED = 8f;

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        // Grown marks can overlap a neighbour on a full floor. Draw the one under the cursor on top.
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData e) { hovered = false; }

    private void OnDisable() { hovered = false; t = 0f; }

    private void Update()
    {
        // ⚠️ Unscaled throughout — the map holds the game at timeScale 0, so Time.deltaTime is 0
        // here and every one of these would sit frozen at its resting value.
        t = Mathf.MoveTowards(t, hovered ? 1f : 0f, Time.unscaledDeltaTime * SPEED);
        float e = t * t * (3f - 2f * t);

        if (body != null) body.localScale = Vector3.one * Mathf.Lerp(1f, GROW, e);

        if (ring != null)
        {
            // ⚠️ A NON-PULSING MARK RESTS AT 0, NOT AT 1. This read `: 1f` and every ring that did
            // not breathe was therefore pinned to hotCol for ever — which silently undid the whole
            // point of the sidelined state: all five branches rendered in full red again, and the
            // chosen one had nothing to stand out from. It survived a screenshot because the
            // capture landed on the frame Refresh() built them, BEFORE this Update had run once.
            // Measured the colours to catch it.
            float breathe = pulses ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f) : 0f;
            // Hover pins the mark to full strength rather than adding a second animation on top —
            // one thing moving is a signal, two things moving is noise.
            ring.color = Color.Lerp(restCol, hotCol, Mathf.Lerp(breathe, 1f, e));
        }

        if (label != null) label.color = Color.Lerp(labelRest, labelHot, e);
    }
}
