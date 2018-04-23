using System.Configuration;
using Xunit;
using Amazon.Lambda.TestUtilities;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MetOfficeDownloadAndParse_AWSLambda.Tests
{
    
    public class FunctionTest
    {
        public TestLambdaContext ctx = new TestLambdaContext() {ClientContext = new TestClientContext() };
        const int REQUIREDCONFIGITEMS = 4;

        //More like a constructor - needs refactored and "test setup" warnings
        //[Fact]
        private void TestCorrectlyInitialised()
        {
            //get test assembly name
            var asn = GetType().Assembly.GetName().Name;
            //check correct config file is present
            Assert.True(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "App.config")), "test App.config not present");
            var conf = ConfigurationManager.OpenExeConfiguration(Path.Combine(Directory.GetCurrentDirectory(), $"{asn}.dll"));
            //extract kvps into native dictionary
            KeyValueConfigurationCollection settings = conf.AppSettings.Settings;
            var dict = settings.AllKeys.ToDictionary(key => key, key => settings[key].Value);
            //check correct number of configuration items
            Assert.True(dict.Count == REQUIREDCONFIGITEMS, "test App.config values not set");
            //correct all config items have values set
            Assert.False(dict.Any(c => string.IsNullOrEmpty(c.Value)), "test App.config values not set");

            //create test ctx and add private config values
            foreach (var c in dict)
            {
                ctx.ClientContext.Environment.Add(c);
            };
        }

        [Fact]
        public void TestMetOfficeFunction()
        {
            TestCorrectlyInitialised();
            // Invoke the lambda function 
            var function = new Function();
            var res = function.FunctionHandler(ctx);

        }
    }
}
