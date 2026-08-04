using UnityEngine;
using UnityEngine.UI;

// The shared look of the HUD's two COUNTER readouts — gold and scrap.
//
// The resource panel shows four things in two visual families, and the split is meaningful:
//   BARS   health and Shift — quantities with a maximum, so a fill length is the right shape
//   CHIPS  gold and scrap  — unbounded counts, so a number in a plate is the right shape
//
// ⚠️ BOTH CHIPS MUST BE BUILT FROM HERE. They were authored separately — the scrap counter as a
// FlatUI iron chip bottom-right, gold as a bare number in the panel — so the two currencies looked
// like they came from different games, which is what the designer reported (2026-08-09). Two
// readouts that sit one above the other and mean the same KIND of thing must be one piece of
// geometry defined once, not two that happen to agree today.
public static class HudChip
{
    public const float Width = 132f;
    public const float Height = 40f;
    public const float Chamfer = 5f;
    // Gap between the gold chip and the scrap chip directly under it.
    public const float RowGap = 6f;

    // Adds the plate + outline behind whatever the caller puts in the rect. Both are pushed to the
    // back of the sibling order so an icon and a number can simply be added afterwards.
    public static void Build(RectTransform target)
    {
        if (target == null) return;

        Image bg = AddLayer(target, "Chip", FlatUI.Panel((int)Chamfer), FlatUI.Surface);
        Image frame = AddLayer(target, "ChipFrame", FlatUI.Outline((int)Chamfer, 1), FlatUI.Border);

        // Behind the content, and behind each other in the right order.
        bg.transform.SetAsFirstSibling();
        frame.transform.SetSiblingIndex(1);
    }

    private static Image AddLayer(RectTransform parent, string name, Sprite sprite, Color color)
    {
        // Reuse rather than stack duplicates if the HUD is rebuilt.
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null) go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;   // 9-sliced plate; Simple would stretch the chamfer
        img.color = color;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }
}
