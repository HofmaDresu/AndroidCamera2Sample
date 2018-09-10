using System;
using Android.Hardware.Camera2;

namespace AndroidCamera2Demo.Callbacks
{
	public class CaptureStateSessionCallback : CameraCaptureSession.StateCallback
	{
		public Action<CameraCaptureSession> Failed;
		public Action<CameraCaptureSession> Configured;

		public override void OnConfigured(CameraCaptureSession session)
		{
			Configured?.Invoke(session);
		}

		public override void OnConfigureFailed(CameraCaptureSession session)
		{
			Failed?.Invoke(session);
		}
	}
}
