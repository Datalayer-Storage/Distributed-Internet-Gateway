using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace dig.server;


public static class IndexRenderer
{
    public static string Render(string storeId, object contents, HttpRequest request) 
    {
        List<string> decodedKeys = contents as List<string>;
        
        if (decodedKeys is null)
        {
            return $"<html><body><h1>Index of {storeId}</h1><p>This store is empty.</p></body></html>";
        }

        var htmlContent = $"<html><body><h1>Index of {storeId}</h1>";

        if (decodedKeys?.Count > 0)
        {
            htmlContent += "<ul>";
            foreach (var key in decodedKeys)
            {
                var link = $"{request.Scheme}://{request.Host}/{storeId}/{key}";
                htmlContent += $"<li><a href='{link}'>{key}</a></li>";
            }
            htmlContent += "</ul>";
        }
        else
        {
            htmlContent += "<p>This store is empty.</p>";
        }

        htmlContent += "</body></html>";

        return htmlContent;
    }
}