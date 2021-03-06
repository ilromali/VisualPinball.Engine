using System.Linq;
using NLog;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.VPT
{
	public abstract class ItemBehavior<TItem, TData> : MonoBehaviour, IEditableItemBehavior, IIdentifiableItemBehavior,
		ILayerableItemBehavior where TData : ItemData where TItem : Item<TData>, IRenderable
	{
		[SerializeField]
		public TData data;

		public TItem Item => _item ?? (_item = GetItem());
		public bool IsLocked { get => data.IsLocked; set => data.IsLocked = value; }
		public string[] UsedMaterials => (Item as IRenderable)?.UsedMaterials;

		protected TableData _tableData;
		private TItem _item;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[HideInInspector]
		[SerializeField]
		private bool _meshDirty;
		public bool MeshDirty { get { return _meshDirty; } set { _meshDirty = value; } }

		public ItemBehavior<TItem, TData> SetItemAndData(TItem item, string gameObjectName = null)
		{
			_item = item;
			return SetData(item.Data, gameObjectName);
		}

		public ItemBehavior<TItem, TData> SetData(TData d, string gameObjectName = null)
		{
			name = gameObjectName ?? d.GetName();
			data = d;
			ItemDataChanged();
			return this;
		}

		public void RebuildMeshes()
		{
			if (data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.GetComponentInParent<TableBehavior>();
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", data.GetName());
				return;
			}

			var rog = Item.GetRenderObjects(table.Table, Origin.Original, false);
			var children = Children;
			if (children == null) {
				UpdateMesh(Item.Name, gameObject, rog, table);
			} else {
				foreach (var child in children) {
					if (transform.childCount == 0) {
						//Find the matching  renderObject  and Update it based on base gameObject
						var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
						if (ro != null)
						{
							UpdateMesh(child, gameObject, rog, table);
							break;
						}
					} else {
						Transform childTransform = transform.Find(child);
						if (childTransform != null) {
							UpdateMesh(child, childTransform.gameObject, rog, table);
						} else {
							// child hasn't been created yet (i.e. ramp might have changed type)
							var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
							if (ro != null) {
								var subObj = new GameObject(ro.Name);
								subObj.transform.SetParent(this.transform, false);
								subObj.layer = VpxImporter.ChildObjectsLayer;
								VpxImporter.ImportRenderObject(this.Item, ro, subObj, table);
							}
						}
					}
				}
			}
			transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());
			ItemDataChanged();
			_meshDirty = false;
		}

		protected virtual void ItemDataChanged() {}

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() { return Vector3.zero; }
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() { return Vector3.zero; }
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() { return Vector3.zero; }
		public virtual void SetEditorScale(Vector3 rot) { }

		public virtual void HandleMaterialRenamed(string undoName, string oldName, string newName) { }

		// rename helper to cut down on the boiler plate in the concrete classes
		protected void TryRenameField(string undoName, ref string field, string oldName, string newName)
		{
			if (field == oldName) {
#if UNITY_EDITOR
				Undo.RecordObject(this, undoName);
#endif
				field = newName;
			}
		}

		protected void Convert(Entity entity, EntityManager dstManager)
		{
			Item.Index = entity.Index;
			Item.Version = entity.Version;
		}

		protected virtual void Awake()
		{
			var rootObj = gameObject.transform.GetComponentInParent<TableBehavior>();
			// can be null in editor, shouldn't be at runtime.
			if (rootObj != null) {
				_tableData = rootObj.data;
			}
		}

		protected virtual void OnDrawGizmos()
		{
			// handle dirty whenever scene view draws just in case a field or dependant changed and our
			// custom inspector window isn't up to process it
			if (_meshDirty) {
				RebuildMeshes();
			}

			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = this.GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				Gizmos.DrawMesh(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
			}
		}

		private void UpdateMesh(string childName, GameObject go, RenderObjectGroup rog, TableBehavior table)
		{
			var mr = go.GetComponent<MeshRenderer>();
			var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == childName);
			if (ro == null || !ro.IsVisible) {
				if (mr != null) {
					mr.enabled = false;
				}
				return;
			}
			var mf = go.GetComponent<MeshFilter>();
			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				ro.Mesh.ApplyToUnityMesh(unityMesh);
			}

			if (mr != null) {
				if (table != null) {
					mr.sharedMaterial = ro.Material.ToUnityMaterial(table);
				}
				mr.enabled = true;
			}
		}

		protected abstract string[] Children { get; }

		protected abstract TItem GetItem();

		public string Name { get => Item.Name; set => Item.Name = value; }

		public int EditorLayer { get => data.EditorLayer; set => data.EditorLayer = value; }
		public string EditorLayerName { get => data.EditorLayerName; set => data.EditorLayerName = value; }
		public bool EditorLayerVisibility { get => data.EditorLayerVisibility; set => data.EditorLayerVisibility = value; }
	}
}
