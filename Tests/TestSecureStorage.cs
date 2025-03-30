using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Moq;
using System;
using System.Collections.Generic;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Wrappers;
using ProjectTerminal.Tests.Mocks;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public class SecureStorageTest
    {
        private MockLogger _mockLogger;
        private Mock<ISecureStorageWrapper> _mockWrapper;

        [Before]
        public void Setup()
        {
            // Create the mock logger
            _mockLogger = new MockLogger();

            // Create a mock for the wrapper
            _mockWrapper = new Mock<ISecureStorageWrapper>();

        }

        [TestCase]
        public void TestStoreObject()
        {
            // Arrange
            string key = "testKey";
            var value = new { Name = "Test", Age = 30 };
            _mockWrapper.Setup(w => w.StoreObject(key, value)).Returns(true);

            // Act
            bool result = _mockWrapper.Object.StoreObject(key, value);

            // Assert
            AssertThat(result).IsTrue();
            _mockLogger.Info("TestStoreObject: Object stored successfully.");
        }

    }
}
