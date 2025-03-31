using GdUnit4;
using System;
using static GdUnit4.Assertions;
using System.Text.Json;
using ProjectTerminal.Globals.Wrappers;
using ProjectTerminal.Tests.Mocks;
using System.Collections.Generic;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public class SecureStorageWrapperTest
    {
        private MockLogger _mockLogger;
        private MockFileSystem _mockFileSystem;
        private SecureStorageWrapper _secureStorageWrapper;

        private class TestClass
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        private class ComplexTestClass
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public List<string> Items { get; set; }
            public TestClass Nested { get; set; }
        }

        [Before]
        public void Setup() => _mockLogger = AutoFree(new MockLogger());
        [BeforeTest]
        public void SetupTest()
        {
            _mockFileSystem = new MockFileSystem(_mockLogger);
            _secureStorageWrapper = new SecureStorageWrapper(_mockLogger, _mockFileSystem);
        }

        [AfterTest]
        public void Teardown() => _mockFileSystem.Reset();

        [After]
        public void AfterEach()
        {
            _secureStorageWrapper = null;
            _mockLogger = null;
        }

        // --- Test StoreObject ---

        [TestCase]
        public void TestStoreObject_GenericObject_Success()
        {
            // Arrange
            string key = "testKey";
            var testObject = new
            {
                Name = "Test",
                Age = 30,
                IsActive = true,
                Address = new
                {
                    Street = "123 Main St",
                    City = "Test City",
                    ZipCode = "12345"
                },
                Tags = new[] { "tag1", "tag2", "tag3" }
            };

            // Act
            bool result = _secureStorageWrapper.StoreObject(key, testObject);

            // Assert
            AssertThat(result).IsTrue();

            // Verify the file was actually "written" to our mock filesystem
            string expectedPath = "user://secure_data/testKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            // Verify content was properly serialized
            string fileContent = _mockFileSystem.GetFileContent(expectedPath);
            AssertThat(fileContent).IsNotEmpty();

            var jsonDoc = JsonDocument.Parse(fileContent);
            AssertThat(jsonDoc.RootElement.TryGetProperty("name", out _)).IsTrue();
            AssertThat(jsonDoc.RootElement.TryGetProperty("age", out JsonElement ageProperty)).IsTrue();
            AssertThat(ageProperty.GetInt32()).IsEqual(30);
            AssertThat(jsonDoc.RootElement.TryGetProperty("isActive", out _)).IsTrue();
            AssertThat(jsonDoc.RootElement.TryGetProperty("address", out JsonElement addressProperty)).IsTrue();
            AssertThat(addressProperty.TryGetProperty("street", out _)).IsTrue();
            AssertThat(addressProperty.TryGetProperty("city", out _)).IsTrue();
            AssertThat(addressProperty.TryGetProperty("zipCode", out _)).IsTrue();
            AssertThat(jsonDoc.RootElement.TryGetProperty("tags", out JsonElement tagsProperty)).IsTrue();
            AssertThat(tagsProperty.ValueKind).IsEqual(JsonValueKind.Array);
            AssertThat(tagsProperty.GetArrayLength()).IsEqual(3);
            AssertThat(tagsProperty[0].GetString()).IsEqual("tag1");
            AssertThat(tagsProperty[1].GetString()).IsEqual("tag2");
            AssertThat(tagsProperty[2].GetString()).IsEqual("tag3");
        }

        [TestCase]
        public void TestStoreObject_NullKey_Fails()
        {
            // Arrange
            string key = null;
            var testObject = new
            {
                Name = "Test",
                Age = 30,
                IsActive = true
            };

            // Act
            bool result = _secureStorageWrapper.StoreObject(key, testObject);

            // Assert
            AssertThat(result).IsFalse();
        }

        [TestCase]
        public void TestStoreObject_EmptyKey_Fails()
        {
            // Arrange
            string key = string.Empty;
            var testObject = new
            {
                Name = "Test",
                Age = 30,
                IsActive = true
            };

            // Act
            bool result = _secureStorageWrapper.StoreObject(key, testObject);

            // Assert
            AssertThat(result).IsFalse();
        }

        [TestCase]
        public void TestStoreObject_NullObject_Fails()
        {
            // Arrange
            string key = "testKey";
            object testObject = null;

            // Act
            bool result = _secureStorageWrapper.StoreObject(key, testObject);

            // Assert
            AssertThat(result).IsFalse();
        }

        // --- Test RetrieveObject ---

        [TestCase]
        public void TestRetrieveObject_Success()
        {
            // Arrange
            string key = "testKey";
            var expectedObject = new TestClass { Name = "Test", Age = 30 };

            // Store the object first
            bool storeResult = _secureStorageWrapper.StoreObject(key, expectedObject);
            AssertThat(storeResult).IsTrue();

            // Act
            TestClass retrievedObject = _secureStorageWrapper.RetrieveObject<TestClass>(key);

            // Assert
            AssertThat(retrievedObject).IsNotNull();
            AssertThat(retrievedObject.Name).IsEqual("Test");
            AssertThat(retrievedObject.Age).IsEqual(30);
        }

        [TestCase]
        public void TestRetrieveObject_ComplexObject()
        {
            // Arrange
            string key = "complexKey";
            var expectedObject = new ComplexTestClass
            {
                Id = 1,
                Name = "Complex",
                Items = ["Item1", "Item2"],
                Nested = new TestClass { Name = "Nested", Age = 25 }
            };

            // Store the complex object
            bool storeResult = _secureStorageWrapper.StoreObject(key, expectedObject);
            AssertThat(storeResult).IsTrue();

            // Act
            ComplexTestClass retrievedObject = _secureStorageWrapper.RetrieveObject<ComplexTestClass>(key);

            // Assert
            AssertThat(retrievedObject).IsNotNull();
            AssertThat(retrievedObject.Id).IsEqual(1);
            AssertThat(retrievedObject.Name).IsEqual("Complex");
            AssertThat(retrievedObject.Items).Contains("Item1", "Item2");
            AssertThat(retrievedObject.Nested).IsNotNull();
            AssertThat(retrievedObject.Nested.Name).IsEqual("Nested");
            AssertThat(retrievedObject.Nested.Age).IsEqual(25);
        }

        [TestCase]
        public void TestRetrieveObject_NonExistentKey_ReturnsDefault()
        {
            // Arrange
            string key = "nonExistentKey";

            // Act
            TestClass result = _secureStorageWrapper.RetrieveObject<TestClass>(key);

            // Assert
            AssertThat(result).IsNull();
        }

        [TestCase]
        public void TestRetrieveObject_EmptyJson_ReturnsDefault()
        {
            // Arrange
            string key = "emptyJsonKey";
            string emptyJson = "";
            string path = "user://secure_data/emptyJsonKey.dat";

            // Create an empty file
            _mockFileSystem.SetFileContent(path, emptyJson);

            // Act
            var result = _secureStorageWrapper.RetrieveObject<TestClass>(key);

            // Assert
            AssertThat(result).IsNull();
        }

        [TestCase]
        public void TestRetrieveObject_InvalidJson_ReturnsDefault()
        {
            // Arrange
            string key = "invalidJsonKey";
            string invalidJson = "{invalid JSON}";
            string path = "user://secure_data/invalidJsonKey.dat";

            // Create a file with invalid JSON
            _mockFileSystem.SetFileContent(path, invalidJson);

            // Act
            var result = _secureStorageWrapper.RetrieveObject<TestClass>(key);

            // Assert
            AssertThat(result).IsNull();
        }

        [TestCase]
        public void TestRetrieveObject_PrimitiveType()
        {
            // Arrange
            string key = "intKey";
            int expectedValue = 42;

            // Store the primitive value
            bool storeResult = _secureStorageWrapper.StoreObject(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            int retrievedValue = _secureStorageWrapper.RetrieveObject<int>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }


        // --- Test StoreValue ---

        [TestCase]
        public void TestStoreValue_String_Success()
        {
            // Arrange
            string key = "stringKey";
            string value = "Test String Value";

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/stringKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("String");
            AssertThat(lines[1]).IsEqual(value);
        }

        [TestCase]
        public void TestStoreValue_Integer_Success()
        {
            // Arrange
            string key = "intKey";
            int value = 42;

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/intKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("Int32");
            AssertThat(lines[1]).IsEqual("42");
        }

        [TestCase]
        public void TestStoreValue_Boolean_Success()
        {
            // Arrange
            string key = "boolKey";
            bool value = true;

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/boolKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("Boolean");
            AssertThat(lines[1]).IsEqual("True");
        }

        [TestCase]
        public void TestStoreValue_Float_Success()
        {
            // Arrange
            string key = "floatKey";
            float value = 3.14f;

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/floatKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("Single");
            AssertThat(lines[1]).Contains("3.14");
        }

        [TestCase]
        public void TestStoreValue_Double_Success()
        {
            // Arrange
            string key = "doubleKey";
            double value = 3.14159265359;

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/doubleKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("Double");
            AssertThat(lines[1]).Contains("3.14159265359");
        }

        [TestCase]
        public void TestStoreValue_DateTime_Success()
        {
            // Arrange
            string key = "dateTimeKey";
            DateTime value = new DateTime(2023, 1, 15, 10, 30, 0);

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            string expectedPath = "user://secure_data/dateTimeKey.dat";
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            string content = _mockFileSystem.GetFileContent(expectedPath);
            string[] lines = content.Split('\n');
            AssertThat(lines.Length).IsGreaterEqual(2);
            AssertThat(lines[0]).Contains("DateTime");
            AssertThat(lines[1]).Contains("2023");
            AssertThat(lines[1]).Contains("10:30");
        }

        [TestCase]
        public void TestStoreValue_NullValue_CallsClearValue()
        {
            // Arrange
            string key = "nullKey";
            object value = null;

            // Create a file first so we can verify it gets deleted
            string expectedPath = "user://secure_data/nullKey.dat";
            _mockFileSystem.SetFileContent(expectedPath, "test content");

            // Verify file exists before test
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsTrue();

            // Act
            bool result = _secureStorageWrapper.StoreValue(key, value);

            // Assert
            AssertThat(result).IsTrue();
            AssertThat(_mockFileSystem.FileExists(expectedPath)).IsFalse();
        }

        // --- Test RetrieveValue ---

        [TestCase]
        public void TestRetrieveValue_String_Success()
        {
            // Arrange
            string key = "stringKey";
            string expectedValue = "Test String Value";

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            string retrievedValue = _secureStorageWrapper.RetrieveValue<string>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_Integer_Success()
        {
            // Arrange
            string key = "intKey";
            int expectedValue = 42;

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            int retrievedValue = _secureStorageWrapper.RetrieveValue<int>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_Boolean_Success()
        {
            // Arrange
            string key = "boolKey";
            bool expectedValue = true;

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            bool retrievedValue = _secureStorageWrapper.RetrieveValue<bool>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_Float_Success()
        {
            // Arrange
            string key = "floatKey";
            float expectedValue = 3.14f;

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            float retrievedValue = _secureStorageWrapper.RetrieveValue<float>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_Double_Success()
        {
            // Arrange
            string key = "doubleKey";
            double expectedValue = 3.14159265359;

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            double retrievedValue = _secureStorageWrapper.RetrieveValue<double>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_DateTime_Success()
        {
            // Arrange
            string key = "dateTimeKey";
            DateTime expectedValue = new DateTime(2023, 1, 15, 10, 30, 0);

            // Store the value first
            bool storeResult = _secureStorageWrapper.StoreValue(key, expectedValue);
            AssertThat(storeResult).IsTrue();

            // Act
            DateTime retrievedValue = _secureStorageWrapper.RetrieveValue<DateTime>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(expectedValue);
        }

        [TestCase]
        public void TestRetrieveValue_NonExistentKey_ReturnsDefault()
        {
            // Arrange
            string key = "nonExistentKey";

            // Act
            int result = _secureStorageWrapper.RetrieveValue<int>(key);

            // Assert
            AssertThat(result).IsEqual(0); // Default for int
        }

        [TestCase]
        public void TestRetrieveValue_EmptyContent_ReturnsDefault()
        {
            // Arrange
            string key = "emptyContentKey";
            string emptyContent = "";
            string path = "user://secure_data/emptyContentKey.dat";

            // Create an empty file
            _mockFileSystem.SetFileContent(path, emptyContent);

            // Act
            string result = _secureStorageWrapper.RetrieveValue<string>(key);

            // Assert
            AssertThat(result).IsNull(); // Default for string
        }

        [TestCase]
        public void TestRetrieveValue_InvalidFormat_ReturnsDefault()
        {
            // Arrange
            string key = "invalidFormatKey";
            // Missing the second line with the actual value
            string invalidContent = "System.String";
            string path = "user://secure_data/invalidFormatKey.dat";

            // Create a file with invalid content
            _mockFileSystem.SetFileContent(path, invalidContent);

            // Act
            string result = _secureStorageWrapper.RetrieveValue<string>(key);

            // Assert
            AssertThat(result).IsNull();
        }

        [TestCase]
        public void TestRetrieveValue_TypeMismatch_ReturnsDefault()
        {
            // Arrange
            string key = "typeMismatchKey";
            string value = "Not an integer";

            // Store a string value
            bool storeResult = _secureStorageWrapper.StoreValue(key, value);
            AssertThat(storeResult).IsTrue();

            // Act
            // Try to retrieve as int
            int retrievedValue = _secureStorageWrapper.RetrieveValue<int>(key);

            // Assert
            AssertThat(retrievedValue).IsEqual(0); // Default for int
        }
    }
}
