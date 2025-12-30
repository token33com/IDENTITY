using UnityEngine;
using PlayerStates;

public class ModifierComponent : MonoBehaviour
{
    [SerializeField]
    private Modifier activeModifiers = Modifier.None;

    public Modifier Current => activeModifiers;

    public void Add(Modifier modifier)
    {
        activeModifiers |= modifier;
    }

    public void Remove(Modifier modifier)
    {
        activeModifiers &= ~modifier;
    }

    public bool Has(Modifier modifier)
    {
        return (activeModifiers & modifier) != 0;
    }

    public void Clear()
    {
        activeModifiers = Modifier.None;
    }
}
