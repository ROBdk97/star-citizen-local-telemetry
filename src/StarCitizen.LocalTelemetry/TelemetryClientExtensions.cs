using System.Runtime.CompilerServices;

namespace StarCitizen.LocalTelemetry;

/// <summary>Helpers built on <see cref="ITelemetryClient"/>, so they also work with fakes.</summary>
public static class TelemetryClientExtensions
{
    /// <summary>Streams telemetry and reconnects when the game is unavailable or ends the stream.</summary>
    /// <param name="client">The client to read from.</param>
    /// <param name="retryDelay">The delay before reconnecting; the client's default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    /// <returns>The frames of <see cref="ITelemetryClient.ObserveTelemetryAsync"/>, without the other events.</returns>
    public static async IAsyncEnumerable<TelemetryFrame> ReadTelemetryWithReconnectAsync(
        this ITelemetryClient client,
        TimeSpan? retryDelay = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await foreach (var update in client.ObserveTelemetryAsync(retryDelay, cancellationToken).ConfigureAwait(false))
        {
            if (update.Frame is { } frame)
            {
                yield return frame;
            }
        }
    }

    /// <summary>Returns advertised actions, optionally filtered by category and holdability.</summary>
    /// <param name="client">The client to query.</param>
    /// <param name="category">Only return actions in this category, for example <see cref="GameActions.LightsController.Category"/>.</param>
    /// <param name="holdable">Only return actions that are (or are not) holdable.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching actions.</returns>
    /// <exception cref="ArgumentException"><paramref name="category"/> is empty or whitespace.</exception>
    public static async Task<IReadOnlyList<GameAction>> FindActionsAsync(
        this ITelemetryClient client,
        string? category = null,
        bool? holdable = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (category is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
        }

        var actions = await client.GetActionsAsync(cancellationToken).ConfigureAwait(false);
        return actions.Where(action =>
                (category is null || action.Category == category) &&
                (!holdable.HasValue || action.Holdable == holdable.Value))
            .ToArray();
    }

    /// <summary>
    /// Like <see cref="ITelemetryClient.TriggerActionAsync"/>, but first checks that the game advertises the action
    /// (and that it is holdable when <paramref name="hold"/> is set).
    /// </summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="actionName">An action name, for example <see cref="GameActions.LightsController.LightsOn"/>.</param>
    /// <param name="hold">How long to hold the action; only valid for holdable actions.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response; check <see cref="ActionResult.IsSuccess"/>.</returns>
    /// <exception cref="ArgumentException">The game does not advertise the action, or it is not holdable.</exception>
    public static async Task<ActionResult> TriggerKnownActionAsync(
        this ITelemetryClient client,
        string actionName,
        TimeSpan? hold = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        var actions = await client.GetActionsAsync(cancellationToken).ConfigureAwait(false);
        var action = actions.FirstOrDefault(a => a.Name == actionName)
            ?? throw new ArgumentException("The game did not advertise this action.", nameof(actionName));

        if (hold.HasValue && !action.Holdable)
        {
            throw new ArgumentException("The game did not advertise this action as holdable.", nameof(hold));
        }

        return await client.TriggerActionAsync(actionName, hold, cancellationToken).ConfigureAwait(false);
    }
}
