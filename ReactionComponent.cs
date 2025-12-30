using UnityEngine;
using PlayerStates;

public class ReactionComponent : MonoBehaviour
{
    [SerializeField]
    private ReactionState currentReaction = ReactionState.None;

    public ReactionState Current => currentReaction;

    public bool IsReacting => currentReaction != ReactionState.None;

    public void Trigger(ReactionState reaction)
    {
        currentReaction = reaction;
    }

    public void Clear()
    {
        currentReaction = ReactionState.None;
    }
}
