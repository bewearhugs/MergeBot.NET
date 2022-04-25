#if NETFRAMEWORK
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SysBot.Pokemon.WinForms
{
    public static class InitUtil
    {
        public static void InitializeStubs(ProgramMode mode)
        {
            try
            {
                SaveFile sav8 = mode switch
                {
                    ProgramMode.SWSH => new SAV8SWSH(),
                    ProgramMode.BDSP => new SAV8BS(),
                    ProgramMode.LA => new SAV8LA(),
                    _ => throw new System.ArgumentOutOfRangeException(nameof(mode)),
                };

                SetUpSpriteCreator(sav8);
            }
            catch
            {
                SaveFile sav7 = mode switch
                {
                    ProgramMode.LGPE => new SAV7b(),
                    _ => throw new System.ArgumentOutOfRangeException(nameof(mode)),
                };

                SetUpSpriteCreator(sav7);
            }
        }

        private static void SetUpSpriteCreator(SaveFile sav)
        {
            SpriteUtil.Initialize(sav);
            PokeTradeBotLGPE.CreateSpriteFile = (code) =>
            {
                int codecount = 0;
                foreach (PokeTradeBotLGPE.pictocodes cd in code)
                {
                    //SpriteUtil.UseLargeAlways = true;
                    var showdown = new ShowdownSet(cd.ToString());
                    PKM pk = PokeTradeBotLGPE.sav.GetLegalFromSet(showdown, out _);
                    Image png = SpriteUtil.GetSprite(pk.Species, 0, 0, 0, 0, false, false, -1, true);
                    png = ResizeImage(png, 137, 130);
                    png.Save($"{System.IO.Directory.GetCurrentDirectory()}//code{codecount}.png");
                    codecount++;
                }
            };
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(-40, -65, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                }
            }
            return destImage;
        }
    }
}
#endif
