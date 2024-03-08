using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace dig.server
{
    public class DatFileReaderService
    {
        private readonly ILogger _logger;
        private readonly FileCacheService _fileCache;

        public DatFileReaderService(AppStorage appStorage, ILogger<DatFileReaderService> logger)
        {
            _logger = logger;
            _fileCache = new FileCacheService(Path.Combine(appStorage.UserSettingsFolder, "store-cache"), _logger);
        }

        public Task ReadDatFileToCacheAsync(string filePath)
        {
            // Use Task.Run to execute ReadDatFileToCache in a separate thread
            return Task.Run(() => ReadDatFileToCache(filePath));
        }

        private void ReadDatFileToCache(string filePath)
        {
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        int entireSize = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        bool isTerminal = reader.ReadBoolean();
                        int value1Size = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        byte[] value1Bytes = reader.ReadBytes(value1Size);
                        string value1 = Encoding.UTF8.GetString(value1Bytes);
                        int value2Size = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        byte[] value2Bytes = reader.ReadBytes(value2Size);
                        string value2 = Encoding.UTF8.GetString(value2Bytes);

                        // Processing of value1 and value2 goes here
                        _logger.LogInformation($"Is Terminal: {isTerminal}, Value1: {value1}, Value2: {value2}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading .dat file: {ex.Message}");
            }
        }
    }
}
