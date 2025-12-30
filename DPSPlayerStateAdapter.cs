using UnityEngine;
using Climbing;             // DPS / TPC enums
using PS = PlayerStates;    // alias dla PlayerStates, ¿eby nie by³o konfliktów

[DisallowMultipleComponent]
public class DPSPlayerStateAdapter : MonoBehaviour
{
    private ThirdPersonController tpc;
    private PS.PlayerState playerState;

    private void Awake()
    {
        tpc = GetComponent<ThirdPersonController>();
        playerState = GetComponent<PS.PlayerState>();

        if (tpc == null)
            Debug.LogError("[DPSPlayerStateAdapter] Missing ThirdPersonController");

        if (playerState == null)
            Debug.LogError("[DPSPlayerStateAdapter] Missing PlayerState");
    }

    private void Update()
    {
        if (tpc == null || playerState == null)
            return;

        UpdateMovementState();
        UpdateActionState();
        UpdateModifiers();
    }

    // =========================
    // MOVEMENT STATE
    // =========================
    private void UpdateMovementState()
    {
        // --- CLIMB ---
        if (tpc.currentMode == ThirdPersonController.ControlMode.CLIMB)
        {
            playerState.Movement.SetState(PS.MovementState.Ladder);
            return;
        }

        // --- PARKOUR ---
        if (tpc.currentMode == ThirdPersonController.ControlMode.PARKOUR)
        {
            playerState.Movement.SetState(PS.MovementState.Run); // parkour = dynamic ground move
            return;
        }

        // --- AIR STATES ---
        if (!tpc.isGrounded)
        {
            if (tpc.isJumping)
                playerState.Movement.SetState(PS.MovementState.Jump);
            else
                playerState.Movement.SetState(PS.MovementState.Fall);

            return;
        }

        // --- LAND ---
        if (tpc.isGrounded && tpc.onAir)
        {
            playerState.Movement.SetState(PS.MovementState.Land);
            return;
        }

        // --- GROUND STATES ---
        float velocity = tpc.GetCurrentVelocity();

        if (velocity < 0.05f)
        {
            playerState.Movement.SetState(PS.MovementState.Idle);
        }
        else
        {
            // bazujemy na stanie MovementCharacterController z DPS
            var moveState = tpc.characterMovement.GetState();

            switch (moveState)
            {
                case Climbing.MovementState.Running:
                    playerState.Movement.SetState(PS.MovementState.Run);
                    break;

                case Climbing.MovementState.Walking:
                    playerState.Movement.SetState(PS.MovementState.Walk);
                    break;

                default:
                    playerState.Movement.SetState(PS.MovementState.Walk);
                    break;
            }
        }
    }

    // =========================
    // ACTION STATE
    // =========================
    private void UpdateActionState()
    {
        if (tpc.isVaulting || tpc.currentMode == ThirdPersonController.ControlMode.PARKOUR)
        {
            playerState.Action.SetState(PS.ActionState.Interact);
            return;
        }

        if (tpc.currentMode == ThirdPersonController.ControlMode.CLIMB)
        {
            playerState.Action.SetState(PS.ActionState.Interact);
            return;
        }

        if (tpc.currentMode == ThirdPersonController.ControlMode.PAUSE)
        {
            playerState.Action.SetState(PS.ActionState.None);
            return;
        }

        playerState.Action.SetState(PS.ActionState.None);
    }

    // =========================
    // MODIFIERS
    // =========================
    private void UpdateModifiers()
    {
        SetModifier(PS.Modifier.Exhausted, tpc.dummy);
        SetModifier(PS.Modifier.Encumbered, !tpc.allowMovement);
        SetModifier(PS.Modifier.InAir, tpc.onAir && !tpc.isGrounded);
        // SetModifier(PS.Modifier.InCombat, combatSystem.IsInCombat); // placeholder
    }

    private void SetModifier(PS.Modifier modifier, bool value)
    {
        if (value)
            playerState.Modifiers.Add(modifier);
        else
            playerState.Modifiers.Remove(modifier);
    }
}
