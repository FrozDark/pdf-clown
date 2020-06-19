﻿using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.Scanner;
using PdfClown.Documents.Interaction.Annotations;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfClown.Tools;
using PdfClown.Documents.Contents.Entities;

namespace PdfClown.Viewer
{
    public class PdfPageView : IDisposable
    {
        private static Task task;
        public static bool IsPaintComplete => (task?.IsCompleted ?? true);

        private SKPicture picture;
        public SKMatrix Matrix = SKMatrix.MakeIdentity();
        private Page page;
        private SKImage image;
        private float imageScale;

        public PdfPageView()
        {
        }

        public SKPicture GetPicture(SKCanvasView canvasView)
        {
            if (picture == null)
            {
                if (task == null || task.IsCompleted)
                {
                    task = new Task(() => Paint(canvasView));
                    task.Start();
                }
            }
            return picture;
        }

        public SKImage GetImage(SKCanvasView canvasView, float scaleX, float scaleY)
        {
            var picture = GetPicture(canvasView);
            if (picture == null)
            {
                return null;
            }
            if (scaleX != imageScale || image == null)
            {
                imageScale = scaleX;
                image?.Dispose();
                var imageSize = new SKSizeI((int)(Size.Width * scaleX), (int)(Size.Height * scaleY));
                var matrix = SKMatrix.Identity;
                if (imageScale < 1F)
                {
                    //matrix = SKMatrix.CreateScale(scaleX, scaleY);
                }
                image = SKImage.FromPicture(picture, imageSize, matrix);//, Matrix, 
            }
            return image;
        }

        private void Paint(SKCanvasView canvasView)
        {
            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(SKRect.Create(Size)))
            {
                try
                {
                    Page.Render(canvas, Size, false);
                }
                catch (Exception ex)
                {
                    using (var paint = new SKPaint { Color = SKColors.DarkRed })
                    {
                        canvas.Save();
                        if (canvas.TotalMatrix.ScaleY < 0)
                        {
                            var matrix = SKMatrix.MakeScale(1, -1);
                            canvas.Concat(ref matrix);
                        }
                        canvas.DrawText(ex.Message, 0, 0, paint);
                        canvas.Restore();
                    }
                }
                picture = recorder.EndRecording();
            }
            //text
            var positionComparator = new TextStringPositionComparer<ITextString>();
            Page.Strings.Sort(positionComparator);

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() => canvasView.InvalidateSurface());
        }

        public Page Page
        {
            get => page;
            set => page = value;
        }

        public SKSize Size { get; set; }

        public SKRect Bounds => SKRect.Create(
            Matrix.TransX,
            Matrix.TransY,
            Size.Width * Matrix.ScaleX,
            Size.Height * Matrix.ScaleY);

        public void Dispose()
        {
            image?.Dispose();
            picture?.Dispose();
        }
    }
}
