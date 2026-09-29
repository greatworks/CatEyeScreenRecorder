using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FreeWindowsScreenRecorder;
internal static class MakeIcon
{
    static void Main(string[] args)
    {
        using (Bitmap image = new Bitmap(64, 64))
        using (Graphics g = Graphics.FromImage(image))
        using (MemoryStream png = new MemoryStream())
        {
            g.Clear(Theme.Background); Theme.Mark(g, new Rectangle(4, 4, 56, 56)); image.Save(png, ImageFormat.Png);
            string logoPath = Path.Combine(Path.GetDirectoryName(args[0]), "CatEyeLogo.png");
            using (Bitmap logo = new Bitmap(512, 512)) using (Graphics logoGraphics = Graphics.FromImage(logo))
            {
                logoGraphics.Clear(Theme.Background); Theme.Mark(logoGraphics, new Rectangle(32, 32, 448, 448)); logo.Save(logoPath, ImageFormat.Png);
            }
            byte[] bytes = png.ToArray();
            using (BinaryWriter writer = new BinaryWriter(File.Create(args[0])))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
                writer.Write((byte)64); writer.Write((byte)64); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(bytes.Length); writer.Write(22); writer.Write(bytes);
            }
        }
    }
}
