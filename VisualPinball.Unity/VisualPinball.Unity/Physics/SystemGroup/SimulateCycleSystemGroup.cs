﻿using System.Collections.Generic;
using NLog;
using Unity.Entities;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		public double DTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private BallBroadPhaseSystem _ballBroadPhaseSystem;
		private BallNarrowPhaseSystemGroup _ballNarrowPhaseSystemGroup;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private BallResolveCollisionSystem _ballResolveCollisionSystem;
		private BallContactSystem _ballContactSystem;

		protected override void OnCreate()
		{
			_ballBroadPhaseSystem = World.GetOrCreateSystem<BallBroadPhaseSystem>();
			_ballNarrowPhaseSystemGroup = World.GetOrCreateSystem<BallNarrowPhaseSystemGroup>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_ballResolveCollisionSystem = World.GetOrCreateSystem<BallResolveCollisionSystem>();
			_ballContactSystem = World.GetOrCreateSystem<BallContactSystem>();
			_systemsToUpdate.Add(_ballBroadPhaseSystem);
			_systemsToUpdate.Add(_ballNarrowPhaseSystemGroup);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_ballResolveCollisionSystem);
			_systemsToUpdate.Add(_ballContactSystem);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			DTime = sim.PhysicsDiffTime;
			while (DTime > 0) {

				//Logger.Info("     ({0}) Player::PhysicsSimulateCycle (loop)\n", DTime);

				var hitTime = DTime;

				_ballBroadPhaseSystem.Update();
				_ballNarrowPhaseSystemGroup.Update();
				_displacementSystemGroup.Update();
				_ballResolveCollisionSystem.Update();
				_ballContactSystem.Update();

				DTime -= hitTime;
			}
		}
	}
}