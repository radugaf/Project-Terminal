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
        private SecureStorageWrapper _wrapper;

        [Before]
        public void Setup()
        {
            _mockLogger = AutoFree(new MockLogger());
            _mockFileSystem = new MockFileSystem(_mockLogger);
            _wrapper = new SecureStorageWrapper(_mockLogger, _mockFileSystem);
        }

        [After]
        public void Teardown()
        {
            _mockFileSystem.Reset();
            _wrapper = null;
            _mockLogger = null;
        }

        [TestCase]
        public void TestStoreObject_Success()
        {
            // Arrange
            string key = "testKey";
            var testObject = new { Name = "Test", Age = 30 };

            // Act
            bool result = _wrapper.StoreObject(key, testObject);

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
        }
    }
}
