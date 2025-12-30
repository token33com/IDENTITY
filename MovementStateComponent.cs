using UnityEngine;
using PlayerStates;

public class MovementStateComponent : MonoBehaviour
{
    [SerializeField]
    private MovementState currentState = MovementState.Idle;

    public MovementState Current => currentState;

    public bool IsGroundedLike =>
        currentState == MovementState.Idle ||
        currentState == MovementState.Walk ||
        currentState == MovementState.Run ||
        currentState == MovementState.Sprint ||
        currentState == MovementState.Crouch ||
        currentState == MovementState.Crawl ||
        currentState == MovementState.AllFours;

    public void SetState(MovementState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
    }

    public bool Is(MovementState state) => currentState == state;
}
