using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Media.Imaging;
using Hurricane.Settings;
using Hurricane.Utilities;
using Newtonsoft.Json;

namespace Hurricane.Music.Track.WebApi.SoundCloudApi
{
    class SoundCloudApi : IMusicApi
    {
        public string ServiceName => "SoundCloud";
        public FrameworkElement ApiSettings => null;
        public bool IsEnabled => true;

        private const string _resolveUrl = "http://api.soundcloud.com/resolve?url={0}&client_id={1}";

        public static async Task<BitmapImage> LoadBitmapImage(SoundCloudTrack track, ImageQuality quality, DirectoryInfo albumDirectory)
        {
            var config = HurricaneSettings.Instance.Config;

            using (var client = new WebClient { Proxy = null })
            {
                var image = await ImageHelper.DownloadImage(client, string.Format(track.ArtworkUrl, GetQualityModifier(quality)));
                if (config.SaveCoverLocal)
                {
                    if (!albumDirectory.Exists) albumDirectory.Create();
                    await ImageHelper.SaveImage(image, string.Format("{0}_{1}", track.SoundCloudID, GetQualityModifier(quality)), albumDirectory.FullName);
                }
                return image;
            }
        }
        public static string GetQualityModifier(ImageQuality quality)
        {
            switch (quality)
            {
                case ImageQuality.Small:
                    return "large";  //100x100
                case ImageQuality.Medium:
                    return "t300x300"; //300x300
                case ImageQuality.Large:
                    return "crop"; //400x400
                case ImageQuality.Maximum:
                    return "t500x500"; //500x500
                default:
                    throw new ArgumentOutOfRangeException("quality");
            }
        }
        public async Task<Tuple<bool, List<WebTrackResultBase>, IPlaylistResult>> CheckForSpecialUrl(string url)
        {
            try
            {
                return new Tuple<bool, List<WebTrackResultBase>, IPlaylistResult>(true, await GetTrackList(url), null);
            }
            catch
            {
                return new Tuple<bool, List<WebTrackResultBase>, IPlaylistResult>(false, null, null);
            }
        }
        public async Task<List<WebTrackResultBase>> Search(string searchText)
        {
            using (var web = new WebClient { Proxy = null })
            {
                var results = JsonConvert.DeserializeObject<List<STrack>>(await web.DownloadStringTaskAsync(string.Format("https://api.soundcloud.com/tracks?q={0}&client_id={1}", searchText.ToEscapedUrl(), SensitiveInformation.SoundCloudKey)));
                return results.Where(x => x.streamable == true).Select(x => new SoundCloudWebTrackResult
                {
                    Duration = TimeSpan.FromMilliseconds(x.duration),
                    //Year = x.release_year != null ? uint.Parse(x.release_year.ToString()) : (uint)DateTime.Parse(x.created_at).Year,
                    Title = x.title,
                    Uploader = x.user.username,
                    Result = x,
                    Views = (uint)x.playback_count,
                    ImageUrl = x.artwork_url,
                    Url = x.permalink_url,
                    Genres = new List<Genre> { PlayableBase.StringToGenre(x.genre) },
                    Description = x.description
                }).Cast<WebTrackResultBase>().ToList();
            }
        }

        private async Task DownloadTrack(STrack track, string fileName)
        {
            ExceptionDispatchInfo exception = null;
            try
            {
                //await _lock.WaitAsync().ConfigureAwait(false);
                var uriBuilder = new UriBuilder(new Uri(track.stream_url));
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["client_id"] = SensitiveInformation.SoundCloudKey;
                uriBuilder.Query = query.ToString();

                string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");

                using (var wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync(uriBuilder.Uri, tmpPath);
                    string tmpArtPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
                    try
                    {
                        await wc.DownloadFileTaskAsync(track.artwork_url, tmpArtPath).ConfigureAwait(false);
                    }
                    catch
                    {
                        tmpArtPath = null;
                    }
                    var tags = TagLib.File.Create(tmpPath);
                    if (tmpArtPath != null)
                    {
                        var art = new TagLib.Picture(tmpArtPath);
                        tags.Tag.Pictures = new TagLib.IPicture[1] { art };
                        File.Delete(tmpArtPath);
                    }
                    tags.Tag.Performers = new[] { track.user.username };
                    tags.Tag.MusicIpId = track.id.ToString();
                    tags.Tag.Comment = track.description;
                    tags.Tag.Title = track.title;
                    tags.Tag.Genres = new[] { track.genre };
                    tags.Save();
                }
                File.Move(tmpPath, fileName);
                track.LocalPath = fileName;
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                //_lock.Release();
            }
            exception?.Throw();
        }
        private async Task<List<WebTrackResultBase>> GetTrackList(string url)
        {
            //var trcl = new TrackCollection();
            //var trcl2 = new List<WebTrackResultBase>();
            var uri = await Resolve(url);
            //trcl.Url = uri.ToString();
            //trcl.FriendlyUrl = url;

            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["client_id"] = SensitiveInformation.SoundCloudKey;
            query["limit"] = "200";
            query["linked_partitioning"] = "1";
            uriBuilder.Query = query.ToString();
            uri = uriBuilder.Uri;

            List<STrack> list = new List<STrack>();
            if (uri.ToString().IndexOf("/playlists/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SPlaylist));
                using (var wc = new WebClient())
                {
                    var data = await wc.DownloadDataTaskAsync(uri).ConfigureAwait(false);
                    using (var ms = new MemoryStream(data))
                    {
                        var playlist = (SPlaylist)serializer.ReadObject(ms);
                        //trcl.ArtworkUrl = playlist.artwork_url;
                        //trcl.Author = playlist.user.username;
                        //trcl.Title = playlist.title;
                        list.AddRange(playlist.tracks);
                    }
                }
            }
            else
            {
                //DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PartitionedCollection));
                var wc = new WebClient();
                try
                {
                    var matches = Regex.Matches(uri.ToString(), @"api\.soundcloud\.com\/users\/(?<id>\d+)\/(?<mode>\w+)")
                        .Cast<Match>().ToArray();
                    if (matches.Any())
                    {
                        var userId = matches.First().Groups["id"].Value;
                        var mode = matches.First().Groups["mode"].Value;
                        var userUrl = String.Format("https://api.soundcloud.com/users/{0}?client_id={1}", userId, SensitiveInformation.SoundCloudKey);
                        //var userData = await wc.DownloadDataTaskAsync(userUrl).ConfigureAwait(false);
                        var userData = await wc.DownloadStringTaskAsync(userUrl).ConfigureAwait(false);
                        var user = JsonConvert.DeserializeObject<SUser>(userData);

                        //var userSerializer = new DataContractJsonSerializer(typeof(SUser));
                        //using (var ms = new MemoryStream(userData))
                        //{

                        //    var user = (SUser)userSerializer.ReadObject(ms);
                        //    trcl.ArtworkUrl = user.avatar_url;
                        //    trcl.Author = user.username;
                        //    trcl.Title = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(mode);
                        //}
                    }
                    while (true)
                    {
                        var data = await wc.DownloadStringTaskAsync(uri).ConfigureAwait(false);
                        var collection = JsonConvert.DeserializeObject<PartitionedCollection>(data);
                        list.AddRange(collection.collection);
                        if (String.IsNullOrWhiteSpace(collection.next_href))
                            break;
                        uri = new Uri(collection.next_href);
                        //var data = await wc.DownloadDataTaskAsync(uri).ConfigureAwait(false);
                        //using (var ms = new MemoryStream(data))
                        //{
                        //    var collection = (PartitionedCollection)serializer.ReadObject(ms);
                        //    list.AddRange(collection.collection);
                        //    if (String.IsNullOrWhiteSpace(collection.next_href))
                        //        break;
                        //    uri = new Uri(collection.next_href);
                        //}
                    }
                }
                finally
                {
                    wc.Dispose();
                }
            }
            //trcl.Tracks = list.ToArray();
            //if (String.IsNullOrWhiteSpace(trcl.ArtworkUrl) && list.Any())
            //    trcl.ArtworkUrl = list[0].artwork_url;
            //return trcl;

            return list.Select(result =>
            {
                return new SoundCloudWebTrackResult
                {
                    Duration = TimeSpan.FromMilliseconds(result.duration),
                    //Year = result.release_year != null ? uint.Parse(result.release_year.ToString()) : (uint)DateTime.Parse(result.created_at).Year,
                    Title = result.title,
                    Uploader = result.user.username,
                    Result = result,
                    Views = (uint)result.playback_count,
                    ImageUrl = result.artwork_url,
                    Url = result.permalink_url,
                    Genres = new List<Genre> { PlayableBase.StringToGenre(result.genre) },
                    Description = result.description
                } as WebTrackResultBase;
            }).ToList();
        }
        private async Task<Uri> Resolve(string url)
        {
            var encoded = HttpUtility.UrlEncode(url);
            var request = WebRequest.CreateHttp(String.Format(_resolveUrl, encoded, SensitiveInformation.SoundCloudKey));
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    return response.ResponseUri;
                }
            }
            catch (WebException ex)
            {
                var original = ExceptionDispatchInfo.Capture(ex);
                using (var response = ex.Response as HttpWebResponse)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            throw new ApplicationException("Provided SoundCloud API key is invalid or has been revoked.", ex);
                    }
                }
                original.Throw();
                throw new InvalidOperationException("This should never happen...");
            }
        }
    }
}
