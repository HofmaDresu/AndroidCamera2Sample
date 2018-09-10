using System;
using Android.Content;
using Android.Util;
using Android.Views;

namespace AndroidCamera2Demo.Controls
{
    /// <summary>
    /// AutoFitTextureView provides the method SetAspectRatio. This method adjusts the dimensions of the view the smallest amount possible to match the desired aspect ratio
    /// </summary>
    class AutoFitTextureView : TextureView
    {
        private int ratioWidth = 0;
        private int ratioHeight = 0;

        public AutoFitTextureView(Context context) : this(context, null)
        {
        }

        public AutoFitTextureView(Context context, IAttributeSet attrs) :
        this(context, attrs, 0)
        {
        }

        public AutoFitTextureView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {

        }


        /// <summary>
        /// Set the desired aspect ratio for this view
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void SetAspectRatio(int width, int height)
        {
            if (width < 0 || height < 0)
                throw new Exception("Size cannot be negative.");
            ratioWidth = width;
            ratioHeight = height;
            RequestLayout();
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            int width = MeasureSpec.GetSize(widthMeasureSpec);
            int height = MeasureSpec.GetSize(heightMeasureSpec);
            if (0 == ratioWidth || 0 == ratioHeight)
            {
                SetMeasuredDimension(width, height);
            }
            else
            {
                // The commented out section allows this method to alter the height of this view if that is the smaller change.
                // In this sample we want to have a fixed height, so we're only allowing this method to change width
                /*
                if (width < (float)height * ratioWidth / ratioHeight)
                {
                    SetMeasuredDimension(width, width * ratioHeight / ratioWidth);
                }
                else
                {
                */
                    SetMeasuredDimension(height * ratioWidth / ratioHeight, height);
                //}
            }
        }
    }
}