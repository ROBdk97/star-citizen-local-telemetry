using System.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sc.Game.Telemetry.V1;
using StarCitizen.LocalTelemetry;

namespace StarCitizen.LocalTelemetry.Tests;

public class GrpcIntegrationTests
{
    [Fact]
    public async Task ClientStreamsReconnectsAndInvokesActionsAgainstLocalServer()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<TestState>();

        await using var app = builder.Build();
        app.MapGrpcService<TestTelemetryService>();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = Assert.Single(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses);
        var state = app.Services.GetRequiredService<TestState>();
        using var client = new StarCitizenTelemetryClient(new Uri(address));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using (var frames = client.ReadTelemetryWithReconnectAsync(TimeSpan.FromMilliseconds(10), timeout.Token)
                         .GetAsyncEnumerator(TestContext.Current.CancellationToken))
        {
            Assert.True(await frames.MoveNextAsync());
            Assert.Equal("first", frames.Current.Vehicle!.Name);
            Assert.True(await frames.MoveNextAsync());
            Assert.Equal("second", frames.Current.Vehicle!.Name);
        }

        Assert.Equal(2, state.StreamCalls);
        var actions = await client.FindActionsAsync("lights", cancellationToken: timeout.Token);
        Assert.Equal("test_lights", Assert.Single(actions).Name);
        var response = await client.TriggerKnownActionAsync("test_lights", cancellationToken: timeout.Token);
        Assert.Equal(ActionStatus.Ok, response.Status);
        Assert.Equal("test_lights", state.LastAction?.ActionName);
        Assert.False(state.LastAction!.HasHoldMs);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.TriggerKnownActionAsync("test_lights", TimeSpan.FromMilliseconds(10), timeout.Token));
        Assert.Equal(1, state.TriggerCalls);
    }

    internal sealed class TestState
    {
        public int StreamCalls;
        public int TriggerCalls;
        public TriggerGameActionRequest? LastAction;
    }

    internal sealed class TestTelemetryService(TestState state) : GameTelemetryService.GameTelemetryServiceBase
    {
        public override async Task TelemetryStream(TelemetryStreamRequest request,
            IServerStreamWriter<TelemetryStreamResponse> responseStream, ServerCallContext context)
        {
            var call = Interlocked.Increment(ref state.StreamCalls);
            await responseStream.WriteAsync(new TelemetryStreamResponse
            {
                Vehicle = new VehicleFrame { State = new VehicleState { Name = call == 1 ? "first" : "second" } }
            });
            if (call == 1)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "test disconnect"));
            }
        }

        public override Task<ListTriggerableActionsResponse> ListTriggerableActions(
            ListTriggerableActionsRequest request, ServerCallContext context) =>
            Task.FromResult(new ListTriggerableActionsResponse
            {
                Actions = { new TriggerableAction { ActionName = "test_lights", Category = "lights" } }
            });

        public override Task<TriggerGameActionResponse> TriggerGameAction(
            TriggerGameActionRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref state.TriggerCalls);
            state.LastAction = request.Clone();
            return Task.FromResult(new TriggerGameActionResponse { Status = TriggerGameActionStatus.Ok });
        }
    }
}
