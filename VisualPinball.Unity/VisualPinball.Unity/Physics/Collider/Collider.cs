﻿using System;
using NLog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Spinner;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Physics.Collider
{
	/// <summary>
	/// Base struct common to all colliders.
	/// Dispatches the interface methods to appropriate implementations for the collider type.
	/// </summary>
	public struct Collider : ICollider, IComponentData
	{
		public ColliderHeader Header;

		public int Id => Header.Id;
		public Entity Entity => Header.Entity;
		public ColliderType Type => Header.Type;
		public PhysicsMaterialData Material => Header.Material;

		public ItemType ItemType => Header.ItemType;
		public float Threshold => Header.Threshold;
		public bool FireEvents => Header.FireEvents;
		public bool IsEnabled => Header.IsEnabled;

		public static Collider None => new Collider
		{
			Header =
			{
				Type = ColliderType.None
			}
		};

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Create(BlobBuilder builder, HitObject src, ref BlobPtr<Collider> dest)
		{
			switch (src)
			{
				case TriggerHitCircle triggerHitCircle:
					CircleCollider.Create(builder, triggerHitCircle, ref dest, ColliderType.TriggerCircle);
					break;
				case TriggerHitLineSeg triggerHitLine:
					LineCollider.Create(builder, triggerHitLine, ref dest, ColliderType.TriggerLine);
					break;
				case BumperHit bumperHit:
					CircleCollider.Create(builder, bumperHit, ref dest, ColliderType.Bumper);
					break;
				case HitCircle hitCircle:
					CircleCollider.Create(builder, hitCircle, ref dest);
					break;
				case LineSegSlingshot lineSegSlingshot:
					LineSlingshotCollider.Create(builder, lineSegSlingshot, ref dest);
					break;
				case FlipperHit flipperHit:
					FlipperCollider.Create(builder, flipperHit, ref dest);
					break;
				case GateHit gateHit:
					GateCollider.Create(builder, gateHit, ref dest);
					break;
				case LineSeg lineSeg:
					LineCollider.Create(builder, lineSeg, ref dest);
					break;
				case HitLine3D hitLine3D:
					Line3DCollider.Create(builder, hitLine3D, ref dest);
					break;
				case HitLineZ hitLineZ:
					LineZCollider.Create(builder, hitLineZ, ref dest);
					break;
				case HitPoint hitPoint:
					PointCollider.Create(builder, hitPoint, ref dest);
					break;
				case Hit3DPoly hit3DPoly:
					Poly3DCollider.Create(builder, hit3DPoly, ref dest);
					break;
				case HitPlane hitPlane:
					PlaneCollider.Create(builder, hitPlane, ref dest);
					break;
				case PlungerHit plungerCollider:
					PlungerCollider.Create(builder, plungerCollider, ref dest);
					break;
				case SpinnerHit spinnerHit:
					SpinnerCollider.Create(builder, spinnerHit, ref dest);
					break;
				case HitTriangle hitTriangle:
					TriangleCollider.Create(builder, hitTriangle, ref dest);
					break;
				default:
					Logger.Warn("Unknown collider {0}, skipping.", src.GetType().Name);
					break;
			}
		}

		public static unsafe float HitTest(ref Collider coll, ref CollisionEventData collEvent,
			ref DynamicBuffer<BallInsideOfBufferElement> insideOf, in BallData ball, float dTime)
		{
			fixed (Collider* collider = &coll)
			{
				switch (collider->Type)
				{
					case ColliderType.Bumper:
					case ColliderType.Circle:
						return ((CircleCollider*) collider)->HitTest(ref collEvent, ref insideOf, in ball, dTime);
					case ColliderType.Gate:
						return ((GateCollider*) collider)->HitTest(ref collEvent, ref insideOf, in ball, dTime);
					case ColliderType.Line:
						return ((LineCollider*) collider)->HitTest(ref collEvent, ref insideOf, in ball, dTime);
					case ColliderType.LineZ:
						return ((LineZCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Line3D:
						return ((Line3DCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Point:
						return ((PointCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Plane:
						return ((PlaneCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Poly3D:
						return ((Poly3DCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Spinner:
						return ((SpinnerCollider*) collider)->HitTest(ref collEvent, ref insideOf, in ball, dTime);
					case ColliderType.Triangle:
						return ((TriangleCollider*) collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.TriggerCircle:
						return ((CircleCollider*) collider)->HitTestBasicRadius(ref collEvent, ref insideOf, in ball,
							dTime, false, false, false);
					case ColliderType.TriggerLine:
						return ((LineCollider*) collider)->HitTestBasic(ref collEvent, ref insideOf, in ball, dTime,
							false, false, false);

					case ColliderType.Plunger:
					case ColliderType.Flipper:
					case ColliderType.LineSlingShot:
						throw new InvalidOperationException(coll.Type + " must be hit-tested separately!");

					default:
						return -1;
				}
			}
		}

		/// <summary>
		/// Most colliders use the standard Collide3DWall routine, only overrides
		/// are cast and dispatched to their respective implementation.
		/// </summary>
		public static unsafe void Collide(ref Collider coll, ref BallData ballData,
			ref NativeQueue<EventData>.ParallelWriter events, in CollisionEventData collEvent,
			ref Random random)
		{
			fixed (Collider* collider = &coll)
			{
				switch (collider->Type)
				{
					case ColliderType.Plane:
						((PlaneCollider*) collider)->Collide(ref ballData, in collEvent, ref random);
						break;
					default:
						collider->Collide(ref ballData, ref events, in collEvent, in coll, ref random);
						break;
				}
			}
		}

		private void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData events, in Collider coll, ref Random random)
		{
			var dot = math.dot(events.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in events, in events.HitNormal, ref random);

			if (dot <= -coll.Threshold) {
				FireHitEvent(ref ball, ref hitEvents, in coll.Header);
			}
		}

		public static void FireHitEvent(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events, in ColliderHeader collHeader)
		{
			if (collHeader.FireEvents/* && collHeader.IsEnabled*/) { // todo enabled

				// is this the same place as last event? if same then ignore it
				var posDiff = ball.EventPosition - ball.Position;
				var distLs = math.lengthsq(posDiff);

				// remember last collide position
				ball.EventPosition = ball.Position;

				// hit targets when used with a captured ball have always a too small distance
				var normalDist = collHeader.ItemType == ItemType.HitTarget ? 0.0f : 0.25f; // magic distance

				// must be a new place if only by a little
				if (distLs > normalDist) {
					events.Enqueue(new EventData(EventType.HitEventsHit, collHeader.Entity));
				}
			}
		}

		public static void Contact(ref Collider coll, ref BallData ball, in CollisionEventData collEvent, double hitTime, in float3 gravity)
		{
			BallCollider.HandleStaticContact(ref ball, collEvent, coll.Header.Material.Friction, (float)hitTime, gravity);
		}

		public static ItemType GetItemType(string name)
		{
			switch (name) {
				case CollisionType.Null: return ItemType.Null;
				case CollisionType.Point: return ItemType.Point;
				case CollisionType.LineSeg: return ItemType.LineSeg;
				case CollisionType.LineSegSlingshot: return ItemType.LineSegSlingshot;
				case CollisionType.Joint: return ItemType.Joint;
				case CollisionType.Circle: return ItemType.Circle;
				case CollisionType.Flipper: return ItemType.Flipper;
				case CollisionType.Plunger: return ItemType.Plunger;
				case CollisionType.Spinner: return ItemType.Spinner;
				case CollisionType.Ball: return ItemType.Ball;
				case CollisionType.Poly: return ItemType.Poly;
				case CollisionType.Triangle: return ItemType.Triangle;
				case CollisionType.Plane: return ItemType.Plane;
				case CollisionType.Line: return ItemType.Line;
				case CollisionType.Gate: return ItemType.Gate;
				case CollisionType.TextBox: return ItemType.TextBox;
				case CollisionType.DispReel: return ItemType.DispReel;
				case CollisionType.LightSeq: return ItemType.LightSeq;
				case CollisionType.Primitive: return ItemType.Primitive;
				case CollisionType.HitTarget: return ItemType.HitTarget;
				case CollisionType.Trigger: return ItemType.Trigger;
				case CollisionType.Kicker: return ItemType.Kicker;
				default: return ItemType.Null;
			}
		}

		public static unsafe string ToString(ref Collider coll)
		{
			fixed (Collider* collider = &coll) {
				switch (collider->Type) {
					case ColliderType.Poly3D:
						return ((Poly3DCollider*)collider)->ToString();

					default:
						return collider->ToString();
				}
			}
		}
	}
}
