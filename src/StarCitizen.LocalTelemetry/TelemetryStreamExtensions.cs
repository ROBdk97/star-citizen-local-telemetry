using System.Runtime.CompilerServices;

namespace StarCitizen.LocalTelemetry;

/// <summary>Filters and change detection for telemetry streams.</summary>
public static class TelemetryStreamExtensions
{
    /// <summary>
    /// Yields frames whose sections differ from the last seen value of each section. Missing sections are
    /// not treated as removed; non-empty event lists are always reported.
    /// </summary>
    /// <remarks>The comparison state lives for one enumeration, across reconnects; enumerate again to reset it.</remarks>
    /// <param name="frames">The telemetry stream.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>The frames with at least one changed section.</returns>
    public static async IAsyncEnumerable<TelemetryChange> ObserveChanges(
        this IAsyncEnumerable<TelemetryFrame> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        VehicleTelemetry? vehicle = null;
        WeaponsTelemetry? weapons = null;
        ActorTelemetry? actor = null;
        ForceFeedbackTelemetry? forceFeedback = null;
        TurretTelemetry? turret = null;

        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var changed = TelemetrySections.None;
            changed |= Track(frame.Vehicle, ref vehicle, TelemetrySections.Vehicle);
            changed |= Track(frame.Weapons, ref weapons, TelemetrySections.Weapons);
            changed |= Track(frame.Actor, ref actor, TelemetrySections.Actor);
            changed |= Track(frame.ForceFeedback, ref forceFeedback, TelemetrySections.ForceFeedback);
            changed |= Track(frame.Turret, ref turret, TelemetrySections.Turret);
            if (frame.Events.Count > 0)
            {
                changed |= TelemetrySections.Events;
            }

            if (changed != TelemetrySections.None)
            {
                yield return new(changed, frame);
            }
        }
    }

    /// <summary>Yields the vehicle section of each frame that has one.</summary>
    /// <param name="frames">The telemetry stream.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>Each <see cref="TelemetryFrame.Vehicle"/> that isn't <see langword="null"/>.</returns>
    public static IAsyncEnumerable<VehicleTelemetry> Vehicles(
        this IAsyncEnumerable<TelemetryFrame> frames,
        CancellationToken cancellationToken = default) =>
        Select(frames, frame => frame.Vehicle, cancellationToken);

    /// <summary>Yields the actor section of each frame that has one.</summary>
    /// <param name="frames">The telemetry stream.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>Each <see cref="TelemetryFrame.Actor"/> that isn't <see langword="null"/>.</returns>
    public static IAsyncEnumerable<ActorTelemetry> Actors(
        this IAsyncEnumerable<TelemetryFrame> frames,
        CancellationToken cancellationToken = default) =>
        Select(frames, frame => frame.Actor, cancellationToken);

    /// <summary>Yields the weapons section of each frame that has one.</summary>
    /// <param name="frames">The telemetry stream.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>Each <see cref="TelemetryFrame.Weapons"/> that isn't <see langword="null"/>.</returns>
    public static IAsyncEnumerable<WeaponsTelemetry> Weapons(
        this IAsyncEnumerable<TelemetryFrame> frames,
        CancellationToken cancellationToken = default) =>
        Select(frames, frame => frame.Weapons, cancellationToken);

    /// <summary>Yields each game event as it arrives.</summary>
    /// <param name="frames">The telemetry stream.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>The events of every frame, in order. Events can repeat.</returns>
    public static async IAsyncEnumerable<GameEventType> GameEvents(
        this IAsyncEnumerable<TelemetryFrame> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var gameEvent in frame.Events)
            {
                yield return gameEvent;
            }
        }
    }

    private static TelemetrySections Track<T>(T? next, ref T? last, TelemetrySections section) where T : class
    {
        if (next is null || next.Equals(last))
        {
            return TelemetrySections.None;
        }

        last = next;
        return section;
    }

    private static async IAsyncEnumerable<T> Select<T>(
        IAsyncEnumerable<TelemetryFrame> frames,
        Func<TelemetryFrame, T?> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(frames);
        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (selector(frame) is { } value)
            {
                yield return value;
            }
        }
    }
}
