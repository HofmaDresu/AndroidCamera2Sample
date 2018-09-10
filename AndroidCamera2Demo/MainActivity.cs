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

        protected override void OnResume()
        {
            base.OnResume();
            switchCameraButton.Click += SwitchCameraButton_Click;
            takePictureButton.Click += TakePictureButton_Click;
            recordVideoButton.Click += RecordVideoButton_Click;
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
        }

        private void OnOpened(CameraDevice cameraDevice)
        {
            // TODO
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
            // TODO
        }
    }
}