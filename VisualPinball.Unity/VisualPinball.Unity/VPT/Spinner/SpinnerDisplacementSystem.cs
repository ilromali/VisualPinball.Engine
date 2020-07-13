using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.Physics.SystemGroup;
using Object = UnityEngine.Object;
using Player = VisualPinball.Unity.Game.Player;

namespace VisualPinball.Unity.VPT.Spinner
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class SpinnerDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("SpinnerDisplacementSystem");

		protected override void OnCreate()
		{
			_player = Object.FindObjectOfType<Player>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}

		protected override void OnUpdate()
		{

			var events = _eventQueue.AsParallelWriter();

			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities
				.WithName("SpinnerDisplacementJob")
				.ForEach((Entity entity, ref SpinnerMovementData movementData, in SpinnerStaticData data) => {

				marker.Begin();

				var angleMin = math.radians(data.AngleMin);
				var angleMax = math.radians(data.AngleMax);

				// blocked spinner, limited motion spinner
				if (data.AngleMin != data.AngleMax) {

					movementData.Angle += movementData.AngleSpeed * dTime;

					if (movementData.Angle > angleMax) {
						movementData.Angle = angleMax;

						// send EOS event
						events.Enqueue(new EventData(EventType.LimitEventsEOS, entity, math.abs(math.degrees(movementData.AngleSpeed))));

						if (movementData.AngleSpeed > 0.0f) {
							movementData.AngleSpeed *= -0.005f -data.Elasticity;
						}
					}

					if (movementData.Angle < angleMin) {
						movementData.Angle = angleMin;

						// send Park event
						events.Enqueue(new EventData(EventType.LimitEventsBOS, entity, math.abs(math.degrees(movementData.AngleSpeed))));

						if (movementData.AngleSpeed < 0.0f) {
							movementData.AngleSpeed *= -0.005f - data.Elasticity;
						}
					}

				} else {

					movementData.Angle += movementData.AngleSpeed * dTime;

					var target = movementData.AngleSpeed > 0.0f
						? movementData.Angle < math.PI ? math.PI : 3.0f * math.PI
						: movementData.Angle < math.PI ? -math.PI : math.PI;
					if (movementData.AngleSpeed > 0.0f) {

						if (movementData.AngleSpeed > target) {
							events.Enqueue(new EventData(EventType.SpinnerEventsSpin, entity, true));
						}

					} else {
						if (movementData.AngleSpeed < target) {
							events.Enqueue(new EventData(EventType.SpinnerEventsSpin, entity, true));
						}
					}

					while (movementData.Angle > 2.0f * math.PI) {
						movementData.Angle -= 2.0f * math.PI;
					}

					while (movementData.Angle < 0.0f) {
						movementData.Angle += 2.0f * math.PI;
					}
				}

				marker.End();

			}).Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {

				var spinnerApi = _player.Spinners[eventData.ItemEntity];
				// todo move this into player, so we handle the group events.
				switch (eventData.Type) {
					case EventType.LimitEventsEOS:
						spinnerApi.OnRotationEvent(eventData.FloatParam, true);
						break;

					case EventType.LimitEventsBOS:
						spinnerApi.OnRotationEvent(eventData.FloatParam, false);
						break;

					case EventType.SpinnerEventsSpin:
						spinnerApi.OnSpinEvent();
						break;

					default:
						throw new InvalidOperationException("Unhandled spinner event " + eventData.Type);
				}
			}
		}
	}
}
