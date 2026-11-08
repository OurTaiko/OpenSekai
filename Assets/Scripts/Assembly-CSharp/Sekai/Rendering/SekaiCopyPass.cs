using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	/// <summary>
	/// Copies the camera's active color target into <see cref="Setup"/>'s destination texture.
	/// </summary>
	public class SekaiCopyPass : ScriptableRenderPass
	{
		private readonly ProfilingSampler m_ProfilingSampler;
		private RTHandle m_Dest;

		public SekaiCopyPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiCopyPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
			// The camera color has to be a readable texture (not the back buffer) to be copied.
			requiresIntermediateTexture = true;
		}

		public void Setup(RTHandle dest)
		{
			m_Dest = dest;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (m_Dest == null)
			{
				return;
			}

			var resourceData = frameData.Get<UniversalResourceData>();
			if (resourceData.isActiveTargetBackBuffer)
			{
				return;
			}

			var source = resourceData.activeColorTexture;
			if (!source.IsValid())
			{
				return;
			}

			var destination = renderGraph.ImportTexture(m_Dest);
			renderGraph.AddBlitPass(source, destination, Vector2.one, Vector2.zero, passName: m_ProfilingSampler.name);
		}
	}
}
