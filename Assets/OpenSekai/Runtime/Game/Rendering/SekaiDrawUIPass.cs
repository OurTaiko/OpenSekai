using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawUIPass : ScriptableRenderPass
	{
		private static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
		private static readonly int s_ScaleBiasRtPropID = Shader.PropertyToID("_ScaleBiasRt");
		private static readonly int s_ScreenParamsPropID = Shader.PropertyToID("_ScreenParams");

		private readonly ProfilingSampler m_ProfilingSampler;
		private ShaderTagId m_ShaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
		private FilteringSettings m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, ~0);
		private RenderStateBlock m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
		private RTHandle m_ColorTargetHandle;
		private RTHandle m_DepthTargetHandle;
		private bool m_IsCameraRenderTarget;

		private class PassData
		{
			public RendererListHandle RendererList;
			public Vector4 ScaleBiasRt;
			public Vector4 ScreenParams;
		}

		public SekaiDrawUIPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiDrawUIPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
		}

		/// <summary>
		/// Configures the pass.
		/// When <paramref name="isCameraRenderTarget"/> is true the pass draws into the camera's active color/depth
		/// targets and <paramref name="colorTargetHandle"/>/<paramref name="depthTargetHandle"/> are ignored
		/// (RenderGraph only exposes the camera targets at record time).
		/// </summary>
		public void Setup(
			LayerMask layerMask,
			StencilState stencilState,
			int stencilReference,
			RTHandle colorTargetHandle,
			RTHandle depthTargetHandle,
			bool isCameraRenderTarget,
			RenderQueueRange renderQueueRange)
		{
			m_ShaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
			m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			m_ColorTargetHandle = colorTargetHandle;
			m_DepthTargetHandle = depthTargetHandle;
			m_IsCameraRenderTarget = isCameraRenderTarget;
			m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
			if (stencilState.enabled)
			{
				m_RenderStateBlock.stencilReference = stencilReference;
				m_RenderStateBlock.mask = RenderStateMask.Stencil;
				m_RenderStateBlock.stencilState = stencilState;
			}
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var resourceData = frameData.Get<UniversalResourceData>();
			var renderingData = frameData.Get<UniversalRenderingData>();
			var cameraData = frameData.Get<UniversalCameraData>();
			var lightData = frameData.Get<UniversalLightData>();

			TextureHandle colorTarget;
			TextureHandle depthTarget;
			if (m_IsCameraRenderTarget)
			{
				colorTarget = resourceData.activeColorTexture;
				depthTarget = resourceData.activeDepthTexture;
			}
			else
			{
				if (m_ColorTargetHandle == null || m_DepthTargetHandle == null)
				{
					return;
				}

				colorTarget = renderGraph.ImportTexture(m_ColorTargetHandle);
				depthTarget = renderGraph.ImportTexture(m_DepthTargetHandle);
			}

			if (!colorTarget.IsValid())
			{
				return;
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_ProfilingSampler.name, out var passData, m_ProfilingSampler))
			{
				var drawingSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagId, renderingData, cameraData, lightData, (SortingCriteria)23);
				passData.RendererList = SekaiRenderGraphUtils.CreateRendererList(
					renderGraph, renderingData.cullResults, drawingSettings, m_FilteringSettings, m_RenderStateBlock);
				passData.ScaleBiasRt = SekaiRenderGraphUtils.GetScaleBiasRt(cameraData, resourceData);
				passData.ScreenParams = GetScreenParams(cameraData);

				builder.UseRendererList(passData.RendererList);
				builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
				if (depthTarget.IsValid())
				{
					builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
				}

				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					var cmd = context.cmd;
					// Replaces the old ConfigureClear(ClearFlag.Depth, Color.black).
					cmd.ClearRenderTarget(RTClearFlags.Depth, Color.black, 1f, 0u);
					cmd.SetGlobalVector(s_DrawObjectPassDataPropID, Vector4.zero);
					cmd.SetGlobalVector(s_ScaleBiasRtPropID, data.ScaleBiasRt);
					cmd.SetGlobalVector(s_ScreenParamsPropID, data.ScreenParams);
					cmd.DrawRendererList(data.RendererList);
				});
			}
		}

		private Vector4 GetScreenParams(UniversalCameraData cameraData)
		{
			var descriptor = cameraData.cameraTargetDescriptor;
			var width = descriptor.width;
			var height = descriptor.height;
			if (!m_IsCameraRenderTarget)
			{
				var scale = SekaiUIEffectSettings.Blur.CaptureResolutionScale;
				width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
				height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
			}

			return new Vector4(width, height, 1f + 1f / width, 1f + 1f / height);
		}
	}
}
