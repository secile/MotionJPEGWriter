# MotionJPEGWriter
C# source code for creating Motion JPEG file.

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
