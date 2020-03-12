using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Compatibility;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.TestData.FailingAssemblyAction;

namespace NUnit.Framework
{
    [TestFixture, NonParallelizable]
    public class FailingAssemblyActionAttributeTests
    {
        private static readonly string ASSEMBLY_PATH = AssemblyHelper.GetAssemblyPath(typeof(FailingAssemblyActionAttribute).GetTypeInfo().Assembly);

        private ITestResult _result = null;

        [OneTimeSetUp]
        public void Setup()
        {
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());

            Fixture.ClearResults();

            IDictionary<string, object> options = new Dictionary<string, object>();
            options["LOAD"] = new string[] { "NUnit.TestData.FailingAssemblyAction" };
            // No need for the overhead of parallel execution here
            options["NumberOfTestWorkers"] = 0;

            Assert.NotNull(runner.Load(ASSEMBLY_PATH, options), "Assembly not loaded");
            Assert.That(runner.LoadedTest.RunState, Is.EqualTo(RunState.Runnable));

            _result = runner.Run(TestListener.NULL, TestFilter.Empty);
        }

        [Test]
        public void TestSuiteIsFailed()
        {
            Assert.That(_result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(_result.ResultState.Site, Is.EqualTo(FailureSite.TearDown));
        }

        [Test]
        public void TestSuiteIndicatesFailureLocation()
        {
            Assert.That(_result.Message, Does.Contain(nameof(NullReferenceException)));
            Assert.That(_result.StackTrace, Does.Contain(typeof(FailingAssemblyActionAttribute).FullName));
            Assert.That(_result.StackTrace, Does.Contain(nameof(FailingAssemblyActionAttribute.AfterTest)));
        }

        [Test]
        public void ActionsWrappingAssembly()
        {
            Assert.That(Fixture.Events, Is.EqualTo(new [] {
                "BeforeAssembly",
                "Test",
                "AfterAssembly",
            }));
        }
    }
}
