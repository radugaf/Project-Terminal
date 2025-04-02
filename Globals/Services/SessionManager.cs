using System;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Interfaces;
using Supabase.Gotrue;

namespace ProjectTerminal.Globals.Services
{
    public class SessionManager : ISessionManager
    {
        private const string SessionStorageKey = "current_user_session";
        private const string SessionExpiryKey = "session_expiry_timestamp";
        private const string UserNewStateKey = "user_new_state";
        private const string PersistentSessionKey = "is_persistent_session";
        private const int StandardRefreshThresholdSeconds = 300; // 5 minutes
        private const int PersistentRefreshThresholdSeconds = 3600 * 24 * 6; // ~6 days

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

                if (session.ExpiresIn > 0)
                {
                    DateTime expiryTime = _timeProvider.UtcNow.AddSeconds(session.ExpiresIn);
                    _storage.StoreValue(SessionExpiryKey, expiryTime.ToString("o"));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"SessionManager: Failed to save session: {ex.Message}");
            }
        }

        public Task<Session> LoadSessionAsync()
        {
            try
            {
                Session sessionFromStorage = _storage.RetrieveObject<Session>(SessionStorageKey);
                if (sessionFromStorage == null || string.IsNullOrEmpty(sessionFromStorage.AccessToken))
                {
                    return Task.FromResult<Session>(null);
                }

                _currentSession = sessionFromStorage;
                _isNewUser = _storage.RetrieveValue<bool>(UserNewStateKey);

                return Task.FromResult(sessionFromStorage);
            }
            catch (Exception ex)
            {
                _logger.Error($"SessionManager: Session loading failed: {ex.Message}");
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
