using System;
using System.Windows.Media;
using Hurricane.Music.Download;
using Hurricane.Utilities;

namespace Hurricane.Music.Track.WebApi.SoundCloudApi
{
    class SoundCloudWebTrackResult : WebTrackResultBase
    {
        public override ProviderName ProviderName
        {
            get { return ProviderName.SoundCloud; }
        }

        public override PlayableBase ToPlayable()
        {
            var result = (STrack) Result;
            var newtrack = new SoundCloudTrack
            {
                Url = Url,
                TimeAdded = DateTime.Now,
                IsChecked = false
            };

            newtrack.LoadInformation(result);
            return newtrack;
        }

        public override GeometryGroup ProviderVector
        {
            get { return SoundCloudTrack.GetProviderVector(); }
        }

        public override bool CanDownload
        {
            get { return ((STrack)Result).downloadable == true && !string.IsNullOrEmpty(((STrack)Result).stream_url); }
        }

        public override string DownloadParameter
        {
            get { return ((STrack)Result).id.ToString(); }
        }

        public override string DownloadFilename
        {
            get { return Title.ToEscapedFilename(); }
        }

        public override DownloadMethod DownloadMethod
        {
            get { return DownloadMethod.SoundCloud; }
        }
    }
}
