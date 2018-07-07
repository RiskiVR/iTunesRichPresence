﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using iTunesLib;
using iTunesRichPresence_Rewrite.Properties;
using iTunesRichPresence_Rewrite.Tokens;
using SharpRaven.Data;

namespace iTunesRichPresence_Rewrite {
    /// <summary>
    /// Simplifies interactions between iTunes and Discord
    /// </summary>
    internal class DiscordBridge {

        public List<IToken> Tokens;

        private string _currentArtist;
        private string _currentTitle;
        private string _currentPlaylist;
        private ITPlaylistKind _currentPlaylistType;
        private ITPlayerState _currentState;
        private int _currentPosition;

        private readonly DispatcherTimer _timer;
        private IiTunes _iTunes;

        /// <summary>
        /// Initializes the bridge and connects it to DiscordRPC
        /// </summary>
        /// <param name="applicationId">The Discord application ID for rich presence</param>
        public DiscordBridge(string applicationId) {
            var handlers = new DiscordRpc.EventHandlers();
            DiscordRpc.Initialize(applicationId, ref handlers, true, null);

            Tokens = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IToken).IsAssignableFrom(p) && p.IsClass).Select(Activator.CreateInstance).Select(i => (IToken) i).ToList();

            _iTunes = new iTunesApp();

            _timer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(15)};
            _timer.Tick += Timer_OnTick;
            _timer.Start();

            _currentArtist = "";
            _currentTitle = "";
            _currentState = ITPlayerState.ITPlayerStateStopped;
            _currentPosition = 0;

        }

        /// <summary>
        /// Renders a string template using information about the currently playing song
        /// </summary>
        /// <param name="template">The template to render</param>
        /// <returns>The rendered string</returns>
        private string RenderString(string template) {

            foreach (var token in Tokens) {
                template = template.Replace(token.Token, token.GetText(_iTunes));
            }

            return template;
        }

        /// <summary>
        /// Truncates a string to fit into Discord's 127 byte size limit
        /// </summary>
        /// <param name="s">String to truncate</param>
        /// <returns>Truncated string</returns>
        private static string TruncateString(string s) {
            var n = Encoding.Unicode.GetByteCount(s);
            if (n <= 127) return s;
            s = s.Substring(0, 64);

            while (Encoding.Unicode.GetByteCount(s) > 123)
                s = s.Substring(0, s.Length - 1);

            return s + "...";
        }

        /// <summary>
        /// Handles checking for playing status changes and pushing out presence updates
        /// </summary>
        /// <param name="sender">Sender of this event</param>
        /// <param name="e">Args of this event</param>
        private void Timer_OnTick(object sender, EventArgs e) {
            if (_iTunes.CurrentTrack == null) {
                DiscordRpc.ClearPresence();
                return;
            }

            if (_currentArtist == _iTunes.CurrentTrack.Artist && _currentTitle == _iTunes.CurrentTrack.Name &&
                _currentState == _iTunes.PlayerState && _currentPosition == _iTunes.PlayerPosition) return;

            _currentArtist = _iTunes.CurrentTrack.Artist;
            _currentTitle = _iTunes.CurrentTrack.Name;
            _currentState = _iTunes.PlayerState;
            _currentPosition = _iTunes.PlayerPosition;

            var presence = new DiscordRpc.RichPresence{largeImageKey = "itunes_logo_big"};
            if (_currentState != ITPlayerState.ITPlayerStatePlaying) {
                presence.details = TruncateString(RenderString(Settings.Default.PausedTopLine));
                presence.state = TruncateString(RenderString(Settings.Default.PausedBottomLine));
            }
            else {
                presence.details = TruncateString(RenderString(Settings.Default.PlayingTopLine));
                presence.state = TruncateString(RenderString(Settings.Default.PlayingBottomLine));
                if (Settings.Default.DisplayPlaybackDuration) {
                    presence.startTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() - _currentPosition;
                    presence.endTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() +
                                            (_iTunes.CurrentTrack.Duration - _currentPosition);
                }
            }

            try {
                DiscordRpc.UpdatePresence(presence);
            }
            catch (Exception exception) {
                exception.Data.Add("CurrentArtist", _currentArtist);
                exception.Data.Add("CurrentTitle", _currentTitle);
                exception.Data.Add("CurrentState", _currentState.ToString());
                exception.Data.Add("CurrentPlaylist", _currentPlaylist);
                exception.Data.Add("CurrentPlaylistType", _currentPlaylistType.ToString());
                exception.Data.Add("CurrentPosition", _currentPosition.ToString());
                exception.Data.Add("Details", presence.details);
                exception.Data.Add("State", presence.state);
                Globals.RavenClient.Capture(new SentryEvent(exception));
            }
            
        }

        /// <summary>
        /// Disconnects from DiscordRPC and shuts down the bridge
        /// </summary>
        public void Shutdown() {
            _timer.Stop();
            DiscordRpc.Shutdown();
        }
    }
}
