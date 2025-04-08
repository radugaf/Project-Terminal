using System;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Interfaces;
using Supabase.Gotrue;
using System.Collections.Generic;
namespace ProjectTerminal.Globals.Services
{
    public class SessionManager : ISessionManager
    {
        private const string SessionStorageKey = "current_user_session";
        private const string SessionExpiryKey = "session_expiry_timestamp";
        private const string UserNewStateKey = "user_new_state";
        private const string PersistentSessionKey = "is_persistent_session";
        private const int StandardRefreshThresholdSeconds = 300; // 5 minutes
        private const int PersistentRefreshThresholdSeconds = 3600 * 12; // 12 hours
        private DateTime _lastRefreshTime;

        private readonly ISecureStorageWrapper _storage;
        private readonly ITimeProvider _timeProvider;
        private readonly Logger _logger;

        private Session _currentSession;
        private bool _isNewUser;

        public Session CurrentSession => _currentSession;
        public bool IsSessionValid => _currentSession != null && !IsSessionExpired();
        public bool IsPersistentSession => _storage.RetrieveValue<bool>(PersistentSessionKey);

        public SessionManager(ISecureStorageWrapper storage, ITimeProvider timeProvider, Logger logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _isNewUser = _storage.RetrieveValue<bool>(UserNewStateKey);
            _lastRefreshTime = _timeProvider.UtcNow;
        }

        public void SaveSession(Session session, bool isPersistent)
        {
            if (session == null)
            {
                ClearSession();
                return;
            }

            try
            {
                _currentSession = session;
                _storage.StoreObject(SessionStorageKey, session);
                _storage.StoreValue(PersistentSessionKey, isPersistent);

                // Record the refresh time
                _lastRefreshTime = _timeProvider.UtcNow;
                _storage.StoreValue("last_refresh_time", _lastRefreshTime.ToString("o"));

                if (session.ExpiresIn > 0)
                {
                    DateTime expiryTime = _timeProvider.UtcNow.AddSeconds(session.ExpiresIn);
                    _storage.StoreValue(SessionExpiryKey, expiryTime.ToString("o"));

                    _logger.Debug($"SessionManager: Session saved with expiry at {expiryTime}. Persistent: {isPersistent}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"SessionManager: Failed to save session: {ex.Message}");
            }
        }

        public Task<Session> LoadSessionAsync()
        {
            _logger.Debug("SessionManager: Starting to load session from storage");

            try
            {
                _logger.Debug($"SessionManager: Attempting to retrieve session with key: {SessionStorageKey}");
                Session sessionFromStorage = _storage.RetrieveObject<Session>(SessionStorageKey);

                if (sessionFromStorage == null)
                {
                    _logger.Info("SessionManager: No session found in storage (null)");
                    return Task.FromResult<Session>(null);
                }

                if (string.IsNullOrEmpty(sessionFromStorage.AccessToken))
                {
                    _logger.Info("SessionManager: Session found but AccessToken is null or empty");
                    return Task.FromResult<Session>(null);
                }

                _logger.Info($"SessionManager: Valid session retrieved User ID: {sessionFromStorage.User.Id}, Token Valid: {!string.IsNullOrEmpty(sessionFromStorage.AccessToken)}");

                _currentSession = sessionFromStorage;
                _logger.Debug("SessionManager: Current session updated with retrieved session");

                _isNewUser = _storage.RetrieveValue<bool>(UserNewStateKey);
                _logger.Debug($"SessionManager: User new state retrieved: IsNewUser = {_isNewUser}");

                // Load the last refresh time
                _logger.Debug("SessionManager: Attempting to retrieve last refresh time");
                string storedTime = _storage.RetrieveValue<string>("last_refresh_time");

                if (string.IsNullOrEmpty(storedTime))
                {
                    _logger.Debug("SessionManager: No last_refresh_time found in storage");
                }
                else
                {
                    if (DateTime.TryParse(storedTime, out DateTime time))
                    {
                        _lastRefreshTime = time.Kind == DateTimeKind.Utc ?
                            time : DateTime.SpecifyKind(time, DateTimeKind.Utc);
                        _logger.Debug($"SessionManager: Last refresh time loaded: {_lastRefreshTime} (UTC)");
                    }
                    else
                    {
                        _logger.Warn($"SessionManager: Could not parse stored refresh time: '{storedTime}'");
                    }
                }

                var context = new Dictionary<string, object>
        {
            { "session_expires_in", sessionFromStorage.ExpiresIn },
            { "has_token", !string.IsNullOrEmpty(sessionFromStorage.AccessToken) },
            { "is_new_user", _isNewUser },
            { "last_refresh_time", _lastRefreshTime }
        };
                _logger.Info("SessionManager: Session loaded successfully", context);

                return Task.FromResult(sessionFromStorage);
            }
            catch (Exception ex)
            {
                var context = new Dictionary<string, object>
        {
            { "exception_type", ex.GetType().Name },
            { "stack_trace", ex.StackTrace }
        };
                _logger.LogException(ex, "SessionManager: Session loading failed");
                _logger.Debug("SessionManager: Returning null session due to exception", context);
                return Task.FromResult<Session>(null);
            }
        }

        public void ClearSession()
        {
            try
            {
                _storage.ClearValue(SessionStorageKey);
                _storage.ClearValue(SessionExpiryKey);
                _storage.ClearValue(UserNewStateKey);
                _currentSession = null;
                _isNewUser = false;
            }
            catch (Exception ex)
            {
                _logger.Error($"SessionManager: Failed to clear session: {ex.Message}");
            }
        }

        public DateTime? GetSessionExpiryTime()
        {
            if (_currentSession == null)
                return null;

            if (_storage.HasKey(SessionExpiryKey))
            {
                string storedTimestamp = _storage.RetrieveValue<string>(SessionExpiryKey);
                if (DateTime.TryParse(storedTimestamp, out DateTime timestamp))
                {
                    return timestamp.Kind == DateTimeKind.Utc ?
                        timestamp : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                }
            }

            if (_currentSession.ExpiresIn > 0)
            {
                DateTime createdAtUtc = _currentSession.CreatedAt.Kind == DateTimeKind.Utc
                    ? _currentSession.CreatedAt
                    : DateTime.SpecifyKind(_currentSession.CreatedAt, DateTimeKind.Utc);

                return createdAtUtc.AddSeconds(_currentSession.ExpiresIn);
            }

            return null;
        }

        public DateTime GetLastRefreshTime()
        {
            string storedTime = _storage.RetrieveValue<string>("last_refresh_time");
            if (!string.IsNullOrEmpty(storedTime) && DateTime.TryParse(storedTime, out DateTime time))
            {
                return time.Kind == DateTimeKind.Utc ?
                    time : DateTime.SpecifyKind(time, DateTimeKind.Utc);
            }
            return _timeProvider.UtcNow.AddDays(-1); // Default to a day ago if not found
        }

        public bool IsSessionExpired()
        {
            DateTime? expiryTime = GetSessionExpiryTime();
            return expiryTime.HasValue && _timeProvider.UtcNow > expiryTime.Value;
        }

        public void SetUserNewState(bool isNew)
        {
            _isNewUser = isNew;
            _storage.StoreValue(UserNewStateKey, isNew);
        }

        public bool GetUserNewState()
        {
            return _isNewUser;
        }

        public int GetRefreshThresholdSeconds()
        {
            return IsPersistentSession ? PersistentRefreshThresholdSeconds : StandardRefreshThresholdSeconds;
        }
    }
}
