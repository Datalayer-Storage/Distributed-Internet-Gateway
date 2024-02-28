using QRCoder;

public class OfferRenderer : IRenderer
{
    public string Render(string storeId, object contents, HttpRequest request)
    {
        var fileContents = contents.ToString() ?? string.Empty;

        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(fileContents, QRCodeGenerator.ECCLevel.L);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        var base64Image = Convert.ToBase64String(qrCodeImage);
        var qrCodeImg = $"<a href=\"data:text/plain;charset=utf-8,{Uri.EscapeDataString(fileContents)}\" download=\"offerFile.txt\"><img src=\"data:image/png;base64,{base64Image}\" class=\"qr-image\" alt=\"QR Code\" title=\"Tap to download offer\" /></a>";
        var templateContent = File.ReadAllText(@"Renderers/Templates/offer.html");

        return templateContent.Replace("{{QR_CODE_PLACEHOLDER}}", qrCodeImg);
    }
}
