using Microsoft.Azure.WebJobs;
using PortraitBuilder.Model.Portrait;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PortraitBuilder.Online
{
    internal class BlobLoader
    {
        private readonly ExecutionContext context;
        internal BlobLoader(ExecutionContext context)
        {
            this.context = context;

            _portraitPack = new Lazy<Task<PortraitData>>(CreatePortraitPack);
            _cultureLookup = new Lazy<Task<Dictionary<string, IEnumerable<string>>>>(CreateCultureLookup);
            _religiousClothingLookup = new Lazy<Task<Dictionary<string, ValueTuple<int, int>>>>(CreateReligiousClothingLookup);
        }

        private string BasePath => context.FunctionAppDirectory;

        internal readonly Lazy<Task<PortraitData>> _portraitPack;
        private Task<PortraitData> CreatePortraitPack()
        {
            var fs = File.OpenRead(Path.Combine(BasePath, "Packs", "portraits.json"));
            var options = ContentPacks.JsonHelper.GetDefaultOptions();
            return JsonSerializer.DeserializeAsync<PortraitData>(fs, options).AsTask();
        }

        internal readonly Lazy<Task<Dictionary<string, IEnumerable<string>>>> _cultureLookup;
        private Task<Dictionary<string, IEnumerable<string>>> CreateCultureLookup()
        {
            var fs = File.OpenRead(Path.Combine(BasePath, "Packs", "culture_lookup.json"));
            var options = ContentPacks.JsonHelper.GetDefaultOptions();
            return JsonSerializer.DeserializeAsync<Dictionary<string, IEnumerable<string>>>(fs, options).AsTask();
        }

        internal readonly Lazy<Task<Dictionary<string, ValueTuple<int, int>>>> _religiousClothingLookup;
        private async Task<Dictionary<string, ValueTuple<int, int>>> CreateReligiousClothingLookup()
        {
            var fs = File.OpenRead(Path.Combine(BasePath, "Packs", "religious_clothing_lookup.json"));
            using var jd = await JsonDocument.ParseAsync(fs);
            return jd.RootElement
                .EnumerateObject()
                .ToDictionary(
                    jp => jp.Name,
                    jp => (
                        jp.Value.GetProperty("religious_clothing_head").GetInt32(),
                        jp.Value.GetProperty("religious_clothing_priest").GetInt32()
                    )
                );
        }

        internal SKBitmap GetBitmap(string path) => _blobTileCache.GetOrAdd(path, DecodeBitmap);
        private SKBitmap DecodeBitmap(string path)
        {
            var bmp = SKBitmap.Decode(Path.Combine(BasePath, path));
            bmp.SetImmutable();
            return bmp;
        }
        private static readonly ConcurrentDictionary<string, SKBitmap> _blobTileCache = new ConcurrentDictionary<string, SKBitmap>();
    }
}
