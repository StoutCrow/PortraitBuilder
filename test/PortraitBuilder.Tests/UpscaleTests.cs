using PortraitBuilder.Engine;
using SkiaSharp;
using Xunit;

namespace PortraitBuilder.Tests
{
    public class UpscaleTests
    {
        // 176x176 portrait: opaque red disc on a transparent background, like a portrait with its round frame.
        private static SKImage MakePortrait()
        {
            using var bmp = new SKBitmap(new SKImageInfo(176, 176, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = true };
            canvas.DrawCircle(88, 88, 80, paint);
            return SKImage.FromBitmap(bmp);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        public void Upscale_multiplies_size_and_keeps_colour_and_transparency(int factor)
        {
            using var source = MakePortrait();
            using var big = PortraitRenderer.Upscale(source, factor);
            Assert.Equal(176 * factor, big.Width);
            Assert.Equal(176 * factor, big.Height);

            using var bmp = SKBitmap.FromImage(big);
            var centre = bmp.GetPixel(88 * factor, 88 * factor);
            Assert.Equal(255, centre.Alpha);
            Assert.True(centre.Red > 240 && centre.Green < 15 && centre.Blue < 15, $"centre was {centre}");
            Assert.Equal(0, bmp.GetPixel(2, 2).Alpha); // corner outside the disc stays transparent
        }

        [Fact]
        public void Upscale_by_one_returns_the_source()
        {
            using var source = MakePortrait();
            Assert.Same(source, PortraitRenderer.Upscale(source, 1));
        }
    }
}
