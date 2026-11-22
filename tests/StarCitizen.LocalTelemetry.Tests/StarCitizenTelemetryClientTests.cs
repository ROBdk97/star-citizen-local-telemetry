using Grpc.Core;
using Sc.Game.Telemetry.V1;
using StarCitizen.LocalTelemetry;

namespace StarCitizen.LocalTelemetry.Tests;

public class StarCitizenTelemetryClientTests
{
    [Fact]
    public async Task ReconnectsAfterUnavailableAndDeliversNextFrame()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream(
            new TelemetryStreamResponse { Vehicle = new VehicleFrame { State = new VehicleState() } },
            new RpcException(new Status(StatusCode.Unavailable, "disconnected"))));
        invoker.Streams.Enqueue(new FakeStream(new TelemetryStreamResponse { Actor = new ActorFrame() }));
        using var client = CreateClient(invoker);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var frames = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMilliseconds(1), timeout.Token)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await frames.MoveNextAsync());
        Assert.NotNull(frames.Current.Vehicle);
        Assert.True(await frames.MoveNextAsync());
        Assert.NotNull(frames.Current.Actor);
        Assert.Equal(2, invoker.StreamCalls);
    }

    [Fact]
    public async Task ObservableStreamReportsConnectionAndUnavailable()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream(
            new TelemetryStreamResponse { Actor = new ActorFrame() },
            new RpcException(new Status(StatusCode.Unavailable, "disconnected"))));
        using var client = CreateClient(invoker);
        await using var updates = client.ObserveTelemetryAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(TelemetryStreamEventKind.Connecting, updates.Current.Kind);
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(TelemetryStreamEventKind.Connected, updates.Current.Kind);
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(TelemetryStreamEventKind.Frame, updates.Current.Kind);
        Assert.NotNull(updates.Current.Frame?.Actor);
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(TelemetryStreamEventKind.Disconnected, updates.Current.Kind);
        Assert.StartsWith("Unavailable", updates.Current.Reason);
    }

    [Fact]
    public async Task DoesNotReconnectOnNonTransientError()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream(
            new RpcException(new Status(StatusCode.PermissionDenied, "denied"))));
        using var client = CreateClient(invoker);
        await using var frames = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<RpcException>(async () => await frames.MoveNextAsync());
        Assert.Equal(StatusCode.PermissionDenied, error.StatusCode);
        Assert.Equal(1, invoker.StreamCalls);
    }

    [Fact]
    public async Task CancellationInterruptsReconnectDelay()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream());
        using var client = CreateClient(invoker);
        using var cancellation = new CancellationTokenSource();
        await using var frames = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMinutes(1), cancellation.Token)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        var move = frames.MoveNextAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => move);
        Assert.Equal(1, invoker.StreamCalls);
    }

    [Fact]
    public async Task ReconnectsAfterStreamCompletes()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream());
        invoker.Streams.Enqueue(new FakeStream(new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame { State = new VehicleState { Name = "Arrow" } }
        }));
        using var client = CreateClient(invoker);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var states = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMilliseconds(1), timeout.Token)
            .Vehicles(timeout.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await states.MoveNextAsync());
        Assert.Equal("Arrow", states.Current.Name);
        Assert.Equal(2, invoker.StreamCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RejectsInvalidReconnectDelay(int milliseconds)
    {
        using var client = CreateClient(new FakeCallInvoker());
        await using var frames = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await frames.MoveNextAsync());
    }

    [Fact]
    public async Task VehicleAndActorReadersSkipFramesWithoutThatSection()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream(
            new TelemetryStreamResponse { Actor = new ActorFrame() },
            new TelemetryStreamResponse { Vehicle = new VehicleFrame { State = new VehicleState { Name = "Arrow" } } }));
        invoker.Streams.Enqueue(new FakeStream(
            new TelemetryStreamResponse { Vehicle = new VehicleFrame() },
            new TelemetryStreamResponse { Actor = new ActorFrame { State = new ActorState { SeatName = "Pilot" } } }));
        using var client = CreateClient(invoker);

        var vehicles = new List<VehicleTelemetry>();
        await foreach (var vehicle in client.ReadTelemetryAsync(TestContext.Current.CancellationToken).Vehicles(cancellationToken: TestContext.Current.CancellationToken)) vehicles.Add(vehicle);
        var actors = new List<ActorTelemetry>();
        await foreach (var actor in client.ReadTelemetryAsync(TestContext.Current.CancellationToken).Actors(cancellationToken: TestContext.Current.CancellationToken)) actors.Add(actor);

        Assert.Equal("Arrow", Assert.Single(vehicles).Name);
        Assert.Equal("Pilot", Assert.Single(actors).State!.SeatName);
    }

    [Fact]
    public async Task ObserveChangesSkipsUnchangedSectionsButKeepsRepeatedEvents()
    {
        var invoker = new FakeCallInvoker();
        var first = new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame { State = new VehicleState { Name = "Arrow" } }
        };
        invoker.Streams.Enqueue(new FakeStream(
            first,
            new TelemetryStreamResponse { Vehicle = new VehicleFrame { State = new VehicleState { Name = "Arrow" } } },
            new TelemetryStreamResponse { Actor = new ActorFrame() },
            new TelemetryStreamResponse { Vehicle = new VehicleFrame { State = new VehicleState { Name = "Gladius" } } },
            new TelemetryStreamResponse { Events = new EventsFrame { Events = { new GameEvent() } } },
            new TelemetryStreamResponse { Events = new EventsFrame { Events = { new GameEvent() } } }));
        using var client = CreateClient(invoker);
        await using var changes = client.ReadTelemetryAsync(TestContext.Current.CancellationToken).ObserveChanges(cancellationToken: TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(TelemetrySections.Vehicle, changes.Current.Sections);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(TelemetrySections.Actor, changes.Current.Sections);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(TelemetrySections.Vehicle, changes.Current.Sections);
        Assert.Equal("Gladius", changes.Current.Frame.Vehicle!.Name);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(TelemetrySections.Events, changes.Current.Sections);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(TelemetrySections.Events, changes.Current.Sections);
        Assert.False(await changes.MoveNextAsync());
    }

    [Fact]
    public async Task FindActionsFiltersByCategoryAndHoldable()
    {
        var invoker = new FakeCallInvoker();
        invoker.Actions.Actions.Add(new TriggerableAction { ActionName = "on", Category = "lights" });
        invoker.Actions.Actions.Add(new TriggerableAction { ActionName = "ping", Category = "radar", Holdable = true });
        invoker.Actions.Actions.Add(new TriggerableAction { ActionName = "other", Category = "radar" });
        using var client = CreateClient(invoker);

        var matches = await client.FindActionsAsync("radar", true, TestContext.Current.CancellationToken);
        Assert.Equal("ping", Assert.Single(matches).Name);
        Assert.Equal(0, invoker.TriggerCalls);
        await Assert.ThrowsAsync<ArgumentException>(() => client.FindActionsAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckedActionUsesDiscoveryAndPreservesOptionalHold()
    {
        var invoker = new FakeCallInvoker();
        invoker.Actions.Actions.Add(new TriggerableAction { ActionName = "ping", Holdable = true });
        using var client = CreateClient(invoker);

        var response = await client.TriggerKnownActionAsync("ping", TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        Assert.Equal(ActionStatus.Ok, response.Status);
        Assert.Equal(1, invoker.TriggerCalls);
        Assert.Equal("ping", invoker.LastTrigger!.ActionName);
        Assert.True(invoker.LastTrigger.HasHoldMs);
        Assert.Equal(50u, invoker.LastTrigger.HoldMs);
    }

    [Fact]
    public async Task CheckedActionRejectsUnknownAndNonHoldableWithoutTriggering()
    {
        var invoker = new FakeCallInvoker();
        invoker.Actions.Actions.Add(new TriggerableAction { ActionName = "lights", Holdable = false });
        using var client = CreateClient(invoker);

        await Assert.ThrowsAsync<ArgumentException>(() => client.TriggerKnownActionAsync("unknown", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => client.TriggerKnownActionAsync("lights", TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal(0, invoker.TriggerCalls);
    }

    [Fact]
    public async Task TriggerIsNotRetriedAfterTransportFailure()
    {
        var invoker = new FakeCallInvoker { FailTrigger = true };
        using var client = CreateClient(invoker);

        await Assert.ThrowsAsync<RpcException>(() => client.TriggerActionAsync("lights", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, invoker.TriggerCalls);
        Assert.False(invoker.LastTrigger!.HasHoldMs);
    }

    [Fact]
    public async Task FramesAreMappedToCSharpModels()
    {
        var invoker = new FakeCallInvoker();
        var flightState = new VehicleFlightState { IsLanded = true, Vtol = VehicleComponentDeploymentState.Deployed };
        invoker.Streams.Enqueue(new FakeStream(new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame
            {
                State = new VehicleState
                {
                    Name = "Arrow",
                    LightsEnabled = true,
                    MasterMode = new VehicleMasterModeState { Mode = VehicleMasterMode.Scm },
                    Shields = new VehicleShields { Faces = { 1, 2 } },
                    Flight = new VehicleFlight
                    {
                        State = flightState,
                        Instruments = new VehicleFlightInstruments { Altitude = float.MaxValue, RadarAltitude = 2.5f }
                    }
                }
            },
            Events = new EventsFrame { Events = { new GameEvent { Type = GameEvent.Types.GameEventType.ActorEnterSeat } } }
        }));
        using var client = CreateClient(invoker);

        var frame = Assert.Single(await client.ReadTelemetryAsync(TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));

        var vehicle = frame.Vehicle!;
        Assert.True(vehicle.LightsOn);
        Assert.Equal(MasterMode.Scm, vehicle.MasterMode!.Mode);
        Assert.Equal(new ValueList<float>([1, 2]), vehicle.Shields!.Faces);
        Assert.True(vehicle.Flight!.IsLanded);
        Assert.Equal(DeploymentState.Deployed, vehicle.Flight.Vtol);
        Assert.Null(vehicle.Flight.LandingGear);
        Assert.Null(vehicle.Flight.Instruments!.Altitude);
        Assert.Equal(2.5f, vehicle.Flight.Instruments.RadarAltitude);
        Assert.Equal(GameEventType.ActorEnterSeat, Assert.Single(frame.Events));
        Assert.Null(frame.Actor);
    }

    [Fact]
    public async Task ObserveChangesComparesListsByValue()
    {
        var invoker = new FakeCallInvoker();
        TelemetryStreamResponse Weapons(string name) => new()
        {
            Weapons = new WeaponsFrame { Weapons = { new WeaponState { Name = name } } }
        };
        invoker.Streams.Enqueue(new FakeStream(Weapons("laser"), Weapons("laser"), Weapons("cannon")));
        using var client = CreateClient(invoker);

        var changes = await client.ReadTelemetryAsync(TestContext.Current.CancellationToken).ObserveChanges(cancellationToken: TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["laser", "cannon"], changes.Select(c => c.Frame.Weapons!.Weapons[0].Name));
    }

    [Fact]
    public async Task ActionHelpersSendTheMatchingActionAndHold()
    {
        var invoker = new FakeCallInvoker();
        using var client = CreateClient(invoker);

        var result = await client.SetLightsAsync(false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(GameActions.LightsController.LightsOff, invoker.LastTrigger!.ActionName);
        Assert.False(invoker.LastTrigger.HasHoldMs);

        await client.RadarPingAsync(TimeSpan.FromMilliseconds(250), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("v_invoke_ping", invoker.LastTrigger.ActionName);
        Assert.Equal(250u, invoker.LastTrigger.HoldMs);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetMasterModeAsync(MasterMode.Stealth, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, invoker.TriggerCalls);
    }

    [Fact]
    public async Task CancellingReadTelemetryThrowsOperationCanceled()
    {
        var invoker = new FakeCallInvoker();
        invoker.Streams.Enqueue(new FakeStream(
            new TelemetryStreamResponse(),
            new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client."))));
        using var client = CreateClient(invoker);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.ReadTelemetryAsync(cts.Token)) cts.Cancel();
        });
    }

    private static StarCitizenTelemetryClient CreateClient(FakeCallInvoker invoker) =>
        new(new GameTelemetryService.GameTelemetryServiceClient(invoker));

    private sealed class FakeStream(params object[] entries) : IAsyncStreamReader<TelemetryStreamResponse>
    {
        private readonly Queue<object> _entries = new(entries);
        public TelemetryStreamResponse Current { get; private set; } = null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            // Grpc.Net.Client reports client cancellation as RpcException(Cancelled), so queued errors win.
            if (_entries.TryPeek(out var next) && next is Exception queued)
            {
                _entries.Dequeue();
                return Task.FromException<bool>(queued);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!_entries.TryDequeue(out var entry)) return Task.FromResult(false);
            Current = (TelemetryStreamResponse)entry;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCallInvoker : CallInvoker
    {
        public Queue<FakeStream> Streams { get; } = new();
        public ListTriggerableActionsResponse Actions { get; } = new();
        public int StreamCalls { get; private set; }
        public int TriggerCalls { get; private set; }
        public TriggerGameActionRequest? LastTrigger { get; private set; }
        public bool FailTrigger { get; init; }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Assert.Equal("TelemetryStream", method.Name);
            StreamCalls++;
            var reader = (IAsyncStreamReader<TResponse>)(object)Streams.Dequeue();
            return new(reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
                () => new Metadata(), () => { });
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            object response;
            if (method.Name == "ListTriggerableActions")
            {
                response = Actions;
            }
            else
            {
                Assert.Equal("TriggerGameAction", method.Name);
                TriggerCalls++;
                LastTrigger = (TriggerGameActionRequest)(object)request;
                if (FailTrigger)
                {
                    return new(Task.FromException<TResponse>(
                        new RpcException(new Status(StatusCode.Unavailable, "disconnected"))),
                        Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
                        () => new Metadata(), () => { });
                }
                response = new TriggerGameActionResponse { Status = TriggerGameActionStatus.Ok };
            }

            return new(Task.FromResult((TResponse)response), Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotSupportedException();
    }
}
