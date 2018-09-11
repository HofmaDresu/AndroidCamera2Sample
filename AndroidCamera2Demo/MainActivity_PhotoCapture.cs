using System;
using System.IO;
using System.Linq;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;

namespace AndroidCamera2Demo
{
    // Photo Capture specific code
    public partial class MainActivity
    {
        private MediaCaptorState state = MediaCaptorState.Preview;

        enum MediaCaptorState
        {
            Preview,
            WaitingLock,
            WaitingPrecapture,
            WaitingNonPrecapture,
            PictureTaken,
        }

        private void TakePictureButton_Click(object sender, EventArgs e)
        {
            LockFocus();
        }

        // Lock the focus as the first step for a still image capture.
        private void LockFocus()
        {
            try
            {
                var availableAutoFocusModes = (int[])characteristics.Get(CameraCharacteristics.ControlAfAvailableModes);

                // Set autofocus if supported
                if (availableAutoFocusModes.Any(afMode => afMode != (int)ControlAFMode.Off))
                {
                    previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
                    state = MediaCaptorState.WaitingLock;
                    // Tell cameraCaptureCallback to wait for the lock.
                    captureSession.Capture(previewRequestBuilder.Build(), cameraCaptureCallback,
                            backgroundHandler);
                }
                else
                {
                    // If autofocus is not enabled, just capture the image
                    CaptureStillPicture();
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private void ProcessImageCapture(CaptureResult result)
        {
            switch (state)
            {
                case MediaCaptorState.WaitingLock:
                    {
                        var afState = (int?)result.Get(CaptureResult.ControlAfState);
                        if (afState == null)
                        {
                            CaptureStillPicture();
                        }
                        else if ((((int)ControlAFState.FocusedLocked) == afState.Value) ||
                                   (((int)ControlAFState.NotFocusedLocked) == afState.Value))
                        {
                            // ControlAeState can be null on some devices
                            var aeState = (int?)result.Get(CaptureResult.ControlAeState);
                            if (aeState == null || aeState.Value == ((int)ControlAEState.Converged))
                            {
                                state = MediaCaptorState.PictureTaken;
                                CaptureStillPicture();
                            }
                            else
                            {
                                RunPrecaptureSequence();
                            }
                        }
                        break;
                    }
                case MediaCaptorState.WaitingPrecapture:
                    {
                        // ControlAeState can be null on some devices
                        var aeState = (int?)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null ||
                                aeState.Value == ((int)ControlAEState.Precapture) ||
                                aeState.Value == ((int)ControlAEState.FlashRequired))
                        {
                            state = MediaCaptorState.WaitingNonPrecapture;
                        }
                        break;
                    }
                case MediaCaptorState.WaitingNonPrecapture:
                    {
                        // ControlAeState can be null on some devices
                        var aeState = (int?)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null || aeState.Value != ((int)ControlAEState.Precapture))
                        {
                            state = MediaCaptorState.PictureTaken;
                            CaptureStillPicture();
                        }
                        break;
                    }
            }
        }

        // Run the precapture sequence for capturing a still image. This method should be called when
        // we get a response in captureCallback from LockFocus().
        public void RunPrecaptureSequence()
        {
            try
            {
                // This is how to tell the camera to trigger.
                previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Start);
                // Tell captureCallback to wait for the precapture sequence to be set.
                state = MediaCaptorState.WaitingPrecapture;
                captureSession.Capture(previewRequestBuilder.Build(), cameraCaptureCallback, backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void CaptureStillPicture()
        {
            try
            {
                if (null == cameraDevice)
                {
                    return;
                }

                // This is the CaptureRequest.Builder that we use to take a picture.
                var stillCaptureBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

                stillCaptureBuilder.AddTarget(imageReader.Surface);

                // Use the same AE and AF modes as the preview.
                stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(stillCaptureBuilder);

                // Orientation
                int rotation = (int)WindowManager.DefaultDisplay.Rotation;
                int orientation = GetOrientation(rotation);
                stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, orientation);

                captureSession.StopRepeating();
                captureSession.AbortCaptures();
                captureSession.Capture(stillCaptureBuilder.Build(), cameraCaptureCallback, null);
                var am = (AudioManager)GetSystemService(Context.AudioService);
                if (am != null && am.RingerMode == RingerMode.Normal)
                {
                    var cameraSound = new MediaActionSound();
                    cameraSound.Load(MediaActionSoundType.ShutterClick);
                    cameraSound.Play(MediaActionSoundType.ShutterClick);
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private void HandleImageCaptured(ImageReader imageReader)
        {
            Java.IO.FileOutputStream fos = null;
            try
            {
                var image = imageReader.AcquireLatestImage();
                var buffer = image.GetPlanes()[0].Buffer;
                var data = new byte[buffer.Remaining()];
                buffer.Get(data);
                var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                var widthGreaterThanHeight = bitmap.Width > bitmap.Height;
                image.Close();
                /*
                if (CurrentCaptureDevice == MediaCaptureDevice.RearCamera)
                {
                    var isSamsung = Build.Manufacturer.IndexOf("samsung", StringComparison.CurrentCultureIgnoreCase) >= 0;
                    if (isSamsung || widthGreaterThanHeight)
                    {
                        bitmap = await bitmap.RotateBitmap(90);
                    }
                }
                else
                {
                    bitmap = await bitmap.FlipHorizontal();
                    if (widthGreaterThanHeight)
                    {
                        bitmap = await bitmap.RotateBitmap(90);
                    }
                }
                */

                string imageFileName = "AndroidCamera2DemoImage";
                var storageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);

                var storageFilePath = storageDir + Java.IO.File.Separator + "AndroidCamera2Demo" + Java.IO.File.Separator + "Photos";
                var folder = new Java.IO.File(storageFilePath);
                if (!folder.Exists())
                {
                    folder.Mkdirs();
                }

                var imageFile = new Java.IO.File(storageFilePath + Java.IO.File.Separator + imageFileName + ".jpg");
                if (imageFile.CreateNewFile())
                {
                    fos = new Java.IO.FileOutputStream(imageFile);
                    using (var stream = new MemoryStream())
                    {
                        if (bitmap.Compress(Bitmap.CompressFormat.Jpeg, 100, stream))
                        {
                            //We set the data array to the rotated bitmap. 
                            data = stream.ToArray();
                            fos.Write(data);
                        }
                        else
                        {
                            //something went wrong, let's just save the bitmap without rotation.
                            fos.Write(data);
                        }
                        stream.Close();

                        // TODO: preview picture
                    }
                }
            }
            catch (Exception)
            {
                // In a real application we would handle this gracefully, likely alerting the user to the error
            }
            finally
            {
                if (fos != null) fos.Close();
                RunOnUiThread(UnlockFocus);
            }           
        }

        void UnlockFocus()
        {
            try
            {
                // Reset the auto-focus trigger
                previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(previewRequestBuilder);
                captureSession.Capture(previewRequestBuilder.Build(), cameraCaptureCallback,
                        backgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                state = MediaCaptorState.Preview;
                captureSession.SetRepeatingRequest(previewRequest, cameraCaptureCallback,
                        backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }
    }
}