﻿// ***********************************************************************
// Copyright (c) 2009-2014 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace NUnit.Framework.TestHarness
{
    /// <summary>
    /// ConsoleRunner provides a text-based user interface to the
    /// test harness, similar to that of nunit-console.
    /// </summary>
    public class ConsoleRunner
    {
        #region Console Runner Return Codes

        public static readonly int OK = 0;
        public static readonly int INVALID_ARG = -1;
        public static readonly int FILE_NOT_FOUND = -2;
        public static readonly int FIXTURE_NOT_FOUND = -3;
        public static readonly int UNEXPECTED_ERROR = -100;

        #endregion

        #region Instance Fields

        private FrameworkDriver driver;
        private CommandLineOptions options;

        private TextWriter outWriter = Console.Out;
        private TextWriter errorWriter = Console.Error;

        private string workDirectory;

        #endregion

        #region Constructor

        public ConsoleRunner(CommandLineOptions commandlineOptions)
        {
            this.options = commandlineOptions;

            string assemblyPath = Path.GetFullPath(options.AssemblyName);
            AppDomain testDomain = AppDomain.CurrentDomain;
            if (options.RunInSeparateAppDomain)
                testDomain = CreateDomain(Path.GetDirectoryName(assemblyPath));

            var driverSettings = options.CreateDriverSettings();
            this.driver = new FrameworkDriver(assemblyPath, testDomain, driverSettings);

            this.workDirectory = options.WorkDirectory;
            if (this.workDirectory == null)
                this.workDirectory = Environment.CurrentDirectory;
            else if (!Directory.Exists(this.workDirectory))
                Directory.CreateDirectory(this.workDirectory);
        }

        #endregion

        #region Execute Method

        public int Execute()
        {
            DisplayRequestedOptions();

            string filter = TestFilterBuilder.CreateTestFilter(options);

            if (options.Explore)
                return ExploreTests(driver, filter);
            else
                return RunTests(driver, filter);
        }

        #endregion

        #region Helper Methods

        private int ExploreTests(FrameworkDriver driver, string filter)
        {
            IDictionary settings = new Dictionary<string, object>();

            XmlNode testNode = driver.ExploreTests(filter);

            if (testNode.Name == "error")
            {
                DisplayUnexpectedErrorMessage(testNode);
                return ConsoleRunner.UNEXPECTED_ERROR;
            }

            if (testNode.Attributes["runstate"].Value == "NotRunnable")
            {
                DisplayNotRunnableMessage(testNode);
                return ConsoleRunner.FILE_NOT_FOUND;
            }

            string exploreFile = options.ExploreFile;
            XmlTextWriter testWriter = exploreFile != null && exploreFile.Length > 0
                ? new XmlTextWriter(Path.Combine(workDirectory, exploreFile), System.Text.Encoding.UTF8)
                : new XmlTextWriter(Console.Out);
            testWriter.Formatting = Formatting.Indented;
            testNode.WriteTo(testWriter);
            testWriter.Close();

            if (exploreFile != null)
                Console.WriteLine("Tests saved as {0}", options.ExploreFile);

            return ConsoleRunner.OK;
        }

        private int RunTests(FrameworkDriver driver, string testFilter)
        {
            XmlNode loadReport = driver.Load();
            if (loadReport.Name == "error")
            {
                DisplayUnexpectedErrorMessage(loadReport);
                return ConsoleRunner.UNEXPECTED_ERROR;
            }

            TextWriter savedOut = Console.Out;
            TextWriter savedError = Console.Error;
            XmlNode resultNode;

            try
            {
                if (options.OutFile != null)
                    Console.SetOut(new StreamWriter(Path.Combine(workDirectory, options.OutFile)));

                if (options.ErrFile != null)
                    Console.SetError(new StreamWriter(Path.Combine(workDirectory, options.ErrFile)));

                resultNode = driver.Run(testFilter);
            }
            finally
            {
                Console.Out.Flush();
                Console.SetOut(savedOut);
                Console.Error.Flush();
                Console.SetError(savedError);
            }

            if (resultNode.Name == "error")
            {
                DisplayUnexpectedErrorMessage(resultNode);
                return ConsoleRunner.UNEXPECTED_ERROR;
            }

            if (resultNode.Attributes["runstate"].Value == "NotRunnable")
            {
                DisplayNotRunnableMessage(resultNode);
                return ConsoleRunner.FILE_NOT_FOUND;
            }

            string v3ResultFile = Path.Combine(workDirectory, options.V3ResultFile);
            NUnit3TestResultWriter nunit3ResultWriter = new NUnit3TestResultWriter();
            nunit3ResultWriter.WriteResultFile(resultNode, v3ResultFile);
            //XmlTextWriter nunit3ResultWriter = new XmlTextWriter(v3ResultFile, System.Text.Encoding.UTF8);
            //nunit3ResultWriter.Formatting = Formatting.Indented;
            //resultNode.WriteTo(nunit3ResultWriter);
            //nunit3ResultWriter.Close();

            string v2ResultFile = Path.Combine(workDirectory, options.V2ResultFile);
            NUnit2TestResultWriter nunit2ResultWriter = new NUnit2TestResultWriter();
            nunit2ResultWriter.WriteResultFile(resultNode, v2ResultFile);

            // We are not always getting the TeamCity options output in the web, so make sure they are also going to the build log
            //if (!options.DisplayTeamCityServiceMessages)
                new ResultReporter(resultNode).ReportResults();

            if (options.OutFile != null)
                Console.WriteLine("Test standard output saved as {0}", Path.Combine(workDirectory, options.OutFile));
            if (options.ErrFile != null)
                Console.WriteLine("Test error output saved as {0}", Path.Combine(workDirectory, options.ErrFile));
            Console.WriteLine("NUnit3 Result File Saved as {0}", v3ResultFile);
            Console.WriteLine("NUnit2 Result File Saved as {0}", v2ResultFile);

            return int.Parse(resultNode.Attributes["failed"].Value);
        }

        private static void DisplayNotRunnableMessage(XmlNode resultNode)
        {
            var messageText = "Not Runnable: ";

            var messageNode = resultNode.SelectSingleNode("reason/message");
            if (messageNode != null) // First look for a message element
            {
                messageText += messageNode.InnerText;
            }
            else // No message Element, so try property value
            {
                var propNode = resultNode.SelectSingleNode("properties/property[@name='_SKIPREASON']");
                if (propNode != null)
                    messageText += propNode.Attributes["value"].Value;
            }

            Console.WriteLine(messageText);
        }

        private static void DisplayUnexpectedErrorMessage(XmlNode errorReport)
        {
            XmlAttribute message = errorReport.Attributes["message"];
            XmlAttribute stackTrace = errorReport.Attributes["stackTrace"];
            Console.WriteLine("Unexpected Error: {0}", message == null ? "" : message.Value);
            if (stackTrace != null)
                Console.WriteLine(stackTrace.Value);
        }

        private void DisplayRequestedOptions()
        {
            Console.WriteLine("Options -");

            if (options.AssemblyName == "test-harness.exe")
                Console.WriteLine("    Run self-test");

            Console.WriteLine(options.RunInSeparateAppDomain
                ? "    Use Separate AppDomain"
                : "    Use Same AppDomain" );

            if (options.DefaultTimeout >= 0)
                Console.WriteLine("    Default timeout: {0}", options.DefaultTimeout);
            
            if (options.NumWorkers.HasValue)
                Console.WriteLine("    Worker Threads: {0}", options.NumWorkers);
            
            Console.WriteLine("    Work Directory: {0}", workDirectory);
            
            Console.WriteLine("    Internal Trace: {0}", options.InternalTraceLevel);

            if (options.DisplayTeamCityServiceMessages)
                Console.WriteLine("    Display TeamCity Service Messages");
            
            Console.WriteLine();

            if (options.Tests.Count > 0)
            {
                Console.WriteLine("Selected test(s):");
                foreach (string testName in options.Tests)
                    Console.WriteLine("    " + testName);
            }

            if (options.Include != null && options.Include != string.Empty)
                Console.WriteLine("Included categories: " + options.Include);

            if (options.Exclude != null && options.Exclude != string.Empty)
                Console.WriteLine("Excluded categories: " + options.Exclude);
        }

        private static AppDomain CreateDomain(string appBase)
        {
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = appBase;
            AppDomain domain = AppDomain.CreateDomain("test-domain", null, setup);
            return domain;
        }

        #endregion
    }
}
