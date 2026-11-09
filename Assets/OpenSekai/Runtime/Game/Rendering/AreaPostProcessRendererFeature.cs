using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class AreaPostProcessRendererFeature : ScriptableRendererFeature
	{
		public Shader shader;

		public override void Create()
		{
			// TODO: Port AreaPostProcessRenderPass from rendering_tmp/Sekai.Rendering.
			// Its shader (Area/Post) was GLSL-only and has been removed: write it in HLSL with the pass.
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
		}
	}
}
