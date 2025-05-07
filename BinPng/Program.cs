using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System.IO.Hashing;

class Program
{
    static void Main(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.EndsWith(".png") || arg.EndsWith(".jpg"))
            {
                File.WriteAllBytes(arg + ".bin", new Decoder().Decode(Image.Load<Rgb24>(arg)));
            }
            else
            {
                new Encoder().Encode(File.ReadAllBytes(arg)).SaveAsPng(arg + ".png");
            }
        }
    }
}

public class Encoder
{
    public Image<Rgb24> Encode(byte[] input)
    {
        byte[] data = PreEncode(input);
        Color[] pixels = new Color[(int)(Math.Ceiling(data.Length / 3d) * 4)];
        for (var i = 0; i < data.Length; i += 3)
        {
            int pixelIndex = (int)(Math.Ceiling(i / 3d) * 4);
            byte r1 = 0, g1 = 0, b1 = 0, r2 = 0, g2 = 0, b2 = 0, r3 = 0, g3 = 0, b3 = 0, r4 = 0, g4 = 0, b4 = 0;
            if (data.Length > i)
            {
                r1 = (byte)((0x3 & (data[i + 0] >> 6)) * (byte.MaxValue / 0x3));
                g1 = (byte)((0x3 & (data[i + 0] >> 4)) * (byte.MaxValue / 0x3));
                b1 = (byte)((0x3 & (data[i + 0] >> 2)) * (byte.MaxValue / 0x3));
                r2 = (byte)((0x3 & (data[i + 0] >> 0)) * (byte.MaxValue / 0x3));
            }
            if (data.Length > i + 1)
            {
                g2 = (byte)((0x3 & (data[i + 1] >> 6)) * (byte.MaxValue / 0x3));
                b2 = (byte)((0x3 & (data[i + 1] >> 4)) * (byte.MaxValue / 0x3));
                r3 = (byte)((0x3 & (data[i + 1] >> 2)) * (byte.MaxValue / 0x3));
                g3 = (byte)((0x3 & (data[i + 1] >> 0)) * (byte.MaxValue / 0x3));
            }
            if (data.Length > i + 2)
            {
                b3 = (byte)((0x3 & (data[i + 2] >> 6)) * (byte.MaxValue / 0x3));
                r4 = (byte)((0x3 & (data[i + 2] >> 4)) * (byte.MaxValue / 0x3));
                g4 = (byte)((0x3 & (data[i + 2] >> 2)) * (byte.MaxValue / 0x3));
                b4 = (byte)((0x3 & (data[i + 2] >> 0)) * (byte.MaxValue / 0x3));
            }
            pixels[pixelIndex + 0] = Color.FromRgb(r1, g1, b1);
            pixels[pixelIndex + 1] = Color.FromRgb(r2, g2, b2);
            pixels[pixelIndex + 2] = Color.FromRgb(r3, g3, b3);
            pixels[pixelIndex + 3] = Color.FromRgb(r4, g4, b4);
        }
        int size = (int)Math.Ceiling(Math.Sqrt(pixels.Length));
        var image = new Image<Rgb24>(size, size);
        image.Mutate(x => x.Fill(Color.White));
        for (int i = 0; i < pixels.Length; i++)
        {
            var (x, y) = GetLocation(size, i);
            image[x, y] = pixels[i];
        }
        image.Mutate(x => x.Resize(image.Width * 2, image.Height * 2, new BoxResampler(), false));
        return image;
    }

    private (int x, int y) GetLocation(int size, int index)
    {
        return (index % size, index / size);
    }

    private byte[] PreEncode(byte[] data)
    {
        var length = BitConverter.GetBytes(data.Length);
        return [.. XxHash32.Hash([.. length, .. data]), .. length, .. data];
    }
}

public class Decoder
{
    /// <summary>
    /// 元のコードのスケーリングを元に戻し、2ビットの値 (0-3) を取得します。
    /// </summary>
    private static byte InverseScale(byte component)
    {
        return (byte)Math.Round(component / (byte.MaxValue / 3d));
    }

    public byte[] Decode(Image<Rgb24> image)
    {
        var stream = new MemoryStream();
        image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2, new BoxResampler(), false));
        for (int i = 0; i < image.Width * image.Height; i += 4)
        {
            byte r1 = 0, g1 = 0, b1 = 0, r2 = 0, g2 = 0, b2 = 0, r3 = 0, g3 = 0, b3 = 0, r4 = 0, g4 = 0, b4 = 0;
            var (x1, y1) = GetLocation(image.Width, i + 0);
            if (image.Width > x1 && image.Height > y1)
            {
                (r1, g1, b1) = (image[x1, y1].R, image[x1, y1].G, image[x1, y1].B);
            }
            var (x2, y2) = GetLocation(image.Width, i + 1);
            if (image.Width > x2 && image.Height > y2)
            {
                (r2, g2, b2) = (image[x2, y2].R, image[x2, y2].G, image[x2, y2].B);
            }
            var (x3, y3) = GetLocation(image.Width, i + 2);
            if (image.Width > x3 && image.Height > y3)
            {
                (r3, g3, b3) = (image[x3, y3].R, image[x3, y3].G, image[x3, y3].B);
            }
            var (x4, y4) = GetLocation(image.Width, i + 3);
            if (image.Width > x4 && image.Height > y4)
            {
                (r4, g4, b4) = (image[x4, y4].R, image[x4, y4].G, image[x4, y4].B);
            }
            stream.Write(ReconstructData(r1, g1, b1, r2, g2, b2, r3, g3, b3, r4, g4, b4));
        }
        stream.Position = 0;
        var raw = stream.ToArray();
        var hash = raw.AsSpan(0, 4).ToArray();
        var length = 0xFFFFFF & BitConverter.ToInt32(raw.AsSpan(4, 4).ToArray());
        var data = raw.AsSpan(8, length).ToArray();

        if (!hash.SequenceEqual(XxHash32.Hash([.. raw.AsSpan(4, 4), .. data])))
        {
            Console.Error.WriteLine("ハッシュが一致しません");
        }
        return data;
    }

    private (int x, int y) GetLocation(int size, int index)
    {
        return (index % size, index / size);
    }

    public static byte[] ReconstructData(
        byte r1, byte g1, byte b1, byte r2,
        byte g2, byte b2, byte r3, byte g3,
        byte b3, byte r4, byte g4, byte b4)
    {
        return [
            (byte)((InverseScale(r1) << 6) | (InverseScale(g1) << 4) | (InverseScale(b1) << 2) | (InverseScale(r2) << 0)),
            (byte)((InverseScale(g2) << 6) | (InverseScale(b2) << 4) | (InverseScale(r3) << 2) | (InverseScale(g3) << 0)),
            (byte)((InverseScale(b3) << 6) | (InverseScale(r4) << 4) | (InverseScale(g4) << 2) | (InverseScale(b4) << 0))
        ];
    }
}
