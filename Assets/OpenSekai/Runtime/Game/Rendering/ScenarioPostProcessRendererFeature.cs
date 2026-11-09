using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class ScenarioPostProcessRendererFeature : ScriptableRendererFeature
	{
		public Shader shader;

		public override void Create()
		{
			// TODO: Port ScenarioPostProcessRenderPass from rendering_tmp/Sekai.Rendering.
			// Its shader (Hidden/Sekai/Scenario/Post) was GLSL-only and has been removed: write it in HLSL with the pass.
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
		}
	}
}
