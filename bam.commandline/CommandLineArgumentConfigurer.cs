/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Bam.Net.Configuration;
using Bam.Net;
using Bam.Console;

namespace Bam.CommandLine
{
    public class CommandLineArgumentConfigurer : IConfigurer
    {
        public CommandLineArgumentConfigurer(ParsedArguments arguments)
        {
            Arguments = arguments;
        }

        public ParsedArguments Arguments
        {
            get;
            private set;
        }

        #region IConfigurer Members

        public void Configure(IConfigurable configurable)
        {
            foreach (string requiredProp in configurable.RequiredProperties)
            {
                string value = Arguments[requiredProp].Or(DefaultConfiguration.GetAppSetting(requiredProp));
                configurable.Property(requiredProp, value);
            }

            configurable.CheckRequiredProperties();
        }

        #endregion
    }
}
