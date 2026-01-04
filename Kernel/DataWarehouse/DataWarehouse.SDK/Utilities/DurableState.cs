using System.Collections.Concurrent;
using System.Text.Json;

namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Durable state
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DurableState<T>: IDisposable
    {
        private readonly string _path;
        private ConcurrentDictionary<string, T> _store;
        private readonly Timer _saveTimer;
        private readonly Lock _lock = new();
        private bool _isDisposed;

        // [Fix] Constructor takes ONLY path. No second argument.

        /// <summary>
        /// COnstructor
        /// </summary>
        /// <param name="path"></param>
        public DurableState(string path)
        {
            _path = path;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            _store = new ConcurrentDictionary<string, T>();
            Load();
            _saveTimer = new Timer(_ => Save(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Save
        /// </summary>
        public void Save()
        {
            if (_isDisposed) return;
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_store);
                File.WriteAllText(_path, json);
            }
        }

        /// <summary>
        /// Load
        /// </summary>
        private void Load()
        {
            if (File.Exists(_path))
            {
                try
                {
                    var json = File.ReadAllText(_path);
                    var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, T>>(json);
                    if (data != null) _store = data;
                }
                catch { }
            }
        }

        /// <summary>
        /// Try get
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGet(string key, out T? value)
        {
            return _store.TryGetValue(key, out value);
        }

        /// <summary>
        /// Set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, T value)
        {
            _store[key] = value;
        }

        /// <summary>
        /// Add Remove overload that matches Dictionary semantics (out value)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Remove(string key, out T? value) => _store.TryRemove(key, out value);

        /// <summary>
        /// Add simple Remove for backward compatibility
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key) => _store.TryRemove(key, out _);

        /// <summary>
        /// Add GetAll/Values accessor
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAll() => _store.Values;

        /// <summary>
        /// Expose as Dictionary for complex LINQ if needed
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, T> ToDictionary() => _store;

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Safely DIspose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _saveTimer?.Dispose();
                    Save();
                }
                _isDisposed = true;
            }
        }
    }
}