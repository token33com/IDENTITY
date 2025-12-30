using UnityEngine;
using PlayerStates;

public class ActionStateComponent : MonoBehaviour
{
    [SerializeField]
    private ActionState currentState = ActionState.None;

    public ActionState Current => currentState;

    public bool IsBusy =>
        currentState != ActionState.None;

    public void SetState(ActionState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
    }

    public void Clear()
    {
        currentState = ActionState.None;
    }

    public bool Is(ActionState state) => currentState == state;
}
