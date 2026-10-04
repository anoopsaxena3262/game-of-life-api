namespace GameOfLife.Domain;

/// <summary>
/// Walks a board forward until it concludes, or until a generation limit is reached.
/// </summary>
/// <remarks>
/// A board concludes on a fixed point (the next generation is identical) or on a
/// cycle (any previously seen generation recurs). Oscillators such as the blinker
/// never go still, so stillness alone would misclassify them.
/// </remarks>
public static class TerminationDetector
{
    private const ulong FnvOffsetA = 0xcbf29ce484222325UL;
    private const ulong FnvOffsetB = 0x6c62272e07bb0142UL;
    private const ulong FnvPrime = 0x100000001b3UL;
    private const ulong GoldenRatio = 0x9e3779b97f4a7c15UL;

    /// <summary>
    /// How often the encoded grid is kept so a cycle can be confirmed without
    /// replaying from generation 0. On a 300x300 board each checkpoint is 90,000
    /// characters, and a walk to the generation ceiling keeps about 40 of them.
    /// </summary>
    internal const int CheckpointInterval = 256;

    /// <param name="initialState">Generation 0, flat encoded.</param>
    /// <param name="maxGenerations">How many generations to walk before giving up.</param>
    /// <returns>The termination outcome, or null if no conclusion was reached in time.</returns>
    public static TerminationResult? Detect(string initialState, int width, int height, int maxGenerations) =>
        Detect(initialState, width, height, maxGenerations, Fingerprint, CheckpointInterval);

    /// <summary>
    /// Same walk with a replaceable fingerprint, so a test can force every state onto one
    /// hash and prove a collision is not a cycle.
    /// </summary>
    internal static TerminationResult? Detect(
        string initialState, int width, int height, int maxGenerations, Func<string, StateHash> fingerprint) =>
        Detect(initialState, width, height, maxGenerations, fingerprint, CheckpointInterval);

    /// <summary>Same walk with a different checkpoint interval.</summary>
    internal static TerminationResult? Detect(
        string initialState, int width, int height, int maxGenerations, int checkpointInterval) =>
        Detect(initialState, width, height, maxGenerations, Fingerprint, checkpointInterval);

    internal static TerminationResult? Detect(
        string initialState,
        int width,
        int height,
        int maxGenerations,
        Func<string, StateHash> fingerprint,
        int checkpointInterval)
    {
        // Seen states are keyed by a fingerprint, not the grid. /final does not write
        // rows, and keeping every grid would exhaust memory on a 300x300 board before
        // the generation ceiling. The fingerprint is two 64-bit FNV-1a lanes over the
        // same characters, so a matching key is only a candidate.
        // The value is the list of generations that produced this fingerprint.
        // A candidate is a cycle only when the full string matches. That string is
        // replayed from the nearest checkpoint, at most checkpointInterval - 1 steps.
        var seen = new Dictionary<StateHash, List<int>>
        {
            [fingerprint(initialState)] = [0],
        };
        var checkpoints = new Dictionary<int, string> { [0] = initialState };

        var current = initialState;

        for (var i = 0; i < maxGenerations; i++)
        {
            var next = LifeEngine.Step(current, width, height);
            var nextGeneration = i + 1;

            // Fixed point: the next generation is identical.
            if (next == current)
            {
                var kind = StateCodec.IsExtinct(next) ? TerminationKind.Extinct : TerminationKind.FixedPoint;
                return new TerminationResult(kind, next, i, 1, nextGeneration);
            }

            // Cycle. Same hash is only a candidate; confirm the grids match.
            var hash = fingerprint(next);
            if (seen.TryGetValue(hash, out var earlier))
            {
                foreach (var firstOccurrence in earlier)
                {
                    var previous = StateAt(checkpoints, width, height, firstOccurrence, checkpointInterval);
                    if (next != previous)
                    {
                        continue;
                    }

                    var period = nextGeneration - firstOccurrence;
                    return new TerminationResult(TerminationKind.Cycle, next, firstOccurrence, period, nextGeneration);
                }
            }
            else
            {
                earlier = [];
                seen[hash] = earlier;
            }

            // Record this state and continue.
            earlier.Add(nextGeneration);
            if (nextGeneration % checkpointInterval == 0)
            {
                checkpoints[nextGeneration] = next;
            }

            current = next;
        }

        // No conclusion reached within maxGenerations.
        return null;
    }

    /// <summary>Two 64-bit FNV-1a lanes over the same characters. A match is checked against the full state.</summary>
    private static StateHash Fingerprint(string state)
    {
        unchecked
        {
            var high = FnvOffsetA;
            var low = FnvOffsetB;
            foreach (var c in state)
            {
                ulong cell = c;
                high ^= cell;
                high *= FnvPrime;
                low ^= cell + GoldenRatio;
                low *= FnvPrime;
            }

            return new StateHash(high, low);
        }
    }

    private static string StateAt(
        Dictionary<int, string> checkpoints, int width, int height, int index, int checkpointInterval)
    {
        // index is never negative, so % matches a floor modulus here.
        var origin = index - (index % checkpointInterval);
        var state = checkpoints[origin];
        for (var generation = origin; generation < index; generation++)
        {
            state = LifeEngine.Step(state, width, height);
        }

        return state;
    }

    internal readonly record struct StateHash(ulong High, ulong Low);
}
