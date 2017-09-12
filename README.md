# MotionJPEGWriter
C# source code for creating Motion JPEG file.

[How to use]
    var dialog = new OpenFileDialog() { Multiselect = true };
    dialog.Filter = "Image Files (*.bmp, *.jpg, *.png)|*.bmp;*.jpg;*.png";
    if (dialog.ShowDialog() == DialogResult.OK)
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\test.avi";
        var output = System.IO.File.OpenWrite(path);
        var writer = new GitHub.secile.Avi.MjpegWriter(output, 640, 480, 1);
        foreach (var item in dialog.FileNames)
        {
            writer.AddImage(new Bitmap(item));
        }
        writer.Close();
        MessageBox.Show(path + "\nis created.");
    }
