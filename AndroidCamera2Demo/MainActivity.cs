using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using AndroidCamera2Demo.Controls;
using Android.Content.PM;
using AndroidCamera2Demo.Callbacks;
using Android.Hardware.Camera2;
using Android.Views;
using Android.Util;
using System;
using Android.Hardware.Camera2.Params;
using Java.Util;
using Android.Graphics;
using Android.Media;
using System.Collections.Generic;
using System.Linq;

namespace AndroidCamera2Demo
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public partial class MainActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            surfaceTextureView = FindViewById<AutoFitTextureView>(Resource.Id.surface);
            switchCameraButton = FindViewById<ImageButton>(Resource.Id.reverse_camera_button);
            takePictureButton = FindViewById<Button>(Resource.Id.take_picture_button);
            recordVideoButton = FindViewById<Button>(Resource.Id.record_video_button);
            
            cameraStateCallback = new CameraStateCallback
            {
                Opened = OnOpened,
                Disconnected = OnDisconnected,
                Error = OnError,
            };
            captureStateSessionCallback = new CaptureStateSessionCallback
            {
                Configured = OnPreviewSessionConfigured,
            };
            videoSessionStateCallback = new CaptureStateSessionCallback
            {
                Configured = OnVideoSessionConfigured,
            };
            cameraCaptureCallback = new CameraCaptureCallback
            {
                CaptureCompleted = (session, request, result) => ProcessImageCapture(result),
                CaptureProgressed = (session, request, result) => ProcessImageCapture(result),
            };
            manager = GetSystemService(CameraService) as CameraManager;
            windowManager = GetSystemService(WindowService).JavaCast<IWindowManager>();
            onImageAvailableListener = new ImageAvailableListener
            {
                ImageAvailable = HandleImageCaptured,
            };
            orientations.Append((int)SurfaceOrientation.Rotation0, 90);
            orientations.Append((int)SurfaceOrientation.Rotation90, 0);
            orientations.Append((int)SurfaceOrientation.Rotation180, 270);
            orientations.Append((int)SurfaceOrientation.Rotation270, 180);
        }

        private AutoFitTextureView surfaceTextureView;
        private ImageButton switchCameraButton;
        private Button takePictureButton;
        private Button recordVideoButton;
        private CameraStateCallback cameraStateCallback;
        private CaptureStateSessionCallback captureStateSessionCallback;
        private CaptureStateSessionCallback videoSessionStateCallback;
        private CameraCaptureCallback cameraCaptureCallback;
        private CameraManager manager;
        private IWindowManager windowManager;
        private ImageAvailableListener onImageAvailableListener;
        private SparseIntArray orientations = new SparseIntArray();
        private LensFacing currentLensFacing = LensFacing.Back;
        private CameraCharacteristics characteristics;
        private CameraDevice cameraDevice;
        private ImageReader imageReader;
        private int sensorOrientation;
        private Size previewSize;
        private HandlerThread backgroundThread;
        private Handler backgroundHandler;
        private bool flashSupported;
        private Surface previewSurface;
        private CameraCaptureSession captureSession;
        private CaptureRequest.Builder previewRequestBuilder;
        private CaptureRequest previewRequest;

        protected override void OnResume()
        {
            base.OnResume();
            switchCameraButton.Click += SwitchCameraButton_Click;
            takePictureButton.Click += TakePictureButton_Click;
            recordVideoButton.Click += RecordVideoButton_Click;

            StartBackgroundThread();

            if (surfaceTextureView.IsAvailable)
            {
                ForceResetLensFacing();
            }
            else
            {
                surfaceTextureView.SurfaceTextureAvailable += SurfaceTextureView_SurfaceTextureAvailable;
            }
        }

        private void SurfaceTextureView_SurfaceTextureAvailable(object sender, TextureView.SurfaceTextureAvailableEventArgs e)
        {
            ForceResetLensFacing();
        }

        private void StartBackgroundThread()
        {
            backgroundThread = new HandlerThread("CameraBackground");
            backgroundThread.Start();
            backgroundHandler = new Handler(backgroundThread.Looper);
        }

        private void SwitchCameraButton_Click(object sender, EventArgs e)
        {
            // TODO
        }

        protected override void OnPause()
        {
            base.OnPause();
            switchCameraButton.Click -= SwitchCameraButton_Click;
            takePictureButton.Click -= TakePictureButton_Click;
            recordVideoButton.Click -= RecordVideoButton_Click;
            surfaceTextureView.SurfaceTextureAvailable -= SurfaceTextureView_SurfaceTextureAvailable;

            StopBackgroundThread();
        }

        private void StopBackgroundThread()
        {
            if (backgroundThread == null) return;

            backgroundThread.QuitSafely();
            try
            {
                backgroundThread.Join();
                backgroundThread = null;
                backgroundHandler = null;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{e.Message} {e.StackTrace}");
            }
        }

        /// <summary>
        /// This method forces our view to re-create the camera session by changing 'currentLensFacing' and requesting the original value
        /// </summary>
        private void ForceResetLensFacing()
        {
            var targetLensFacing = currentLensFacing;
            currentLensFacing = currentLensFacing == LensFacing.Back ? LensFacing.Front : LensFacing.Back;
            SetLensFacing(targetLensFacing);
        }

        private void SetLensFacing(LensFacing lenseFacing)
        {
            bool shouldRestartCamera = currentLensFacing != lenseFacing;
            currentLensFacing = lenseFacing;
            string cameraId = string.Empty;
            characteristics = null;

            foreach (var id in manager.GetCameraIdList())
            {
                cameraId = id;
                characteristics = manager.GetCameraCharacteristics(id);

                var face = (int)characteristics.Get(CameraCharacteristics.LensFacing);
                if (face == (int)currentLensFacing)
                {
                    break;
                }
            }

            if (characteristics == null) return;

            if (cameraDevice != null)
            {
                try
                {
                    if (!shouldRestartCamera)
                        return;
                    if (cameraDevice.Handle != IntPtr.Zero)
                    {
                        cameraDevice.Close();
                        cameraDevice.Dispose();
                        cameraDevice = null;
                    }
                }
                catch (Exception e)
                {
                    //Ignored
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }

            SetUpCameraOutputs(cameraId);
            ConfigureTransform(surfaceTextureView.Width, surfaceTextureView.Height);
            manager.OpenCamera(cameraId, cameraStateCallback, null);
        }

        private void SetUpCameraOutputs(string selectedCameraId)
        {
            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            if (map == null)
            {
                return;
            }

            // For still image captures, we use the largest available size.
            Size largest = (Size)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)),
                new CompareSizesByArea());

            if (imageReader == null)
            {
                imageReader = ImageReader.NewInstance(largest.Width, largest.Height, ImageFormatType.Jpeg, maxImages: 1);
                imageReader.SetOnImageAvailableListener(onImageAvailableListener, backgroundHandler);
            }

            // Find out if we need to swap dimension to get the preview size relative to sensor
            // coordinate.
            var displayRotation = windowManager.DefaultDisplay.Rotation;
            sensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);
            bool swappedDimensions = false;
            switch (displayRotation)
            {
                case SurfaceOrientation.Rotation0:
                case SurfaceOrientation.Rotation180:
                    if (sensorOrientation == 90 || sensorOrientation == 270)
                    {
                        swappedDimensions = true;
                    }
                    break;
                case SurfaceOrientation.Rotation90:
                case SurfaceOrientation.Rotation270:
                    if (sensorOrientation == 0 || sensorOrientation == 180)
                    {
                        swappedDimensions = true;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"Display rotation is invalid: {displayRotation}");
                    break;
            }

            Point displaySize = new Point();
            windowManager.DefaultDisplay.GetSize(displaySize);
            var rotatedPreviewWidth = surfaceTextureView.Width;
            var rotatedPreviewHeight = surfaceTextureView.Height;
            var maxPreviewWidth = displaySize.X;
            var maxPreviewHeight = displaySize.Y;

            if (swappedDimensions)
            {
                rotatedPreviewWidth = surfaceTextureView.Height;
                rotatedPreviewHeight = surfaceTextureView.Width;
                maxPreviewWidth = displaySize.Y;
                maxPreviewHeight = displaySize.X;
            }

            // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
            // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
            // garbage capture data.
            previewSize = ChooseOptimalSize(map.GetOutputSizes(Java.Lang.Class.FromType(typeof(SurfaceTexture))),
                rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                maxPreviewHeight, largest);

            // We fit the aspect ratio of TextureView to the size of preview we picked.
            // The commented code handles landscape layouts. This app is portrait only, so this is not needed
            /*
            var orientation = Application.Context.Resources.Configuration.Orientation;
            if (orientation == global::Android.Content.Res.Orientation.Landscape)
            {
                surfaceTextureView.SetAspectRatio(previewSize.Width, previewSize.Height);
            }
            else
            {*/
                surfaceTextureView.SetAspectRatio(previewSize.Height, previewSize.Width);
            /*}*/

            // Check if the flash is supported.
            var available = (bool?)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
            if (available == null)
            {
                flashSupported = false;
            }
            else
            {
                flashSupported = (bool)available;
            }
            return;
        }

        // Configures the necessary matrix
        // transformation to `surfaceTextureView`.
        // This method should be called after the camera preview size is determined in
        // setUpCameraOutputs and also the size of `surfaceTextureView` is fixed.
        public void ConfigureTransform(int viewWidth, int viewHeight)
        {
            if (null == surfaceTextureView || null == previewSize)
            {
                return;
            }
            var rotation = (int)WindowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, previewSize.Height, previewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Math.Max((float)viewHeight / previewSize.Height, (float)viewWidth / previewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }
            surfaceTextureView.SetTransform(matrix);
        }

        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth,
            int textureViewHeight, int maxWidth, int maxHeight, Size aspectRatio)
        {
            // Collect the supported resolutions that are at least as big as the preview Surface
            var bigEnough = new List<Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if (option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else if ((option.Width <= maxWidth) && (option.Height <= maxHeight))
                    {
                        notBigEnough.Add(option);
                    }
                }
            }

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new CompareSizesByArea());
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new CompareSizesByArea());
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Couldn't find any suitable preview size");
                return choices[0];
            }
        }

        private void OnOpened(CameraDevice cameraDevice)
        {
            this.cameraDevice = cameraDevice;
            surfaceTextureView.SurfaceTexture.SetDefaultBufferSize(previewSize.Width, previewSize.Height);
            previewSurface = new Surface(surfaceTextureView.SurfaceTexture);

            this.cameraDevice.CreateCaptureSession(new List<Surface> { previewSurface, imageReader.Surface }, captureStateSessionCallback, backgroundHandler);
        }

        private void OnDisconnected(CameraDevice cameraDevice)
        {
            // In a real application we may need to handle the user disconnecting external devices.
            // Here we're only worring about built-in cameras
        }

        private void OnError(CameraDevice cameraDevice, CameraError cameraError)
        {
            // In a real application we should handle errors gracefully
        }

        private void OnPreviewSessionConfigured(CameraCaptureSession session)
        {
            captureSession = session;

            previewRequestBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            previewRequestBuilder.AddTarget(previewSurface);

            var availableAutoFocusModes = (int[])characteristics.Get(CameraCharacteristics.ControlAfAvailableModes);
            if (availableAutoFocusModes.Any(afMode => afMode == (int)ControlAFMode.ContinuousPicture))
            {
                previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            }
            SetAutoFlash(previewRequestBuilder);

            previewRequest = previewRequestBuilder.Build();

            captureSession.SetRepeatingRequest(previewRequest, cameraCaptureCallback, backgroundHandler);
        }

        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (flashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
            }
        }
    }
}