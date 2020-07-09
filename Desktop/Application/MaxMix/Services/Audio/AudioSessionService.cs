﻿using CSCore.CoreAudioAPI;
using MaxMix.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MaxMix.Services.Audio
{
    /// <summary>
    /// Provides functionality to interact with windows audio sessions.
    /// </summary>
    internal class AudioSessionService : IAudioSessionService
    {
        #region Constructor
        public AudioSessionService()
        {
            _synchronizationContext = SynchronizationContext.Current;
        }
        #endregion

        #region Fields
        private readonly SynchronizationContext _synchronizationContext;
        private AudioSessionManager2 _sessionManager;
        private IDictionary<int, AudioSessionWrapper> _wrappers;
        #endregion

        #region Events
        /// <summary>
        /// Raised when a new audio session has been created.
        /// </summary>
        public event AudioSessionDelegate SessionCreated;

        /// <summary>
        /// Raised when a previously active audio session has been removed.
        /// </summary>
        public event EventHandler<int> SessionRemoved;

        /// <summary>
        /// Raised when the volume for an active session has changed.
        /// </summary>
        public event AudioSessionVolumeDelegate SessionVolumeChanged;
        #endregion

        #region Public Methods
        /// <summary>
        /// Deferred initialization of dependencies.
        /// </summary>
        public void Start()
        {
            _wrappers = new Dictionary<int, AudioSessionWrapper>();
            new Thread(() => InitializeEvents()).Start();
        }

        /// <summary>
        /// Deferred disposal of dependencies used by this instance.
        /// </summary>
        public void Stop()
        {
            if (_sessionManager != null)
            {
                _sessionManager.SessionCreated -= OnSessionCreated;
                _sessionManager.Dispose();
            }

            if (_wrappers != null)
            {
                foreach (var wrapper in _wrappers.Values)
                    wrapper.Dispose();

                _wrappers.Clear();
            }
        }

        /// <summary>
        /// Sets the volume of an audio session.
        /// </summary>
        /// <param name="pid">The process Id of the target session.</param>
        /// <param name="volume">The desired volume.</param>
        public void SetVolume(int pid, int volume)
        {
            if (!_wrappers.ContainsKey(pid))
                return;

            _wrappers[pid].Volume = volume;
        }

        /// <summary>
        /// Sets the mute state of an audio session.
        /// </summary>
        /// <param name="pid">The process Id of the target session.</param>
        /// <param name="isMuted">The mute state.</param>
        public void SetMute(int pid, bool isMuted)
        {
            if (!_wrappers.ContainsKey(pid))
                return;

            _wrappers[pid].IsMuted = isMuted;
        }
        #endregion

        #region Private Methods
        private void InitializeEvents()
        {
            _sessionManager = GetDefaultAudioSessionManager(DataFlow.Render);
            _sessionManager.SessionCreated += OnSessionCreated;

            using (var sessionEnumerator = _sessionManager.GetSessionEnumerator())
                foreach (var session in sessionEnumerator)
                {
                    var session2 = session.QueryInterface<AudioSessionControl2>();
                    if (session2.IsSystemSoundSession)
                        continue;

                    if (session2.Process == null)
                        continue;

                    var wrapper = RegisterSession(session);
                    RaiseSessionCreated(wrapper.ProcessID, wrapper.DisplayName, wrapper.Volume, wrapper.IsMuted);
                }
        }

        private AudioSessionWrapper RegisterSession(AudioSessionControl session)
        {
            var wrapper = new AudioSessionWrapper(session);
            _wrappers[wrapper.ProcessID] = wrapper;

            wrapper.SimpleVolumeChanged += OnSimpleVolumeChanged;
            wrapper.SessionDisconnected += OnSessionRemoved;

            return wrapper;
        }

        private void UnregisterSession(AudioSessionWrapper wrapper)
        {
            _wrappers.Remove(wrapper.ProcessID);

            wrapper.SimpleVolumeChanged -= OnSimpleVolumeChanged;
            wrapper.SessionDisconnected -= OnSessionRemoved;
            wrapper.Dispose();
        }

        private AudioSessionManager2 GetDefaultAudioSessionManager(DataFlow dataFlow)
        {
            AudioSessionManager2 sessionManager;
            using (var enumerator = new MMDeviceEnumerator())
                using (var device = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia))
                    sessionManager = AudioSessionManager2.FromMMDevice(device);

            return sessionManager;
        }
        #endregion

        #region Event Handlers
        private void OnSessionCreated(object sender, SessionCreatedEventArgs e)
        {
            var session = e.NewSession;
            var wrapper = RegisterSession(session);
            RaiseSessionCreated(wrapper.ProcessID, wrapper.DisplayName, wrapper.Volume, wrapper.IsMuted);
        }

        private void OnSessionRemoved(object sender, AudioSessionDisconnectedEventArgs e)
        {
            var wrapper = (AudioSessionWrapper)sender;
            RaiseSessionRemoved(wrapper.ProcessID);
            UnregisterSession(wrapper);
        }

        private void OnSimpleVolumeChanged(object sender, AudioSessionSimpleVolumeChangedEventArgs e)
        {
            var wrapper = (AudioSessionWrapper)sender;
            RaiseSessionVolumeChanged(wrapper.ProcessID, wrapper.Volume, wrapper.IsMuted);
        }
        #endregion

        #region Event Dispatchers
        private void RaiseSessionCreated(int pid, string displayName, int volume, bool isMuted)
        {
            if (SynchronizationContext.Current != _synchronizationContext)
                _synchronizationContext.Post(o => SessionCreated?.Invoke(this, pid, displayName, volume, isMuted), null);
            else
                SessionCreated.Invoke(this, pid, displayName, volume, isMuted);
        }

        private void RaiseSessionRemoved(int pid)
        {
            if (SynchronizationContext.Current != _synchronizationContext)
                _synchronizationContext.Post(o => SessionRemoved?.Invoke(this, pid), null);
            else
                SessionRemoved?.Invoke(this, pid);
        }

        private void RaiseSessionVolumeChanged(int pid, int volume, bool isMuted)
        {
            if (SynchronizationContext.Current != _synchronizationContext)
                _synchronizationContext.Post(o => SessionVolumeChanged?.Invoke(this, pid, volume, isMuted), null);
            else
                SessionVolumeChanged?.Invoke(this, pid, volume, isMuted);
        }
        #endregion
    }

}
