// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Audio track receiving audio frames from the remote peer.
    /// </summary>
    public class RemoteAudioTrack : MediaTrack
    {
        /// <summary>
        /// Enabled status of the track. If enabled, receives audio frames from the remote peer as
        /// expected. If disabled, does not receive anything (silence).
        /// </summary>
        /// <remarks>
        /// Reading the value of this property after the track has been disposed is valid, and returns
        /// <c>false</c>.
        /// The remote audio track enabled status is controlled by the remote peer only.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                return (RemoteAudioTrackInterop.RemoteAudioTrack_IsEnabled(_nativeHandle) != 0);
            }
        }

        /// <summary>
        /// Event that occurs when a audio frame has been received from the remote peer.
        /// </summary>
        public event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Handle to the native RemoteAudioTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::RemoteAudioTrackHandle</code>.
        /// </remarks>
        internal IntPtr _nativeHandle = IntPtr.Zero;

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private RemoteAudioTrackInterop.InteropCallbackArgs _interopCallbackArgs;

        // Constructor for interop-based creation; SetHandle() will be called later
        internal RemoteAudioTrack(IntPtr handle, PeerConnection peer, string trackName) : base(peer, trackName)
        {
            _nativeHandle = handle;
            RegisterInteropCallbacks();
        }

        private void RegisterInteropCallbacks()
        {
            _interopCallbackArgs = new RemoteAudioTrackInterop.InteropCallbackArgs()
            {
                Track = this,
                FrameCallback = RemoteAudioTrackInterop.FrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            RemoteAudioTrackInterop.RemoteAudioTrack_RegisterFrameCallback(
                _nativeHandle, _interopCallbackArgs.FrameCallback, _selfHandle);
        }

        private void UnregisterInteropCallbacks()
        {
            if (_selfHandle != IntPtr.Zero)
            {
                RemoteAudioTrackInterop.RemoteAudioTrack_RegisterFrameCallback(_nativeHandle, null, IntPtr.Zero);
                Utils.ReleaseWrapperRef(_selfHandle);
                _selfHandle = IntPtr.Zero;
                _interopCallbackArgs = null;
            }
        }

        /// <summary>
        /// Dispose of the native track. Invoked by its owner (<see cref="PeerConnection"/>).
        /// </summary>
        internal void Destroy()
        {
            if (_nativeHandle == IntPtr.Zero)
            {
                return;
            }

            Debug.Assert(PeerConnection == null); // see OnTrackRemoved

            UnregisterInteropCallbacks();

            _nativeHandle = IntPtr.Zero;
        }

        internal void OnFrameReady(AudioFrame frame)
        {
            MainEventSource.Log.RemoteAudioFrameReady(frame.bitsPerSample, frame.sampleRate, frame.channelCount, frame.sampleCount);
            AudioFrameReady?.Invoke(frame);
        }

        internal void OnTrackAddedToTransceiver(Transceiver transceiver)
        {
            Debug.Assert(transceiver.MediaKind == MediaKind.Audio);
            Debug.Assert(PeerConnection == transceiver.PeerConnection);
            Debug.Assert(_nativeHandle != IntPtr.Zero);
            Debug.Assert(Transceiver == null);
            Debug.Assert(transceiver != null);
            Transceiver = transceiver;
            transceiver.OnRemoteTrackAdded(this);
        }

        internal override void OnTrackAdded(PeerConnection newConnection, Transceiver newTransceiver)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Internal callback invoked when the track is removed from a peer connection,
        /// either because the peer connection was closed or because it was detached from
        /// the transceiver it was attached to.
        /// </summary>
        /// <param name="previousConnection">The peer connection the track was previously attached to.</param>
        /// <remarks><see cref="_nativeHandle"/> is non-<c>null</c> but the native object may have been
        /// destroyed already, so the handle should not be used.</remarks>
        internal override void OnTrackRemoved(PeerConnection previousConnection)
        {
            Debug.Assert(PeerConnection == previousConnection);
            Debug.Assert(_nativeHandle != IntPtr.Zero);
            PeerConnection = null;
            Transceiver.OnRemoteTrackRemoved(this);
            Transceiver = null;
        }

        /// <summary>
        /// Internal callback invoked by the owner (<see cref="PeerConnection"/>) after the track
        /// has been removed as a result of the peer connection being closed.
        /// In this case, unlike <see cref="Destroy"/>, the <see cref="_nativeHandle"/> has been
        /// invalidated, so should not be used to access the native object, which was destroyed. This
        /// is because the peer connection callbacks are unregistered before closing the peer connection,
        /// so there is no notification about the track being removed as part of the closing.
        /// </summary>
        internal void OnConnectionClosed()
        {
            Debug.Assert(PeerConnection == null); // see OnTrackRemoved
            // No need (and can't) unregister callbacks, the native object is already destroyed
            _nativeHandle = IntPtr.Zero;
        }

        internal override void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(RemoteAudioTrack)\"{Name}\"";
        }
    }
}
