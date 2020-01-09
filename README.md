# MotionJPEGWriter
C# source code for creating Motion JPEG file.  
Add 'AviWriter.cs', 'MjpegWriter.cs', 'RiffFile.cs' to your project.

```C#
// create writer
var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\test.avi";
var output = System.IO.File.OpenWrite(path);
var writer = new MjpegWriter(output, 640, 480, 1);

// writer frame
foreach (var item in dialog.FileNames)
{
    writer.AddImage(new Bitmap(item));
}
writer.Close();
```

# Sample for create Motion JPEG file with audio stream.
```C#
private void Sample()
{
    // set VideoFormat.
    var videoFormat = new AviWriter.VideoFormat();
    videoFormat.Width = 640;
    videoFormat.Height = 480;
    videoFormat.FramesPerSec = 10;

    // set AudioFormat.
    var audioFormat = new AviWriter.AudioFormat();
    audioFormat.Channels = 1;
    audioFormat.BitsPerSample = 16;
    audioFormat.SamplesPerSec = 44100;

    // create writer.
    var output = System.IO.File.OpenWrite(@"C:\Debug\text.avi");
    var avi = new MjpegWriter(output, videoFormat, audioFormat);

    // write each frame.
    // video play time = frame count / videoFormat.FramesPerSec.
    for (int i = 0; i < 3600; i++)
    {
        // write frame image.
        var bmp = CreateBitmapWithNumber(640, 480, i);
        avi.AddImage(bmp);

        // write audio data per frame.
        var data_size = (int)(audioFormat.SamplesPerSec * audioFormat.Channels / videoFormat.FramesPerSec);
        var sine_wave = SineWaveGenerator(1000, audioFormat.SamplesPerSec, short.MaxValue, 0).Select(x => (short)x).Take(data_size).ToArray();
        avi.AddAudio(sine_wave);
    }

    // close file.
    avi.Close();
}

private Bitmap CreateBitmapWithNumber(int width, int height, int no)
{
    var bmp = new Bitmap(width, height);
    var g = Graphics.FromImage(bmp);
    g.Clear(Color.White);
    g.DrawString(no.ToString(), this.Font, Brushes.Black, width / 2, height / 2);
    g.Dispose();
    return bmp;
}

private static IEnumerable<double> SineWaveGenerator(double freq, int rate, double amplitude, int phase)
{
    while (true)
    {
        yield return Math.Sin(2 * Math.PI * freq * phase++ / rate) * amplitude;
    }
}

```
