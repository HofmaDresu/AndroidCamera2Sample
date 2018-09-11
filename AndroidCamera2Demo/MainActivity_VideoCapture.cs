using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.Util;
using Android.Views;

namespace AndroidCamera2Demo
{
    // Video Capture specific code
    public partial class MainActivity
    {
        private MediaRecorder mediaRecorder;
        private bool isRecording;
        private string videoFileName;

        private void RecordVideoButton_Click(object sender, EventArgs e)
        {
            if (!isRecording)
            {
                recordVideoButton.Text = "Stop Recording";
                PrepareMediaRecorder();
                cameraDevice.CreateCaptureSession(new List<Surface> { previewSurface, mediaRecorder.Surface }, videoSessionStateCallback, backgroundHandler);
            }
            else
            {
                recordVideoButton.Text = "Record Video";
                isRecording = false;
                if (mediaRecorder != null)
                {
                    try
                    {
                        mediaRecorder.Stop();
                        var intent = new Intent(Intent.ActionView);
                        intent.AddFlags(ActivityFlags.NewTask);
                        intent.SetDataAndType(Android.Net.Uri.Parse(videoFileName), "video/mp4");
                        StartActivity(intent);
                    }
                    catch (Exception)
                    {
                        // Stop can throw an exception if the user records a 0 length video.This should be handled by deleting the empty file
                    }
                    finally
                    {
                        mediaRecorder.Reset();
                        captureSession.Close();
                    }
                }
            }
        }

        void PrepareMediaRecorder()
        {
            if (mediaRecorder == null)
            {
                mediaRecorder = new MediaRecorder();
            }
            else
            {
                mediaRecorder.Reset();
            }

            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            if (map == null)
            {
                return;
            }

            videoFileName = Guid.NewGuid().ToString();

            var storageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMovies);
            var storageFilePath = storageDir + Java.IO.File.Separator + "AndroidCamera2Demo" + Java.IO.File.Separator + "Videos" + Java.IO.File.Separator;
            videoFileName = storageFilePath + videoFileName;

            var file = new Java.IO.File(storageFilePath);
            if (!file.Exists())
            {
                file.Mkdirs();
            }

            mediaRecorder.SetAudioSource(AudioSource.Mic);
            mediaRecorder.SetVideoSource(VideoSource.Surface);
            mediaRecorder.SetOutputFormat(OutputFormat.Mpeg4);
            mediaRecorder.SetOutputFile(videoFileName);
            mediaRecorder.SetVideoEncodingBitRate(10000000);
            mediaRecorder.SetVideoFrameRate(30);
            var videoSize = ChooseVideoSize(map.GetOutputSizes(Java.Lang.Class.FromType(typeof(MediaRecorder))));
            mediaRecorder.SetVideoEncoder(VideoEncoder.H264);
            mediaRecorder.SetAudioEncoder(AudioEncoder.Aac);
            mediaRecorder.SetVideoSize(videoSize.Width, videoSize.Height);
            int rotation = (int)WindowManager.DefaultDisplay.Rotation;
            mediaRecorder.SetOrientationHint(GetOrientation(rotation));
            mediaRecorder.Prepare();
        }

        Size ChooseVideoSize(Size[] choices)
        {
            foreach (Size size in choices)
            {
                if (size.Width == size.Height * 4 / 3 && size.Width <= 1000)
                    return size;
            }
            System.Diagnostics.Debug.WriteLine("Couldn't find any suitable video size");
            return choices[choices.Length - 1];
        }

        private void OnVideoSessionConfigured(CameraCaptureSession session)
        {
            var recordRequestBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            recordRequestBuilder.AddTarget(previewSurface);
            recordRequestBuilder.AddTarget(mediaRecorder.Surface);

            var availableAutoFocusModes = (int[])characteristics.Get(CameraCharacteristics.ControlAfAvailableModes);
            if (availableAutoFocusModes.Any(afMode => afMode == (int)ControlAFMode.ContinuousVideo))
            {
                previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousVideo);
            }

            captureSession.Close();
            captureSession = session;
            captureSession.SetRepeatingRequest(recordRequestBuilder.Build(), null, null);

            mediaRecorder.Start();
            isRecording = true;
        }
    }
}