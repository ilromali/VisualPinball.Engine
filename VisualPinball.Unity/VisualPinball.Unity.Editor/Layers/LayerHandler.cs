using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Editor.Utils.TreeView;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// This handler will construct a layer structure from the table loaded data and store it into a LayerTreeElement tree structure.
	/// It will then be in charge of layers management (add/remove/rename/item assignment) by listening several events from the <see cref="LayerTreeView"/>
	/// </summary>
	internal class LayerHandler
	{
		private const string NewLayerDefaultName = "New Layer ";

		/// <summary>
		/// Attached <see cref="TableBehavior"/>, Set by calling OnHierarchyChange
		/// </summary>
		private TableBehavior _tableBehavior;

		/// <summary>
		/// Maps the the game items' <see cref="MonoBehaviour"/> to their respective layers.
		/// </summary>
		private Dictionary<string, List<MonoBehaviour>> _layers = new Dictionary<string, List<MonoBehaviour>>();

		/// <summary>
		/// Expose the list of current layer names (used by <see cref="LayerEditor"/> for populating context menu)
		/// </summary>
		public string[] Layers => _layers.Keys.ToArray();

		/// <summary>
		/// TreeModel used by the <see cref="LayerTreeView"/>, will be built based on the Layers structure
		/// </summary>
		public LayerTreeElement TreeRoot { get; } = new LayerTreeElement { Depth = -1, Id = -1 };

		/// <summary>
		/// this event is fired each time the Tree structure has been updated based on the data gathered from layer BiffData
		/// </summary>
		public event Action TreeRebuilt;

		/// <summary>
		/// Is called by the <see cref="LayerEditor"/> when a new TableBehavior is created/deleted
		/// </summary>
		/// <param name="tableBehavior"></param>
		public void OnHierarchyChange(TableBehavior tableBehavior)
		{
			_tableBehavior = tableBehavior;
			_layers.Clear();
			RebuildLayers();
		}

		#region Layers & Tree construction
		/// <summary>
		/// Recursively runs through the <see cref="TableBehavior"/>'s children and
		/// adds the game items' <see cref="MonoBehaviour"/> to the layers map. <p/>
		///
		/// It also rebuilds the tree model.
		/// </summary>
		private void RebuildLayers()
		{
			_layers = _layers.Where(pair => pair.Value?.Count == 0)
							 .ToDictionary(pair => pair.Key,
										pair => pair.Value);

			if (_tableBehavior != null) {
				BuildLayersRecursively(_tableBehavior.gameObject);
			}
			RebuildTree();
		}

		/// <summary>
		/// Recursively runs through the given <see cref="GameObject"/> and
		/// adds its children's <see cref="MonoBehaviour"/> to the layers map.
		/// </summary>
		private void BuildLayersRecursively(GameObject gameObj)
		{
			for (var i = 0; i < gameObj.transform.childCount; ++i) {
				var child = gameObj.transform.GetChild(i).gameObject;
				AddToLayer(child.GetComponent<ILayerableItemBehavior>());
				BuildLayersRecursively(child);
			}
		}

		private void AddToLayer(ILayerableItemBehavior item)
		{
			if (item == null) {
				return;
			}
			if (item.EditorLayerName == string.Empty) {
				item.EditorLayerName = $"Layer_{item.EditorLayer + 1}";
			}
			if (!_layers.ContainsKey(item.EditorLayerName)) {
				_layers.Add(item.EditorLayerName, new List<MonoBehaviour>());
			}
			_layers[item.EditorLayerName].Add((MonoBehaviour)item);
		}

		private void RebuildTree()
		{
			TreeRoot.Children.Clear();

			// init with root element
			if (_tableBehavior != null && _tableBehavior.Table != null) {

				// table node
				var tableItem = new LayerTreeElement(_tableBehavior.Table) { Id = 0 };
				TreeRoot.AddChild(tableItem);

				var layerCount = 1;
				foreach (var pair in _layers.OrderBy(key=> key.Key)) {

					// layer node
					var layerItem = new LayerTreeElement(pair.Key) { Id = layerCount++ };
					tableItem.AddChild(layerItem);

					foreach (var item in pair.Value.OrderBy(behaviour => behaviour.name)) {
						if (item is ILayerableItemBehavior layeredItem) {
							layerItem.AddChild(new LayerTreeElement(layeredItem) { Id = item.gameObject.GetInstanceID() });
						}
					}
				}
			}

			TreeRebuilt?.Invoke();
		}
		#endregion

		#region Layer Renaming

		/// <summary>
		/// Updates <see cref="ItemData.EditorLayerName"/>, managing Undo.
		/// </summary>
		/// <param name="item">Tree layer element to update</param>
		/// <param name="layerName">New layer name</param>
		private static void ApplyLayerNameToItem(ILayerableItemBehavior item, string layerName)
		{
			if (item.EditorLayerName != layerName) {
				if (item is MonoBehaviour behaviour) {
					Undo.RecordObject(behaviour, $"Item {behaviour.name}: Change layer name from {item.EditorLayerName} to {layerName}");
				}
				item.EditorLayerName = layerName;
			}
		}

		/// <summary>
		/// Recursively updates <see cref="ItemData.EditorLayerName"/> of all provided elements with a new layer name
		/// </summary>
		/// <param name="elements">Tree layer elements to update</param>
		/// <param name="layerName">New layer name</param>
		private static void ApplyLayerNameToItems(IEnumerable<LayerTreeElement> elements, string layerName)
		{
			foreach (var element in elements) {
				if (element.Item != null) {
					ApplyLayerNameToItem(element.Item, layerName);
					if (element.HasChildren) {
						ApplyLayerNameToItems(element.GetChildren<LayerTreeElement>(), layerName);
					}
				}
			}
		}

		/// <summary>
		/// Callback when LayerTreeView has validated a layer rename
		/// </summary>
		/// <param name="element">Renamed layer TreeElement</param>
		/// <param name="newName">Validated name</param>
		internal void OnLayerRenamed(LayerTreeElement element, string newName)
		{
			if (element.LayerName == newName) {
				return;
			}

			// Check if there is not already a layers with the same name
			if (_layers.ContainsKey(newName)) {
				EditorUtility.DisplayDialog("Visual Pinball", $"There is already a layer named {newName}.\nFind another layer name.", "Close");
				return;
			}

			// Rename in _layers
			if (_layers.TryGetValue(element.LayerName, out var items)) {
				_layers.Remove(element.LayerName);
				_layers[newName] = items;
			}
			element.LayerName = newName;

			// Update layer name for all items within this layer
			if (element.HasChildren) {
				ApplyLayerNameToItems(element.GetChildren<LayerTreeElement>(), newName);
			}
			RebuildTree();
		}

		#endregion

		#region Layer Add/Removal

		/// <summary>
		/// Create a new layer with first free name formatted as "New Layer {num}"
		/// </summary>
		public string CreateNewLayer()
		{
			var newLayerNum = 0;
			while (_layers.ContainsKey($"{NewLayerDefaultName}{newLayerNum}")) {
				newLayerNum++;
			}
			string newLayerName = $"{NewLayerDefaultName}{newLayerNum}";
			_layers.Add(newLayerName, new List<MonoBehaviour>());
			RebuildTree();
			return newLayerName;
		}

		/// <summary>
		/// Deletes a layer using a TreeElement ID used within TreeRoot
		/// </summary>
		/// <param name="id">the id of the layer TreeElement</param>
		/// <remarks>
		/// Cannot delete the last layer, table need at least one layer
		/// Will transfer all items from the deleted layer to the first layer of the table
		/// </remarks>
		public void DeleteLayer(int id)
		{
			var layerItem = TreeRoot.Find<LayerTreeElement>(id);
			if (layerItem != null && layerItem.Type == LayerTreeViewElementType.Layer) {

				if (_layers.Keys.Count == 1) {
					EditorUtility.DisplayDialog("Visual Pinball", "Cannot delete all layers.", "Close");
					return;
				}

				// Keep layer's items and put them in the first layer
				var items = layerItem.GetChildren<LayerTreeElement>();
				_layers.Remove(layerItem.LayerName);
				var firstLayer = TreeRoot.GetChildren<LayerTreeElement>(e => e.Type == LayerTreeViewElementType.Layer)[0];
				foreach (var item in items) {
					item.ReParent(firstLayer);
				}
				RebuildLayers();
			}
		}
		#endregion

		#region Items Drag & Drop

		internal void OnItemsDropped(LayerTreeElement[] droppedElements, LayerTreeElement newParent)
		{
			AssignToLayer(droppedElements, newParent);
		}

		#endregion

		#region Layer Assignement
		private void AssignToLayer(LayerTreeElement[] elements, LayerTreeElement layer)
		{
			if (layer.Type != LayerTreeViewElementType.Layer) {
				return;
			}

			foreach (var element in elements) {
				if (element.Type == LayerTreeViewElementType.Item) {
					element.ReParent(layer);
				}
			}
			RebuildLayers();
		}

		internal void AssignToLayer(LayerTreeElement[] elements, string layerName)
		{
			if (string.IsNullOrEmpty(layerName)) {
				layerName = CreateNewLayer();
			}

			var layer = TreeRoot.Find<LayerTreeElement>(e => e.Type == LayerTreeViewElementType.Layer && e.LayerName == layerName);
			AssignToLayer(elements, layer);
		}

		#endregion

		#region Items selection
		/// <summary>
		/// Callback when a TreeViewItem is double clicked
		/// </summary>
		/// <param name="element">the TreeElement attached to the TreeViewItem</param>
		internal void OnItemDoubleClicked(LayerTreeElement element)
		{
			switch (element.Type) {
				case LayerTreeViewElementType.Table:
				case LayerTreeViewElementType.Layer: {
					LayerTreeElement[] items = element.GetChildren<LayerTreeElement>(child => child.Type == LayerTreeViewElementType.Item);
					Selection.objects = items.Select(item => EditorUtility.InstanceIDToObject(item.Id)).ToArray();
					break;
				}

				case LayerTreeViewElementType.Item: {
					Selection.activeObject = EditorUtility.InstanceIDToObject(element.Id);
					break;
				}

				default: {
					break;
				}
			}
		}
		#endregion

	}
}
