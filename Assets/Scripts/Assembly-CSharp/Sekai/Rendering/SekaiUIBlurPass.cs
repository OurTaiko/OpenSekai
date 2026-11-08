using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiUIBlurPass : ScriptableRenderPass
	{
		private static readonly int SamplingDistance = Shader.PropertyToID("_SamplingDistance");
		private static readonly int BlurResolutionParams = Shader.PropertyToID("_BlurResolutionParams");

		private readonly ProfilingSampler m_ProfilingSampler;
		private readonly Material m_UIEffectMaterial;
		private RTHandle m_Source;
		private RTHandle m_Destination;

		public SekaiUIBlurPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiUIBlurPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
			var shader = Shader.Find("Hidden/CP/PostEffect/UIEffect");
			if (shader == null)
			{
				shader = Shader.Find("Sekai/UI/UIGaussianBlur");
			}

			if (shader != null)
			{
				m_UIEffectMaterial = CoreUtils.CreateEngineMaterial(shader);
			}
		}

		public void Setup(RTHandle source, RTHandle dest)
		{
			m_Source = source;
			m_Destination = dest;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (m_Source == null || m_Destination == null || SekaiUIBuffer.BlurTempHandle == null || m_UIEffectMaterial == null)
			{
				return;
			}

			var cameraData = frameData.Get<UniversalCameraData>();
			var descriptor = cameraData.cameraTargetDescriptor;

			// These used to be set with cmd.SetGlobal*; they are only read by the blur material,
			// so they are set on the material directly (RenderGraph executes right after recording).
			m_UIEffectMaterial.SetFloat(SamplingDistance, SekaiUIEffectSettings.Blur.BlurSamplingDistance);
			m_UIEffectMaterial.SetVector(
				BlurResolutionParams,
				new Vector4(1f / descriptor.width, 1f / descriptor.height, 0f, 0f));

			var source = renderGraph.ImportTexture(m_Source);
			var temp = renderGraph.ImportTexture(SekaiUIBuffer.BlurTempHandle);
			var destination = renderGraph.ImportTexture(m_Destination);

			renderGraph.AddBlitPass(
				new RenderGraphUtils.BlitMaterialParameters(source, temp, m_UIEffectMaterial, 0),
				m_ProfilingSampler.name + " (Pass 0)");
			renderGraph.AddBlitPass(
				new RenderGraphUtils.BlitMaterialParameters(temp, destination, m_UIEffectMaterial, 1),
				m_ProfilingSampler.name + " (Pass 1)");
		}

		public void Cleanup()
		{
			CoreUtils.Destroy(m_UIEffectMaterial);
		}
	}
}
