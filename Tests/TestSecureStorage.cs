using Godot;
using GdUnit4;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTerminal.Resources.Mocks;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public partial class TestSecureStorage
    {
        // Constants
        private const string TEST_PREFIX = "test_";

        // Test fixtures
        private SecureStorage _secureStorage;
        private MockLogger _mockLogger;
        private Node _testScene;

        [Before]
        public void Setup()
        {
            // Create a test scene tree and add it to the tree to enable file access
            _testScene = new Node();
            if (Engine.GetMainLoop() is SceneTree root)
            {
                root.Root.AddChild(_testScene);
            }

            // Set up mock logger
            _mockLogger = new MockLogger();
            _testScene.AddChild(_mockLogger);
            _mockLogger.Name = "Logger"; // Must match the path in SecureStorage._Ready()

            // Set up SecureStorage instance
            _secureStorage = new SecureStorage();
            _testScene.AddChild(_secureStorage);
        }

        [After]
        public void TearDown()
        {
            // Clean up all test data
            CleanupTestData();

            // Free resources and remove from tree
            if (_testScene.IsInsideTree())
            {
                _testScene.GetParent().RemoveChild(_testScene);
            }
            _secureStorage.QueueFree();
            _mockLogger.QueueFree();
            _testScene.QueueFree();
        }

        [AfterTest]
        public void AfterTest()
        {
            // Clean up test data after each test
            CleanupTestData();
        }

        private void CleanupTestData()
        {
            foreach (string key in _secureStorage.GetAllKeys().Where(k => k.StartsWith(TEST_PREFIX)))
            {
                _secureStorage.ClearValue(key);
            }
        }

        #region Primitive Value Storage Tests

        [TestCase]
        public void StoreAndRetrieve_String_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "string";
            string value = "Hello, World!";

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            string retrievedValue = _secureStorage.RetrieveValue<string>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreAndRetrieve_Int_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "int";
            int value = 42;

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            int retrievedValue = _secureStorage.RetrieveValue<int>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreAndRetrieve_Bool_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "bool";
            bool value = true;

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            bool retrievedValue = _secureStorage.RetrieveValue<bool>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreAndRetrieve_Float_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "float";
            float value = 3.14159f;

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            float retrievedValue = _secureStorage.RetrieveValue<float>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreAndRetrieve_Double_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "double";
            double value = 3.14159265359;

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            double retrievedValue = _secureStorage.RetrieveValue<double>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreAndRetrieve_DateTime_ShouldReturnSameValue()
        {
            // Arrange
            string key = TEST_PREFIX + "datetime";
            DateTime value = new DateTime(2023, 12, 31, 23, 59, 59);

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            DateTime retrievedValue = _secureStorage.RetrieveValue<DateTime>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        #endregion

        #region Object Storage Tests

        [TestCase]
        public void StoreAndRetrieve_SimpleObject_ShouldReturnEquivalentObject()
        {
            // Arrange
            string key = TEST_PREFIX + "object";
            var value = new TestObject { Id = 1, Name = "Test", IsActive = true };

            // Act
            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestObject>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNotNull();
            Assertions.AssertThat(retrievedValue.Id).IsEqual(value.Id);
            Assertions.AssertThat(retrievedValue.Name).IsEqual(value.Name);
            Assertions.AssertThat(retrievedValue.IsActive).IsEqual(value.IsActive);
        }

        [TestCase]
        public void StoreAndRetrieve_NullObject_ShouldReturnNull()
        {
            // Arrange
            string key = TEST_PREFIX + "null_object";
            TestObject value = null;

            // Act
            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestObject>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNull();
        }

        [TestCase]
        public void StoreAndRetrieve_ComplexObject_ShouldReturnEquivalentObject()
        {
            // Arrange
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

            // Act
            bool storeResult = _secureStorage.StoreObject(key, value);
            var retrievedValue = _secureStorage.RetrieveObject<TestComplexObject>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsNotNull();
            Assertions.AssertThat(retrievedValue.Id).IsEqual(value.Id);
            Assertions.AssertThat(retrievedValue.Items.Count).IsEqual(value.Items.Count);
            Assertions.AssertThat(retrievedValue.Items[0].Name).IsEqual("Item 1");
            Assertions.AssertThat(retrievedValue.Items[1].Name).IsEqual("Item 2");
            Assertions.AssertThat(retrievedValue.Items[0].IsActive).IsTrue();
            Assertions.AssertThat(retrievedValue.Items[1].IsActive).IsFalse();
        }

        #endregion

        #region Key Management Tests

        [TestCase]
        public void HasKey_AfterStoring_ShouldReturnTrue()
        {
            // Arrange
            string key = TEST_PREFIX + "exists";
            string value = "test_value";

            // Act
            _secureStorage.StoreValue(key, value);
            bool hasKey = _secureStorage.HasKey(key);

            // Assert
            Assertions.AssertThat(hasKey).IsTrue();
        }

        [TestCase]
        public void HasKey_NonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            string key = TEST_PREFIX + "nonexistent_key";

            // Act
            bool hasKey = _secureStorage.HasKey(key);

            // Assert
            Assertions.AssertThat(hasKey).IsFalse();
        }

        [TestCase]
        public void HasKey_AfterClearing_ShouldReturnFalse()
        {
            // Arrange
            string key = TEST_PREFIX + "to_clear";
            _secureStorage.StoreValue(key, "value");

            // Act
            _secureStorage.ClearValue(key);
            bool hasKey = _secureStorage.HasKey(key);

            // Assert
            Assertions.AssertThat(hasKey).IsFalse();
        }

        [TestCase]
        public void GetAllKeys_AfterStoringMultipleValues_ShouldReturnAllKeys()
        {
            // Arrange - store multiple test values
            _secureStorage.StoreValue(TEST_PREFIX + "key1", "value1");
            _secureStorage.StoreValue(TEST_PREFIX + "key2", "value2");
            _secureStorage.StoreValue(TEST_PREFIX + "key3", "value3");

            // Act
            string[] allKeys = _secureStorage.GetAllKeys();

            // Filter to only our test keys to avoid interference with other tests
            string[] testKeys = allKeys.Where(k => k.StartsWith(TEST_PREFIX)).ToArray();

            // GD.Print(testKeys.Length);
            // GD.Print(testKeys);
            // GD.Print(allKeys.Length);
            // GD.Print(allKeys);

            // Assert
            Assertions.AssertThat(testKeys.Length).IsEqual(3);
            Assertions.AssertThat(testKeys).Contains(TEST_PREFIX + "key1");
            Assertions.AssertThat(testKeys).Contains(TEST_PREFIX + "key2");
            Assertions.AssertThat(testKeys).Contains(TEST_PREFIX + "key3");
        }

        [TestCase]
        public void ClearValue_ExistingKey_ShouldRemoveKey()
        {
            // Arrange
            string key = TEST_PREFIX + "to_clear";
            _secureStorage.StoreValue(key, "value");

            // Act
            bool clearResult = _secureStorage.ClearValue(key);

            // Assert
            Assertions.AssertThat(clearResult).IsTrue();
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsFalse();
            Assertions.AssertThat(_secureStorage.RetrieveValue<string>(key)).IsNull();
        }

        [TestCase]
        public void ClearValue_NonExistentKey_ShouldReturnTrue()
        {
            // Arrange
            string key = TEST_PREFIX + "nonexistent";

            // Act
            bool clearResult = _secureStorage.ClearValue(key);

            // Assert - should return true even for non-existent keys
            Assertions.AssertThat(clearResult).IsTrue();
        }

        #endregion

        #region Edge Cases & Special Scenarios

        [TestCase]
        public void RetrieveValue_NonExistentKey_ShouldReturnDefault()
        {
            // Arrange
            string key = TEST_PREFIX + "does_not_exist";

            // Act & Assert for different types
            Assertions.AssertThat(_secureStorage.RetrieveValue<string>(key)).IsNull();
            Assertions.AssertThat(_secureStorage.RetrieveValue<int>(key)).IsEqual(0);
            Assertions.AssertThat(_secureStorage.RetrieveValue<bool>(key)).IsFalse();
            Assertions.AssertThat(_secureStorage.RetrieveValue<DateTime>(key)).IsEqual(default(DateTime));
        }

        [TestCase]
        public void StoreAndRetrieve_KeyWithSpecialCharacters_ShouldWorkCorrectly()
        {
            // Arrange
            string key = TEST_PREFIX + "special/chars\\with:symbols";
            string value = "special value";

            // Act
            bool storeResult = _secureStorage.StoreValue(key, value);
            string retrievedValue = _secureStorage.RetrieveValue<string>(key);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(retrievedValue).IsEqual(value);
        }

        [TestCase]
        public void StoreValue_NullValue_ShouldRemoveKey()
        {
            // Arrange
            string key = TEST_PREFIX + "null_value";
            _secureStorage.StoreValue(key, "some value");

            // Act
            bool storeResult = _secureStorage.StoreValue(key, null);

            // Assert
            Assertions.AssertThat(storeResult).IsTrue();
            Assertions.AssertThat(_secureStorage.HasKey(key)).IsFalse();
        }

        #endregion

        #region Helper Classes

        // Test class for object serialization tests
        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
        }

        // Complex test class for nested object serialization tests
        private class TestComplexObject
        {
            public int Id { get; set; }
            public List<TestObject> Items { get; set; }
        }

        #endregion
    }
}
