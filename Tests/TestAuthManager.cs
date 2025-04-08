using GdUnit4;
using System;
using System.Threading.Tasks;
using static GdUnit4.Assertions;
using Moq;
using ProjectTerminal.Tests.Mocks;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Services;
using ProjectTerminal.Globals.Factories;
using Supabase.Gotrue;
using System.Collections.Generic;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public class AuthManagerRegisterTests
    {
        private MockLogger _mockLogger;
        private MockSupabaseClient _mockSupabaseClient;
        private Mock<ISessionManager> _mockSessionManager;
        private Mock<ITimeProvider> _mockTimeProvider;
        private AuthManager _authManager;

        [Before]
        public void Setup()
        {
            // Initialize mocks
            _mockLogger = AutoFree(new MockLogger());

            // Initialize the mock Supabase client with explicit setup
            _mockSupabaseClient = new MockSupabaseClient();

            // Ensure default session and user are properly initialized
            if (_mockSupabaseClient.DefaultSession == null)
            {
                _mockSupabaseClient.DefaultUser = new User
                {
                    Id = "test-user-id",
                    Email = "test@example.com",
                    CreatedAt = DateTime.UtcNow
                };

                _mockSupabaseClient.DefaultSession = new Session
                {
                    AccessToken = "test-access-token",
                    RefreshToken = "test-refresh-token",
                    User = _mockSupabaseClient.DefaultUser,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresIn = 3600
                };
            }

            _mockSessionManager = new Mock<ISessionManager>();
            _mockTimeProvider = new Mock<ITimeProvider>();
            _mockTimeProvider.Setup(tp => tp.UtcNow).Returns(DateTime.UtcNow);

            // Create auth manager directly rather than using factory to isolate potential issues
            _authManager = AutoFree(new AuthManager(
                _mockSessionManager.Object,
                _mockSupabaseClient.WrapperMock.Object,
                _mockLogger,
                _mockTimeProvider.Object
            ));

            // Verify the auth manager was created successfully
            AssertThat(_authManager).IsNotNull();
        }

        // [TestCase]
        // public async Task TestBasicRegistration()
        // {
        //     // Use simple hardcoded values for the basic test
        //     string email = "test@example.com";
        //     string password = "Password123!";
        //     bool rememberMe = false;

        //     // Ensure our mocked Supabase client is set up correctly for this test
        //     _mockSupabaseClient.AuthMock
        //         .Setup(a => a.SignUp(email, password, It.IsAny<SignUpOptions>()))
        //         .ReturnsAsync(_mockSupabaseClient.DefaultSession);

        //     // Act
        //     Session result = await _authManager.RegisterWithEmailAsync(email, password, rememberMe);

        //     // Assert - basic check first
        //     AssertThat(result).IsNotNull();

        //     // Verify session manager calls
        //     _mockSessionManager.Verify(sm => sm.SetUserNewState(true), Times.Once);
        //     _mockSessionManager.Verify(sm => sm.SaveSession(It.IsAny<Session>(), rememberMe), Times.Once);

        //     // Check basic logging
        //     AssertThat(_mockLogger.InfoCalled).IsTrue();
        // }
    }
}
