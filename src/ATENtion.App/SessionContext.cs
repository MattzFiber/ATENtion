using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ATENtion.App.Video;
using ATENtion.Core.Net;
using System.Windows.Media;

namespace ATENtion.App
{
    /// <summary>
    /// Everything belonging to one console session, so several can be open at once as tabs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OPERATION - Exactly one context is active; the window mirrors it into its controls. Status
    /// and overlay text are recorded here rather than written straight to a control, so a background
    /// session can update them and have them replayed when its tab is selected.
    /// </para>
    /// <para>
    /// RESTRICTIONS - Read and written on the UI thread. Session callbacks arrive on background
    /// threads and must be marshalled first. <see cref="ReconnectTimer"/>'s Tag carries this context
    /// so the shared tick handler knows which session fired.
    /// </para>
    /// </remarks>
    public sealed class SessionContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>The renderer for this tab's framebuffer. One per session, never shared.</summary>
        public WpfFrameRenderer Renderer { get; } = new WpfFrameRenderer();

        /// <summary>The live session, or null while disconnected.</summary>
        public KvmVideoSession Session;

        /// <summary>The ISO currently served to this session, or null.</summary>
        public VirtualMediaSession Vmedia;
        public EventHandler<Exception> VmFaulted;
        public EventHandler VmClosed;

        // Connection details, kept so a dropped link can be re-established without prompting.
        public KvmConnectionOptions ConnectOptions;
        public ConnectSettings ConnectProfile;
        public bool ArmViaWeb;
        public string BmcUser;
        public string BmcPassword;

        /// <summary>Auto-reconnect timer for this session alone. Tag is this context.</summary>
        public readonly DispatcherTimer ReconnectTimer = new DispatcherTimer();
        public int ReconnectAttempts;

        // Presentation state: the snapshot the UI thread paints from, and the regions that changed.
        public byte[] Present;
        public int PresentW, PresentH, PresentStride;
        public readonly List<Int32Rect> PresentDirty = new List<Int32Rect>();
        public bool PresentFull;
        public bool RenderPending;
        public readonly object PresentLock = new object();

        // Connection/overlay state.
        public bool LiveConnected;
        public bool UserDisconnected;
        public DateTime? ConnectedAt;
        public int NoFrameTicks;
        public int ButtonMask;

        // Status-bar and overlay text for this tab, replayed when it becomes visible.
        public string StatusText = "Ready";
        public System.Windows.Media.Brush StatusBrush = StatusColors.Neutral;
        public string InfoText = "";
        public string StatsText = "";
        public string OverlayText = "";
        public bool OverlayVisible;

        // Per session, not per window: each tab may be a different server on a different link, so
        // the bandwidth trade belongs to the connection.
        public int FullFrameInterval = 5;
        public ushort ImageMode = ATENtion.Core.Protocol.ScreenInfoRequest.EnhancedTextMode;
        public byte ImageQuality = ATENtion.Core.Protocol.ScreenInfoRequest.MaximumQuality;

        // Throughput bookkeeping for the status line.
        public long LastFrames, LastBytes, LastFps, LastRateBytes;

        private VideoSignalState _power = VideoSignalState.Unknown;

        /// <summary>Host state as far as the video signal reveals it.</summary>
        public VideoSignalState Power
        {
            get => _power;
            set
            {
                if (_power == value) return;
                _power = value;
                Raise(nameof(Power));
                Raise(nameof(PowerBrush));
                Raise(nameof(PowerTip));
            }
        }

        /// <summary>Green when the host is definitely on, grey otherwise.</summary>
        /// <remarks>
        /// Two colours, not three: only "on" is certain. No picture is usually a powered-down host
        /// but equally matches one running without video output, so red would overstate it.
        /// </remarks>
        public Brush PowerBrush =>
            _power == VideoSignalState.Present ? StatusColors.Controlling : PowerUnknownBrush;

        public string PowerTip
        {
            get
            {
                switch (_power)
                {
                    case VideoSignalState.Present:
                        return "Host on - video signal present";
                    case VideoSignalState.Absent:
                        return "No video signal - host is off, or on but not producing video";
                    default:
                        return "No video signal information yet - not connected, or no frame seen";
                }
            }
        }

        // Lifted off the tab strip's #252526 so it reads as "no picture", not a missing glyph.
        private static readonly Brush PowerUnknownBrush = MakeFrozen(0x55, 0x55, 0x55);

        private static Brush MakeFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();   // shared across threads and tabs
            return brush;
        }

        private string _displayName = "New session";

        /// <summary>The tab caption: the host name, or a placeholder before the first connect.</summary>
        public string DisplayName
        {
            get => _displayName;
            set { if (_displayName != value) { _displayName = value; Raise(nameof(DisplayName)); } }
        }

        /// <summary>A short state marker shown next to the caption, for tabs that are not visible.</summary>
        public string TabState
        {
            get
            {
                if (Session == null) return "–";
                if (!LiveConnected) return "…";
                return Vmedia != null ? "● CD" : "●";
            }
        }

        public void RaiseTabState() => Raise(nameof(TabState));

        /// <summary>Names the tab after the saved profile, falling back to the host.</summary>
        /// <remarks>Keeps tabs narrow; the address stays available in <see cref="TabTip"/>.</remarks>
        public void UpdateDisplayName()
        {
            string name = ConnectProfile?.Name;
            string host = ConnectOptions?.Host;
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name.Trim()
                        : !string.IsNullOrWhiteSpace(host) ? host
                        : "New session";
            Raise(nameof(TabTip));
        }

        /// <summary>Hover text for the tab: the profile name and the address it resolves to.</summary>
        public string TabTip
        {
            get
            {
                string host = ConnectOptions?.Host;
                if (string.IsNullOrWhiteSpace(host)) return "Not connected";
                int port = ConnectOptions?.Port ?? 0;
                string endpoint = port > 0 ? host + ":" + port : host;
                string name = ConnectProfile?.Name;
                return string.IsNullOrWhiteSpace(name) ? endpoint : name.Trim() + "  -  " + endpoint;
            }
        }
    }
}
