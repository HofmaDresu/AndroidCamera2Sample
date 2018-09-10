using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using AndroidCamera2Demo.Controls;
using Android.Content.PM;

namespace AndroidCamera2Demo
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);


            surfaceTextureView = FindViewById<AutoFitTextureView>(Resource.Id.surface);
            switchCameraButton = FindViewById<ImageButton>(Resource.Id.surface);
            takePictureButton = FindViewById<Button>(Resource.Id.surface);
            recordVideoButton = FindViewById<Button>(Resource.Id.surface);
        }

        private AutoFitTextureView surfaceTextureView;
        private ImageButton switchCameraButton;
        private Button takePictureButton;
        private Button recordVideoButton;
    }
}