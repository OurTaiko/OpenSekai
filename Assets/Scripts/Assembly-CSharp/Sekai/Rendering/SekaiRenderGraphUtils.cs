using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	/// <summary>
	/// Shared helpers for the RenderGraph versions of the Sekai render passes (URP 17+ removed the
	/// non-RenderGraph "Compatibility Mode" path, so every pass has to be recorded through RenderGraph).
	/// </summary>
	internal static class SekaiRenderGraphUtils
	{
		private static readonly ShaderTagId[] s_ShaderTagValues = new ShaderTagId[1];
		private static readonly RenderStateBlock[] s_RenderStateBlocks = new RenderStateBlock[1];

		/// <summary>
		/// Creates a renderer list, applying <paramref name="stateBlock"/> as an override when it changes anything.
		/// Mirrors the old <c>context.DrawRenderers(cull, ref drawing, ref filtering, ref stateBlock)</c> call.
		/// </summary>
		public static RendererListHandle CreateRendererList(
			RenderGraph renderGraph,
			CullingResults cullResults,
			DrawingSettings drawingSettings,
			FilteringSettings filteringSettings,
			RenderStateBlock stateBlock)
		{
			var param = new RendererListParams(cullResults, drawingSettings, filteringSettings);
			if (stateBlock.mask == RenderStateMask.Nothing)
			{
				return renderGraph.CreateRendererList(param);
			}

			s_ShaderTagValues[0] = ShaderTagId.none;
			s_RenderStateBlocks[0] = stateBlock;
			param.tagValues = new NativeArray<ShaderTagId>(s_ShaderTagValues, Allocator.Temp);
			param.stateBlocks = new NativeArray<RenderStateBlock>(s_RenderStateBlocks, Allocator.Temp);
			param.isPassTagName = false;
			return renderGraph.CreateRendererList(param);
		}

		/// <summary>
		/// Equivalent of the URP 14 <c>CameraData.IsCameraProjectionMatrixFlipped()</c> for the current RenderGraph target.
		/// </summary>
		public static bool IsCameraProjectionMatrixFlipped(UniversalCameraData cameraData, UniversalResourceData resourceData)
		{
			if (!SystemInfo.graphicsUVStartsAtTop)
			{
				return false;
			}

			return !resourceData.isActiveTargetBackBuffer || cameraData.targetTexture != null;
		}

		public static Vector4 GetScaleBiasRt(UniversalCameraData cameraData, UniversalResourceData resourceData)
		{
			var flipSign = IsCameraProjectionMatrixFlipped(cameraData, resourceData) ? -1f : 1f;
			return flipSign < 0f
				? new Vector4(flipSign, 1f, -1f, 1f)
				: new Vector4(flipSign, 0f, 1f, 1f);
		}
	}
}
