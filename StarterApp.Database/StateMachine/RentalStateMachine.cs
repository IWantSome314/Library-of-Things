namespace StarterApp.Database.StateMachine;

// File purpose:
// Defines allowed rental status transitions and which actor is allowed to trigger each transition.
// This gives one source-of-truth for workflow enforcement.
public static class RentalStateMachine
{
    // Defines which transitions are valid and who can trigger them.
    public enum Actor { Owner, Requestor }

    private static readonly Dictionary<string, (string[] Targets, Actor[] AllowedActors)> _transitions = new()
    {
        ["Pending"]  = (["Approved", "Denied", "Cancelled"], [Actor.Owner, Actor.Owner, Actor.Requestor]),
        ["Approved"] = (["Active", "Cancelled"],             [Actor.Owner, Actor.Requestor]),
        ["Active"]   = (["Returned"],                        [Actor.Owner]),
    };

    public static bool CanTransition(string from, string to)
    {
        if (!_transitions.TryGetValue(from, out var entry))
            return false;

        return entry.Targets.Contains(to, StringComparer.OrdinalIgnoreCase);
    }

    public static bool CanTransitionAs(string from, string to, Actor actor)
    {
        // Actor checks run after structural transition checks to prevent unauthorized actions.
        if (!_transitions.TryGetValue(from, out var entry))
            return false;

        for (int i = 0; i < entry.Targets.Length; i++)
        {
            if (string.Equals(entry.Targets[i], to, StringComparison.OrdinalIgnoreCase))
                return entry.AllowedActors[i] == actor;
        }

        return false;
    }

    public static string[] GetAllowedTransitions(string from)
    {
        return _transitions.TryGetValue(from, out var entry) ? entry.Targets : [];
    }
}