using GdUnit4;
using static GdUnit4.Assertions;
using System.Text.Json;
using ProjectTerminal.Globals.Wrappers;
using ProjectTerminal.Tests.Mocks;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public class SecureStorageWrapperTest
    {
        private MockLogger _mockLogger;
        private MockFileSystem _mockFileSystem;
        private SecureStorageWrapper _secureStorageWrapper;

        [Before]
        public void Setup()
        {
            _mockLogger = AutoFree(new MockLogger());
            _mockFileSystem = new MockFileSystem(_mockLogger);
            _secureStorageWrapper = new SecureStorageWrapper(_mockLogger, _mockFileSystem);
        }

        [AfterTest]
        public void Teardown()
        {
            _mockFileSystem.Reset();
        }

        [After]
        public void AfterEach()
        {
            _secureStorageWrapper = null;
            _mockLogger = null;
        }

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
    }
}
