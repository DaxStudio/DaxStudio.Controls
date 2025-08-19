using System;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DaxStudio.Controls.Tests.Helpers
{
    [CollectionDefinition("WPF Tests")]
    public class WpfTestCollection : ICollectionFixture<WpfTestEnvironment>
    {
        // This class has no code, and is never created.
        // Its purpose is to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
    
    /// <summary>
    /// Fixture to provide a shared WPF environment for tests
    /// </summary>
    public class WpfTestEnvironment : IDisposable
    {
        public WpfTestEnvironment()
        {
            // Initialize WPF for the entire test collection
            WpfTestHelper.InitializeWpfEnvironment();
        }
        
        public void Dispose()
        {
            // No cleanup needed
        }
    }
}