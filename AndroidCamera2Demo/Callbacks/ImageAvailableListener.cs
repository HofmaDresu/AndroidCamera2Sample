using System;
using Android.Media;

namespace AndroidCamera2Demo.Callbacks
{
	public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
	{
		public Action<ImageReader> ImageAvailable;

		public void OnImageAvailable(ImageReader reader)
		{
			ImageAvailable?.Invoke(reader);
		}
	}
}