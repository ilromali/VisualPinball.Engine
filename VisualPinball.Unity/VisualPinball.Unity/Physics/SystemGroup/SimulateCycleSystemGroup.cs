﻿using System.Collections.Generic;
using NLog;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		public float HitTime;
		public bool SwapBallCollisionHandling;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private StaticBroadPhaseSystem _staticBroadPhaseSystem;
		private DynamicBroadPhaseSystem _dynamicBroadPhaseSystem;
		private StaticNarrowPhaseSystem _staticNarrowPhaseSystem;
		private DynamicNarrowPhaseSystem _dynamicNarrowPhaseSystem;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private StaticCollisionSystem _staticCollisionSystem;
		private DynamicCollisionSystem _dynamicCollisionSystem;
		private ContactSystem _contactSystem;

		protected override void OnCreate()
		{
			_staticBroadPhaseSystem = World.GetOrCreateSystem<StaticBroadPhaseSystem>();
			_dynamicBroadPhaseSystem = World.GetOrCreateSystem<DynamicBroadPhaseSystem>();
			_staticNarrowPhaseSystem = World.GetOrCreateSystem<StaticNarrowPhaseSystem>();
			_dynamicNarrowPhaseSystem = World.GetOrCreateSystem<DynamicNarrowPhaseSystem>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_staticCollisionSystem = World.GetOrCreateSystem<StaticCollisionSystem>();
			_dynamicCollisionSystem = World.GetOrCreateSystem<DynamicCollisionSystem>();
			_contactSystem = World.GetOrCreateSystem<ContactSystem>();
			_systemsToUpdate.Add(_staticBroadPhaseSystem);
			_systemsToUpdate.Add(_dynamicBroadPhaseSystem);
			_systemsToUpdate.Add(_staticNarrowPhaseSystem);
			_systemsToUpdate.Add(_dynamicNarrowPhaseSystem);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_staticCollisionSystem);
			_systemsToUpdate.Add(_dynamicCollisionSystem);
			_systemsToUpdate.Add(_contactSystem);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			var staticCnts = PhysicsConstants.StaticCnts;
			var dTime = sim.PhysicsDiffTime;
			while (dTime > 0) {

				HitTime = (float)dTime;
				var hitTime1 = HitTime;

				ApplyFlipperTime(sim);
				var hitTime2 = HitTime;

				_dynamicBroadPhaseSystem.Update();
				_staticBroadPhaseSystem.Update();
				_staticNarrowPhaseSystem.Update();
				_dynamicNarrowPhaseSystem.Update();

				ApplyStaticTime(ref staticCnts);
				var hitTime3 = HitTime;

				_displacementSystemGroup.Update();
				_dynamicCollisionSystem.Update();
				_staticCollisionSystem.Update();
				_contactSystem.Update();
				var hitTime4 = HitTime;

				dTime -= HitTime;

				SwapBallCollisionHandling = !SwapBallCollisionHandling;

				#if TIME_LOG
				if (sim.StartLogTimeUsec > 0 && dTime > 0) {
					sim.Log($"     ({dTime}) Player::PhysicsSimulateCycle (inner loop): {hitTime1} -> {hitTime2} -> {hitTime3} -> {hitTime4}");
				}
				#endif

			}
		}

		private void ApplyFlipperTime(VisualPinballSimulationSystemGroup sim)
		{
			// update hittime
			var collDataEntityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FlipperMovementData>(), ComponentType.ReadOnly<FlipperStaticData>());
			var entities = collDataEntityQuery.ToEntityArray(Allocator.TempJob);
			foreach (var entity in entities) {
				var movementData = EntityManager.GetComponentData<FlipperMovementData>(entity);
				var staticData = EntityManager.GetComponentData<FlipperStaticData>(entity);
				var flipperHitTime = movementData.GetHitTime(staticData.AngleStart, staticData.AngleEnd);
				//sim.Log($"     flipper hit time = {flipperHitTime}");
				if (flipperHitTime > 0 && flipperHitTime < HitTime) { //!! >= 0.f causes infinite loop
					HitTime = flipperHitTime;
				}
			}
			entities.Dispose();
		}

		private void ApplyStaticTime(ref float staticCnts)
		{
			// update hittime
			var collDataEntityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());
			var entities = collDataEntityQuery.ToEntityArray(Allocator.TempJob);
			foreach (var entity in entities) {
				var collEvent = EntityManager.GetComponentData<CollisionEventData>(entity);
				if (collEvent.HasCollider() && collEvent.HitTime <= HitTime) {       // smaller hit time??
					HitTime = collEvent.HitTime;                                     // record actual event time
					if (collEvent.HitTime < PhysicsConstants.StaticTime) {           // less than static time interval
						if (--staticCnts < 0) {
							staticCnts = 0;                                          // keep from wrapping
							HitTime = PhysicsConstants.StaticTime;
						}
					}
				}
			}
			entities.Dispose();
		}
	}
}
