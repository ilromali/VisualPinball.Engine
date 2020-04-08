﻿using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Physics.HitTest
{
	public struct HitObjectBlobAsset
	{
		private BlobArray<FlipperHitBlob> _flipperHits;

		private const uint NumSlots = 18;
		private const uint SlotSize = uint.MaxValue / NumSlots;

		public static BlobAssetReference<HitObjectBlobAsset> Create(IEnumerable<HitObject> hitObjects)
		{
			var flipperHits = new List<FlipperHitBlob>();
			foreach (var hitObject in hitObjects) {
				if (hitObject is FlipperHit flipperHit) {
					flipperHits.Add(FlipperHitBlob.Create(flipperHit));
				}
			}

			using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
				ref var hitObjectBlobAsset = ref blobBuilder.ConstructRoot<HitObjectBlobAsset>();

				Copy(blobBuilder, flipperHits, ref hitObjectBlobAsset._flipperHits);

				return blobBuilder.CreateBlobAssetReference<HitObjectBlobAsset>(Allocator.Persistent);
			}
		}

		private static void Copy<T>(BlobBuilder blobBuilder, IReadOnlyList<T> src, ref BlobArray<T> dest) where T : struct
		{
			var flipperHitsArray = blobBuilder.Allocate(ref dest, src.Count);
			for (var i = 0; i < src.Count; i++) {
				flipperHitsArray[i] = src[i];
			}
		}

		private static uint GetSlot(Type t)
		{
			if (t == typeof(FlipperHit)) {
				return 1;
			}
			return 0;
		}

		public IHitObject Get(uint hitObjectId)
		{
			var arr = hitObjectId / SlotSize;
			var index = (int)(hitObjectId % SlotSize);
			switch (arr) {
				case 1: return _flipperHits[index];
				default: throw new ArgumentException("Unknown array " + arr + " for ID " + hitObjectId);
			}
		}
	}
}
