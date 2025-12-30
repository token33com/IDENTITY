using UnityEngine;

namespace PlayerStates
{
    public enum MovementState
    {
        Idle,
        Walk,
        Run,
        Sprint,
        Jump,
        Fall,
        Land,
        Swim,
        Dive,
        Ladder,
        Crawl,
        AllFours,
        Crouch,
        CarryHeavy,
        DragHeavy
    }

    public enum ActionState
    {
        None,
        Work,
        Train,
        Sleep,
        Rest,
        Attack,
        Throw,
        UseObject,
        Interact
    }

    [System.Flags]
    public enum Modifier
    {
        None = 0,
        Sneaking = 1 << 0,
        Injured = 1 << 1,
        Exhausted = 1 << 2,
        Encumbered = 1 << 3,
        InZeroGravity = 1 << 4,
        InCombat = 1 << 5
    }

    public enum InteractionProfile
    {
        None,
        Push,
        Pull,
        Carry,
        Roll
    }

    public enum ReactionState
    {
        None,
        Dodge,
        Stagger,
        Fall
    }
}
