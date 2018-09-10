using System;
using Android.Hardware.Camera2;
using Android.Runtime;

namespace AndroidCamera2Demo.Callbacks
{
	public class CameraStateCallback : CameraDevice.StateCallback
	{
		public Action<CameraDevice> Disconnected;
		public Action<CameraDevice, CameraError> Error;
		public Action<CameraDevice> Opened;

		public override void OnDisconnected(CameraDevice camera)
		{
			Disconnected?.Invoke(camera);
		}

		public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error)
		{
			Error?.Invoke(camera, error);
		}

		public override void OnOpened(CameraDevice camera)
		{
			Opened?.Invoke(camera);
		}
	}
}
