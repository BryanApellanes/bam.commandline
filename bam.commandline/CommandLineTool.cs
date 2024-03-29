/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Bam.Net.Logging;
using Bam.Net.Configuration;
using Bam.Net;
using Bam.Net.ExceptionHandling;
using System.Diagnostics;
using Bam.Console;

namespace Bam.CommandLine
{
    [Serializable]
    public abstract class CommandLineTool : CommandLineInterface
    {
        static CommandLineTool()
        {
            InitLogger();
        }

        protected static ILogger Logger { get; set; }
        public static LogEntryAddedListener MessageToConsole { get; set; }

        protected static MethodInfo DefaultMethod { get; set; }

        /// <summary>
        /// Executes command line switches or starts interactive mode if no command line switches are specified.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="parseErrorHandler"></param>
        public static void ExecuteMainOrInteractive(string[] args, ConsoleArgsParsedDelegate parseErrorHandler = null)
        {
            if (!ExecuteMain(args, parseErrorHandler))
            {
                Interactive();
            }
        }

        /// <summary>
        /// Parses command arguments and executes any switches specified.  Returns true if command line switches were
        /// specified, otherwise false.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="parseErrorHandler"></param>
        /// <returns></returns>
        public static bool ExecuteMain(string[] args, ConsoleArgsParsedDelegate parseErrorHandler = null)
        {
            return ExecuteMain(args, () => { }, parseErrorHandler);
        }

        /// <summary>
        /// Parses command arguments and executes any switches specified.  Returns true if command line switches were
        /// specified, otherwise false.
        /// </summary>
        public static bool ExecuteMain(string[] args, Action preInit, ConsoleArgsParsedDelegate parseErrorHandler = null)
        {
            AddSwitches();
            AddConfigurationSwitches();
            Initialize(args, preInit, parseErrorHandler);
            if (Arguments.Length > 0 && !Arguments.Contains("i"))
            {
                return ExecuteSwitches(false, new ConsoleLogger());
            }

            return false;
        }

        public static void Initialize(string[] args, ConsoleArgsParsedDelegate parseErrorHandler = null)
        {
            Initialize(args, () => { }, parseErrorHandler);
        }

        /// <summary>
        /// Prepares commandline arguments for reading.  If this method completes without errors, parsed arguments
        /// are in the static `Arguments` property.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <param name="parseErrorHandler">The parse error handler.</param>
        public static void Initialize(string[] args, Action preInit, ConsoleArgsParsedDelegate parseErrorHandler = null)
        {
            
            if (parseErrorHandler == null)
            {
                parseErrorHandler = (a) => throw new ArgumentException(a.Message);
            }

            ArgsParsedError += parseErrorHandler;

            preInit();

            AddValidArgument("i", true, description: "Run interactively");
            AddValidArgument("?", true, description: "Show usage");
            AddValidArgument("v", true, description: "Show version information");
            AddValidArgument("t", true, description: "Run all unit tests");
            AddValidArgument("it", true, description: "Run all integration tests");
            AddValidArgument("spec", true, description: "Run all specification tests");
            AddValidArgument("tag", false, description: "Specify a tag to associate with test executions");
            AddValidArgument("group", false, description: "When running unit and spec tests, only run the tests in the specified test group");

            ParseArgs(args);

            if (Arguments.Contains("?"))
            {
                Usage(Assembly.GetEntryAssembly());
                Exit();
            }
            else if (Arguments.Contains("v"))
            {
                Version(Assembly.GetEntryAssembly());
                Exit();
            }
            else if (Arguments.Contains("i"))
            {
                Interactive();
                return;
            }
            else
            {
                if (DefaultMethod != null)
                {
                    DefaultMethod.IsStatic.IsTrue("DefaultMethod must be static.");
                    if (DefaultMethod.GetParameters().Length > 0)
                    {
                        DefaultMethod.Invoke(null, new object[] { Arguments });
                    }
                    else
                    {
                        DefaultMethod.Invoke(null, null);
                    }
                    return;
                }
            }
        }
        /* Moved these to bam.testing.TestableCommandLineTool
                private static void RunSpecTests()
                {
                    if (Arguments.Contains("group"))
                    {
                        RunSpecTestGroup(Assembly.GetEntryAssembly(), Arguments["group"]);
                    }
                    else
                    {
                        RunAllSpecTests(Assembly.GetEntryAssembly());
                    }
                }

                private static void RunUnitTests()
                {
                    if (Arguments.Contains("group"))
                    {
                        RunUnitTestGroup(Assembly.GetEntryAssembly(), Arguments["group"]);
                    }
                    else
                    {
                        RunAllUnitTests(Assembly.GetEntryAssembly());
                    }
                }*/

        private static bool _loggerInitialized;
        static object _initLoggerLock = new object();
        protected static ConsoleLogger InitLogger()
        {
            lock (_initLoggerLock)
            {
                if (!_loggerInitialized)
                {
                    _loggerInitialized = true;

                    ConsoleLogger logger = (ConsoleLogger)Log.CreateLogger(typeof(ConsoleLogger));
                    logger.UseColors = true;
                    logger.ShowTime = true;
                    logger.AddDetails = false;
                    logger.StartLoggingThread();
                    logger.EntryAdded += new LogEntryAddedListener(LoggerEntryAdded);
                    Logger = logger;
                    Log.Default = Logger;
                }
            }

            return Log.Default as ConsoleLogger;
        }

        private static void TryInvoke<T>(ConsoleMethod cim)
        {
            try
            {
                cim.Invoke();
            }
            catch (Exception ex)
            {
                Message.PrintLine("Exception in {0} method {1}: {2}", ConsoleColor.Magenta, typeof(T).Name, cim.Method.Name, ex.Message);
            }
        }

        /*        public static void RunIntegrationTests()
                {
                    IntegrationTestRunner.RunIntegrationTests(Assembly.GetEntryAssembly());
                }*/

        protected static bool IsInteractive { get; set; }

        /// <summary>
        /// Execute the entry assembly as an interactive console menu.
        /// </summary>
        public static void Interactive()
        {
            try
            {
                IsInteractive = true;
                AddMenu(Assembly.GetEntryAssembly(), "Main", 'm', new ConsoleMenuDelegate(ShowMenu));
                //AddMenu(Assembly.GetEntryAssembly(), "Test", 't', new ConsoleMenuDelegate(UnitTestMenu));

                ShowMenu(Assembly.GetEntryAssembly(), OtherMenus.ToArray(), "Main");
            }
            catch (Exception ex)
            {
                if (ex is ReflectionTypeLoadException typeLoadEx)
                {
                    ex = new ReflectionTypeLoadAggregateException(typeLoadEx);
                }
                Message.PrintLine(ex.Message);
                Message.PrintLine(ex.StackTrace);
                throw;
            }
        }

        protected static FileInfo ProcessFile()
        {
            Process process = Process.GetCurrentProcess();
            return new FileInfo(process.MainModule.FileName);
        }

        protected static DirectoryInfo ProcessDirectory()
        {
            return ProcessFile().Directory;
        }

        protected static void MainMenu(string header)
        {
            AddMenu(Assembly.GetEntryAssembly(), header, 'm', new ConsoleMenuDelegate(ShowMenu));

            ShowMenu(Assembly.GetEntryAssembly(), OtherMenus.ToArray(), header);
        }

        protected static void Pass(string text)
        {
            Message.PrintLine("{0}:Passed", ConsoleColor.Green, text);
        }

        /*        public static void UnitTestMenu(Assembly assembly, ConsoleMenu[] otherMenus, string header)
                {
                    Console.WriteLine(header);
                    ITestRunner<UnitTestMethod> runner = GetUnitTestRunner(assembly, Log.Default);
                    ShowActions(runner.GetTests());
                    Console.WriteLine();
                    Console.WriteLine("Q to quit\ttype all to run all tests.");
                    string answer = ShowSelectedMenuOrReturnAnswer(otherMenus);
                    Console.WriteLine();

                    try
                    {
                        answer = answer.Trim().ToLowerInvariant();
                        runner.RunSpecifiedTests(answer);
                    }
                    catch (Exception ex)
                    {                
                        Error("An error occurred running tests", ex);                
                    }

                    if (Confirm("Return to the Test menu? [y][N]"))
                    {
                        UnitTestMenu(assembly, otherMenus, header);
                    }
                    else
                    {
                        Exit(0);
                    }
                }*/
        /*
                public static EventHandler DefaultPassedHandler;
                public static EventHandler DefaultFailedHandler;

                public static void RunAllSpecTests(Assembly assembly, ILogger logger = null, EventHandler passedHandler = null, EventHandler failedHandler = null)
                {
                    passedHandler = passedHandler ?? DefaultPassedHandler;
                    failedHandler = failedHandler ?? DefaultFailedHandler;
                    ITestRunner<SpecTestMethod> runner = GetSpecTestRunner(assembly, logger);
                    AttachHandlers<SpecTestMethod>(passedHandler, failedHandler, runner);
                    AttachSpecTestRunListeners(runner);
                    runner.RunAllTests();
                }

                public static void RunSpecTestGroup(Assembly assembly, string testGroup, ILogger logger = null, EventHandler passedHandler = null, EventHandler failedHandler = null)
                {
                    passedHandler = passedHandler ?? DefaultPassedHandler;
                    failedHandler = failedHandler ?? DefaultFailedHandler;
                    ITestRunner<SpecTestMethod> runner = GetSpecTestRunner(assembly, logger);
                    AttachHandlers<SpecTestMethod>(passedHandler, failedHandler, runner);
                    AttachSpecTestRunListeners(runner);
                    runner.RunTestGroup(testGroup);
                }

                public static void RunAllUnitTests(Assembly assembly, ILogger logger = null, EventHandler passedHandler = null, EventHandler failedHandler = null)
                {
                    passedHandler = passedHandler ?? DefaultPassedHandler;
                    failedHandler = failedHandler ?? DefaultFailedHandler;
                    ITestRunner<UnitTestMethod> runner = GetUnitTestRunner(assembly, logger);
                    AttachHandlers<UnitTestMethod>(passedHandler, failedHandler, runner);
                    AttachUnitTestRunListeners(runner);
                    runner.RunAllTests();
                }

                public static void RunUnitTestGroup(Assembly assembly, string testGroup, ILogger logger = null, EventHandler passedHandler = null, EventHandler failedHandler = null)
                {
                    passedHandler = passedHandler ?? DefaultPassedHandler;
                    failedHandler = failedHandler ?? DefaultFailedHandler;
                    ITestRunner<UnitTestMethod> runner = GetUnitTestRunner(assembly, logger);
                    AttachHandlers<UnitTestMethod>(passedHandler, failedHandler, runner);
                    AttachUnitTestRunListeners(runner);
                    runner.RunTestGroup(testGroup);
                }
        */
        /*        protected internal static Func<IEnumerable<ITestRunListener<UnitTestMethod>>> GetUnitTestRunListeners
                {
                    get;
                    set;
                }

                protected internal static Func<IEnumerable<ITestRunListener<SpecTestMethod>>> GetSpecTestRunListeners
                {
                    get;
                    set;
                }*/
        /*
                protected internal static ITestRunner<SpecTestMethod> GetSpecTestRunner(Assembly assembly, ILogger logger)
                {
                    return GetTestRunner<SpecTestMethod>(assembly, logger);
                }

                protected internal static ITestRunner<UnitTestMethod> GetUnitTestRunner(Assembly assembly, ILogger logger)
                {
                    return GetTestRunner<UnitTestMethod>(assembly, logger);
                }

                protected internal static ITestRunner<TTestMethod> GetTestRunner<TTestMethod>(Assembly assembly, ILogger logger) where TTestMethod : TestMethod
                {
                    ITestRunner<TTestMethod> runner = TestRunner<TTestMethod>.Create(assembly, logger);
                    if (Arguments != null && Arguments.Contains("tag"))
                    {
                        runner.Tag = Arguments["tag"];
                    }
                    runner.NoTestsDiscovered += (o, e) => Message.PrintLine("No tests were found in {0}", ConsoleColor.Yellow, assembly.FullName);
                    runner.TestsDiscovered += (o, e) =>
                    {
                        TestsDiscoveredEventArgs<TTestMethod> args = (TestsDiscoveredEventArgs<TTestMethod>)e;
                        Message.PrintLine("Running all tests in {0}", ConsoleColor.Green, args.Assembly.FullName);
                        Message.PrintLine("\tFound {0} tests", ConsoleColor.Cyan, args.Tests.Count);
                    };
                    runner.TestPassed += (o, e) =>
                    {
                        TestEventArgs<TTestMethod> args = (TestEventArgs<TTestMethod>)e;
                        Pass(args.Test.Information);
                    };
                    runner.TestFailed += (o, t) =>
                    {
                        TestExceptionEventArgs args = (TestExceptionEventArgs)t;
                        Message.PrintLine("Test Failed: ({0})", ConsoleColor.Red,  args.TestMethod.ToString());
                        Message.PrintLine(args.Exception.Message, ConsoleColor.Magenta);
                        Message.PrintLine();
                        Message.PrintLine(args.Exception.StackTrace, ConsoleColor.Red);
                        Message.PrintLine("---", ConsoleColor.Red);
                    };
                    runner.TestsFinished += (o, e) =>
                    {
                        TestEventArgs<TTestMethod> args = (TestEventArgs<TTestMethod>)e;
                        TestRunnerSummary summary = args.TestRunner.TestSummary;

                        Message.PrintLine("********", ConsoleColor.Blue);
                        if (summary.FailedTests.Count > 0)
                        {
                            Message.PrintLine("({0}) tests passed", ConsoleColor.Green, summary.PassedTests.Count);
                            Message.PrintLine("({0}) tests failed", ConsoleColor.Red, summary.FailedTests.Count);
                            StringBuilder failedTests = new StringBuilder();
                            summary.FailedTests.ForEach(cim =>
                            {
                                MethodInfo method = cim.Test.Method;
                                Type type = method.DeclaringType;
                                string testIdentifier = $"{type.Namespace}.{type.Name}.{method.Name}";
                                failedTests.AppendFormat("\t{0}: ({1}) => {2}\r\n", testIdentifier, cim.Test.Information, cim.Exception?.Message ?? "[no message]");
                            });
                            Message.PrintLine("FAILED TESTS: \r\n {0})", new ConsoleColorCombo(ConsoleColor.Yellow, ConsoleColor.Red), failedTests.ToString());
                        }
                        else
                        {
                            Message.PrintLine("All ({0}) tests passed", ConsoleColor.Green, ConsoleColor.Black, summary.PassedTests.Count);
                        }
                        Message.PrintLine("********", ConsoleColor.Blue, ConsoleColor.Black);
                    };
                    return runner;
                }
        */
        public static void Warn(string message)
        {
            Warn(message, new object[] { });
        }

        /// <summary>
        /// Outputs a warning to the console.
        /// </summary>
        /// <param name="messageSignature">The message text to output</param>
        /// <param name="ex">The Exception that occurred.</param>
        /// <param name="signatureVariableValues"></param>
        public static void Warn(string messageSignature, params object[] signatureVariableValues)
        {
            Message.PrintLine(messageSignature, ConsoleColor.Yellow, ToStringArray(signatureVariableValues));
        }

        public static void Error(string message, Exception ex)
        {
            Error(message, ex, new object[] { });
        }
        /// <summary>
        /// Outputs an error to the console.
        /// </summary>
        /// <param name="messageSignature">The message text to output</param>
        /// <param name="ex">The Exception that occurred.</param>
        /// <param name="signatureVariableValues"></param>
        public static void Error(string messageSignature, Exception ex, params object[] signatureVariableValues)
        {
            Message.PrintLine(messageSignature, ConsoleColor.Magenta, ToStringArray(signatureVariableValues));
            Message.PrintLine("{0}\r\n\t{1}", ConsoleColor.DarkMagenta, ex.StackTrace);
        }

        private static string[] ToStringArray(object[] signatureVariableValues)
        {
            List<string> variableValues = new List<string>(signatureVariableValues.Length);
            foreach (object obj in signatureVariableValues)
            {
                variableValues.Add(obj.ToString());
            }
            return variableValues.ToArray();
        }

        private static void LoggerEntryAdded(string applicationName, LogEvent logEvent)
        {
            MessageToConsole?.Invoke(applicationName, logEvent);

            if (logEvent.Severity == LogEventType.Fatal)
            {
                Environment.Exit(1);
            }
        }
        /*
        private static void AttachSpecTestRunListeners(ITestRunner<SpecTestMethod> runner)
        {
            foreach (ITestRunListener<SpecTestMethod> listener in GetSpecTestRunListeners())
            {
                listener.Tag = runner.Tag;
                listener.Listen(runner);
            }
        }
        
        private static void AttachUnitTestRunListeners(ITestRunner<UnitTestMethod> runner)
        {
            foreach (ITestRunListener<UnitTestMethod> listener in GetUnitTestRunListeners())
            {
                listener.Tag = runner.Tag;
                listener.Listen(runner);
            }
        }
        
        private static void AttachHandlers<TTestMethod>(EventHandler passedHandler, EventHandler failedHandler, ITestRunner<TTestMethod> runner) where TTestMethod : TestMethod
        {
            if (passedHandler != null)
            {
                runner.TestPassed += passedHandler;
            }
            if (failedHandler != null)
            {
                runner.TestFailed += failedHandler;
            }

            if (DefaultPassedHandler != null)
            {
                runner.TestPassed += DefaultPassedHandler;
            }
            
            if (DefaultFailedHandler != null)
            {
                runner.TestFailed += DefaultFailedHandler;
            }
        }*/
    }
}
