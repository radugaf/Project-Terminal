using Godot;
using GdUnit4;
using GdUnit4.Api;
using System;
using System.Collections.Generic;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public partial class SecureStorageTest
    {
        private SecureStorage _secureStorage;
        private MockLogger _mockLogger;
        private const string TEST_PREFIX = "test_";

        [Before]
        public void Setup()
        {
            // Create a mock logger
            _mockLogger = new MockLogger();

            // Create a test scene tree
            var scene = new Node();

            // Add mock logger to scene
            scene.AddChild(_mockLogger);
            _mockLogger.Name = "Logger";

            // Create SecureStorage instance
            _secureStorage = new SecureStorage();
            scene.AddChild(_secureStorage);

            // Call _Ready directly
            _secureStorage._Ready();
        }

        [After]
        public void TearDown()
        {
            // Clean up test data
            foreach (var key in _secureStorage.GetAllKeys())
            {
                if (key.StartsWith(TEST_PREFIX))
                {
                    _secureStorage.ClearValue(key);
                }
            }

            _secureStorage.QueueFree();
            _mockLogger.QueueFree();
        }
        #region Primitive Value Tests

        [TestCase]
        public void TestStoreAndRetrieveString()
        {
            string key = TEST_PREFIX + "string";
            string value = "Hello, World!";

            bool storeResult = _secureStorage.StoreValue(key, value);
            string retrievedValue = _secureStorage.RetrieveValue<string>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestStoreAndRetrieveInt()
        {
            string key = TEST_PREFIX + "int";
            int value = 42;

            bool storeResult = _secureStorage.StoreValue(key, value);
            int retrievedValue = _secureStorage.RetrieveValue<int>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestStoreAndRetrieveFloat()
        {
            string key = TEST_PREFIX + "float";
            float value = 3.14159f;

            bool storeResult = _secureStorage.StoreValue(key, value);
            float retrievedValue = _secureStorage.RetrieveValue<float>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestStoreAndRetrieveBool()
        {
            string key = TEST_PREFIX + "bool";
            bool value = true;

            bool storeResult = _secureStorage.StoreValue(key, value);
            bool retrievedValue = _secureStorage.RetrieveValue<bool>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestStoreAndRetrieveDateTime()
        {
            string key = TEST_PREFIX + "datetime";
            DateTime value = new DateTime(2023, 1, 1);

            bool storeResult = _secureStorage.StoreValue(key, value);
            DateTime retrievedValue = _secureStorage.RetrieveValue<DateTime>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestStoreValueWithUnsupportedType()
        {
            string key = TEST_PREFIX + "unsupported";
            var value = new List<string> { "item1", "item2" };

            // Should throw ArgumentException for unsupported type
            Assertions.AssertThrown(() => _secureStorage.StoreValue(key, value))
                .IsInstanceOf<ArgumentException>();
        }

        #endregion

        #region Object Tests

        [TestCase]
        public void TestStoreAndRetrieveObject()
        {
            string key = TEST_PREFIX + "object";
            var value = new TestObject { Id = 1, Name = "Test", IsActive = true };

            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestObject>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNotNull();
            Assertions.AssertThat(retrievedValue.Id).IsEqual(value.Id);
            Assertions.AssertThat(retrievedValue.Name).IsEqual(value.Name);
            Assertions.AssertThat(retrievedValue.IsActive).IsEqual(value.IsActive);
        }

        [TestCase]
        public void TestStoreAndRetrieveNullObject()
        {
            string key = TEST_PREFIX + "null_object";
            TestObject value = null;

            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestObject>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNull();
        }

        [TestCase]
        public void TestStoreAndRetrieveComplexObject()
        {
            string key = TEST_PREFIX + "complex_object";
            var value = new TestComplexObject
            {
                Id = 100,
                Items = new List<TestObject>
                {
                    new TestObject { Id = 1, Name = "Item 1", IsActive = true },
                    new TestObject { Id = 2, Name = "Item 2", IsActive = false }
                }
            };

            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestComplexObject>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNotNull();
            Assertions.AssertThat(retrievedValue.Id).IsEqual(value.Id);
            Assertions.AssertThat(retrievedValue.Items.Count).IsEqual(value.Items.Count);
            Assertions.AssertThat(retrievedValue.Items[0].Name).IsEqual("Item 1");
            Assertions.AssertThat(retrievedValue.Items[1].Name).IsEqual("Item 2");
        }

        #endregion

        #region Key Management Tests

        [TestCase]
        public void TestHasKey()
        {
            string key = TEST_PREFIX + "exists";
            string value = "value";

            // Initially the key should not exist
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsFalse();

            // After storing, the key should exist
            _secureStorage.StoreValue(key, value);
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsTrue();

            // After clearing, the key should not exist
            _secureStorage.ClearValue(key);
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsFalse();
        }

        [TestCase]
        public void TestGetAllKeys()
        {
            // Store multiple test values
            _secureStorage.StoreValue(TEST_PREFIX + "key1", "value1");
            _secureStorage.StoreValue(TEST_PREFIX + "key2", "value2");
            _secureStorage.StoreValue(TEST_PREFIX + "key3", "value3");

            string[] keys = _secureStorage.GetAllKeys();

            // Only count keys with our test prefix to avoid issues with other tests
            int testKeyCount = 0;
            foreach (var key in keys)
            {
                if (key.StartsWith(TEST_PREFIX))
                {
                    testKeyCount++;
                }
            }

            Assertions.AssertThat(testKeyCount).IsEqual(3);
            Assertions.AssertThat(keys).Contains(TEST_PREFIX + "key1");
            Assertions.AssertThat(keys).Contains(TEST_PREFIX + "key2");
            Assertions.AssertThat(keys).Contains(TEST_PREFIX + "key3");
        }

        [TestCase]
        public void TestClearValue()
        {
            string key = TEST_PREFIX + "to_clear";
            _secureStorage.StoreValue(key, "value");

            bool clearResult = _secureStorage.ClearValue(key);

            Assertions.AssertThat(clearResult).IsTrue();
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsFalse();
            Assertions.AssertThat(_secureStorage.RetrieveValue<string>(key)).IsNull();
        }

        [TestCase]
        public void TestClearNonExistentValue()
        {
            string key = TEST_PREFIX + "nonexistent";
            bool clearResult = _secureStorage.ClearValue(key);

            // Should return true even if key doesn't exist
            Assertions.AssertThat(clearResult).IsTrue();
        }

        #endregion

        #region Edge Cases

        [TestCase]
        public void TestRetrieveNonExistentValue()
        {
            string key = TEST_PREFIX + "does_not_exist";

            string stringValue = _secureStorage.RetrieveValue<string>(key);
            int intValue = _secureStorage.RetrieveValue<int>(key);
            bool boolValue = _secureStorage.RetrieveValue<bool>(key);

            Assertions.AssertThat(stringValue).IsNull();
            Assertions.AssertThat(intValue).IsEqual(0);
            Assertions.AssertThat(boolValue).IsFalse();
        }

        [TestCase]
        public void TestSpecialCharactersInKey()
        {
            string key = TEST_PREFIX + "special/chars\\with:symbols";
            string value = "special value";

            bool storeResult = _secureStorage.StoreValue(key, value);
            string retrievedValue = _secureStorage.RetrieveValue<string>(key);

            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void TestTypeConversionFailure()
        {
            string key = TEST_PREFIX + "type_mismatch";
            int value = 123;

            _secureStorage.StoreValue(key, value);

            // Attempting to retrieve as wrong type should return default value
            string retrievedAsString = _secureStorage.RetrieveValue<string>(key);
            Assertions.AssertThat(retrievedAsString).IsNull();
        }

        #endregion

        #region Helper Classes

        // Mock logger class for testing - needs to be partial for Godot
        private partial class MockLogger : Node
        {
            public void debug(string message) { }
            public void info(string message) { }
            public void warn(string message) { }
            public void error(string message) { }
            public void error(string message, object context) { }
        }

        // Test objects for serialization tests
        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
        }

        private class TestComplexObject
        {
            public int Id { get; set; }
            public List<TestObject> Items { get; set; }
        }

        #endregion
    }
}
