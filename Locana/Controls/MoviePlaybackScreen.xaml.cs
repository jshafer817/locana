using Kazyx.RemoteApi.AvContent;
using Locana.DataModel;
using Locana.Utility;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Locana.Controls
{
    public sealed partial class MoviePlaybackScreen : UserControl
    {
        public MoviePlaybackScreen()
        {
            this.InitializeComponent();
            SeekBar.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);

            InfoTimer.Interval = new TimeSpan(0, 0, 0, 3); // 3 sec. to hide.
            InfoTimer.Tick += (obj, sender) =>
            {
                if (!AnimationRunning && DetailInfoDisplayed)
                {
                    StartToHideInfo();
                }
                InfoTimer.Stop();
            };

            LocalMoviePlayer.MediaFailed += LocalMoviePlayer_MediaFailed;
            LocalMoviePlayer.MediaOpened += LocalMoviePlayer_MediaOpened;
            LocalMoviePlayer.CurrentStateChanged += LocalMoviePlayer_CurrentStateChanged;

            LocalMoviePositionTimer.Interval = TimeSpan.FromMilliseconds(200);
            LocalMoviePositionTimer.Tick += (obj, sender) =>
            {
                if (DataContext != null && MovieType == MovieFileType.LocalMovie)
                {
                    (DataContext as MoviePlaybackData).CurrentPosition = LocalMoviePlayer.Position;
                }
            };
        }

        void LocalMoviePlayer_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var data = DataContext as MoviePlaybackData;

            switch (LocalMoviePlayer.CurrentState)
            {
                case MediaElementState.Paused:
                    data.StreamingStatus = StreamStatus.Paused;
                    break;
                case MediaElementState.Playing:
                    data.StreamingStatus = StreamStatus.Started;
                    break;
                case MediaElementState.Stopped:
                    data.StreamingStatus = StreamStatus.PausedByEdge;
                    break;
            }
        }

        private void LocalMoviePlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (LocalMediaOpened != null) { LocalMediaOpened(sender, e); }
            var data = DataContext as MoviePlaybackData;
            data.Duration = LocalMoviePlayer.NaturalDuration.TimeSpan;
        }

        private void LocalMoviePlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (LocalMediaFailed != null) { LocalMediaFailed(sender, e.ErrorMessage); }
        }

        public event EventHandler<SeekBarOperationArgs> SeekOperated;
        public event EventHandler<PlaybackRequestArgs> OnStreamingOperationRequested;
        public event EventHandler<RoutedEventArgs> LocalMediaOpened;
        public event EventHandler<string> LocalMediaFailed;

        DispatcherTimer InfoTimer = new DispatcherTimer();
        DispatcherTimer LocalMoviePositionTimer = new DispatcherTimer();

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            RenewInfoTimer();
            var targetPosition = TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * (sender as Slider).Value / 1000);
            switch (this.MovieType)
            {
                case MovieFileType.SimpleStreamingMovie:
                    if (SeekOperated != null && Duration.TotalMilliseconds > 0)
                    {
                        SeekOperated(this, new SeekBarOperationArgs() { SeekPosition = targetPosition });
                    }
                    break;
                case MovieFileType.LocalMovie:
                    LocalMoviePlayer.Position = targetPosition;
                    break;
            }
        }

        public TimeSpan CurrentPosition
        {
            get { return (TimeSpan)GetValue(CurrentPositionProperty); }
            set
            {
                SetValue(CurrentPositionProperty, value);
                UpdatePlaybackPosition(value, this.Duration);
            }
        }

        public static readonly DependencyProperty CurrentPositionProperty = DependencyProperty.Register(
            nameof(CurrentPosition),
            typeof(TimeSpan),
            typeof(MoviePlaybackScreen),
            new PropertyMetadata(new TimeSpan(0, 0, 0), new PropertyChangedCallback(MoviePlaybackScreen.OnCurrentPositionChanged)));

        private static void OnCurrentPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //DebugUtil.Log(() => "Current position updated: " + ((TimeSpan)e.NewValue).TotalSeconds);
            (d as MoviePlaybackScreen).UpdatePlaybackPosition((TimeSpan)e.NewValue, (d as MoviePlaybackScreen).Duration);
        }

        void UpdatePlaybackPosition(TimeSpan current, TimeSpan duration)
        {
            if (duration.TotalMilliseconds <= 0)
            {
                DetailInfoSurface.Visibility = Visibility.Collapsed;
                return;
            }
            double value = current.TotalMilliseconds / duration.TotalMilliseconds * 1000;
            if (value < 0 || value > 1000) { return; }
            this.SeekBar.Value = value;
            this.ProgressBar.Value = value;
            PositionText.Text = ToString(current);
            DetailInfoSurface.Visibility = Visibility.Visible;
        }

        public TimeSpan Duration
        {
            get { return (TimeSpan)GetValue(DurationProperty); }
            set
            {
                SetValue(DurationProperty, value);
                UpdateDurationDisplay(value);
            }
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
            nameof(Duration),
            typeof(TimeSpan),
            typeof(MoviePlaybackScreen),
            new PropertyMetadata(new TimeSpan(0, 0, 0), new PropertyChangedCallback(MoviePlaybackScreen.OnDurationChanged)));

        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //DebugUtil.Log(() => "Duration updated: " + ((TimeSpan)e.NewValue).TotalSeconds);
            (d as MoviePlaybackScreen).UpdateDurationDisplay((TimeSpan)e.NewValue);
        }

        void UpdateDurationDisplay(TimeSpan duration)
        {
            if (duration.TotalMilliseconds <= 0)
            {
                this.DurationText.Text = "--:--:--";
            }
            else
            {
                this.DurationText.Text = ToString(duration);
            }
        }

        private static string ToString(TimeSpan time)
        {
            StringBuilder sb = new StringBuilder();
            if (time.TotalMilliseconds < 0) { return "--:--:--"; }
            if (time.Hours > 0)
            {
                sb.Append(string.Format("{0:D2}", time.Hours));
                sb.Append(":");
            }
            sb.Append(string.Format("{0:D2}", time.Minutes));
            sb.Append(":");
            sb.Append(string.Format("{0:D2}", time.Seconds));
            return sb.ToString();
        }

        public static readonly DependencyProperty SeekAvailabilityProperty = DependencyProperty.Register(
            nameof(SeekAvailable),
            typeof(bool),
            typeof(MoviePlaybackScreen),
            new PropertyMetadata(false, new PropertyChangedCallback(MoviePlaybackScreen.OnSeekAvailabilityChanged)));

        private static void OnSeekAvailabilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //DebugUtil.Log(() => "Seek availability changed: " + (bool)(e.NewValue));
            (d as MoviePlaybackScreen).UpdateBarDisplay((bool)(e.NewValue));
        }

        public bool SeekAvailable
        {
            get { return (bool)GetValue(SeekAvailabilityProperty); }
            set
            {
                SetValue(SeekAvailabilityProperty, value);
                UpdateBarDisplay(value);
            }
        }

        void UpdateBarDisplay(bool SeekAvailable)
        {
            if (SeekAvailable)
            {
                this.ProgressBar.Visibility = Visibility.Collapsed;
                this.SeekBar.Visibility = Visibility.Visible;
            }
            else
            {
                this.ProgressBar.Visibility = Visibility.Visible;
                this.SeekBar.Visibility = Visibility.Collapsed;
            }
        }

        public static readonly DependencyProperty PlaybackStatusProperty = DependencyProperty.Register(
            nameof(PlaybackStatus),
            typeof(string),
            typeof(MoviePlaybackScreen),
            new PropertyMetadata("", new PropertyChangedCallback(MoviePlaybackScreen.OnPlaybackStatusUpdated)));

        private static void OnPlaybackStatusUpdated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MoviePlaybackScreen).UpdatePlaybackStatus((string)(e.NewValue));
        }

        public string PlaybackStatus
        {
            get { return (string)GetValue(PlaybackStatusProperty); }
            set { SetValue(PlaybackStatusProperty, value); }
        }

        void UpdatePlaybackStatus(string status)
        {
            if (!DetailInfoDisplayed)
            {
                StartToShowInfo();
            }
        }

        public static readonly DependencyProperty MovieTypeProperty = DependencyProperty.Register(
            nameof(MovieType),
            typeof(MovieFileType),
            typeof(MoviePlaybackScreen),
            new PropertyMetadata(MovieFileType.LocalMovie, new PropertyChangedCallback(MoviePlaybackScreen.OnMovieTypeUpdated)));

        private static void OnMovieTypeUpdated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MoviePlaybackScreen).SetupForMovieType((MovieFileType)(e.NewValue));
        }

        private void SetupForMovieType(MovieFileType movieFileType)
        {
            switch (movieFileType)
            {
                case MovieFileType.LocalMovie:
                    LocalMoviePlayer.Visibility = Visibility.Visible;
                    StreamingPlayer.Visibility = Visibility.Collapsed;
                    break;
                case MovieFileType.SimpleStreamingMovie:
                    LocalMoviePlayer.Visibility = Visibility.Collapsed;
                    StreamingPlayer.Visibility = Visibility.Visible;
                    LocalMoviePositionTimer.Stop();
                    break;
            }
        }

        public MovieFileType MovieType
        {
            get { return (MovieFileType)GetValue(MovieTypeProperty); }
            set { SetValue(MovieTypeProperty, value); }
        }

        public void Reset()
        {
            if (SeekAvailable)
            {
                this.SeekBar.Value = 0;
            }
            else
            {
                this.ProgressBar.Value = 0;
            }
            PositionText.Text = "--";
            DurationText.Text = "--:--";
            InfoTimer.Stop();
        }

        bool DetailInfoDisplayed = true;
        bool AnimationRunning = false;

        private void Screen_Tapped(object sender, TappedRoutedEventArgs e)
        {
            RenewInfoTimer();
            if (AnimationRunning) { return; }

            if (DetailInfoDisplayed)
            {
                StartToHideInfo();
            }
            else
            {
                StartToShowInfo();
            }

            switch ((DataContext as MoviePlaybackData).StreamingStatus)
            {
                case StreamStatus.Paused:
                    Resume();
                    break;
                case StreamStatus.Started:
                    Pause();
                    break;
                case StreamStatus.PausedByEdge:
                    // todo: if seek is supported, seek to the beginning and start.
                    break;
            }
        }

        async void StartToHideInfo()
        {
            AnimationRunning = true;
            var time = TimeSpan.FromMilliseconds(250);
            var fade = FadeType.FadeOut;
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = HeaderForeground, Duration = time, RequestFadeSide = FadeSide.Top, RequestFadeType = fade }).Begin();
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = FooterForeground, Duration = time, RequestFadeSide = FadeSide.Bottom, RequestFadeType = fade }).Begin();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = HeaderBackground, Duration = time, RequestFadeSide = FadeSide.Top, RequestFadeType = fade }).Begin();
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest()
            {
                Target = FooterBackground,
                Duration = time,
                Completed = (sender, obj) =>
                {
                    DetailInfoDisplayed = false;
                    AnimationRunning = false;
                },
                RequestFadeSide = FadeSide.Bottom,
                RequestFadeType = fade
            }).Begin();
        }

        async void StartToShowInfo()
        {
            AnimationRunning = true;
            var time = TimeSpan.FromMilliseconds(250);
            var fade = FadeType.FadeIn;
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = HeaderBackground, Duration = time, RequestFadeSide = FadeSide.Top, RequestFadeType = fade }).Begin();
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = FooterBackground, Duration = time, RequestFadeSide = FadeSide.Bottom, RequestFadeType = fade }).Begin();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest() { Target = HeaderForeground, Duration = time, RequestFadeSide = FadeSide.Top, RequestFadeType = fade }).Begin();
            AnimationHelper.CreateSlideAnimation(new SlideAnimationRequest()
            {
                Target = FooterForeground,
                Duration = time,
                Completed = (sender, obj) =>
                {
                    DetailInfoDisplayed = true;
                    AnimationRunning = false;
                    InfoTimer.Start();

                },
                RequestFadeSide = FadeSide.Bottom,
                RequestFadeType = fade
            }).Begin();
        }

        public void Resume()
        {
            RenewInfoTimer();

            if (PlaybackState != PlayerState.PausedOrStopped)
            {
                return;
            }

            switch (MovieType)
            {
                case MovieFileType.SimpleStreamingMovie:
                    OnStreamingOperationRequested?.Invoke(this, new PlaybackRequestArgs() { Request = PlaybackRequest.Start });
                    break;
                case MovieFileType.LocalMovie:
                    LocalMoviePlayer.Play();
                    break;
            }
        }

        public void Pause()
        {
            RenewInfoTimer();

            if (PlaybackState != PlayerState.Playing)
            {
                return;
            }

            switch (MovieType)
            {
                case MovieFileType.SimpleStreamingMovie:
                    OnStreamingOperationRequested?.Invoke(this, new PlaybackRequestArgs() { Request = PlaybackRequest.Pause });
                    break;
                case MovieFileType.LocalMovie:
                    LocalMoviePlayer.Pause();
                    break;
            }
        }

        public PlayerState PlaybackState
        {
            get
            {
                switch (MovieType)
                {
                    case MovieFileType.SimpleStreamingMovie:
                        if (OnStreamingOperationRequested != null)
                        {
                            switch (PlaybackStatus)
                            {
                                case StreamStatus.Started:
                                    return PlayerState.Playing;
                                case StreamStatus.Paused:
                                case StreamStatus.PausedByEdge:
                                    return PlayerState.PausedOrStopped;
                            }
                        }
                        break;
                    case MovieFileType.LocalMovie:
                        switch (LocalMoviePlayer.CurrentState)
                        {
                            case MediaElementState.Playing:
                                return PlayerState.Playing;
                            case MediaElementState.Paused:
                            case MediaElementState.Stopped:
                                return PlayerState.PausedOrStopped;
                        }
                        break;
                }

                return PlayerState.Undefined;
            }
        }

        public void NotifyStartingStreamingMoviePlayback()
        {
            if (!DetailInfoDisplayed) { StartToShowInfo(); }
            InfoTimer.Start();
        }

        void RenewInfoTimer()
        {
            InfoTimer.Stop();
            InfoTimer.Start();
        }

        public async void SetLocalContent(Thumbnail content)
        {
            try
            {
                var stream = await content.CacheFile.OpenAsync(FileAccessMode.Read);
                LocalMoviePlayer.SetSource(stream, content.CacheFile.ContentType);

                var data = new MoviePlaybackData();
                data.FileName = content.CacheFile.Name;
                data.CurrentPosition = TimeSpan.FromMilliseconds(0);
                data.SeekAvailable = LocalMoviePlayer.CanSeek;

                DataContext = data;

                LocalMoviePlayer.Play();
                LocalMoviePositionTimer.Start();

                if (!DetailInfoDisplayed) { StartToShowInfo(); }
                InfoTimer.Start();
            }
            catch (Exception ex)
            {
                if (LocalMediaFailed != null) { LocalMediaFailed(this, ex.Message); }
            }
        }

        public void Finish()
        {
            LocalMoviePlayer.Stop();
            LocalMoviePositionTimer.Stop();
        }

        public enum PlayerState
        {
            Playing,
            PausedOrStopped,
            Undefined,
        }
    }

    public class SeekBarOperationArgs
    {
        public TimeSpan SeekPosition { get; set; }
    }

    public class PlaybackRequestArgs
    {
        public PlaybackRequest Request { get; set; }
    }

    public enum PlaybackRequest
    {
        None,
        Start,
        Pause,
    }

    public enum MovieFileType
    {
        /// <summary>
        /// Using embedded movie player for movies on local storage.
        /// </summary>
        LocalMovie,
        /// <summary>
        /// To support jpeg based streaming from camera device.
        /// </summary>
        SimpleStreamingMovie,
    }
}
