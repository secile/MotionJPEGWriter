using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace GitHub.secile.Avi
{
    class MjpegWriter
    {
        private AviWriter aviWriter;

        public MjpegWriter(System.IO.Stream outputAvi, int width, int height, int fps)
        {
            aviWriter = new AviWriter(outputAvi, "MJPG", width, height, fps);
        }

        public void AddImage(Bitmap bmp)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                aviWriter.AddImage(ms.GetBuffer());
            }
        }

        public void Close()
        {
            aviWriter.Close();
        }
    }
}
