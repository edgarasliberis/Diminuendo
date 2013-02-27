using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Timers;
using Diminuendo.Core.Helpers;

[assembly: CLSCompliant(true)]

namespace Diminuendo.Core
{
    [Serializable]
    public class DManager : IDisposable
    {
        [NonSerialized]
        private Timer _syncTimer;
        private List<IStorageProvider> _providers = new List<IStorageProvider>();
        private bool _autoSync = false;

        /// <summary>
        /// List of present _providers.
        /// </summary>
        public List<IStorageProvider> Providers { get { return _providers; } }

        /// <summary>
        /// Total bytes of data available to store in _providers.
        /// </summary>
        public long TotalQuota { get { return _providers.Sum(prov => prov.Quota); } }

        /// <summary>
        /// Enables or disables automatic periodic synchronization with storage providers.
        /// </summary>
        public bool AutoSync { 
            get { return _autoSync; } 
            set
            { 
                // Any changes happen only if current value of
                // _autoSync differs from the value which is being set.

                // Autosync is being turned off.
                if (_autoSync && !value)
                {
                    _syncTimer.Dispose();
                    _syncTimer = null;
                }
                // Autosync is being turned on.
                if (!_autoSync && value)
                {
                    createTimer();
                }
                _autoSync = value;
            }
        }

        /// <summary>
        /// Loads the provider into the system.
        /// </summary>
        /// <param name="provider">The provider to add to the list.</param>
        public async Task LoadAsync(IStorageProvider provider)
        {
            if (provider == null)
                throw new NullReferenceException(ExceptionMessage.IsNullOrInvalid("provider"));
            await provider.LoadInfoAsync();
            _providers.Add(provider);            
        }

        /// <summary>
        /// Asks provider plug-ins to sync their local state with the server.
        /// </summary>
        public void Synchronize()
        {
            if(AutoSync && _syncTimer != null) _syncTimer.Enabled = false;
            _providers.AsParallel().ForAll(async provider => await provider.SynchronizeAsync());
            if(AutoSync) createTimer();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            createTimer(1000);
        }

        // Default value of interval is 5 minutes.
        private void createTimer(double interval = 5 * 60 * 1000)
        {
            if (!AutoSync) return;
            if (_syncTimer == null) _syncTimer = new Timer();
            _syncTimer.AutoReset = false;
            _syncTimer.Enabled = true;
            _syncTimer.Elapsed += _syncTimer_Elapsed;
            _syncTimer.Interval = interval;
            _syncTimer.Start();
        }

        void _syncTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (AutoSync) this.Synchronize();
        }

        ~DManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool freeAll)
        {
            if(freeAll)
                _syncTimer.Dispose();
        }
    }
}
