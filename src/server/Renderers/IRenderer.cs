public interface IRenderer
{
    string Render(string storeId, object contents, HttpRequest request);
}
