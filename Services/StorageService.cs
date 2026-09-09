using System;
using System.IO;
using System.Text.Json;
using RyzerDNS.Models;

namespace RyzerDNS.Services
{
    public class StorageService
    {
        private readonly string _filePath;

        public StorageService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "RyzerDNS");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _filePath = Path.Combine(folder, "config.json");
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Ignore and return defaults on read error
            }

            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Log or ignore silently
            }
        }

        public void ClearSession()
        {
            try
            {
                var settings = LoadSettings();
                settings.Token = null;
                SaveSettings(settings);
            }
            catch
            {
            }
        }
    }
}
