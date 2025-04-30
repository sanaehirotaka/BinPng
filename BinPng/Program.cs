using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

class Program
{
    static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].EndsWith(".png"))
            {
                using var input = File.OpenRead(args[i]);
                using var output = File.OpenWrite(args[i] + ".out");
                new Decoder().Decode(input, output);
            }
            else
            {
                using var input = File.OpenRead(args[i]);
                using var output = File.OpenWrite(args[i] + ".png");
                new Encoder().Encode(input, output);
            }
        }
    }
}

class Encoder
{
    public void Encode(Stream input, Stream output)
    {
        var header = new MemoryStream();
        header.Write(BitConverter.GetBytes(input.Length));　// 8 bytes
        header.Position = 0;

        var bytes = input.Length + 9;
        var pixels = (int)Math.Ceiling(bytes / 3d);
        var size = (int)Math.Ceiling(Math.Sqrt(pixels));
        using var image = new Image<Rgb24>(size + 2, size + 2);
        image.Mutate(x => x.Fill(Color.White));
        var index = Write(image, header, 0, size);
        Write(image, input, index, size);
        image.SaveAsPng(output);
    }

    public int Write(Image<Rgb24> image, Stream input, int startIndex, int size)
    {
        var pixels = (int)Math.Ceiling(input.Length / 3d);
        for (int i = 0; i < pixels; i++)
        {
            var (x, y) = GetLocation(size, startIndex + i);
            image[x, y] = GetColor(input);
        }
        return pixels;
    }

    public (int x, int y) GetLocation(int size, int index)
    {
        return ((index % size) + 1, (index / size) + 1);
    }

    public Color GetColor(Stream data)
    {
        var bytes = new byte[3];
        var size = data.Read(bytes);
        return size switch
        {
            0 => Color.FromRgb(0, 0, 0),
            1 => Color.FromRgb(bytes[0], 0, 0),
            2 => Color.FromRgb(bytes[0], bytes[1], 0),
            _ => Color.FromRgb(bytes[0], bytes[1], bytes[2]),
        };
    }
}

class Decoder
{
    public void Decode(Stream input, Stream output)
    {
        using var image = Image.Load<Rgb24>(input);

        var dataAreaWidth = image.Width - 2;
        var dataAreaHeight = image.Height - 2;

        var size = dataAreaWidth;

        var byteList = new List<byte>();

        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                var pixel = image[x, y];
                byteList.Add(pixel.R);
                byteList.Add(pixel.G);
                byteList.Add(pixel.B);
            }
        }
        using var dataStream = new MemoryStream(byteList.ToArray());
        var headerBytes = new byte[9];
        dataStream.Read(headerBytes, 0, 9);
        long originalFileSize = BitConverter.ToInt64(headerBytes, 0);
        long availableDataBytes = dataStream.Length - dataStream.Position;

        long totalBytesWritten = 0;
        byte[] buffer = new byte[1024];
        while (totalBytesWritten < originalFileSize)
        {
            int bytesToReadInChunk = (int)Math.Min(buffer.Length, originalFileSize - totalBytesWritten);
            bytesToReadInChunk = (int)Math.Min(bytesToReadInChunk, (int)(dataStream.Length - dataStream.Position));
            int currentBytesRead = dataStream.Read(buffer, 0, bytesToReadInChunk);
            output.Write(buffer, 0, currentBytesRead);
            totalBytesWritten += currentBytesRead;
        }
    }
}
