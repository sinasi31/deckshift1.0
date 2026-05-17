using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardActionExecutor : MonoBehaviour
{
    private PlayerController player;
    private Dictionary<CardActionType, CardAction> actions;

    // Tracks which coroutine-based actions are currently mid-execution.
    // Instant actions (IsCoroutine = false) are never added — they complete synchronously.
    // Note: a HashSet cannot hold duplicate references, so replaying a coroutine action
    // before the previous run completes will not double-count it in the set.
    private HashSet<CardAction> runningEffects = new HashSet<CardAction>();
    private ConflictFlags activeFlags = ConflictFlags.None;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        actions = new Dictionary<CardActionType, CardAction>
        {
            { CardActionType.Jump,           new JumpAction()           },
            { CardActionType.Dash,           new DashAction()           },
            { CardActionType.PlatformCreate, new PlatformCreateAction() },
            { CardActionType.Fireball,       new FireballAction()       },
            { CardActionType.Portal,         new PortalAction()         },
            { CardActionType.VampiricBite,   new VampiricBiteAction()   },
            { CardActionType.GlassWail,      new GlassWailAction()      },
            { CardActionType.Phase,          new PhaseAction()          },
            { CardActionType.CometDive,      new CometDiveAction()      },
            { CardActionType.Adrenaline,     new AdrenalineAction()     },
            { CardActionType.Stagger,        new StaggerAction()        },
            { CardActionType.ReverseGravity, new ReverseGravityAction() },
        };
    }

    // Dispatches a card action. Returns true if the action executed successfully.
    // keepCardInHand is set to true by Portal on its first click (card stays in hand).
    // Overlapping flags are tracked but never block execution — combos run concurrently.
    public bool TryExecute(CardActionType type, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (!actions.TryGetValue(type, out CardAction action)) return false;

        if (action.IsCoroutine)
        {
            // Execute acts as a gate check for coroutine actions.
            if (!action.Execute(player, value, out keepCardInHand)) return false;
            IEnumerator routine = action.ExecuteCoroutine(player, value);
            if (routine == null) return false;
            StartCoroutine(ManagedCoroutine(action, routine));
            return true;
        }

        return action.Execute(player, value, out keepCardInHand);
    }

    private IEnumerator ManagedCoroutine(CardAction action, IEnumerator inner)
    {
        runningEffects.Add(action);
        activeFlags |= action.ModifiedState;
        try
        {
            yield return StartCoroutine(inner);
        }
        finally
        {
            runningEffects.Remove(action);
            activeFlags &= ~action.ModifiedState;
        }
    }

    public bool IsEffectActive(CardActionType type) =>
        actions.TryGetValue(type, out CardAction a) && runningEffects.Contains(a);

    public ConflictFlags ActiveFlags => activeFlags;
}
