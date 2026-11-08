using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiTransparentReflectionRendererFeature : SekaiDrawObjectsRendererFeature
	{
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			base.AddRenderPasses(renderer, ref renderingData);
		}
	}
}
