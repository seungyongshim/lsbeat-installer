using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ElastiBuild.Commands;
using ElastiBuild.Extensions;
using Elastic.Installer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElastiBuild.Infra
{
    public class ArtifactsApi
    {
        public static Uri BaseAddress { get; } = new Uri(MagicStrings.ArtifactsApi.BaseAddress);

        public static async Task<IEnumerable<ArtifactContainer>> ListNamedContainers()
        {
            using var http = new HttpClient()
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromMilliseconds(3000)
            };

            var namedItems = new List<ArtifactContainer>();

            using (var stm = await http.GetStreamAsync(MagicStrings.ArtifactsApi.Branches))
            using (var sr = new StreamReader(stm))
            using (var jtr = new JsonTextReader(sr))
            {
                var js = new JsonSerializer();
                var data = js.Deserialize<JToken>(jtr);

                foreach (var itm in data[MagicStrings.ArtifactsApi.Branches] ?? new JArray())
                    namedItems.Add(new ArtifactContainer((string) itm, isBranch: true));
            }

            using (var stm = await http.GetStreamAsync(MagicStrings.ArtifactsApi.Versions))
            using (var sr = new StreamReader(stm))
            using (var jtr = new JsonTextReader(sr))
            {
                var js = new JsonSerializer();
                var data = js.Deserialize<JToken>(jtr);

                foreach (var itm in data[MagicStrings.ArtifactsApi.Versions] ?? new JArray())
                    namedItems.Add(new ArtifactContainer((string) itm, isVersion: true));

                foreach (var itm in data[MagicStrings.ArtifactsApi.Aliases] ?? new JArray())
                    namedItems.Add(new ArtifactContainer((string) itm, isAlias: true));
            }

            return namedItems;
        }

        public static async Task<IEnumerable<ArtifactPackage>> FindArtifact(
            string target, Action<ArtifactFilter> filterConfiguration)
        {
            // TODO: validate filterConfiguraion
            await Task.Delay(0);

            var filter = new ArtifactFilter(target);
            filterConfiguration?.Invoke(filter);

            using var http = new HttpClient()
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromMilliseconds(3000)
            };

            var packages = new List<ArtifactPackage>();

            var Version = Environment.GetEnvironmentVariable("LSBEAT_VERSION");

            packages.Add(new ArtifactPackage {
                TargetName = "lsbeat",
                Url = $"https://github.com/seungyongshim/lsbeat/releases/download/{Version}/lsbeat.exe",
                FileName = "lsbeat",
            });

            return packages;
        }

        public static async Task<(bool wasAlreadyPresent, string localPath)> FetchArtifact(
            BuildContext ctx, ArtifactPackage ap, bool forceSwitch)
        {
            var destDir = Path.Combine(ctx.InDir, Path.GetFileNameWithoutExtension(ap.FileName));

            var localPath = Path.Combine(destDir, ap.FileName);

            if (!forceSwitch && File.Exists(localPath))
                return (true, localPath);

            if (!ap.IsDownloadable)
                throw new Exception($"{ap.FileName} is missing {nameof(ap.Url)}");


            

            localPath = Path.Combine(destDir, Path.GetFileName(ap.Url));

            Directory.CreateDirectory(destDir);

            using var http = new HttpClient();
            using var stm = await http.GetStreamAsync(ap.Url);
            using var fs = File.Open(localPath, FileMode.Create, FileAccess.Write);

            // Buffer size just shy of one that would get onto LOH
            // (hopefully ArrayPool will oblige...)

            var bytes = ArrayPool<byte>.Shared.Rent(81920);

            try
            {
                int bytesRead = 0;

                while (true)
                {
                    if ((bytesRead = await stm.ReadAsync(bytes, 0, bytes.Length)) <= 0)
                        break;

                    await fs.WriteAsync(bytes, 0, bytesRead);
                }
            }
            finally
            {
                if (bytes != null)
                    ArrayPool<byte>.Shared.Return(bytes);
            }

            return (false, localPath);
        }
    }
}
