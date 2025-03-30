using Moq;
using System.Collections.Generic;
using ProjectTerminal.Globals.Interfaces;

namespace ProjectTerminal.Tests.Mocks
{
    public class MockSecureStorage
    {
        public Mock<ISecureStorageWrapper> Mock { get; }

        // In-memory store for more realistic behavior
        private readonly Dictionary<string, object> _store = [];

        public MockSecureStorage()
        {
            Mock = new Mock<ISecureStorageWrapper>();
            SetupDefaultBehavior();
        }

        public void SetupDefaultBehavior()
        {
            // StoreObject behavior
            Mock.Setup(m => m.StoreObject(It.IsAny<string>(), It.IsAny<object>()))
                .Returns<string, object>((key, value) =>
                {
                    _store[key] = value;
                    return true;
                });

            // StoreValue behavior
            Mock.Setup(m => m.StoreValue(It.IsAny<string>(), It.IsAny<object>()))
                .Returns<string, object>((key, value) =>
                {
                    _store[key] = value;
                    return true;
                });

            // ClearValue behavior
            Mock.Setup(m => m.ClearValue(It.IsAny<string>()))
                .Returns<string>(key => _store.Remove(key));

            // HasKey behavior
            Mock.Setup(m => m.HasKey(It.IsAny<string>()))
                .Returns<string>(key => _store.ContainsKey(key));

            // GetAllKeys behavior
            Mock.Setup(m => m.GetAllKeys())
                .Returns(() => [.. _store.Keys]);
        }

        // Set up specific generic type behaviors
        public void SetupRetrieveObject<T>(string key, T value)
        {
            Mock.Setup(m => m.RetrieveObject<T>(key))
                .Returns(value);
            _store[key] = value;
        }

        public void SetupRetrieveValue<T>(string key, T value)
        {
            Mock.Setup(m => m.RetrieveValue<T>(key))
                .Returns(value);
            _store[key] = value;
        }

        // Generic setups for any key
        public void SetupRetrieveObjectForAnyKey<T>(T value)
        {
            Mock.Setup(m => m.RetrieveObject<T>(It.IsAny<string>()))
                .Returns(value);
        }

        public void SetupRetrieveValueForAnyKey<T>(T value)
        {
            Mock.Setup(m => m.RetrieveValue<T>(It.IsAny<string>()))
                .Returns(value);
        }

        // Helper methods for test verification
        public void VerifyStoreObject<T>(string key, Times times) => Mock.Verify(m => m.StoreObject(key, It.IsAny<T>()), times);

        public void VerifyRetrieveObject<T>(string key, Times times) => Mock.Verify(m => m.RetrieveObject<T>(key), times);

        public void VerifyStoreValue(string key, Times times) => Mock.Verify(m => m.StoreValue(key, It.IsAny<object>()), times);

        public void VerifyRetrieveValue<T>(string key, Times times) => Mock.Verify(m => m.RetrieveValue<T>(key), times);

        public void VerifyHasKey(string key, Times times) => Mock.Verify(m => m.HasKey(key), times);

        // Direct access to the internal store for test assertions
        public Dictionary<string, object> InternalStore => _store;

        // Helper to add test data directly
        public void SetupTestData<T>(string key, T value)
        {
            _store[key] = value;
            SetupRetrieveObject(key, value);
            SetupRetrieveValue(key, value);
        }

        // Helper to clear all test data
        public void ClearAllData() => _store.Clear();
    }
}
