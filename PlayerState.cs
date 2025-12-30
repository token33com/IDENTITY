using UnityEngine;

[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    public MovementStateComponent Movement { get; private set; }
    public ActionStateComponent Action { get; private set; }
    public ModifierComponent Modifiers { get; private set; }
    public ReactionComponent Reaction { get; private set; }
    public InteractionProfileComponent Interaction { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<MovementStateComponent>();
        Action = GetComponent<ActionStateComponent>();
        Modifiers = GetComponent<ModifierComponent>();
        Reaction = GetComponent<ReactionComponent>();
        Interaction = GetComponent<InteractionProfileComponent>();

        Validate();
    }

    private void Validate()
    {
        if (!Movement) Debug.LogError("Missing MovementStateComponent", this);
        if (!Action) Debug.LogError("Missing ActionStateComponent", this);
        if (!Modifiers) Debug.LogError("Missing ModifierComponent", this);
        if (!Reaction) Debug.LogError("Missing ReactionComponent", this);
        if (!Interaction) Debug.LogError("Missing InteractionProfileComponent", this);
    }
}
