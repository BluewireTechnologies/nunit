using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.TestData.FailingAssemblyAction;

[assembly: FailingAssemblyAction]

namespace NUnit.TestData.FailingAssemblyAction
{
    [TestFixture]
    public class Fixture
    {
        public static List<string> Events { get; } = new List<string>();

        public static void ClearResults()
        {
            Events.Clear();
        }

        [Test]
        public void Test()
        {
            Events.Add("Test");
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class FailingAssemblyActionAttribute : TestActionAttribute
    {
        public override void BeforeTest(ITest test)
        {
            AddResult("BeforeAssembly");
        }

        public override void AfterTest(ITest test)
        {
            AddResult("AfterAssembly");
            throw new NullReferenceException();
        }

        public override ActionTargets Targets
        {
            get { return ActionTargets.Suite; }
        }

        private void AddResult(string phase)
        {
            if(Fixture.Events != null)
                Fixture.Events.Add(phase);
        }
    }
}
