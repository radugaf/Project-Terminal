using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Gotrue;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Globals.Interfaces;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Interfaces;
using Supabase;


namespace ProjectTerminal.Tests.Mocks
{
    public class MockSupabaseClient
    {
        // Mock for the simplified wrapper interface
        public Mock<ISupabaseClientWrapper> WrapperMock { get; }

        // Mock for the actual Supabase client
        public Mock<Supabase.Client> ClientMock { get; }

        // Mock for the Auth component of Supabase
        public Mock<IGotrueClient<User, Session>> AuthMock { get; }

        // Mock for the Postgrest component
        public Mock<IPostgrestClient> PostgrestMock { get; }

        // Default test data
        public Session DefaultSession { get; set; }
        public User DefaultUser { get; set; }

        public MockSupabaseClient()
        {
            // Create the mocks
            WrapperMock = new Mock<ISupabaseClientWrapper>();
            ClientMock = new Mock<Supabase.Client>("url", "key", null);
            AuthMock = new Mock<IGotrueClient<User, Session>>();
            PostgrestMock = new Mock<IPostgrestClient>();

            // Setup default test data
            DefaultUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                Phone = "+40722123456",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            DefaultSession = new Session
            {
                AccessToken = "test_access_token_" + Guid.NewGuid().ToString("N"),
                RefreshToken = "test_refresh_token_" + Guid.NewGuid().ToString("N"),
                ExpiresIn = 3600, // 1 hour
                TokenType = "bearer",
                User = DefaultUser,
                CreatedAt = DateTime.UtcNow
            };

            // Setup default behaviors
            SetupDefaultBehavior();
        }

        private void SetupDefaultBehavior()
        {
            // Setup wrapper interface methods
            WrapperMock.Setup(w => w.GetClient()).Returns(ClientMock.Object);
            WrapperMock.Setup(w => w.IsInitialized).Returns(true);
            WrapperMock.Setup(w => w.Initialize()).Returns(Task.CompletedTask);

            // Setup client properties
            ClientMock.Setup(c => c.Auth).Returns(AuthMock.Object);
            ClientMock.Setup(c => c.Postgrest).Returns(PostgrestMock.Object);

            // Setup auth methods
            SetupDefaultAuthBehavior();
        }

        private void SetupDefaultAuthBehavior()
        {
            AuthMock.Setup(a => a.SignIn(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DefaultSession);

            AuthMock.Setup(a => a.SignUp(It.IsAny<SignUpType>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync(DefaultSession);

            AuthMock.Setup(a => a.SignIn(It.IsAny<SignInType>(), It.IsAny<string>(), null, null))
                .ReturnsAsync((Session)null);

            AuthMock.Setup(a => a.VerifyOTP(It.IsAny<string>(), It.IsAny<string>(), MobileOtpType.SMS))
                .ReturnsAsync(DefaultSession);

            AuthMock.Setup(a => a.RefreshSession())
                .ReturnsAsync(DefaultSession);

            AuthMock.Setup(a => a.SignOut(It.IsAny<SignOutScope>()))
                .Returns(Task.CompletedTask);

            AuthMock.Setup(a => a.SetSession(It.IsAny<string>(), It.IsAny<string>(), false))
                .ReturnsAsync(DefaultSession);

            AuthMock.Setup(a => a.Update(It.IsAny<UserAttributes>()))
                .ReturnsAsync(DefaultUser);
        }

        // Mock Postgrest functionality using Table<T> instead of From<T>
        public void SetupModelData<T>(List<T> data) where T : BaseModel, new()
        {
            // Mock the Table interface
            var mockTable = new Mock<IPostgrestTable<T>>();
            var mockResponse = new Mock<ModeledResponse<T>>();

            // Setup model responses
            mockResponse.SetupGet(r => r.Models).Returns(data);
            mockResponse.SetupGet(r => r.Model).Returns(data.FirstOrDefault());

            // Setup Table methods - fix for CancellationToken optional param
            mockTable.Setup(t => t.Get(default))
                .ReturnsAsync(mockResponse.Object);

            mockTable.Setup(t => t.Single(default))
                .ReturnsAsync(data.FirstOrDefault());

            // Fix for optional params in Insert
            mockTable.Setup(t => t.Insert(It.IsAny<ICollection<T>>(), null, default))
                .ReturnsAsync(mockResponse.Object);

            mockTable.Setup(t => t.Insert(It.IsAny<T>(), null, default))
                .ReturnsAsync(mockResponse.Object);

            // Setup Postgrest client to use Table instead of From
            PostgrestMock.Setup(p => p.Table<T>())
                .Returns(mockTable.Object);

            // For backward compatibility if any code still uses From
            ClientMock.Setup(c => c.From<T>())
                .Returns(new SupabaseTable<T>(PostgrestMock.Object, null));
        }

        // Setup RPC methods - fix optional parameter issues
        public void SetupRpc<TResponse>(string procedureName, TResponse result)
        {
            PostgrestMock.Setup(p => p.Rpc<TResponse>(procedureName, It.IsAny<object>()))
                .ReturnsAsync(result);

            // For backward compatibility
            ClientMock.Setup(c => c.Rpc<TResponse>(procedureName, It.IsAny<object>()))
                .ReturnsAsync(result);
        }

        // Verification methods
        public void VerifySignIn(string email, string password, Times times)
        {
            AuthMock.Verify(a => a.SignIn(email, password), times);
        }

        public void VerifySignIn(SignInType type, string credential, Times times)
        {
            // Fix optional parameters for verification
            AuthMock.Verify(a => a.SignIn(type, credential, null, null), times);
        }

        public void VerifyVerifyOTP(string phone, string code, MobileOtpType type, Times times)
        {
            AuthMock.Verify(a => a.VerifyOTP(phone, code, type), times);
        }
    }
}
