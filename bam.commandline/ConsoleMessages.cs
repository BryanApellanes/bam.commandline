using Bam.Console;
using System;
using System.Collections.Generic;

namespace Bam.CommandLine
{
    public static class ConsoleMessages
    {
        public static List<ConsoleMessage> Print(this string message, params object[] args)
        {
            return message.Print(ConsoleColor.Cyan, args);
        }

        public static List<ConsoleMessage> Print(this string message, ConsoleColor textColor, params object[] args)
        {
            List<ConsoleMessage> messages = new List<ConsoleMessage>();
            return messages.Add(message, textColor, args);
        }

        public static List<ConsoleMessage> Add(this List<ConsoleMessage> list, string message, params object[] args)
        {
            return list.Add(message, ConsoleColor.Cyan, args);
        }

        public static List<ConsoleMessage> Add(this List<ConsoleMessage> list, string message, ConsoleColor textColor, params object[] args)
        {
            list.Add(new ConsoleMessage(message, textColor, args));
            return list;
        }

        public static void Print(this List<ConsoleMessage> list)
        {
            ConsoleMessage.Print(list);
        }
    }
}