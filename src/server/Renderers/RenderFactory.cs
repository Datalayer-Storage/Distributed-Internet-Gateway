public class RenderFactory
{
    public static string? Render(string fileContents, string fileExtension)
    {
        var renderer = GetRendererForFileExtension(fileExtension);
        return renderer?.Render(fileContents);
    }

    private static IRenderer? GetRendererForFileExtension(string fileExtension)
    {
        // Example for selecting different renderers based on MIME type
        switch (fileExtension)
        {
            case ".offer":
                return new OfferRenderer();
            // Add cases for other MIME types and their corresponding renderers
            default:
                return null; // or throw an exception, based on how you want to handle unsupported MIME types
        }
    }
}
