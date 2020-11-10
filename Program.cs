using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Serilog;

#nullable enable
internal class Program {
	public static async Task<int> Main(string[] args) {
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		var results = await Task.WhenAll(Enumerable.Range(0, Environment.ProcessorCount).Select(TestHang));

		if (!results.All(x => x)) {
			Log.Warning("Test run failed.");
			return 1;
		}

		Log.Information("Test run succeeded.");
		return 0;
	}

	private static async Task<bool> TestHang(int index) {
		const int batchSize = 36;
		const int batches = 40;

		var data = Encoding.UTF8.GetBytes("{}");
		var complete = new TaskCompletionSource<bool>();
		var streamName = $"eventstore-tests-hang-{index}-{Guid.NewGuid():n}";

		var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
//		settings.LoggerFactory = new SerilogLoggerFactory();

		var client = new EventStoreClient(settings);

		using var subscription = client.SubscribeToAllAsync(
			Position.End, EventAppeared, subscriptionDropped: SubscriptionDropped);

		await Task.Delay(10);
		await client.AppendToStreamAsync(streamName, StreamState.NoStream,
			new[] {new EventData(Uuid.NewUuid(), "start", data)});

		var lastStreamRevision = new StreamRevision(0);

		for (var i = 0; i < batches; i++) {
			const int firstSliceCount = 10;
			await client.AppendToStreamAsync(streamName, lastStreamRevision,
				Enumerable.Range(0, firstSliceCount)
					.Select(_ => new EventData(Uuid.NewUuid(), "event", data)));
			await client.AppendToStreamAsync(streamName, lastStreamRevision + firstSliceCount,
				Enumerable.Range(0, batchSize - firstSliceCount)
					.Select(_ => new EventData(Uuid.NewUuid(), "event", data)));
			lastStreamRevision += batchSize;
		}

		await client.AppendToStreamAsync(streamName, lastStreamRevision,
			new[] {new EventData(Uuid.NewUuid(), "complete", data)});

		return await complete.Task;

		Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
			if (e.OriginalStreamId != streamName) {
				return Task.CompletedTask;
			}

			if (e.OriginalEvent.EventType != "complete") {
				return Task.Delay(TimeSpan.FromMilliseconds(0.8), ct);
			}

			complete.TrySetResult(true);
			return Task.CompletedTask;
		}

		void SubscriptionDropped(StreamSubscription subscription, SubscriptionDroppedReason reason,
			Exception? ex) {
			if (ex != null) {
				Log.Warning(ex, "Subscription {subscriptionId} dropped: {reason}", subscription.SubscriptionId,
					reason);
			} else {
				Log.Warning("Subscription {subscriptionId} dropped: {reason}", subscription.SubscriptionId, reason);
			}

			complete.TrySetResult(false);
		}
	}
}
