using Godot;
using GdUnit4;
using System;
using System.Collections.Generic;
using ProjectTerminal.Resources.Mocks;

namespace ProjectTerminal.Tests
{
    [TestSuite]
    public partial class TestAuthManager
    {
        private MockLogger _mockLogger;
        private Node _testScene;

        [Before]
        public void Setup()
        {
            _testScene = new Node();
            if (Engine.GetMainLoop() is SceneTree root)
            {
                root.Root.AddChild(_testScene);
            }

            _mockLogger = new MockLogger();
            _testScene.AddChild(_mockLogger);
            _mockLogger.Name = "Logger";

            //

        }
    }
}
