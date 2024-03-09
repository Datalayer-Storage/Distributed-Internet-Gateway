using System.Text;

namespace dig.server;

public static class IndexRenderer
{
    public static string Render(string storeId, object contents)
    {
        if (contents is not IEnumerable<string> decodedKeys)
        {
            return $"<html><body><h1>Index of {storeId}</h1><p>This store is empty.</p></body></html>";
        }

        var sb = new StringBuilder($"<html><body><h1>Index of {storeId}</h1>");

        if (decodedKeys.Any())
        {
            sb.Append("<ul>");
            foreach (var key in decodedKeys)
            {
                // Use cdnOverride if it exists, otherwise use the original request scheme and host
                var link = $"/{storeId}/{key}";

                sb.Append($"<li><a href='{link}'>{key}</a></li>");
            }
            sb.Append("</ul>");
        }
        else
        {
            sb.Append("<p>This store is empty.</p>");
        }

        sb.Append("</body></html>");

        return sb.ToString();
    }
}
