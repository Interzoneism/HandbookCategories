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
            if (config == null)
            {
                return Fail(out code, out error, "No configuration available to save.");
            }

            try
            {
                Normalize(config);

                using MemoryStream compressedStream = new();
                using (DeflateStream deflate = new(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                        config,
                        Formatting.None,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                    deflate.Write(jsonBytes, 0, jsonBytes.Length);
                }

                code = Convert.ToBase64String(compressedStream.ToArray())
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
                error = null;
                return true;
            }
            catch (Exception e)
            {
                return Fail(out code, out error, $"Failed to encode preset: {e.Message}");
            }
        }

        internal static bool TryDecode(string code, out HandbookCategoriesConfig config, out string error)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Fail(out config, out error, "Preset code cannot be empty.");
            }

            try
            {
                string normalized = code.Trim().Replace('-', '+').Replace('_', '/');
                int padding = normalized.Length % 4;

                if (padding == 1)
                {
                    return Fail(out config, out error, "Invalid preset code.");
                }

                if (padding > 0)
                {
                    normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
                }

                using MemoryStream compressedStream = new(Convert.FromBase64String(normalized));
                using DeflateStream deflate = new(compressedStream, CompressionMode.Decompress);
                using MemoryStream jsonStream = new();

                deflate.CopyTo(jsonStream);
                HandbookCategoriesConfig result = JsonConvert.DeserializeObject<HandbookCategoriesConfig>(
                    Encoding.UTF8.GetString(jsonStream.ToArray()));

                if (result == null)
                {
                    return Fail(out config, out error, "Preset code did not contain a valid configuration.");
                }

                Normalize(result);
                config = result;
                error = null;
                return true;
            }
            catch (Exception e)
            {
                return Fail(out config, out error, $"Failed to decode preset: {e.Message}");
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

        private static bool Fail<T>(out T result, out string error, string message)
        {
            result = default;
            error = message;
            return false;
        }
    }
}
