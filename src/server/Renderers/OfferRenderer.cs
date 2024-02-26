using QRCoder;

public class OfferRenderer : IRenderer
{
    public string Render(string fileContents) 
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(fileContents, QRCodeGenerator.ECCLevel.L);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImage = qrCode.GetGraphic(20);

        string base64Image = Convert.ToBase64String(qrCodeImage);
        string qrCodeImg = $"<a href=\"data:text/plain;charset=utf-8,{Uri.EscapeDataString(fileContents)}\" download=\"offerFile.txt\"><img src=\"data:image/png;base64,{base64Image}\" class=\"qr-image\" alt=\"QR Code\" title=\"Tap to download offer\" /></a>";
        string templateContent = File.ReadAllText(@"Renderers/Templates/offer.html");
        return templateContent.Replace("{{QR_CODE_PLACEHOLDER}}", qrCodeImg);
    }
}
