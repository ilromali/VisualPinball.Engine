﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Flipper
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class FlipperRotateSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			Entities.WithName("FlipperRotateJob").ForEach((ref Rotation rot, in FlipperMovementData movement) => {

				Profiler.BeginSample("FlipperRotateSystem");

				rot.Value = math.mul(movement.BaseRotation, quaternion.EulerXYZ(0, 0, movement.Angle));

				Profiler.EndSample();

			}).Run();
		}
	}
}
