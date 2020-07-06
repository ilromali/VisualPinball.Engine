﻿using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Rubber;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class Player : MonoBehaviour
	{
		private readonly TableApi _tableApi = new TableApi();

		private readonly Dictionary<Entity, IApiHittable> _hittables = new Dictionary<Entity, IApiHittable>();
		private readonly Dictionary<Entity, FlipperApi> _flippers = new Dictionary<Entity, FlipperApi>();
		private FlipperApi Flipper(Entity entity) => _flippers.Values.FirstOrDefault(f => f.Entity == entity);

		private Table _table;
		private BallManager _ballManager;
		private Player _player;

		public Matrix4x4 TableToWorld => transform.localToWorldMatrix;

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			//AttachToRoot(entity, go);
			var flipperApi = new FlipperApi(flipper, entity, this);
			_tableApi.Flippers[flipper.Name] = flipperApi;
			_flippers[entity] = flipperApi;
			_hittables[entity] = flipperApi;
			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}

			// World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FlipperSystem>().OnRotated +=
			// 	(sender, e) => flipperApi.HandleEvent(e);
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			var kickerApi = new KickerApi(kicker, entity, this);
			_tableApi.Kickers[kicker.Name] = kickerApi;
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, GameObject go)
		{
			_tableApi.Plungers[plunger.Name] = new PlungerApi(plunger, entity, this);
		}

		public void RegisterSurface(Surface item, Entity entity, GameObject go)
		{
			_hittables[entity] = new SurfaceApi(item, entity, this);
		}

		public void RegisterRubber(Rubber item, Entity entity, GameObject go)
		{
			_hittables[entity] = new RubberApi(item, entity, this);
		}

		public void OnItemHit(HitEvent hitEvent)
		{
			if (_hittables.ContainsKey(hitEvent.ItemEntity)) {
				Debug.Log("Got a hit on entity " + hitEvent.ItemEntity);
				_hittables[hitEvent.ItemEntity].OnHit();
			}
		}

		public BallApi CreateBall(IBallCreationPosition ballCreator, float radius = 25, float mass = 1)
		{
			// todo callback and other stuff
			return _ballManager.CreateBall(this, ballCreator, radius, mass);
		}

		public float3 GetGravity()
		{
			var slope = _table.Data.AngleTiltMin + (_table.Data.AngleTiltMax - _table.Data.AngleTiltMin) * _table.Data.GlobalDifficulty;
			var strength = _table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : _table.Data.Gravity;
			return new float3(0,  math.sin(math.radians(slope)) * strength, -math.cos(math.radians(slope)) * strength);
		}

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableBehavior>();
			_table = tableComponent.CreateTable();
			_ballManager = new BallManager(_table);
			_player = gameObject.GetComponent<Player>();

			World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<StaticCollisionSystem>().Player = _player;
		}

		private void Start()
		{
			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(_tableApi);
			}

			// trigger init events now
			foreach (var i in _tableApi.Initializables) {
				i.OnInit();
			}
		}

		private void Update()
		{
			// flippers will be handled via script later, but until scripting works, do it here.
			if (Input.GetKeyDown("left shift")) {
				_tableApi.Flipper("LeftFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("left shift")) {
				_tableApi.Flipper("LeftFlipper")?.RotateToStart();
			}
			if (Input.GetKeyDown("right shift")) {
				_tableApi.Flipper("RightFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("right shift")) {
				_tableApi.Flipper("RightFlipper")?.RotateToStart();
			}

			if (Input.GetKeyUp("b")) {
				_player.CreateBall(new DebugBallCreator());
				// _player.CreateBall(new DebugBallCreator(425, 1325));
				// _player.CreateBall(new DebugBallCreator(390, 1125));

				// _player.CreateBall(new DebugBallCreator(475, 1727.5f));
				// _tableApi.Flippers["RightFlipper"].RotateToEnd();
			}

			if (Input.GetKeyUp("n")) {
				_player.CreateBall(new DebugBallCreator(129f, 1450f));
				//_tableApi.Flippers["LeftFlipper"].RotateToEnd();
			}

			if (Input.GetKeyDown(KeyCode.Return)) {
				_tableApi.Plunger("CustomPlunger")?.PullBack();
				_tableApi.Plunger("Plunger001")?.PullBack();
				_tableApi.Plunger("Plunger002")?.PullBack();
			}
			if (Input.GetKeyUp(KeyCode.Return)) {
				_tableApi.Plunger("CustomPlunger")?.Fire();
				_tableApi.Plunger("Plunger001")?.Fire();
				_tableApi.Plunger("Plunger002")?.Fire();
			}
		}
	}
}
