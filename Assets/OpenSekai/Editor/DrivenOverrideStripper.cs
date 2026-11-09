using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSekai.Editor
{
	/// <summary>
	/// Drops prefab instance overrides on RectTransform properties that a layout component drives
	/// (HorizontalLayoutGroup, ContentSizeFitter, AspectRatioFitter, Scrollbar, ...) before a scene or prefab is saved.
	/// </summary>
	/// <remarks>
	/// When a layout pass re-drives a child of a prefab instance, for example after the Canvas changes size, Unity
	/// records the driven anchors, sizeDelta and anchoredPosition as instance overrides. They are written as 0, hidden
	/// in the Overrides dropdown and recomputed by the driver on load, so they carry nothing; they only make a scene
	/// save show dozens of new overrides. Overrides on properties that nothing drives are kept.
	///
	/// The overrides are removed with <see cref="PrefabUtility.SetPropertyModifications"/>. Deleting them from the
	/// PrefabInstance's serialized <c>m_Modification.m_Modifications</c> also works, but the next save then asserts
	/// <c>!m_HasValidAddedGameObjects</c> and <c>!m_HasValidAddedComponents</c>.
	/// </remarks>
	[InitializeOnLoad]
	internal sealed class DrivenOverrideStripper : AssetModificationProcessor
	{
		private static readonly PropertyInfo DrivenPropertiesInfo =
			typeof(RectTransform).GetProperty("drivenProperties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly Dictionary<string, DrivenTransformProperties> PropertyFlags = new Dictionary<string, DrivenTransformProperties>
		{
			{ "m_AnchorMin.x", DrivenTransformProperties.AnchorMinX },
			{ "m_AnchorMin.y", DrivenTransformProperties.AnchorMinY },
			{ "m_AnchorMax.x", DrivenTransformProperties.AnchorMaxX },
			{ "m_AnchorMax.y", DrivenTransformProperties.AnchorMaxY },
			{ "m_AnchoredPosition.x", DrivenTransformProperties.AnchoredPositionX },
			{ "m_AnchoredPosition.y", DrivenTransformProperties.AnchoredPositionY },
			{ "m_LocalPosition.z", DrivenTransformProperties.AnchoredPositionZ },
			{ "m_SizeDelta.x", DrivenTransformProperties.SizeDeltaX },
			{ "m_SizeDelta.y", DrivenTransformProperties.SizeDeltaY },
			{ "m_Pivot.x", DrivenTransformProperties.PivotX },
			{ "m_Pivot.y", DrivenTransformProperties.PivotY },
			{ "m_LocalScale.x", DrivenTransformProperties.ScaleX },
			{ "m_LocalScale.y", DrivenTransformProperties.ScaleY },
			{ "m_LocalScale.z", DrivenTransformProperties.ScaleZ },
		};

		static DrivenOverrideStripper()
		{
			if (DrivenPropertiesInfo == null)
			{
				Debug.LogWarning("DrivenOverrideStripper: RectTransform.drivenProperties was not found; driven overrides are kept.");
				return;
			}

			PrefabStage.prefabSaving += OnPrefabSaving;
		}

		private static string[] OnWillSaveAssets(string[] paths)
		{
			if (DrivenPropertiesInfo == null)
			{
				return paths;
			}

			var saving = new HashSet<string>(paths);
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
				if (scene.isLoaded && saving.Contains(scene.path))
				{
					foreach (GameObject root in scene.GetRootGameObjects())
					{
						StripInstancesUnder(root);
					}
				}
			}
			return paths;
		}

		private static void OnPrefabSaving(GameObject prefabContentsRoot)
		{
			// The prefab's own root is not an instance; its nested prefab instances carry their own overrides.
			StripInstancesUnder(prefabContentsRoot);
		}

		private static void StripInstancesUnder(GameObject root)
		{
			foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
			{
				if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
				{
					Strip(transform.gameObject);
				}
			}
		}

		private static void Strip(GameObject instanceRoot)
		{
			PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
			if (modifications == null || modifications.Length == 0)
			{
				return;
			}

			// Modifications target the objects of the instance's source prefab; map them back to the scene objects.
			var instanceOf = new Dictionary<Object, RectTransform>();
			foreach (RectTransform rect in instanceRoot.GetComponentsInChildren<RectTransform>(true))
			{
				RectTransform source = PrefabUtility.GetCorrespondingObjectFromSource(rect);
				if (source != null)
				{
					instanceOf[source] = rect;
				}
			}

			PropertyModification[] kept = modifications
				.Where(modification => !IsDriven(modification.target, modification.propertyPath, instanceOf))
				.ToArray();
			if (kept.Length != modifications.Length)
			{
				// This re-applies the prefab's values. An AspectRatioFitter re-drives its rect at once and records its
				// overrides again; those are recorded on every load too, so the scene keeps them unchanged.
				PrefabUtility.SetPropertyModifications(instanceRoot, kept);
			}
		}

		private static bool IsDriven(Object target, string propertyPath, Dictionary<Object, RectTransform> instanceOf)
		{
			if (!(target is RectTransform)
				|| !PropertyFlags.TryGetValue(propertyPath, out DrivenTransformProperties flag)
				|| !instanceOf.TryGetValue(target, out RectTransform rect))
			{
				return false;
			}

			var driven = (DrivenTransformProperties)DrivenPropertiesInfo.GetValue(rect);
			return (driven & flag) != 0;
		}
	}
}
