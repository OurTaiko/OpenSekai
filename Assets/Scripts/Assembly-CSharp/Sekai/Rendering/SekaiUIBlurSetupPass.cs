using UnityEngine;

namespace Sekai.Rendering
{
	/// <summary>
	/// Allocates the UI blur render textures. This used to be an empty ScriptableRenderPass whose only job was
	/// the allocation in <see cref="Setup"/>; with RenderGraph it no longer needs to be enqueued, so it is a plain helper.
	/// </summary>
	public class SekaiUIBlurSetupPass
	{
		private SekaiUIBuffer m_UIBuffer;
		private RenderTextureDescriptor m_ColorDescriptor;
		private RenderTextureDescriptor m_DepthDescriptor;

		public void Setup(SekaiUIBuffer uiBuffer, RenderTextureDescriptor colorDescriptor, RenderTextureDescriptor depthDescriptor)
		{
			m_UIBuffer = uiBuffer;
			m_ColorDescriptor = colorDescriptor;
			m_DepthDescriptor = depthDescriptor;
			m_UIBuffer?.TryGetRTHandle(m_ColorDescriptor, m_DepthDescriptor);
		}

		public void Dispose()
		{
			m_UIBuffer?.Dispose();
			m_UIBuffer = null;
		}
	}
}
