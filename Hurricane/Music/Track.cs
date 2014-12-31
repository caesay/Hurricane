﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using CSCore;
using CSCore.Codecs;
using Hurricane.Music.MusicDatabase;
using Hurricane.Settings;
using Hurricane.Utilities;
using Hurricane.ViewModelBase;
using File = TagLib.File;

namespace Hurricane.Music
{
    [Serializable]
    public class Track : PropertyChangedBase
    {
        #region Properties
        public string Duration { get; set; }
        public int kHz { get; set; }
        public int kbps { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Artist { get; set; }
        public string Extension { get; set; }
        public string Genres { get; set; }
        public uint Year { get; set; }
        public DateTime TimeAdded { get; set; }
        public DateTime LastTimePlayed { get; set; }
        
        private String _album;
        public String Album
        {
            get { return _album; }
            set
            {
                SetProperty(value, ref _album);
            }
        }

        private bool _isfavorite;
        public bool IsFavorite
        {
            get { return _isfavorite; }
            set
            {
                SetProperty(value, ref _isfavorite);
            }
        }

        private String _queueid;
        [XmlIgnore]
        public String QueueID //I know that the id should be an int, but it wouldn't make sense because what would be the id for non queued track? We would need a converter -> less performance -> string is wurf
        {
            get { return _queueid; }
            set
            {
                SetProperty(value, ref _queueid);
            }
        }
        private bool _isplaying;
        [XmlIgnore]
        public bool IsPlaying
        {
            get { return _isplaying; }
            set
            {
                SetProperty(value, ref _isplaying);
            }
        }
        
        private BitmapImage _image;
        [XmlIgnore]
        public BitmapImage Image
        {
            get { return _image; }
            set
            {
                SetProperty(value, ref _image);
            }
        }

        private bool _isloadingimage;
        [XmlIgnore]
        public bool IsLoadingImage
        {
            get { return _isloadingimage; }
            set
            {
                SetProperty(value, ref _isloadingimage);
            }
        }

        public TimeSpan DurationTimespan
        {
            get
            {
                return TimeSpan.ParseExact(Duration, Duration.Split(':').Length == 2 ? @"mm\:ss" : @"hh\:mm\:ss", null);
            }
        }

        public bool TrackExists
        {
            get
            {
                return TrackInformations.Exists;
            }
        }

        private FileInfo _trackinformations;
        public FileInfo TrackInformations
        {
            get { return _trackinformations ?? (_trackinformations = new FileInfo(Path)); }
        }

        #endregion

        #region Import

        public bool NotChecked { get; set; }
        public bool ShouldSerializeNotChecked { get { return NotChecked; } }

        public async Task<bool> CheckTrack()
        {
            TimeSpan duration = TimeSpan.Zero;
            try
            {
                await Task.Run(() =>
                {
                    using (IWaveSource soundSource = CodecFactory.Instance.GetCodec(Path))
                    {
                        duration = soundSource.GetLength();
                    }
                });
                Duration = duration.ToString(duration.Hours == 0 ? @"mm\:ss" : @"hh\:mm\:ss");
            }
            catch (Exception)
            {
                return false;
            }

            NotChecked = false;
            return true;
        }

        public async Task<bool> LoadInformations()
        {
            _trackinformations = null; //to refresh the fileinfo
            FileInfo file = TrackInformations;

            try
            {
                File info = null;
                try
                {
                    await Task.Run(() => info = File.Create(file.FullName));
                }
                catch
                {
                    return false;
                }

                using (info)
                {
                    Artist = RemoveInvalidXmlChars(!string.IsNullOrWhiteSpace(info.Tag.FirstPerformer) ? info.Tag.FirstPerformer : info.Tag.FirstAlbumArtist);
                    Title = !string.IsNullOrWhiteSpace(info.Tag.Title) ? RemoveInvalidXmlChars(info.Tag.Title) : System.IO.Path.GetFileNameWithoutExtension(file.FullName);
                    Genres = info.Tag.JoinedGenres;
                    kbps = info.Properties.AudioBitrate;
                    kHz = info.Properties.AudioSampleRate / 1000;
                    Extension = file.Extension.ToUpper().Replace(".", string.Empty);
                    Year = info.Tag.Year;
                    Duration = info.Properties.Duration.ToString(info.Properties.Duration.Hours == 0 ? @"mm\:ss" : @"hh\:mm\:ss");
                }
            }
            catch (NullReferenceException)
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(file.FullName);
            }
            return true;
        }
        #endregion

        #region Methods
        public void RefreshTrackExists()
        {
            _trackinformations = null;
            OnPropertyChanged("TrackExists");
        }

        /// <summary>
        /// Make ready for playing
        /// </summary>
        public async void Load()
        {
            IsLoadingImage = true;
            try
            {
                using (File file = File.Create(Path))
                {
                    if (file.Tag.Pictures != null && file.Tag.Pictures.Any())
                        Image = GeneralHelper.ByteArrayToBitmapImage(file.Tag.Pictures.First().Data.ToArray());
                }
            }
            catch (Exception)
            {
                Image = null;
            }

            if (Image == null)
            {
                DirectoryInfo diAlbumCover = new DirectoryInfo("AlbumCover");
                Image = MusicCoverManager.GetImage(this, diAlbumCover);
                if (Image == null && HurricaneSettings.Instance.Config.LoadAlbumCoverFromInternet)
                {
                    try
                    {
                        Image = await MusicCoverManager.LoadCoverFromWeb(this, diAlbumCover).ConfigureAwait(false);
                    }
                    catch (WebException)
                    {
                        //Happens, doesn't matter
                    }
                }
            }
            IsLoadingImage = false;
        }

        /// <summary>
        /// Delete all which was needed for playing
        /// </summary>
        public void Unload()
        {
            if (Image != null)
            {
                if (Image.StreamSource != null) Image.StreamSource.Dispose();
                Image = null;
            }
        }

        public string GenerateHash()
        {
            using (var md5Hasher = MD5.Create())
            {
                byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(Path));
                return BitConverter.ToString(data);
            }
        }

        protected string RemoveInvalidXmlChars(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            try
            {
                XmlConvert.VerifyXmlChars(content);
                return content;
            }
            catch { return new string(content.Where(XmlConvert.IsXmlChar).ToArray()); }
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Artist) ? string.Format("{0} - {1}", Artist, Title) : Title;
        }
        #endregion

        #region Static Methods
        public static bool IsSupported(FileInfo fi)
        {
            return CodecFactory.Instance.GetSupportedFileExtensions().Contains(fi.Extension.ToLower().Replace(".", string.Empty));
        }

        #endregion

        #region Animations
        private bool _isremoving;
        [XmlIgnore]
        public bool IsRemoving
        {
            get { return _isremoving; }
            set
            {
                SetProperty(value, ref _isremoving);
            }
        }

        private bool _isadded;
        [XmlIgnore]
        public bool IsAdded
        {
            get { return _isadded; }
            set
            {
                SetProperty(value, ref _isadded);
            }
        }

        private bool _isselected;
        [XmlIgnore]
        public bool IsSelected
        {
            get { return _isselected; }
            set
            {
                SetProperty(value, ref _isselected);
            }
        }

        #endregion
    }
}
