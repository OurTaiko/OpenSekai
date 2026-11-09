using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiAfterPostProcessRendererFeature : ScriptableRendererFeature
	{
		[SerializeField]
		private LayerMask _targetLayer;

		[SerializeField]
		private Shader _fadeOutBlendShader;

		public override void Create()
		{
			// TODO: Port SekaiAfterPostProcessRenderPass from rendering_tmp/Sekai.Rendering.
			// Its shader (Hidden/AfterPostProcess/FadeOutBlend) was GLSL-only and has been removed: write it in HLSL with the pass.
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
		}
	}
}
