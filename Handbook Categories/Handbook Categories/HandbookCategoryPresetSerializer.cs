using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Handbook_Categories
{
    internal static class HandbookCategoryPresetSerializer
    {
        internal static bool TryEncode(HandbookCategoriesConfig config, out string code, out string error)
        {
            code = null;
            error = null;

            if (config == null)
            {
                error = "No configuration available to save.";
                return false;
            }

            try
            {
                Normalize(config);

                string json = JsonConvert.SerializeObject(config, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                using MemoryStream compressedStream = new();
                using (DeflateStream deflate = new(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(jsonBytes, 0, jsonBytes.Length);
                }

                string base64 = Convert.ToBase64String(compressedStream.ToArray());
                string shortCode = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');

                code = shortCode;
                return true;
            }
            catch (Exception e)
            {
                error = $"Failed to encode preset: {e.Message}";
                return false;
            }
        }

        internal static bool TryDecode(string code, out HandbookCategoriesConfig config, out string error)
        {
            config = null;
            error = null;

            if (string.IsNullOrWhiteSpace(code))
            {
                error = "Preset code cannot be empty.";
                return false;
            }

            try
            {
                string normalized = code.Trim().Replace('-', '+').Replace('_', '/');

                int padding = normalized.Length % 4;
                if (padding == 1)
                {
                    error = "Invalid preset code.";
                    return false;
                }

                if (padding > 0)
                {
                    normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
                }

                byte[] compressedBytes = Convert.FromBase64String(normalized);

                using MemoryStream compressedStream = new(compressedBytes);
                using DeflateStream deflate = new(compressedStream, CompressionMode.Decompress);
                using MemoryStream jsonStream = new();
                deflate.CopyTo(jsonStream);

                string json = Encoding.UTF8.GetString(jsonStream.ToArray());
                HandbookCategoriesConfig result = JsonConvert.DeserializeObject<HandbookCategoriesConfig>(json);

                if (result == null)
                {
                    error = "Preset code did not contain a valid configuration.";
                    return false;
                }

                Normalize(result);
                config = result;
                return true;
            }
            catch (Exception e)
            {
                error = $"Failed to decode preset: {e.Message}";
                return false;
            }
        }

        private static void Normalize(HandbookCategoriesConfig config)
        {
            config.Categories ??= new List<HandbookCategoryConfigEntry>();

            foreach (HandbookCategoryConfigEntry entry in config.Categories)
            {
                if (entry == null)
                {
                    continue;
                }

                entry.MatchWords ??= new List<string>();
                entry.ForbiddenWords ??= new List<string>();
                entry.TabBackgroundColor ??= HandbookCategoryColors.DefaultColorName;
            }
        }
    }
}
