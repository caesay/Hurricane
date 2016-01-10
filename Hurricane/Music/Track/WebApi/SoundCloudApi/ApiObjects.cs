using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hurricane.Music.Track.WebApi.SoundCloudApi
{
    public class SUser
    {
        public int id { get; set; }
        public string permalink { get; set; }
        public string username { get; set; }
        public string last_modified { get; set; }
        public string uri { get; set; }
        public string permalink_url { get; set; }
        public string avatar_url { get; set; }
    }

    public class STrack
    {
        public int id { get; set; }
        public int duration { get; set; }
        public string last_modified { get; set; }
        public string tag_list { get; set; }
        public bool? streamable { get; set; }
        public bool? downloadable { get; set; }
        public string genre { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string track_type { get; set; }
        public string uri { get; set; }
        public SUser user { get; set; }
        public string permalink_url { get; set; }
        public string artwork_url { get; set; }
        public string waveform_url { get; set; }
        public string stream_url { get; set; }
        public int playback_count { get; set; }
        public int download_count { get; set; }
        public int favoritings_count { get; set; }
        public string LocalPath { get; set; }
    }
    public class SPlaylist
    {
        public int duration { get; set; }
        public object release_day { get; set; }
        public string permalink_url { get; set; }
        public string genre { get; set; }
        public string description { get; set; }
        public string uri { get; set; }
        public string tag_list { get; set; }
        public List<STrack> tracks { get; set; }
        public string playlist_type { get; set; }
        public int id { get; set; }
        public object downloadable { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string artwork_url { get; set; }
        public bool streamable { get; set; }
        public SUser user { get; set; }
    }

    internal class PartitionedCollection
    {
        public List<STrack> collection { get; set; }
        public string next_href { get; set; }
    }
}
