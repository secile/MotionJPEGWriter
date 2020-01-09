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

        /// <summary>Create with video stream.</summary>
        public MjpegWriter(System.IO.Stream outputAvi, int width, int height, int fps)
            : this(outputAvi, new AviWriter.VideoFormat() { Width = width, Height = height, FramesPerSec = fps }) { }

        /// <summary>Create with video stream.</summary>
        public MjpegWriter(System.IO.Stream outputAvi, AviWriter.VideoFormat videoFormat)
            : this(outputAvi, videoFormat, null) { }

        /// <summary>Create with video and audio stream.</summary>
        public MjpegWriter(System.IO.Stream outputAvi, AviWriter.VideoFormat videoFormat, AviWriter.AudioFormat audioFormat)
        {
            aviWriter = new AviWriter(outputAvi, "MJPG", videoFormat, audioFormat);
        }

        /// <summary>Add image to video stream.</summary>
        public void AddImage(Bitmap bmp)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                aviWriter.AddImage(ms.GetBuffer());
            }
        }

        /// <summary>Add audio data. Use when BitsPerSample = 8.</summary>
        /// <param name="data">8bit(0 to 255, base line = 128) wave pcm audio data. if stereo (Channels = 2), data order is LRLR...</param>
        public void AddAudio(byte[] data)
        {
            aviWriter.AddAudio(data);
        }

        /// <summary>Add audio data. Use when BitsPerSample = 16.</summary>
        /// <param name="data">16bit(-32767 to 32767) wave pcm audio data. if stereo (Channels = 2), data order is LRLR...</param>
        public void AddAudio(short[] data)
        {
            var byte_data = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                byte_data[(i * 2) + 0] = (byte)(data[i] & 0xFF);
                byte_data[(i * 2) + 1] = (byte)(data[i] >> 8);
            }
            aviWriter.AddAudio(byte_data);
        }

        public void Close()
        {
            aviWriter.Close();
        }
    }
}
