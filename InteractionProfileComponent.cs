using UnityEngine;
using PlayerStates;

public class InteractionProfileComponent : MonoBehaviour
{
    [SerializeField]
    private InteractionProfile currentProfile = InteractionProfile.None;

    public InteractionProfile Current => currentProfile;

    public bool IsActive => currentProfile != InteractionProfile.None;

    public void SetProfile(InteractionProfile profile)
    {
        currentProfile = profile;
    }

    public void Clear()
    {
        currentProfile = InteractionProfile.None;
    }
}
