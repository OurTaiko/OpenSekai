using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiLegacyDrawUIRendererFeature : ScriptableRendererFeature
	{
		[Serializable]
		public class AreaCameraSettings
		{
			public int Event_DrawUI = (int)RenderPassEvent.AfterRenderingTransparents;
			public int Event_CopyScreenToCapture = (int)RenderPassEvent.AfterRenderingPostProcessing;
		}

		[Serializable]
		public class PassSettings
		{
			public int Event_DrawBlurUI = (int)RenderPassEvent.AfterRenderingOpaques;
			public int Event_Blur = (int)RenderPassEvent.AfterRenderingPostProcessing;
			public int Event_DrawUI = (int)RenderPassEvent.AfterRendering;
		}

		[Serializable]
		public class UIBlurSettings
		{
			public LayerMask BlurLayerMask;
			public LayerMask UILayerMask;
			public RenderQueueRange RenderQueueRange = RenderQueueRange.transparent;
		}

		[SerializeField]
		private UIBlurSettings m_BlurSettings = new UIBlurSettings();

		[SerializeField]
		private PassSettings m_PassSettings = new PassSettings();

		[SerializeField]
		private AreaCameraSettings m_AreaCameraSettings = new AreaCameraSettings();

		private SekaiUIBuffer m_UIBuffer;
		private SekaiUIBlurSetupPass m_SetupPass;
		private SekaiCopyPass m_CopyScreenToCapturePass;
		private SekaiDrawUIPass m_DrawBlurUIPass;
		private SekaiUIBlurPass m_BlurPass;
		private SekaiDrawUIPass m_DrawUIPass;

		public override void Create()
		{
			m_UIBuffer?.Dispose();
			m_BlurPass?.Cleanup();
			m_UIBuffer = new SekaiUIBuffer();
			m_SetupPass = new SekaiUIBlurSetupPass();
			m_DrawBlurUIPass = new SekaiDrawUIPass("Render BlurUI [Sekai UICamera] ");
			m_BlurPass = new SekaiUIBlurPass("Execute Blur [Sekai UICamera] ");
			m_DrawUIPass = new SekaiDrawUIPass("Render UI [Sekai DefaultCamera]");
			m_CopyScreenToCapturePass = new SekaiCopyPass("Copy Screen to CaptureTex [Sekai]");
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (renderer == null || m_DrawUIPass == null)
			{
				return;
			}

			// URP 17 removed SetupRenderPasses / cameraColorTargetHandle: passes are configured here and
			// resolve the camera targets themselves while recording the RenderGraph.
			var camera = renderingData.cameraData.camera;
			if (SekaiUIEffectSettings.Blur.IsActive && IsFinalCamera(camera))
			{
				SetupUICameraPass(renderingData.cameraData.cameraTargetDescriptor, camera);
				renderer.EnqueuePass(m_CopyScreenToCapturePass);
				renderer.EnqueuePass(m_DrawBlurUIPass);
				renderer.EnqueuePass(m_BlurPass);
				renderer.EnqueuePass(m_DrawUIPass);
				return;
			}

			SetupDefaultCameraPass(camera);
			renderer.EnqueuePass(m_DrawUIPass);
		}

		protected override void Dispose(bool disposing)
		{
			m_BlurPass?.Cleanup();
			m_SetupPass?.Dispose();
			m_UIBuffer?.Dispose();
		}

		private void SetupUICameraPass(RenderTextureDescriptor cameraTargetDescriptor, Camera camera)
		{
			var colorDescriptor = GetColorDescriptor(cameraTargetDescriptor);
			var depthDescriptor = GetDepthDescriptor(cameraTargetDescriptor);
			var cameraLayerMask = GetCameraLayerMask(camera);

			m_SetupPass.Setup(m_UIBuffer, colorDescriptor, depthDescriptor);

			m_CopyScreenToCapturePass.renderPassEvent = (RenderPassEvent)1;
			m_CopyScreenToCapturePass.Setup(SekaiUIBuffer.CaptureColorTexHandle);

			m_DrawBlurUIPass.renderPassEvent = (RenderPassEvent)m_PassSettings.Event_DrawBlurUI;
			m_DrawBlurUIPass.Setup(
				cameraLayerMask & m_BlurSettings.BlurLayerMask,
				StencilState.defaultValue,
				0,
				SekaiUIBuffer.CaptureColorTexHandle,
				SekaiUIBuffer.CaptureDepthTexHandle,
				false,
				m_BlurSettings.RenderQueueRange);

			m_BlurPass.renderPassEvent = (RenderPassEvent)m_PassSettings.Event_Blur;
			m_BlurPass.Setup(SekaiUIBuffer.CaptureColorTexHandle, SekaiUIBuffer.UIBlurTexHandle);

			m_DrawUIPass.renderPassEvent = (RenderPassEvent)m_PassSettings.Event_DrawUI;
			m_DrawUIPass.Setup(
				cameraLayerMask & m_BlurSettings.UILayerMask,
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				m_BlurSettings.RenderQueueRange);
		}

		private void SetupDefaultCameraPass(Camera camera)
		{
			m_DrawUIPass.renderPassEvent = (RenderPassEvent)m_AreaCameraSettings.Event_DrawUI;
			m_DrawUIPass.Setup(
				GetCameraLayerMask(camera),
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				m_BlurSettings.RenderQueueRange);
		}

		private static RenderTextureDescriptor GetColorDescriptor(RenderTextureDescriptor cameraTargetDescriptor)
		{
			var desc = cameraTargetDescriptor;
			desc.depthStencilFormat = GraphicsFormat.None;
			desc.depthBufferBits = 0;
			return desc;
		}

		private static RenderTextureDescriptor GetDepthDescriptor(RenderTextureDescriptor cameraTargetDescriptor)
		{
			var desc = cameraTargetDescriptor;
			var depthFormat = desc.depthStencilFormat != GraphicsFormat.None
				? desc.depthStencilFormat
				: CoreUtils.GetDefaultDepthStencilFormat();
			desc.graphicsFormat = GraphicsFormat.None;
			desc.depthStencilFormat = depthFormat;
			desc.msaaSamples = 1;
			desc.bindMS = false;
			return desc;
		}

		private static LayerMask GetCameraLayerMask(Camera camera)
		{
			return camera != null ? camera.cullingMask : ~0;
		}

		private static bool IsFinalCamera(Camera camera)
		{
			if (camera == null)
			{
				return true;
			}

			return camera.TryGetComponent<UnityEngine.Rendering.Universal.SekaiAdditionalCameraData>(out var data)
				? data.IsFinalCamera
				: true;
		}
	}
}
