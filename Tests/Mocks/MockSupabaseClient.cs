// Tests/Mocks/MockSupabaseClient.cs
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Gotrue;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Realtime;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Globals.Interfaces;
using Supabase.Interfaces;
using Supabase.Postgrest;

namespace ProjectTerminal.Tests.Mocks
{
    public class MockSupabaseClient
    {
        public Mock<ISupabaseClientWrapper> Mock { get; }

        // Default session and user for testing
        public Session DefaultSession { get; set; }
        public User DefaultUser { get; set; }

        public MockSupabaseClient()
        {
            Mock = new Mock<ISupabaseClientWrapper>();

            // Create default test data
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

            // Setup authentication methods
            SetupDefaultAuthBehavior();
        }

        private void SetupDefaultAuthBehavior()
        {
            // SignIn with email/password
            Mock.Setup(m => m.SignIn(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DefaultSession);

            // SignUp
            Mock.Setup(m => m.SignUp(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DefaultSession);

            // SignIn with type
            Mock.Setup(m => m.SignIn(It.IsAny<SignInType>(), It.IsAny<string>()))
                .Returns(Task.FromResult<Session>(null));

            // VerifyOTP
            Mock.Setup(m => m.VerifyOTP(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MobileOtpType>()))
                .ReturnsAsync(DefaultSession);

            // RefreshSession
            Mock.Setup(m => m.RefreshSession())
                .ReturnsAsync(DefaultSession);

            // SignOut
            Mock.Setup(m => m.SignOut())
                .Returns(Task.CompletedTask);

            // SetSession
            Mock.Setup(m => m.SetSession(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Update
            Mock.Setup(m => m.Update(It.IsAny<UserAttributes>()))
                .ReturnsAsync(DefaultUser);

            // Initialize
            Mock.Setup(m => m.Initialize())
                .Returns(Task.CompletedTask);
        }

        // Super-simplified approach for testing with model data
        public void SetupModelData<T>(List<T> data) where T : BaseModel, new()
        {
            // Skip direct ModeledResponse creation - instead create a mock to avoid constructor issues
            var mockTable = new Mock<ISupabaseTable<T, RealtimeChannel>>();
            var mockResponse = new Mock<ModeledResponse<T>>();

            // Setup the Models property using SetupGet to return our test data
            mockResponse.SetupGet(r => r.Models).Returns(data);
            mockResponse.SetupGet(r => r.Model).Returns(data.FirstOrDefault());

            // Setup table Get() to return our mocked response without optional parameters
            mockTable.Setup(t => t.Get(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Setup table Single() to return first item (avoiding optional parameter)
            mockTable.Setup(t => t.Single(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(data.FirstOrDefault());

            // Setup table Insert() to return our mocked response
            mockTable.Setup(t => t.Insert(It.IsAny<ICollection<T>>(), It.IsAny<QueryOptions>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Configure the From method without any optional parameters
            Mock.Setup(m => m.From<T>())
                .Returns(mockTable.Object);
        }

        // Setup RPC methods
        public void SetupRpc<TResponse>(string procedureName, TResponse result)
        {
            Mock.Setup(m => m.Rpc<TResponse>(procedureName, It.IsAny<object>()))
                .ReturnsAsync(result);
        }

        // Basic verification methods
        public void VerifySignIn(string email, string password, Times times)
        {
            Mock.Verify(m => m.SignIn(email, password), times);
        }

        public void VerifySignIn(SignInType type, string credential, Times times)
        {
            Mock.Verify(m => m.SignIn(type, credential), times);
        }

        public void VerifyVerifyOTP(string phone, string code, MobileOtpType type, Times times)
        {
            Mock.Verify(m => m.VerifyOTP(phone, code, type), times);
        }
    }
}
