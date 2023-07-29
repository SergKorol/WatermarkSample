using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace WatermarkSample.Controllers;

[ApiController]
[Route("api/v1/pdf")]
public class WatermarkController : ControllerBase
{
    [HttpPost]
    [Route("watermark")]
    public IActionResult AddWatermark(IFormFile? watermark, IFormFile? document)
    {
        try
        {
            if (watermark is null || document is null)
            {
                return BadRequest("Both PNG and PDF images are required.");
            }

            byte[]? pngBytes = null;
            byte[]? pdfBytes = null;

            if (watermark.ContentType == "image/png")
            {
                using var memoryStream = new MemoryStream();
                watermark.CopyTo(memoryStream);
                pngBytes = memoryStream.ToArray();
            }
            else
            {
                return BadRequest("The watermark should be a .PNG format");
            }

            if (document.ContentType == "application/pdf")
            {
                using var memoryStream = new MemoryStream();
                document.CopyTo(memoryStream);
                pdfBytes = memoryStream.ToArray();
            }
            else
            {
                return BadRequest("The document should be a .PDF format");
            }

            var mergedImages = MergeImages(pngBytes, pdfBytes);

            var pdfDocument = ConvertToPdf(mergedImages);

            return File(pdfDocument, "application/pdf", "watermarked.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(ex);
        }
    }

    private SKBitmap MakeBackgroundTransparent(byte[] pngBytes)
    {
        using var pngStream = new MemoryStream(pngBytes);
        var image = SKBitmap.Decode(pngStream);
        var transparentImage = new SKBitmap(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var pixelColor = image.GetPixel(x, y);

                if (pixelColor.Red == 255 && pixelColor.Green == 255 && pixelColor.Blue == 255)
                {
                    pixelColor = new SKColor(255, 255, 255, 0);
                }
                else
                {
                    pixelColor = new SKColor(0, 0, 0, 64);
                }

                transparentImage.SetPixel(x, y, pixelColor);
            }
        }

        return transparentImage;
    }


    private IEnumerable<SKBitmap> MergeImages(byte[] pngBytes, byte[] pdfBytes)
    {
        var pngImage = MakeBackgroundTransparent(pngBytes);

        foreach (var pdfImage in PDFtoImage.Conversion.ToImages(pdfBytes))
        {
            var mergedImage = new SKBitmap(pdfImage.Width, pdfImage.Height);
            using (var canvas = new SKCanvas(mergedImage))
            {
                canvas.DrawBitmap(pdfImage, 0, 0);
                int x = pdfImage.Width - pngImage.Width - 5;
                int y = pdfImage.Height - pngImage.Height - 5;
                canvas.DrawBitmap(pngImage, x, y);
            }

            yield return mergedImage;
        }
    }

    private byte[] ConvertToPdf(IEnumerable<SKBitmap> images)
    {
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            foreach (var image in images)
            {
                using var canvas = document.BeginPage(image.Width, image.Height);
                canvas.DrawBitmap(image, 0, 0);
                document.EndPage();
            }
        }

        return stream.ToArray();
    }
}
